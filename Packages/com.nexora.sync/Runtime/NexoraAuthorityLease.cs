using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Nexora.Sync
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class NexoraAuthorityLease : UdonSharpBehaviour
    {
        [Header("Lease policy")]
        public float heartbeatIntervalSeconds = 5f;
        public float staleAfterSeconds = 18f;
        public bool masterMayRecoverStaleLease = true;

        [Header("Objects that follow authority")]
        public GameObject[] authorityObjects;

        [UdonSynced] public double lastHeartbeatServerTime;
        [UdonSynced] public int authorityPlayerId = -1;
        [UdonSynced] public int authorityEpoch;

        private bool heartbeatScheduled;

        private void Start()
        {
            ScheduleHeartbeat();
        }

        public bool IsStale()
        {
            if (lastHeartbeatServerTime <= 0d) return true;
            return Networking.GetServerTimeInSeconds() - lastHeartbeatServerTime > staleAfterSeconds;
        }

        public bool IsLocalAuthority()
        {
            return Networking.IsOwner(gameObject);
        }

        public void HeartbeatTick()
        {
            heartbeatScheduled = false;

            if (Networking.IsOwner(gameObject))
            {
                WriteHeartbeat();
            }
            else if (masterMayRecoverStaleLease && Networking.IsMaster && IsStale())
            {
                RecoverLease();
            }

            ScheduleHeartbeat();
        }

        public void RecoverLease()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (local == null || !local.IsValid()) return;

            Networking.SetOwner(local, gameObject);
            TransferAuthorityObjects(local);
            authorityEpoch++;
            WriteHeartbeat();
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (player == null || player.playerId != authorityPlayerId) return;
            if (Networking.IsMaster) RecoverLease();
        }

        private void WriteHeartbeat()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            lastHeartbeatServerTime = Networking.GetServerTimeInSeconds();
            authorityPlayerId = local == null ? -1 : local.playerId;
            RequestSerialization();
        }

        private void TransferAuthorityObjects(VRCPlayerApi owner)
        {
            if (authorityObjects == null) return;
            int i = 0;
            while (i < authorityObjects.Length)
            {
                if (authorityObjects[i] != null && !Networking.IsOwner(authorityObjects[i]))
                {
                    Networking.SetOwner(owner, authorityObjects[i]);
                }
                i++;
            }
        }

        private void ScheduleHeartbeat()
        {
            if (heartbeatScheduled) return;
            heartbeatScheduled = true;
            SendCustomEventDelayedSeconds(nameof(HeartbeatTick), Mathf.Max(1f, heartbeatIntervalSeconds));
        }
    }
}
