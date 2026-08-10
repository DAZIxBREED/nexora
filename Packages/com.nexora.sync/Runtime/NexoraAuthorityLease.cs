using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using Nexora.Permissions;

namespace Nexora.Sync
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class NexoraAuthorityLease : UdonSharpBehaviour
    {
        [Header("Lease policy")]
        public float heartbeatIntervalSeconds = 5f;
        public float staleAfterSeconds = 18f;
        public bool masterMayRecoverStaleLease = true;

        [Header("Authority state")]
        public NexoraMediaState mediaState;
        public NexoraAccessControl accessControl;

        [Header("Objects that follow authority")]
        public GameObject[] authorityObjects;

        [UdonSynced] public double lastHeartbeatServerTime;
        [UdonSynced] public int authorityPlayerId = -1;
        [UdonSynced] public int authorityEpoch;
        [UdonSynced] public int heartbeatSequence;

        [HideInInspector] public int recoveryCount;
        [HideInInspector] public int ownershipTransferCount;
        [HideInInspector] public int automaticRecoveryCount;
        [HideInInspector] public int deniedRecoveryCount;
        [HideInInspector] public int deniedTransferCount;
        [HideInInspector] public int lastRecoveryPlayerId = -1;

        private bool heartbeatScheduled;

        private void Start()
        {
            if (Networking.IsOwner(gameObject) && authorityPlayerId < 0)
            {
                authorityEpoch++;
                WriteHeartbeat();
                AdoptMediaEpoch();
            }
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

        public bool IsPlayerAuthority(VRCPlayerApi player)
        {
            return player != null && player.IsValid() && player.playerId == authorityPlayerId;
        }

        public void HeartbeatTick()
        {
            heartbeatScheduled = false;

            if (Networking.IsOwner(gameObject))
            {
                WriteHeartbeat();
                AdoptMediaEpoch();
            }
            else if (masterMayRecoverStaleLease && Networking.IsMaster && IsStale())
            {
                automaticRecoveryCount++;
                RecoverLeaseInternal();
            }

            ScheduleHeartbeat();
        }

        public void RecoverLease()
        {
            if (accessControl == null || !accessControl.AuthorizeAdministration("authority-recovery"))
            {
                deniedRecoveryCount++;
                return;
            }

            RecoverLeaseInternal();
        }

        public void TransferLeaseToLocal()
        {
            if (accessControl == null || !accessControl.AuthorizeAdministration("authority-transfer"))
            {
                deniedTransferCount++;
                return;
            }

            TransferLeaseToLocalInternal();
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (player == null || player.playerId != authorityPlayerId) return;
            if (Networking.IsMaster)
            {
                automaticRecoveryCount++;
                RecoverLeaseInternal();
            }
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (!Networking.IsOwner(gameObject)) return;
            authorityEpoch++;
            ownershipTransferCount++;
            TransferAuthorityObjects(Networking.LocalPlayer);
            WriteHeartbeat();
            AdoptMediaEpoch();
        }

        private void RecoverLeaseInternal()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (local == null || !local.IsValid()) return;

            Networking.SetOwner(local, gameObject);
            TransferAuthorityObjects(local);
            authorityEpoch++;
            recoveryCount++;
            lastRecoveryPlayerId = local.playerId;
            WriteHeartbeat();
            AdoptMediaEpoch();
        }

        private void TransferLeaseToLocalInternal()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (local == null || !local.IsValid()) return;

            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(local, gameObject);
            }
            TransferAuthorityObjects(local);
            authorityEpoch++;
            ownershipTransferCount++;
            lastRecoveryPlayerId = local.playerId;
            WriteHeartbeat();
            AdoptMediaEpoch();
        }

        private void WriteHeartbeat()
        {
            if (!Networking.IsOwner(gameObject)) return;

            VRCPlayerApi local = Networking.LocalPlayer;
            lastHeartbeatServerTime = Networking.GetServerTimeInSeconds();
            authorityPlayerId = local == null ? -1 : local.playerId;
            heartbeatSequence++;
            RequestSerialization();
        }

        private void AdoptMediaEpoch()
        {
            if (mediaState == null || !Networking.IsOwner(mediaState.gameObject)) return;
            mediaState.AdoptAuthorityEpoch(authorityEpoch);
        }

        private void TransferAuthorityObjects(VRCPlayerApi owner)
        {
            if (owner == null || !owner.IsValid() || authorityObjects == null) return;

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
