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

        public double ExpectedMediaTime()
        {
            if (playbackState != NexoraPlaybackState.Playing) return mediaTimeAtState;
            double elapsed = Networking.GetServerTimeInSeconds() - stateServerTime;
            if (elapsed < 0d) elapsed = 0d;
            return mediaTimeAtState + elapsed * playbackSpeed;
        }

        public void Commit(byte newState, double targetTime)
        {
            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }

            playbackState = newState;
            mediaTimeAtState = targetTime < 0d ? 0d : targetTime;
            stateServerTime = Networking.GetServerTimeInSeconds();
            revision++;
            RequestSerialization();
            if (moduleHost != null) moduleHost.Broadcast(NexoraEvent.Snapshot);
        }

        public override void OnDeserialization()
        {
            if (moduleHost != null) moduleHost.Broadcast(NexoraEvent.Snapshot);
        }
    }
}
