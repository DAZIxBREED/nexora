using UdonSharp;
using UnityEngine;
using Nexora.Api;
using Nexora.Video;

namespace Nexora.Sync
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraSnapshotApplier : UdonSharpBehaviour
    {
        public NexoraMediaState state;
        public NexoraVideoBackendRouter video;

        [Header("Correction thresholds")]
        public float softDriftSeconds = 0.35f;
        public float hardDriftSeconds = 1.25f;
        public float checkIntervalSeconds = 0.5f;

        private int appliedRevision = -1;
        private bool scheduled;

        private void Start()
        {
            ApplySnapshot(true);
            Schedule();
        }

        public void ApplySnapshot(bool force)
        {
            if (state == null || video == null) return;
            if (!force && appliedRevision == state.revision) return;

            appliedRevision = state.revision;
            video.mediaUrl = state.mediaUrl;
            video.mediaTime = (float)state.ExpectedMediaTime();
            video.volume = state.volume;
            video.loop = state.loop;

            if (state.playbackState == NexoraPlaybackState.Loading)
            {
                video.Load();
                return;
            }

            if (state.playbackState == NexoraPlaybackState.Playing)
            {
                video.Seek();
                video.Play();
                return;
            }

            if (state.playbackState == NexoraPlaybackState.Paused)
            {
                video.Seek();
                video.Pause();
                return;
            }

            video.Stop();
        }

        public void Tick()
        {
            scheduled = false;
            if (state != null && video != null)
            {
                ApplySnapshot(false);

                if (state.playbackState == NexoraPlaybackState.Playing && video.backendReady)
                {
                    float expected = (float)state.ExpectedMediaTime();
                    float drift = video.backendReportedTime - expected;
                    float magnitude = Mathf.Abs(drift);
                    if (magnitude >= hardDriftSeconds || magnitude >= softDriftSeconds)
                    {
                        video.mediaTime = expected;
                        video.Seek();
                    }
                }
            }
            Schedule();
        }

        private void Schedule()
        {
            if (scheduled) return;
            scheduled = true;
            SendCustomEventDelayedSeconds(nameof(Tick), Mathf.Max(0.1f, checkIntervalSeconds));
        }
    }
}
