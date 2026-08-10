using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using Nexora.Video;

namespace Nexora.Streaming
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraStreamWatchdog : UdonSharpBehaviour
    {
        [Header("Dependencies")]
        public NexoraVideoBackendRouter video;
        public NexoraStreamController controller;

        [Header("Observation policy")]
        public float sampleIntervalSeconds = 1f;
        public float stallTimeoutSeconds = 5f;
        public float minimumProgressSeconds = 0.05f;
        public float readyGraceSeconds = 4f;

        [Header("Local telemetry")]
        [HideInInspector] public float secondsWithoutProgress;
        [HideInInspector] public float secondsWaitingForReady;
        [HideInInspector] public bool stalled;
        [HideInInspector] public int healthySampleCount;
        [HideInInspector] public int stalledSampleCount;
        [HideInInspector] public int backendFaultSampleCount;
        [HideInInspector] public int delegatedRecoveryCount;

        private float lastBackendTime;
        private bool sampleScheduled;

        private void Start()
        {
            sampleIntervalSeconds = Mathf.Max(0.1f, sampleIntervalSeconds);
            stallTimeoutSeconds = Mathf.Max(1f, stallTimeoutSeconds);
            readyGraceSeconds = Mathf.Max(1f, readyGraceSeconds);
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

            if (video.faultCode != NexoraBackendFault.None && video.faultCode != NexoraBackendFault.NotReady)
            {
                backendFaultSampleCount++;
                DelegateRecovery();
                ScheduleSample();
                return;
            }

            if (!video.backendReady)
            {
                secondsWaitingForReady += sampleIntervalSeconds;
                if (secondsWaitingForReady >= readyGraceSeconds)
                {
                    video.ReportFault(NexoraBackendFault.NotReady, "Stream watchdog observed a backend readiness timeout.");
                    DelegateRecovery();
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
            }
            else
            {
                stalledSampleCount++;
                secondsWithoutProgress += sampleIntervalSeconds;
                if (secondsWithoutProgress >= stallTimeoutSeconds)
                {
                    stalled = true;
                    video.ReportFault(NexoraBackendFault.Stalled, "Stream watchdog observed stalled playback.");
                    DelegateRecovery();
                }
            }

            lastBackendTime = now;
            ScheduleSample();
        }

        public void ResetWatchdog()
        {
            stalled = false;
            secondsWithoutProgress = 0f;
            secondsWaitingForReady = 0f;
            lastBackendTime = video == null ? 0f : video.backendReportedTime;
        }

        private void DelegateRecovery()
        {
            if (controller == null) return;
            if (!Networking.IsOwner(controller.gameObject)) return;
            delegatedRecoveryCount++;
            controller.ForceRecoverFromWatchdog();
            secondsWithoutProgress = 0f;
            secondsWaitingForReady = 0f;
        }

        private void ScheduleSample()
        {
            if (sampleScheduled) return;
            sampleScheduled = true;
            SendCustomEventDelayedSeconds(nameof(Tick), sampleIntervalSeconds);
        }
    }
}
