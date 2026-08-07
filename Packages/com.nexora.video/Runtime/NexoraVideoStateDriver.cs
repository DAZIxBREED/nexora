using UdonSharp;
using UnityEngine;
using Nexora.Api;
using Nexora.Sync;

namespace Nexora.Video
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraVideoStateDriver : UdonSharpBehaviour
    {
        public NexoraMediaState state;
        public NexoraVideoBackendRouter router;

        [Header("Correction")]
        public float correctionIntervalSeconds = 1f;
        public float softDriftSeconds = 0.35f;
        public float hardDriftSeconds = 1.25f;

        private int appliedRevision = -1;
        private bool correctionScheduled;

        private void Start()
        {
            ApplySnapshot(true);
            ScheduleCorrection();
        }

        public void ApplySnapshot()
        {
            ApplySnapshot(false);
        }

        public void ApplySnapshot(bool force)
        {
            if (state == null || router == null) return;
            if (!force && appliedRevision == state.revision) return;

            appliedRevision = state.revision;
            router.mediaUrl = state.mediaUrl;
            router.mediaTime = (float)state.ExpectedMediaTime();
            router.volume = state.volume;
            router.loop = state.loop;
            router.PushSettings();

            if (state.playbackState == NexoraPlaybackState.Loading)
            {
                router.Load();
            }
            else if (state.playbackState == NexoraPlaybackState.Playing)
            {
                router.Seek();
                router.Play();
            }
            else if (state.playbackState == NexoraPlaybackState.Paused)
            {
                router.Seek();
                router.Pause();
            }
            else
            {
                router.Stop();
            }
        }

        public void CorrectionTick()
        {
            correctionScheduled = false;

            if (state != null && router != null && state.playbackState == NexoraPlaybackState.Playing && router.backendReady)
            {
                float expected = (float)state.ExpectedMediaTime();
                float drift = router.backendReportedTime - expected;
                float magnitude = Mathf.Abs(drift);

                if (magnitude >= hardDriftSeconds || magnitude >= softDriftSeconds)
                {
                    router.mediaTime = expected;
                    router.Seek();
                }
            }

            ScheduleCorrection();
        }

        public void ForceLateJoinReplay()
        {
            ApplySnapshot(true);
        }

        private void ScheduleCorrection()
        {
            if (correctionScheduled) return;
            correctionScheduled = true;
            SendCustomEventDelayedSeconds(nameof(CorrectionTick), Mathf.Max(0.1f, correctionIntervalSeconds));
        }
    }
}
