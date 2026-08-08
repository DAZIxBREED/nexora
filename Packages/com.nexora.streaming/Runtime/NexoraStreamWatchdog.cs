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

        [HideInInspector] public float secondsWithoutProgress;
        [HideInInspector] public bool stalled;
        [HideInInspector] public int recoveryCount;
        [HideInInspector] public int exhaustedRecoveryCount;

        private float lastBackendTime;
        private bool scheduled;

        private void Start()
        {
            lastBackendTime = video == null ? 0f : video.backendReportedTime;
            Schedule();
        }

        public void Tick()
        {
            scheduled = false;

            if (video != null)
            {
                if (video.faultCode != NexoraBackendFault.None && video.faultCode != NexoraBackendFault.NotReady)
                {
                    Recover();
                }
                else if (video.backendReady)
                {
                    float now = video.backendReportedTime;
                    float delta = Mathf.Abs(now - lastBackendTime);
                    if (delta >= minimumProgressSeconds)
                    {
                        secondsWithoutProgress = 0f;
                        stalled = false;
                        if (reconnectPolicy != null) reconnectPolicy.ResetPolicy();
                    }
                    else
                    {
                        secondsWithoutProgress += Mathf.Max(0.1f, sampleIntervalSeconds);
                        if (secondsWithoutProgress >= Mathf.Max(1f, stallTimeoutSeconds))
                        {
                            stalled = true;
                            video.ReportFault(NexoraBackendFault.Stalled, "Stream watchdog detected stalled playback.");
                            Recover();
                        }
                    }
                    lastBackendTime = now;
                }
            }

            Schedule();
        }

        public void Recover()
        {
            if (video == null) return;
            if (reconnectPolicy != null && !reconnectPolicy.CanRetry())
            {
                exhaustedRecoveryCount++;
                video.ReportFault(NexoraBackendFault.RecoveryExhausted, "Stream recovery attempts exhausted.");
                return;
            }

            recoveryCount++;
            secondsWithoutProgress = 0f;
            video.Recover();
            if (reconnectPolicy != null) reconnectPolicy.RegisterFailure();
        }

        private void Schedule()
        {
            if (scheduled) return;
            scheduled = true;
            SendCustomEventDelayedSeconds(nameof(Tick), Mathf.Max(0.1f, sampleIntervalSeconds));
        }
    }
}
