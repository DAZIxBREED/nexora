using UdonSharp;
using UnityEngine;
using VRC.Udon;

namespace Nexora.Diagnostics
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraOperatorHealthBridge : UdonSharpBehaviour
    {
        [Header("Source")]
        public NexoraHealthMonitor health;

        [Header("Receiver")]
        public UdonBehaviour receiver;
        public string summaryVariable = "nexoraHealthSummary";
        public string overallHealthVariable = "nexoraOverallHealth";
        public string platformVariable = "nexoraPlatform";
        public string driftVariable = "nexoraDriftSeconds";
        public string authorityPlayerVariable = "nexoraAuthorityPlayerId";
        public string authorityEpochVariable = "nexoraAuthorityEpoch";
        public string backendFaultVariable = "nexoraBackendFault";
        public string streamStateVariable = "nexoraStreamState";
        public string streamFailureVariable = "nexoraStreamFailure";
        public string queueCountVariable = "nexoraQueueCount";
        public string requestCountVariable = "nexoraRequestCount";
        public string quarantineCountVariable = "nexoraQuarantineCount";
        public string lockedVariable = "nexoraControlsLocked";
        public string roleVariable = "nexoraLocalRole";
        public string updateEvent = "OnNexoraHealthUpdated";

        [Header("Update policy")]
        public float refreshIntervalSeconds = 0.5f;
        public bool sendOnlyOnChange = true;

        [HideInInspector] public int pushCount;
        [HideInInspector] public int skippedUnchangedCount;
        [HideInInspector] public int missingReceiverCount;

        private int lastSignature = int.MinValue;
        private bool scheduled;

        private void Start()
        {
            refreshIntervalSeconds = Mathf.Max(0.1f, refreshIntervalSeconds);
            PushNow();
            Schedule();
        }

        public void Tick()
        {
            scheduled = false;
            PushNow();
            Schedule();
        }

        public void PushNow()
        {
            if (health == null || receiver == null)
            {
                missingReceiverCount++;
                return;
            }

            int signature = BuildSignature();
            if (sendOnlyOnChange && signature == lastSignature)
            {
                skippedUnchangedCount++;
                return;
            }

            lastSignature = signature;
            receiver.SetProgramVariable(summaryVariable, health.BuildOperatorSummary());
            receiver.SetProgramVariable(overallHealthVariable, health.overallHealth);
            receiver.SetProgramVariable(platformVariable, health.platformName);
            receiver.SetProgramVariable(driftVariable, health.currentDriftSeconds);
            receiver.SetProgramVariable(authorityPlayerVariable, health.authorityPlayerId);
            receiver.SetProgramVariable(authorityEpochVariable, health.authorityEpoch);
            receiver.SetProgramVariable(backendFaultVariable, health.backendFault);
            receiver.SetProgramVariable(streamStateVariable, health.observedStreamState);
            receiver.SetProgramVariable(streamFailureVariable, health.observedStreamFailure);
            receiver.SetProgramVariable(queueCountVariable, health.queueCount);
            receiver.SetProgramVariable(requestCountVariable, health.requestCount);
            receiver.SetProgramVariable(quarantineCountVariable, health.quarantineCount);
            receiver.SetProgramVariable(lockedVariable, health.controlsLocked);
            receiver.SetProgramVariable(roleVariable, health.localRole);

            if (!string.IsNullOrEmpty(updateEvent)) receiver.SendCustomEvent(updateEvent);
            pushCount++;
        }

        private int BuildSignature()
        {
            int value = health.overallHealth;
            value = value * 31 + health.syncHealth;
            value = value * 31 + health.videoHealth;
            value = value * 31 + health.streamHealth;
            value = value * 31 + health.playlistHealth;
            value = value * 31 + health.permissionsHealth;
            value = value * 31 + health.authorityPlayerId;
            value = value * 31 + health.authorityEpoch;
            value = value * 31 + health.acceptedRevision;
            value = value * 31 + health.backendFault;
            value = value * 31 + health.observedStreamState;
            value = value * 31 + health.observedStreamFailure;
            value = value * 31 + health.queueCount;
            value = value * 31 + health.requestCount;
            value = value * 31 + health.quarantineCount;
            value = value * 31 + (health.controlsLocked ? 1 : 0);
            value = value * 31 + health.localRole;
            value = value * 31 + Mathf.RoundToInt(health.currentDriftSeconds * 20f);
            return value;
        }

        private void Schedule()
        {
            if (scheduled) return;
            scheduled = true;
            SendCustomEventDelayedSeconds(nameof(Tick), refreshIntervalSeconds);
        }
    }
}
