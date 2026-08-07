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

        [UdonSynced] public VRCUrl mediaUrl;
        [UdonSynced] public byte playbackState;
        [UdonSynced] public double stateServerTime;
        [UdonSynced] public double mediaTimeAtState;
        [UdonSynced] public float playbackSpeed = 1f;
        [UdonSynced] public float volume = 1f;
        [UdonSynced] public bool loop;
        [UdonSynced] public int revision;
        [UdonSynced] public int sourcePlayerId = -1;

        public double ExpectedMediaTime()
        {
            if (playbackState != NexoraPlaybackState.Playing) return mediaTimeAtState;
            double elapsed = Networking.GetServerTimeInSeconds() - stateServerTime;
            if (elapsed < 0d) elapsed = 0d;
            return mediaTimeAtState + elapsed * playbackSpeed;
        }

        public void Commit(byte newState, double targetTime)
        {
            TakeOwnership();
            playbackState = newState;
            mediaTimeAtState = targetTime < 0d ? 0d : targetTime;
            stateServerTime = Networking.GetServerTimeInSeconds();
            VRCPlayerApi local = Networking.LocalPlayer;
            sourcePlayerId = local == null ? -1 : local.playerId;
            revision++;
            RequestSerialization();
            NotifySnapshot();
        }

        public void SetVolume(float value)
        {
            TakeOwnership();
            volume = Mathf.Clamp01(value);
            revision++;
            RequestSerialization();
            NotifySnapshot();
        }

        public void SetLoop(bool value)
        {
            TakeOwnership();
            loop = value;
            revision++;
            RequestSerialization();
            NotifySnapshot();
        }

        public void SetPlaybackSpeed(float value)
        {
            TakeOwnership();
            mediaTimeAtState = ExpectedMediaTime();
            stateServerTime = Networking.GetServerTimeInSeconds();
            playbackSpeed = Mathf.Clamp(value, 0.25f, 4f);
            revision++;
            RequestSerialization();
            NotifySnapshot();
        }

        public override void OnDeserialization()
        {
            NotifySnapshot();
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
            NotifySnapshot();
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
