using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using Nexora.Api;
using Nexora.Sync;
using Nexora.Video;
using Nexora.Streaming;
using Nexora.Playlists;
using Nexora.Permissions;

namespace Nexora.Diagnostics
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraHealthMonitor : UdonSharpBehaviour
    {
        public const byte HealthUnknown = 0;
        public const byte HealthHealthy = 1;
        public const byte HealthWarning = 2;
        public const byte HealthCritical = 3;

        [Header("Telemetry sources")]
        public NexoraDiagnostics diagnostics;
        public NexoraMediaState mediaState;
        public NexoraSyncStatus syncStatus;
        public NexoraAuthorityLease authorityLease;
        public NexoraVideoBackendRouter video;
        public NexoraStreamController stream;
        public NexoraPlaylist playlist;
        public NexoraAccessControl permissions;

        [Header("Sampling")]
        public float sampleIntervalSeconds = 1f;
        public float driftWarningSeconds = 0.50f;
        public float driftCriticalSeconds = 1.50f;

        [Header("Live health")]
        [HideInInspector] public byte overallHealth = HealthUnknown;
        [HideInInspector] public byte syncHealth = HealthUnknown;
        [HideInInspector] public byte videoHealth = HealthUnknown;
        [HideInInspector] public byte streamHealth = HealthUnknown;
        [HideInInspector] public byte playlistHealth = HealthUnknown;
        [HideInInspector] public byte permissionsHealth = HealthUnknown;
        [HideInInspector] public string platformName = "Unknown";
        [HideInInspector] public bool localPlayerInVR;
        [HideInInspector] public float currentDriftSeconds;
        [HideInInspector] public int healthSampleCount;
        [HideInInspector] public int healthTransitionCount;
        [HideInInspector] public int emittedFaultCount;

        [Header("Observed operator state")]
        [HideInInspector] public int authorityPlayerId = -1;
        [HideInInspector] public int authorityEpoch = -1;
        [HideInInspector] public float authorityHeartbeatAge;
        [HideInInspector] public int acceptedRevision = -1;
        [HideInInspector] public bool backendReady;
        [HideInInspector] public bool backendFallback;
        [HideInInspector] public byte backendFault;
        [HideInInspector] public byte observedStreamState;
        [HideInInspector] public byte observedStreamFailure;
        [HideInInspector] public int queueCount;
        [HideInInspector] public int requestCount;
        [HideInInspector] public int quarantineCount;
        [HideInInspector] public bool controlsLocked;
        [HideInInspector] public byte localRole;

        private byte previousOverall = 255;
        private byte previousSync = 255;
        private byte previousVideo = 255;
        private byte previousStream = 255;
        private byte previousPlaylist = 255;
        private byte previousPermissions = 255;
        private byte previousBackendFault = 255;
        private byte previousStreamFailure = 255;
        private byte previousStreamState = 255;
        private bool previousAuthorityStale;
        private int previousStaleSnapshotCount;
        private int previousDeniedControlCount;
        private int previousDeniedAdministrationCount;
        private int previousQuarantineCount;
        private bool scheduled;

        private void Start()
        {
            sampleIntervalSeconds = Mathf.Max(0.25f, sampleIntervalSeconds);
            driftWarningSeconds = Mathf.Max(0.05f, driftWarningSeconds);
            driftCriticalSeconds = Mathf.Max(driftWarningSeconds, driftCriticalSeconds);
            Sample();
            CaptureOperatorSnapshot();
            Schedule();
        }

        public void Tick()
        {
            scheduled = false;
            Sample();
            Schedule();
        }

        public void Sample()
        {
            healthSampleCount++;
            ResolvePlatform();
            SampleSync();
            SampleVideo();
            SampleStream();
            SamplePlaylist();
            SamplePermissions();

            overallHealth = HighestHealth(syncHealth, videoHealth);
            overallHealth = HighestHealth(overallHealth, streamHealth);
            overallHealth = HighestHealth(overallHealth, playlistHealth);
            overallHealth = HighestHealth(overallHealth, permissionsHealth);

            EmitHealthTransition("Sync", previousSync, syncHealth);
            EmitHealthTransition("Video", previousVideo, videoHealth);
            EmitHealthTransition("Streaming", previousStream, streamHealth);
            EmitHealthTransition("Playlist", previousPlaylist, playlistHealth);
            EmitHealthTransition("Permissions", previousPermissions, permissionsHealth);
            EmitHealthTransition("Overall", previousOverall, overallHealth);

            EmitFaultTransitions();

            previousSync = syncHealth;
            previousVideo = videoHealth;
            previousStream = streamHealth;
            previousPlaylist = playlistHealth;
            previousPermissions = permissionsHealth;
            previousOverall = overallHealth;
        }

        public void CaptureOperatorSnapshot()
        {
            if (diagnostics == null) return;
            diagnostics.LogFrom("HealthSnapshot", BuildOperatorSummary());
        }

        public string BuildOperatorSummary()
        {
            string summary = "Platform=" + platformName + " Overall=" + HealthName(overallHealth);
            summary += "\nAuthority player=" + authorityPlayerId + " epoch=" + authorityEpoch + " heartbeatAge=" + FormatSeconds(authorityHeartbeatAge);
            summary += "\nSync revision=" + acceptedRevision + " drift=" + FormatSeconds(currentDriftSeconds) + " health=" + HealthName(syncHealth);
            summary += "\nVideo ready=" + BoolName(backendReady) + " fallback=" + BoolName(backendFallback) + " fault=" + BackendFaultName(backendFault) + " health=" + HealthName(videoHealth);

            if (stream != null)
            {
                summary += "\nStream state=" + StreamStateName(observedStreamState) + " failure=" + StreamFailureName(observedStreamFailure) +
                    " latency=" + FormatSeconds(stream.reportedLatencySeconds) + " recoveryGen=" + stream.recoveryGeneration + " health=" + HealthName(streamHealth);
            }
            else
            {
                summary += "\nStream state=NotConfigured health=" + HealthName(streamHealth);
            }

            if (playlist != null)
            {
                summary += "\nPlaylist active=" + playlist.activePlaylistIndex + " current=" + playlist.currentIndex + " queue=" + queueCount +
                    " requests=" + requestCount + " history=" + playlist.historyCount + " quarantine=" + quarantineCount +
                    " revision=" + playlist.playlistRevision + " health=" + HealthName(playlistHealth);
            }
            else
            {
                summary += "\nPlaylist state=NotConfigured health=" + HealthName(playlistHealth);
            }

            if (permissions != null)
            {
                summary += "\nPermissions locked=" + BoolName(controlsLocked) + " role=" + RoleName(localRole) +
                    " deniedControl=" + permissions.deniedControlCount + " deniedAdmin=" + permissions.deniedAdministrationCount +
                    " health=" + HealthName(permissionsHealth);
            }
            else
            {
                summary += "\nPermissions state=Missing health=" + HealthName(permissionsHealth);
            }

            return summary;
        }

        private void SampleSync()
        {
            syncHealth = HealthHealthy;
            currentDriftSeconds = 0f;

            if (syncStatus == null || mediaState == null || authorityLease == null)
            {
                syncHealth = HealthCritical;
                authorityPlayerId = -1;
                authorityEpoch = -1;
                acceptedRevision = -1;
                return;
            }

            syncStatus.Sample();
            authorityPlayerId = authorityLease.authorityPlayerId;
            authorityEpoch = authorityLease.authorityEpoch;
            authorityHeartbeatAge = syncStatus.authorityHeartbeatAge;
            acceptedRevision = mediaState.acceptedRevision;

            if (syncStatus.authorityStale)
            {
                syncHealth = HealthCritical;
            }
            else if (syncStatus.snapshotAging)
            {
                syncHealth = HealthWarning;
            }

            if (video != null && video.backendReady && mediaState.acceptedPlaybackState == NexoraPlaybackState.Playing)
            {
                float expected = (float)mediaState.AcceptedExpectedMediaTime();
                currentDriftSeconds = Mathf.Abs(video.backendReportedTime - expected);
                if (currentDriftSeconds >= driftCriticalSeconds) syncHealth = HealthCritical;
                else if (currentDriftSeconds >= driftWarningSeconds && syncHealth < HealthWarning) syncHealth = HealthWarning;
            }
        }

        private void SampleVideo()
        {
            videoHealth = HealthHealthy;
            backendReady = false;
            backendFallback = false;
            backendFault = NexoraBackendFault.PlatformBackendMissing;

            if (video == null)
            {
                videoHealth = HealthCritical;
                return;
            }

            backendReady = video.backendReady;
            backendFallback = video.usingFallback;
            backendFault = video.faultCode;

            if (backendFault == NexoraBackendFault.PlatformBackendMissing || backendFault == NexoraBackendFault.RecoveryExhausted)
            {
                videoHealth = HealthCritical;
            }
            else if (backendFault == NexoraBackendFault.LoadFailed || backendFault == NexoraBackendFault.PlaybackFailed ||
                     backendFault == NexoraBackendFault.UnsupportedMedia || backendFault == NexoraBackendFault.Stalled)
            {
                videoHealth = HealthCritical;
            }
            else if (backendFault == NexoraBackendFault.NotReady || backendFallback)
            {
                videoHealth = HealthWarning;
            }
        }

        private void SampleStream()
        {
            if (stream == null)
            {
                streamHealth = HealthUnknown;
                observedStreamState = 0;
                observedStreamFailure = 0;
                return;
            }

            observedStreamState = stream.streamState;
            observedStreamFailure = stream.failureCode;
            streamHealth = HealthHealthy;

            if (stream.streamState == NexoraStreamState.Failed)
            {
                streamHealth = HealthCritical;
            }
            else if (stream.streamState == NexoraStreamState.Connecting || stream.streamState == NexoraStreamState.Buffering || stream.streamState == NexoraStreamState.Recovering)
            {
                streamHealth = HealthWarning;
            }

            if (stream.failureCode == NexoraStreamFailure.RecoveryExhausted || stream.failureCode == NexoraStreamFailure.UnsupportedSource)
            {
                streamHealth = HealthCritical;
            }
            else if (stream.failureCode != NexoraStreamFailure.None && streamHealth < HealthWarning)
            {
                streamHealth = HealthWarning;
            }
        }

        private void SamplePlaylist()
        {
            if (playlist == null)
            {
                playlistHealth = HealthUnknown;
                queueCount = 0;
                requestCount = 0;
                quarantineCount = 0;
                return;
            }

            queueCount = playlist.queueCount;
            requestCount = playlist.requestCount;
            quarantineCount = playlist.quarantineCount;
            playlistHealth = HealthHealthy;

            if (playlist.maximumConsecutiveFailures > 0 && playlist.consecutiveFailureCount >= playlist.maximumConsecutiveFailures)
            {
                playlistHealth = HealthCritical;
            }
            else if (playlist.consecutiveFailureCount > 0 || playlist.quarantineCount > 0)
            {
                playlistHealth = HealthWarning;
            }
        }

        private void SamplePermissions()
        {
            if (permissions == null)
            {
                permissionsHealth = HealthCritical;
                controlsLocked = false;
                localRole = NexoraRole.Guest;
                return;
            }

            permissionsHealth = HealthHealthy;
            controlsLocked = permissions.controlsLocked;
            localRole = permissions.LocalRole();
        }

        private void EmitFaultTransitions()
        {
            if (diagnostics == null) return;

            if (previousBackendFault != 255 && backendFault != previousBackendFault && backendFault != NexoraBackendFault.None)
            {
                emittedFaultCount++;
                if (videoHealth == HealthCritical) diagnostics.FailFrom("Video", "Backend fault changed to " + BackendFaultName(backendFault) + ": " + Safe(video == null ? "" : video.faultMessage));
                else diagnostics.WarnFrom("Video", "Backend fault changed to " + BackendFaultName(backendFault));
            }

            if (stream != null)
            {
                if (previousStreamState != 255 && observedStreamState != previousStreamState)
                {
                    diagnostics.LogFrom("Streaming", "Lifecycle " + StreamStateName(previousStreamState) + " -> " + StreamStateName(observedStreamState));
                }

                if (previousStreamFailure != 255 && observedStreamFailure != previousStreamFailure && observedStreamFailure != NexoraStreamFailure.None)
                {
                    emittedFaultCount++;
                    if (streamHealth == HealthCritical) diagnostics.FailFrom("Streaming", "Failure changed to " + StreamFailureName(observedStreamFailure));
                    else diagnostics.WarnFrom("Streaming", "Failure changed to " + StreamFailureName(observedStreamFailure));
                }
            }

            if (syncStatus != null)
            {
                if (!previousAuthorityStale && syncStatus.authorityStale)
                {
                    emittedFaultCount++;
                    diagnostics.FailFrom("Sync", "Authority lease became stale at heartbeat age " + FormatSeconds(syncStatus.authorityHeartbeatAge));
                }
                else if (previousAuthorityStale && !syncStatus.authorityStale)
                {
                    diagnostics.LogFrom("Sync", "Authority lease recovered");
                }

                if (syncStatus.staleSnapshotCount > previousStaleSnapshotCount)
                {
                    diagnostics.WarnFrom("Sync", "Rejected stale snapshot count increased to " + syncStatus.staleSnapshotCount);
                }
                previousAuthorityStale = syncStatus.authorityStale;
                previousStaleSnapshotCount = syncStatus.staleSnapshotCount;
            }

            if (permissions != null)
            {
                if (permissions.deniedControlCount > previousDeniedControlCount)
                {
                    diagnostics.WarnFrom("Permissions", "Denied control attempt: " + Safe(permissions.lastDecisionAction) + " player=" + permissions.lastDecisionPlayerId);
                }
                if (permissions.deniedAdministrationCount > previousDeniedAdministrationCount)
                {
                    diagnostics.WarnFrom("Permissions", "Denied administration attempt: " + Safe(permissions.lastDecisionAction) + " player=" + permissions.lastDecisionPlayerId);
                }
                previousDeniedControlCount = permissions.deniedControlCount;
                previousDeniedAdministrationCount = permissions.deniedAdministrationCount;
            }

            if (playlist != null && quarantineCount > previousQuarantineCount)
            {
                diagnostics.WarnFrom("Playlist", "Quarantine count increased to " + quarantineCount + " after media failures");
            }
            previousQuarantineCount = quarantineCount;
            previousBackendFault = backendFault;
            previousStreamFailure = observedStreamFailure;
            previousStreamState = observedStreamState;
        }

        private void EmitHealthTransition(string subsystem, byte previous, byte current)
        {
            if (previous == 255 || previous == current || diagnostics == null) return;
            healthTransitionCount++;
            string message = subsystem + " health " + HealthName(previous) + " -> " + HealthName(current);
            if (current == HealthCritical) diagnostics.FailFrom("Health", message);
            else if (current == HealthWarning) diagnostics.WarnFrom("Health", message);
            else diagnostics.LogFrom("Health", message);
        }

        private void ResolvePlatform()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            localPlayerInVR = local != null && local.IsValid() && local.IsUserInVR();
#if UNITY_IOS
            platformName = "iOS";
#elif UNITY_ANDROID
            platformName = localPlayerInVR ? "Quest/Android VR" : "Android";
#else
            platformName = localPlayerInVR ? "PCVR" : "PC";
#endif
        }

        private byte HighestHealth(byte a, byte b)
        {
            if (a == HealthUnknown) return b;
            if (b == HealthUnknown) return a;
            return a > b ? a : b;
        }

        private string HealthName(byte health)
        {
            if (health == HealthHealthy) return "HEALTHY";
            if (health == HealthWarning) return "WARNING";
            if (health == HealthCritical) return "CRITICAL";
            return "UNKNOWN";
        }

        private string BackendFaultName(byte fault)
        {
            if (fault == NexoraBackendFault.None) return "None";
            if (fault == NexoraBackendFault.NotReady) return "NotReady";
            if (fault == NexoraBackendFault.LoadFailed) return "LoadFailed";
            if (fault == NexoraBackendFault.PlaybackFailed) return "PlaybackFailed";
            if (fault == NexoraBackendFault.Stalled) return "Stalled";
            if (fault == NexoraBackendFault.UnsupportedMedia) return "UnsupportedMedia";
            if (fault == NexoraBackendFault.PlatformBackendMissing) return "PlatformBackendMissing";
            if (fault == NexoraBackendFault.RecoveryExhausted) return "RecoveryExhausted";
            return "Unknown(" + fault + ")";
        }

        private string StreamStateName(byte state)
        {
            if (state == NexoraStreamState.Idle) return "Idle";
            if (state == NexoraStreamState.Connecting) return "Connecting";
            if (state == NexoraStreamState.Buffering) return "Buffering";
            if (state == NexoraStreamState.Live) return "Live";
            if (state == NexoraStreamState.Recovering) return "Recovering";
            if (state == NexoraStreamState.Failed) return "Failed";
            if (state == NexoraStreamState.Stopped) return "Stopped";
            return "Unknown(" + state + ")";
        }

        private string StreamFailureName(byte failure)
        {
            if (failure == NexoraStreamFailure.None) return "None";
            if (failure == NexoraStreamFailure.StartupTimeout) return "StartupTimeout";
            if (failure == NexoraStreamFailure.BackendFault) return "BackendFault";
            if (failure == NexoraStreamFailure.Stalled) return "Stalled";
            if (failure == NexoraStreamFailure.RecoveryExhausted) return "RecoveryExhausted";
            if (failure == NexoraStreamFailure.UnsupportedSource) return "UnsupportedSource";
            return "Unknown(" + failure + ")";
        }

        private string RoleName(byte role)
        {
            if (role == NexoraRole.Master) return "Master";
            if (role == NexoraRole.Owner) return "Owner";
            if (role == NexoraRole.Moderator) return "Moderator";
            if (role == NexoraRole.DJ) return "DJ";
            if (role == NexoraRole.Trusted) return "Trusted";
            return "Guest";
        }

        private string FormatSeconds(float value)
        {
            return Mathf.Max(0f, value).ToString("0.00") + "s";
        }

        private string BoolName(bool value)
        {
            return value ? "YES" : "NO";
        }

        private string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "(none)" : value;
        }

        private void Schedule()
        {
            if (scheduled) return;
            scheduled = true;
            SendCustomEventDelayedSeconds(nameof(Tick), sampleIntervalSeconds);
        }
    }
}
