using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Nexora.Sync
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraSyncStatus : UdonSharpBehaviour
    {
        public NexoraMediaState mediaState;
        public NexoraAuthorityLease authorityLease;

        [Header("Sampling")]
        public float sampleIntervalSeconds = 1f;
        public float snapshotWarningAgeSeconds = 15f;

        [Header("Runtime status")]
        [HideInInspector] public bool authorityStale;
        [HideInInspector] public bool snapshotAging;
        [HideInInspector] public bool localIsAuthority;
        [HideInInspector] public float authorityHeartbeatAge;
        [HideInInspector] public float acceptedSnapshotAge;
        [HideInInspector] public int acceptedRevision = -1;
        [HideInInspector] public int acceptedAuthorityEpoch = -1;
        [HideInInspector] public int acceptedSourcePlayerId = -1;
        [HideInInspector] public int staleSnapshotCount;
        [HideInInspector] public int duplicateSnapshotCount;
        [HideInInspector] public int rejectedStateRestoreCount;
        [HideInInspector] public int statusSampleCount;

        private double lastAcceptedServerTime;
        private int observedRevision = -1;
        private int observedEpoch = -1;
        private bool scheduled;

        private void Start()
        {
            Sample();
            Schedule();
        }

        public void Tick()
        {
            scheduled = false;
            Sample();
            Schedule();
        }

        public void Sample()
        {
            statusSampleCount++;
            double serverNow = Networking.GetServerTimeInSeconds();

            if (mediaState != null)
            {
                acceptedRevision = mediaState.acceptedRevision;
                acceptedAuthorityEpoch = mediaState.acceptedAuthorityEpoch;
                acceptedSourcePlayerId = mediaState.acceptedSourcePlayerId;
                staleSnapshotCount = mediaState.staleSnapshotCount;
                duplicateSnapshotCount = mediaState.duplicateSnapshotCount;
                rejectedStateRestoreCount = mediaState.rejectedStateRestoreCount;

                if (observedRevision != acceptedRevision || observedEpoch != acceptedAuthorityEpoch)
                {
                    observedRevision = acceptedRevision;
                    observedEpoch = acceptedAuthorityEpoch;
                    lastAcceptedServerTime = serverNow;
                }

                acceptedSnapshotAge = lastAcceptedServerTime <= 0d ? 0f : (float)(serverNow - lastAcceptedServerTime);
                snapshotAging = acceptedSnapshotAge >= Mathf.Max(1f, snapshotWarningAgeSeconds);
            }

            if (authorityLease != null)
            {
                localIsAuthority = authorityLease.IsLocalAuthority();
                authorityStale = authorityLease.IsStale();
                authorityHeartbeatAge = authorityLease.lastHeartbeatServerTime <= 0d
                    ? 0f
                    : (float)(serverNow - authorityLease.lastHeartbeatServerTime);
            }
        }

        private void Schedule()
        {
            if (scheduled) return;
            scheduled = true;
            SendCustomEventDelayedSeconds(nameof(Tick), Mathf.Max(0.25f, sampleIntervalSeconds));
        }
    }
}
