using UdonSharp;
using UnityEngine;
using Nexora.Video;

namespace Nexora.Streaming
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraStreamWatchdog : UdonSharpBehaviour
    {
        public NexoraVideoBackendRouter video;
        public NexoraReconnectPolicy reconnectPolicy;

        [Header("Stall detection")]
        public float sampleIntervalSeconds = 1f;
        public float stallTimeoutSeconds = 5f;
        public float minimumProgressSeconds = 0.05f;
        public float readyGraceSeconds = 4f;

        [HideInInspector] public float secondsWithoutProgress;
        [HideInInspector] public float secondsWaitingForReady;
        [HideInInspector] public bool stalled;
        [HideInInspector] public bool recoveryPending;
        [HideInInspector] public int recoveryCount;
        [HideInInspector] public int exhaustedRecoveryCount;
        [HideInInspector] public int healthySampleCount;
        [HideInInspector] public int stalledSampleCount;
        [HideInInspector] public float scheduledRecoveryDelay;

        private float lastBackendTime;
        private bool sampleScheduled;

        private void Start()
        {
            lastBackendTime = video == null ? 0f : video.backendReportedTime;
            ScheduleSample();
        }

        public void Tick()
        {
            sampleScheduled = false;

            if (video == null)
            {
                ScheduleSample();
                return;
            }

            if (recoveryPending)
            {
                ScheduleSample();
                return;
            }

            if (video.faultCode != NexoraBackendFault.None && video.faultCode != NexoraBackendFault.NotReady)
            {
                QueueRecovery();
                ScheduleSample();
                return;
            }

            if (!video.backendReady)
            {
                secondsWaitingForReady += Mathf.Max(0.1f, sampleIntervalSeconds);
                if (secondsWaitingForReady >= Mathf.Max(1f, readyGraceSeconds))
                {
                    video.ReportFault(NexoraBackendFault.NotReady, "Backend did not become ready inside the watchdog grace period.");
                    QueueRecovery();
                }
                ScheduleSample();
                return;
            }

            secondsWaitingForReady = 0f;
            float now = video.backendReportedTime;
            float delta = Mathf.Abs(now - lastBackendTime);

            if (delta >= Mathf.Max(0.001f, minimumProgressSeconds))
            {
                healthySampleCount++;
                secondsWithoutProgress = 0f;
                stalled = false;
                if (reconnectPolicy != null && reconnectPolicy.attempt > 0)
                {
                    reconnectPolicy.ResetPolicy();
                }
            }
            else
            {
                stalledSampleCount++;
                secondsWithoutProgress += Mathf.Max(0.1f, sampleIntervalSeconds);
                if (secondsWithoutProgress >= Mathf.Max(1f, stallTimeoutSeconds))
                {
                    stalled = true;
                    video.ReportFault(NexoraBackendFault.Stalled, "Stream watchdog detected stalled playback.");
                    QueueRecovery();
                }
            }

            lastBackendTime = now;
            ScheduleSample();
        }

        public void QueueRecovery()
        {
            if (video == null || recoveryPending) return;

            if (reconnectPolicy != null && !reconnectPolicy.CanRetry())
            {
                exhaustedRecoveryCount++;
                video.ReportFault(NexoraBackendFault.RecoveryExhausted, "Stream recovery attempts exhausted.");
                return;
            }

            float delay = 0.1f;
            if (reconnectPolicy != null)
            {
                delay = reconnectPolicy.RegisterFailure();
                if (delay < 0f)
                {
                    exhaustedRecoveryCount++;
                    video.ReportFault(NexoraBackendFault.RecoveryExhausted, "Stream recovery attempts exhausted.");
                    return;
                }
            }

            recoveryPending = true;
            scheduledRecoveryDelay = Mathf.Max(0.1f, delay);
            SendCustomEventDelayedSeconds(nameof(ExecuteRecovery), scheduledRecoveryDelay);
        }

        public void ExecuteRecovery()
        {
            recoveryPending = false;
            if (video == null) return;

            recoveryCount++;
            stalled = false;
            secondsWithoutProgress = 0f;
            secondsWaitingForReady = 0f;
            lastBackendTime = video.backendReportedTime;
            video.Recover();
        }

        public void ResetWatchdog()
        {
            recoveryPending = false;
            stalled = false;
            secondsWithoutProgress = 0f;
            secondsWaitingForReady = 0f;
            scheduledRecoveryDelay = 0f;
            lastBackendTime = video == null ? 0f : video.backendReportedTime;
            if (reconnectPolicy != null) reconnectPolicy.ResetPolicy();
        }

        private void ScheduleSample()
        {
            if (sampleScheduled) return;
            sampleScheduled = true;
            SendCustomEventDelayedSeconds(nameof(Tick), Mathf.Max(0.1f, sampleIntervalSeconds));
        }
    }
}
