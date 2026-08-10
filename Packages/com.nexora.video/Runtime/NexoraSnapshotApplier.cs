using UdonSharp;
using UnityEngine;
using Nexora.Api;
using Nexora.Sync;

namespace Nexora.Video
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraSnapshotApplier : UdonSharpBehaviour
    {
        public NexoraMediaState state;
        public NexoraVideoBackendRouter video;

        [Header("Correction thresholds")]
        public float softDriftSeconds = 0.35f;
        public float hardDriftSeconds = 1.25f;
        public float correctionCooldownSeconds = 1f;
        public float checkIntervalSeconds = 0.5f;

        [Header("Runtime telemetry")]
        [HideInInspector] public int appliedRevision = -1;
        [HideInInspector] public int appliedAuthorityEpoch = -1;
        [HideInInspector] public int appliedCommandSequence = -1;
        [HideInInspector] public int snapshotApplyCount;
        [HideInInspector] public int softCorrectionCount;
        [HideInInspector] public int hardCorrectionCount;
        [HideInInspector] public float lastMeasuredDrift;

        private string appliedUrl = "";
        private float lastCorrectionAt = -1000f;
        private bool scheduled;

        private void Start()
        {
            if (state != null)
            {
                state.AcceptCurrentSnapshot(true);
            }
            ApplyAcceptedSnapshot(true);
            Schedule();
        }

        public void ApplySnapshot(bool force)
        {
            ApplyAcceptedSnapshot(force);
        }

        public void ApplyAcceptedSnapshot(bool force)
        {
            if (state == null || video == null) return;

            bool newerEpoch = state.acceptedAuthorityEpoch > appliedAuthorityEpoch;
            bool newerRevision = state.acceptedAuthorityEpoch == appliedAuthorityEpoch && state.acceptedRevision > appliedRevision;
            if (!force && !newerEpoch && !newerRevision) return;

            string newUrl = UrlString(state.acceptedMediaUrl);
            bool mediaChanged = newUrl != appliedUrl;
            byte command = state.acceptedCommandType;

            appliedAuthorityEpoch = state.acceptedAuthorityEpoch;
            appliedRevision = state.acceptedRevision;
            appliedCommandSequence = state.acceptedCommandSequence;
            appliedUrl = newUrl;
            snapshotApplyCount++;

            video.mediaUrl = state.acceptedMediaUrl;
            video.mediaTime = (float)state.AcceptedExpectedMediaTime();
            video.volume = state.acceptedVolume;
            video.loop = state.acceptedLoop;

            if (force || mediaChanged || command == NexoraSyncCommand.Load)
            {
                video.ResetToPrimaryBackend();
                video.Load();
                if (state.acceptedPlaybackState == NexoraPlaybackState.Playing)
                {
                    video.Play();
                }
                return;
            }

            if (command == NexoraSyncCommand.Volume || command == NexoraSyncCommand.Loop || command == NexoraSyncCommand.PlaybackSpeed)
            {
                video.PushSettings();
                return;
            }

            if (command == NexoraSyncCommand.Stop || state.acceptedPlaybackState == NexoraPlaybackState.Stopped)
            {
                video.Stop();
                return;
            }

            if (command == NexoraSyncCommand.Pause || state.acceptedPlaybackState == NexoraPlaybackState.Paused)
            {
                video.Seek();
                video.Pause();
                return;
            }

            if (command == NexoraSyncCommand.Seek)
            {
                video.Seek();
                if (state.acceptedPlaybackState == NexoraPlaybackState.Playing) video.Play();
                return;
            }

            if (command == NexoraSyncCommand.AuthorityTransfer)
            {
                ReconcileToAcceptedState();
                return;
            }

            if (command == NexoraSyncCommand.Play || state.acceptedPlaybackState == NexoraPlaybackState.Playing)
            {
                video.Seek();
                video.Play();
                return;
            }

            ReconcileToAcceptedState();
        }

        public void Tick()
        {
            scheduled = false;

            if (state != null && video != null)
            {
                ApplyAcceptedSnapshot(false);
                CorrectDrift();
            }

            Schedule();
        }

        private void CorrectDrift()
        {
            if (state.acceptedPlaybackState != NexoraPlaybackState.Playing || !video.backendReady) return;

            float expected = (float)state.AcceptedExpectedMediaTime();
            lastMeasuredDrift = video.backendReportedTime - expected;
            float magnitude = Mathf.Abs(lastMeasuredDrift);

            if (magnitude < Mathf.Max(0.05f, softDriftSeconds)) return;
            if (Time.realtimeSinceStartup - lastCorrectionAt < Mathf.Max(0.1f, correctionCooldownSeconds)) return;

            lastCorrectionAt = Time.realtimeSinceStartup;
            video.mediaTime = expected;

            if (magnitude >= Mathf.Max(softDriftSeconds, hardDriftSeconds))
            {
                hardCorrectionCount++;
                video.Recover();
                return;
            }

            softCorrectionCount++;
            video.Seek();
        }

        private void ReconcileToAcceptedState()
        {
            video.mediaTime = (float)state.AcceptedExpectedMediaTime();

            if (state.acceptedPlaybackState == NexoraPlaybackState.Playing)
            {
                video.Seek();
                video.Play();
            }
            else if (state.acceptedPlaybackState == NexoraPlaybackState.Paused)
            {
                video.Seek();
                video.Pause();
            }
            else if (state.acceptedPlaybackState == NexoraPlaybackState.Loading)
            {
                video.Load();
            }
            else
            {
                video.Stop();
            }
        }

        private string UrlString(VRC.SDKBase.VRCUrl url)
        {
            if (url == null) return "";
            string value = url.Get();
            return value == null ? "" : value;
        }

        private void Schedule()
        {
            if (scheduled) return;
            scheduled = true;
            SendCustomEventDelayedSeconds(nameof(Tick), Mathf.Max(0.1f, checkIntervalSeconds));
        }
    }
}
