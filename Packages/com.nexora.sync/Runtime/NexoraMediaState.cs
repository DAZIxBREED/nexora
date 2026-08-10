using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using Nexora.Api;
using Nexora.Core;

namespace Nexora.Sync
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class NexoraMediaState : UdonSharpBehaviour
    {
        public NexoraModuleHost moduleHost;
        public NexoraAuthorityLease authorityLease;

        [Header("Synchronized authoritative state")]
        [UdonSynced] public VRCUrl mediaUrl;
        [UdonSynced] public byte playbackState;
        [UdonSynced] public double stateServerTime;
        [UdonSynced] public double mediaTimeAtState;
        [UdonSynced] public float playbackSpeed = 1f;
        [UdonSynced] public float volume = 1f;
        [UdonSynced] public bool loop;
        [UdonSynced] public int revision;
        [UdonSynced] public int authorityEpoch;
        [UdonSynced] public int sourcePlayerId = -1;
        [UdonSynced] public int commandSequence;
        [UdonSynced] public byte commandType;

        [Header("Locally accepted snapshot")]
        [HideInInspector] public VRCUrl acceptedMediaUrl;
        [HideInInspector] public byte acceptedPlaybackState;
        [HideInInspector] public double acceptedStateServerTime;
        [HideInInspector] public double acceptedMediaTimeAtState;
        [HideInInspector] public float acceptedPlaybackSpeed = 1f;
        [HideInInspector] public float acceptedVolume = 1f;
        [HideInInspector] public bool acceptedLoop;
        [HideInInspector] public int acceptedRevision = -1;
        [HideInInspector] public int acceptedAuthorityEpoch = -1;
        [HideInInspector] public int acceptedSourcePlayerId = -1;
        [HideInInspector] public int acceptedCommandSequence = -1;
        [HideInInspector] public byte acceptedCommandType;

        [Header("Snapshot telemetry")]
        [HideInInspector] public int acceptedSnapshotCount;
        [HideInInspector] public int duplicateSnapshotCount;
        [HideInInspector] public int staleSnapshotCount;
        [HideInInspector] public int epochAdvanceCount;

        private void Start()
        {
            AcceptCurrentSnapshot(true);
        }

        public double ExpectedMediaTime()
        {
            if (playbackState != NexoraPlaybackState.Playing) return mediaTimeAtState;
            return ReconstructTime(mediaTimeAtState, stateServerTime, playbackSpeed);
        }

        public double AcceptedExpectedMediaTime()
        {
            if (acceptedPlaybackState != NexoraPlaybackState.Playing) return acceptedMediaTimeAtState;
            return ReconstructTime(acceptedMediaTimeAtState, acceptedStateServerTime, acceptedPlaybackSpeed);
        }

        public void Commit(byte newState, double targetTime)
        {
            CommitCommand(CommandForState(newState), newState, targetTime);
        }

        public void CommitCommand(byte newCommandType, byte newState, double targetTime)
        {
            TakeOwnership();
            CaptureAuthorityEpoch();

            playbackState = newState;
            mediaTimeAtState = targetTime < 0d ? 0d : targetTime;
            stateServerTime = Networking.GetServerTimeInSeconds();

            VRCPlayerApi local = Networking.LocalPlayer;
            sourcePlayerId = local == null ? -1 : local.playerId;
            commandType = newCommandType;
            commandSequence++;
            revision++;

            AcceptCurrentSnapshot(true);
            RequestSerialization();
            NotifySnapshot();
        }

        public void SetVolume(float value)
        {
            TakeOwnership();
            CaptureAuthorityEpoch();
            volume = Mathf.Clamp01(value);
            StampCommand(NexoraSyncCommand.Volume);
        }

        public void SetLoop(bool value)
        {
            TakeOwnership();
            CaptureAuthorityEpoch();
            loop = value;
            StampCommand(NexoraSyncCommand.Loop);
        }

        public void SetPlaybackSpeed(float value)
        {
            TakeOwnership();
            CaptureAuthorityEpoch();
            mediaTimeAtState = ExpectedMediaTime();
            stateServerTime = Networking.GetServerTimeInSeconds();
            playbackSpeed = Mathf.Clamp(value, 0.25f, 4f);
            StampCommand(NexoraSyncCommand.PlaybackSpeed);
        }

        public void AdoptAuthorityEpoch(int newEpoch)
        {
            if (!Networking.IsOwner(gameObject)) return;
            if (newEpoch <= authorityEpoch) return;

            mediaTimeAtState = ExpectedMediaTime();
            stateServerTime = Networking.GetServerTimeInSeconds();
            authorityEpoch = newEpoch;
            StampCommand(NexoraSyncCommand.AuthorityTransfer);
        }

        public override void OnDeserialization()
        {
            if (AcceptCurrentSnapshot(false))
            {
                NotifySnapshot();
            }
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            if (Networking.IsOwner(gameObject))
            {
                RequestSerialization();
            }
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (Networking.IsOwner(gameObject))
            {
                CaptureAuthorityEpoch();
            }
            NotifySnapshot();
        }

        public bool AcceptCurrentSnapshot(bool force)
        {
            if (!force)
            {
                if (authorityEpoch < acceptedAuthorityEpoch)
                {
                    staleSnapshotCount++;
                    return false;
                }

                if (authorityEpoch == acceptedAuthorityEpoch)
                {
                    if (revision < acceptedRevision)
                    {
                        staleSnapshotCount++;
                        return false;
                    }

                    if (revision == acceptedRevision)
                    {
                        duplicateSnapshotCount++;
                        return false;
                    }
                }
            }

            if (acceptedAuthorityEpoch >= 0 && authorityEpoch > acceptedAuthorityEpoch)
            {
                epochAdvanceCount++;
            }

            acceptedMediaUrl = mediaUrl;
            acceptedPlaybackState = playbackState;
            acceptedStateServerTime = stateServerTime;
            acceptedMediaTimeAtState = mediaTimeAtState;
            acceptedPlaybackSpeed = playbackSpeed;
            acceptedVolume = volume;
            acceptedLoop = loop;
            acceptedRevision = revision;
            acceptedAuthorityEpoch = authorityEpoch;
            acceptedSourcePlayerId = sourcePlayerId;
            acceptedCommandSequence = commandSequence;
            acceptedCommandType = commandType;
            acceptedSnapshotCount++;
            return true;
        }

        private void StampCommand(byte newCommandType)
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            sourcePlayerId = local == null ? -1 : local.playerId;
            commandType = newCommandType;
            commandSequence++;
            revision++;
            AcceptCurrentSnapshot(true);
            RequestSerialization();
            NotifySnapshot();
        }

        private void CaptureAuthorityEpoch()
        {
            if (authorityLease != null && authorityLease.authorityEpoch > authorityEpoch)
            {
                authorityEpoch = authorityLease.authorityEpoch;
            }
        }

        private double ReconstructTime(double baseTime, double baseServerTime, float speed)
        {
            double elapsed = Networking.GetServerTimeInSeconds() - baseServerTime;
            if (elapsed < 0d) elapsed = 0d;
            return baseTime + elapsed * speed;
        }

        private byte CommandForState(byte state)
        {
            if (state == NexoraPlaybackState.Loading) return NexoraSyncCommand.Load;
            if (state == NexoraPlaybackState.Playing) return NexoraSyncCommand.Play;
            if (state == NexoraPlaybackState.Paused) return NexoraSyncCommand.Pause;
            return NexoraSyncCommand.Stop;
        }

        private void NotifySnapshot()
        {
            if (moduleHost != null) moduleHost.Broadcast(NexoraEvent.Snapshot);
        }

        private void TakeOwnership()
        {
            if (!Networking.IsOwner(gameObject) && Networking.LocalPlayer != null)
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }
        }
    }
}
