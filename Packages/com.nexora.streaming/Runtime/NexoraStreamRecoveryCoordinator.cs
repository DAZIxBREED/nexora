using UdonSharp;
using UnityEngine;
using Nexora.Video;

namespace Nexora.Streaming
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraStreamRecoveryCoordinator : UdonSharpBehaviour
    {
        public NexoraReconnectPolicy reconnectPolicy;
        public NexoraVideoBackendRouter router;

        [Header("Health")]
        public float stallTimeoutSeconds = 12f;
        public float progressEpsilonSeconds = 0.05f;

        [HideInInspector] public bool recovering;
        [HideInInspector] public int recoveries;

        private float lastReportedTime;
        private float lastProgressLocalTime;
        private bool checkScheduled;

        private void Start()
        {
            lastProgressLocalTime = Time.realtimeSinceStartup;
            ScheduleCheck();
        }

        public void NotifyPlaybackProgress()
        {
            if (router == null) return;

            float current = router.backendReportedTime;
            if (Mathf.Abs(current - lastReportedTime) >= progressEpsilonSeconds)
            {
                lastReportedTime = current;
                lastProgressLocalTime = Time.realtimeSinceStartup;
                recovering = false;
                if (reconnectPolicy != null) reconnectPolicy.ResetPolicy();
            }
        }

        public void HealthCheck()
        {
            checkScheduled = false;
            NotifyPlaybackProgress();

            if (router != null && router.backendReady)
            {
                float stalledFor = Time.realtimeSinceStartup - lastProgressLocalTime;
                if (stalledFor >= Mathf.Max(1f, stallTimeoutSeconds))
                {
                    BeginRecovery();
                }
            }

            ScheduleCheck();
        }

        public void BeginRecovery()
        {
            if (router == null || reconnectPolicy == null || !reconnectPolicy.CanRetry()) return;

            float delay = reconnectPolicy.RegisterFailure();
            if (delay < 0f) return;

            recovering = true;
            router.ReportBackendNotReady();
            SendCustomEventDelayedSeconds(nameof(ExecuteRecovery), delay);
        }

        public void ExecuteRecovery()
        {
            if (router == null) return;
            recoveries++;
            router.Recover();
            lastProgressLocalTime = Time.realtimeSinceStartup;
        }

        public void ResetRecovery()
        {
            recovering = false;
            recoveries = 0;
            lastProgressLocalTime = Time.realtimeSinceStartup;
            if (reconnectPolicy != null) reconnectPolicy.ResetPolicy();
        }

        private void ScheduleCheck()
        {
            if (checkScheduled) return;
            checkScheduled = true;
            SendCustomEventDelayedSeconds(nameof(HealthCheck), 1f);
        }
    }
}
