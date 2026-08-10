using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using Nexora.Permissions;
using Nexora.Sync;
using Nexora.Video;

namespace Nexora.Streaming
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class NexoraStreamController : UdonSharpBehaviour
    {
        public const byte SourceUnknown = 0;
        public const byte SourceHls = 1;
        public const byte SourceDash = 2;
        public const byte SourceProviderLive = 3;
        public const byte SourceDirect = 4;

        [Header("Dependencies")]
        public NexoraAccessControl access;
        public NexoraMediaState mediaState;
        public NexoraVideoBackendRouter video;
        public NexoraReconnectPolicy reconnectPolicy;

        [Header("Lifecycle policy")]
        public float startupTimeoutSeconds = 15f;
        public float bufferingGraceSeconds = 8f;
        public float liveEdgeToleranceSeconds = 2.5f;
        public float healthSampleIntervalSeconds = 1f;
        public int healthySamplesToConfirmLive = 2;

        [Header("Synchronized stream state")]
        [UdonSynced] public VRCUrl sourceUrl;
        [UdonSynced] public byte sourceKind = SourceUnknown;
        [UdonSynced] public byte streamState = NexoraStreamState.Idle;
        [UdonSynced] public byte failureCode = NexoraStreamFailure.None;
        [UdonSynced] public int streamRevision;
        [UdonSynced] public int recoveryGeneration;
        [UdonSynced] public int sourcePlayerId = -1;
        [UdonSynced] public double stateServerTime;
        [UdonSynced] public double liveEdgeServerTime;
        [UdonSynced] public float reportedLatencySeconds;

        [Header("Local telemetry")]
        [HideInInspector] public int startCount;
        [HideInInspector] public int stopCount;
        [HideInInspector] public int recoveryCount;
        [HideInInspector] public int failedRecoveryCount;
        [HideInInspector] public int healthySampleCount;
        [HideInInspector] public int bufferingSampleCount;
        [HideInInspector] public int startupTimeoutCount;
        [HideInInspector] public int watchdogRecoveryCount;
        [HideInInspector] public float secondsInCurrentState;
        [HideInInspector] public float lastBackendTime;
        [HideInInspector] public float localLatencySeconds;

        private int consecutiveHealthySamples;
        private bool sampleScheduled;

        private void Start()
        {
            healthSampleIntervalSeconds = Mathf.Max(0.25f, healthSampleIntervalSeconds);
            startupTimeoutSeconds = Mathf.Max(2f, startupTimeoutSeconds);
            bufferingGraceSeconds = Mathf.Max(1f, bufferingGraceSeconds);
            liveEdgeToleranceSeconds = Mathf.Max(0.25f, liveEdgeToleranceSeconds);
            healthySamplesToConfirmLive = Mathf.Max(1, healthySamplesToConfirmLive);
            lastBackendTime = video == null ? 0f : video.backendReportedTime;
            ScheduleSample();
        }

        public void StartStream(VRCUrl url)
        {
            if (!Authorize("start")) return;
            if (VRCUrl.IsNullOrEmpty(url))
            {
                SetFailedAuthorized(NexoraStreamFailure.UnsupportedSource);
                return;
            }

            TakeOwnership();
            sourceUrl = url;
            sourceKind = ClassifySource(url);
            failureCode = NexoraStreamFailure.None;
            recoveryGeneration = 0;
            consecutiveHealthySamples = 0;
            startCount++;
            CommitState(NexoraStreamState.Connecting);

            if (reconnectPolicy != null) reconnectPolicy.ResetPolicy();
            StartPlaybackPath(url);
        }

        public void StopStream()
        {
            if (!Authorize("stop")) return;
            TakeOwnership();
            stopCount++;
            failureCode = NexoraStreamFailure.None;
            reportedLatencySeconds = 0f;
            consecutiveHealthySamples = 0;
            CommitState(NexoraStreamState.Stopped);

            if (mediaState != null) mediaState.Stop();
            else if (video != null) video.Stop();
        }

        public void ForceRecover()
        {
            if (!Authorize("recover")) return;
            if (!Networking.IsOwner(gameObject)) TakeOwnership();
            QueueRecoveryOwner(NexoraStreamFailure.BackendFault);
        }

        public void ForceRecoverFromWatchdog()
        {
            if (!Networking.IsOwner(gameObject)) return;
            watchdogRecoveryCount++;
            QueueRecoveryOwner(NexoraStreamFailure.Stalled);
        }

        public void MarkLiveEdgeNow()
        {
            if (!Authorize("mark-live-edge")) return;
            TakeOwnership();
            liveEdgeServerTime = Networking.GetServerTimeInSeconds();
            streamRevision++;
            StampActor();
            RequestSerialization();
        }

        public void HealthSampleTick()
        {
            sampleScheduled = false;
            secondsInCurrentState += healthSampleIntervalSeconds;
            if (video != null) EvaluateBackend();
            ScheduleSample();
        }

        public override void OnDeserialization()
        {
            secondsInCurrentState = 0f;
            consecutiveHealthySamples = 0;
        }

        public bool IsHealthy()
        {
            return streamState == NexoraStreamState.Live && failureCode == NexoraStreamFailure.None &&
                (video == null || video.faultCode == NexoraBackendFault.None);
        }

        public byte ClassifySource(VRCUrl url)
        {
            if (VRCUrl.IsNullOrEmpty(url)) return SourceUnknown;
            string raw = url.Get();
            if (string.IsNullOrEmpty(raw)) return SourceUnknown;
            string lower = raw.ToLower();
            if (lower.IndexOf(".m3u8") >= 0) return SourceHls;
            if (lower.IndexOf(".mpd") >= 0) return SourceDash;
            if (lower.IndexOf("twitch.tv") >= 0 || lower.IndexOf("youtube.com/live") >= 0 || lower.IndexOf("youtu.be/live") >= 0 || lower.IndexOf("kick.com") >= 0) return SourceProviderLive;
            if (lower.IndexOf(".mp4") >= 0 || lower.IndexOf(".webm") >= 0 || lower.IndexOf(".mov") >= 0) return SourceDirect;
            return SourceUnknown;
        }

        private void EvaluateBackend()
        {
            if (streamState == NexoraStreamState.Idle || streamState == NexoraStreamState.Stopped || streamState == NexoraStreamState.Failed) return;

            if (video.faultCode != NexoraBackendFault.None && video.faultCode != NexoraBackendFault.NotReady)
            {
                if (Networking.IsOwner(gameObject)) QueueRecoveryOwner(NexoraStreamFailure.BackendFault);
                return;
            }

            if (!video.backendReady)
            {
                bufferingSampleCount++;
                consecutiveHealthySamples = 0;
                if (Networking.IsOwner(gameObject) && streamState == NexoraStreamState.Connecting && secondsInCurrentState >= startupTimeoutSeconds)
                {
                    startupTimeoutCount++;
                    QueueRecoveryOwner(NexoraStreamFailure.StartupTimeout);
                    return;
                }
                if (Networking.IsOwner(gameObject) && streamState != NexoraStreamState.Recovering && streamState != NexoraStreamState.Connecting) CommitStateIfOwner(NexoraStreamState.Buffering);
                return;
            }

            float now = video.backendReportedTime;
            float progress = Mathf.Abs(now - lastBackendTime);
            lastBackendTime = now;

            if (progress < 0.01f)
            {
                bufferingSampleCount++;
                consecutiveHealthySamples = 0;
                if (Networking.IsOwner(gameObject) && (streamState == NexoraStreamState.Live || streamState == NexoraStreamState.Buffering) && secondsInCurrentState >= bufferingGraceSeconds)
                    QueueRecoveryOwner(NexoraStreamFailure.Stalled);
                else if (Networking.IsOwner(gameObject) && streamState != NexoraStreamState.Recovering && streamState != NexoraStreamState.Connecting)
                    CommitStateIfOwner(NexoraStreamState.Buffering);
                return;
            }

            healthySampleCount++;
            consecutiveHealthySamples++;
            if (Networking.IsOwner(gameObject) && reconnectPolicy != null && reconnectPolicy.attempt > 0 && consecutiveHealthySamples >= healthySamplesToConfirmLive) reconnectPolicy.ResetPolicy();

            if (Networking.IsOwner(gameObject) && consecutiveHealthySamples >= healthySamplesToConfirmLive)
            {
                failureCode = NexoraStreamFailure.None;
                CommitStateIfOwner(NexoraStreamState.Live);
                UpdateLatencyIfOwner();
            }
        }

        private void QueueRecoveryOwner(byte reason)
        {
            if (!Networking.IsOwner(gameObject) || streamState == NexoraStreamState.Recovering) return;

            float delay = 0.1f;
            if (reconnectPolicy != null)
            {
                if (!reconnectPolicy.CanRetry())
                {
                    failedRecoveryCount++;
                    SetFailedOwner(NexoraStreamFailure.RecoveryExhausted);
                    return;
                }
                delay = reconnectPolicy.RegisterFailure();
                if (delay < 0f)
                {
                    failedRecoveryCount++;
                    SetFailedOwner(NexoraStreamFailure.RecoveryExhausted);
                    return;
                }
            }

            failureCode = reason;
            recoveryGeneration++;
            recoveryCount++;
            CommitState(NexoraStreamState.Recovering);
            SendCustomEventDelayedSeconds(nameof(ExecuteRecovery), Mathf.Max(0.1f, delay));
        }

        public void ExecuteRecovery()
        {
            if (!Networking.IsOwner(gameObject) || streamState != NexoraStreamState.Recovering) return;
            consecutiveHealthySamples = 0;
            secondsInCurrentState = 0f;
            if (video != null) video.Recover();
            else if (mediaState != null && !VRCUrl.IsNullOrEmpty(sourceUrl)) StartPlaybackPath(sourceUrl);
            CommitState(NexoraStreamState.Connecting);
        }

        private void StartPlaybackPath(VRCUrl url)
        {
            if (mediaState != null)
            {
                mediaState.LoadMedia(url);
                mediaState.Play();
                return;
            }
            if (video != null)
            {
                video.mediaUrl = url;
                video.Load();
                video.Play();
            }
        }

        private void UpdateLatencyIfOwner()
        {
            if (!Networking.IsOwner(gameObject) || liveEdgeServerTime <= 0d) return;
            localLatencySeconds = Mathf.Max(0f, (float)(Networking.GetServerTimeInSeconds() - liveEdgeServerTime));
            float previous = reportedLatencySeconds;
            reportedLatencySeconds = localLatencySeconds;
            if (Mathf.Abs(previous - reportedLatencySeconds) >= 0.25f || reportedLatencySeconds > liveEdgeToleranceSeconds)
            {
                streamRevision++;
                StampActor();
                RequestSerialization();
            }
        }

        private void SetFailedAuthorized(byte reason)
        {
            TakeOwnership();
            SetFailedOwner(reason);
        }

        private void SetFailedOwner(byte reason)
        {
            if (!Networking.IsOwner(gameObject)) return;
            failureCode = reason;
            CommitState(NexoraStreamState.Failed);
        }

        private void CommitStateIfOwner(byte nextState)
        {
            if (!Networking.IsOwner(gameObject) || streamState == nextState) return;
            CommitState(nextState);
        }

        private void CommitState(byte nextState)
        {
            if (!Networking.IsOwner(gameObject)) return;
            streamState = nextState;
            stateServerTime = Networking.GetServerTimeInSeconds();
            secondsInCurrentState = 0f;
            streamRevision++;
            StampActor();
            RequestSerialization();
        }

        private void StampActor()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            sourcePlayerId = local == null ? -1 : local.playerId;
        }

        private bool Authorize(string action)
        {
            return access != null && access.AuthorizeStreaming(action);
        }

        private void TakeOwnership()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (local != null && local.IsValid() && !Networking.IsOwner(gameObject)) Networking.SetOwner(local, gameObject);
        }

        private void ScheduleSample()
        {
            if (sampleScheduled) return;
            sampleScheduled = true;
            SendCustomEventDelayedSeconds(nameof(HealthSampleTick), healthSampleIntervalSeconds);
        }
    }
}
