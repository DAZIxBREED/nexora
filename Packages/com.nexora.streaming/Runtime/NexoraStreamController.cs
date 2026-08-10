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
                SetFailed(NexoraStreamFailure.UnsupportedSource);
                return;
            }

            TakeOwnership();
            sourceUrl = url;
            failureCode = NexoraStreamFailure.None;
            recoveryGeneration = 0;
            startCount++;
            CommitState(NexoraStreamState.Connecting);

            if (reconnectPolicy != null) reconnectPolicy.ResetPolicy();
            if (mediaState != null)
            {
                mediaState.LoadMedia(url);
                mediaState.Play();
            }
            else if (video != null)
            {
                video.mediaUrl = url;
                video.Load();
                video.Play();
            }
        }

        public void StopStream()
        {
            if (!Authorize("stop")) return;
            TakeOwnership();
            stopCount++;
            failureCode = NexoraStreamFailure.None;
            reportedLatencySeconds = 0f;
            CommitState(NexoraStreamState.Stopped);

            if (mediaState != null) mediaState.Stop();
            else if (video != null) video.Stop();
        }

        public void ForceRecover()
        {
            if (!Authorize("recover")) return;
            QueueRecovery(NexoraStreamFailure.BackendFault);
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

            if (video != null)
            {
                EvaluateBackend();
            }

            ScheduleSample();
        }

        public override void OnDeserialization()
        {
            secondsInCurrentState = 0f;
            consecutiveHealthySamples = 0;
        }

        private void EvaluateBackend()
        {
            if (streamState == NexoraStreamState.Idle || streamState == NexoraStreamState.Stopped || streamState == NexoraStreamState.Failed)
            {
                return;
            }

            if (video.faultCode != NexoraBackendFault.None && video.faultCode != NexoraBackendFault.NotReady)
            {
                QueueRecovery(NexoraStreamFailure.BackendFault);
                return;
            }

            if (!video.backendReady)
            {
                bufferingSampleCount++;
                consecutiveHealthySamples = 0;

                if (streamState == NexoraStreamState.Connecting && secondsInCurrentState >= startupTimeoutSeconds)
                {
                    startupTimeoutCount++;
                    QueueRecovery(NexoraStreamFailure.StartupTimeout);
                    return;
                }

                if (streamState != NexoraStreamState.Recovering && streamState != NexoraStreamState.Connecting)
                {
                    CommitStateIfOwner(NexoraStreamState.Buffering);
                }
                return;
            }

            float now = video.backendReportedTime;
            float progress = Mathf.Abs(now - lastBackendTime);
            lastBackendTime = now;

            if (progress < 0.01f)
            {
                bufferingSampleCount++;
                consecutiveHealthySamples = 0;
                if (streamState == NexoraStreamState.Live && secondsInCurrentState >= bufferingGraceSeconds)
                {
                    QueueRecovery(NexoraStreamFailure.Stalled);
                }
                else if (streamState != NexoraStreamState.Recovering)
                {
                    CommitStateIfOwner(NexoraStreamState.Buffering);
                }
                return;
            }

            healthySampleCount++;
            consecutiveHealthySamples++;
            if (reconnectPolicy != null && reconnectPolicy.attempt > 0 && consecutiveHealthySamples >= healthySamplesToConfirmLive)
            {
                reconnectPolicy.ResetPolicy();
            }

            if (consecutiveHealthySamples >= healthySamplesToConfirmLive)
            {
                failureCode = NexoraStreamFailure.None;
                CommitStateIfOwner(NexoraStreamState.Live);
                UpdateLatencyIfOwner();
            }
        }

        private void QueueRecovery(byte reason)
        {
            if (streamState == NexoraStreamState.Recovering) return;

            float delay = 0.1f;
            if (reconnectPolicy != null)
            {
                if (!reconnectPolicy.CanRetry())
                {
                    failedRecoveryCount++;
                    SetFailed(NexoraStreamFailure.RecoveryExhausted);
                    return;
                }
                delay = reconnectPolicy.RegisterFailure();
                if (delay < 0f)
                {
                    failedRecoveryCount++;
                    SetFailed(NexoraStreamFailure.RecoveryExhausted);
                    return;
                }
            }

            failureCode = reason;
            recoveryGeneration++;
            recoveryCount++;
            CommitStateIfOwner(NexoraStreamState.Recovering);
            SendCustomEventDelayedSeconds(nameof(ExecuteRecovery), Mathf.Max(0.1f, delay));
        }

        public void ExecuteRecovery()
        {
            if (streamState != NexoraStreamState.Recovering) return;

            consecutiveHealthySamples = 0;
            secondsInCurrentState = 0f;
            if (video != null) video.Recover();
            else if (mediaState != null && !VRCUrl.IsNullOrEmpty(sourceUrl))
            {
                mediaState.LoadMedia(sourceUrl);
                mediaState.Play();
            }

            CommitStateIfOwner(NexoraStreamState.Connecting);
        }

        private void UpdateLatencyIfOwner()
        {
            if (liveEdgeServerTime <= 0d) return;
            localLatencySeconds = Mathf.Max(0f, (float)(Networking.GetServerTimeInSeconds() - liveEdgeServerTime));
            if (!Networking.IsOwner(gameObject)) return;

            float previous = reportedLatencySeconds;
            reportedLatencySeconds = localLatencySeconds;
            if (Mathf.Abs(previous - reportedLatencySeconds) >= 0.25f || reportedLatencySeconds > liveEdgeToleranceSeconds)
            {
                streamRevision++;
                StampActor();
                RequestSerialization();
            }
        }

        private void SetFailed(byte reason)
        {
            failureCode = reason;
            if (Networking.LocalPlayer != null) TakeOwnership();
            CommitState(NexoraStreamState.Failed);
        }

        private void CommitStateIfOwner(byte nextState)
        {
            if (!Networking.IsOwner(gameObject)) return;
            if (streamState == nextState) return;
            CommitState(nextState);
        }

        private void CommitState(byte nextState)
        {
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
            if (local != null && local.IsValid() && !Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(local, gameObject);
            }
        }

        private void ScheduleSample()
        {
            if (sampleScheduled) return;
            sampleScheduled = true;
            SendCustomEventDelayedSeconds(nameof(HealthSampleTick), healthSampleIntervalSeconds);
        }
    }
}
