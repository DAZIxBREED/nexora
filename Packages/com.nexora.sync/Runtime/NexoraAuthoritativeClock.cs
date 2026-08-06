using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Nexora.Sync
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class NexoraAuthoritativeClock : UdonSharpBehaviour
    {
        [UdonSynced] public double mediaTimeAtSnapshot;
        [UdonSynced] public double serverTimeAtSnapshot;
        [UdonSynced] public float playbackRate = 1f;
        [UdonSynced] public bool playing;
        [UdonSynced] public int revision;

        public double CurrentTime
        {
            get
            {
                if (!playing)
                {
                    return mediaTimeAtSnapshot;
                }

                double elapsed = Networking.GetServerTimeInSeconds() - serverTimeAtSnapshot;
                if (elapsed < 0d)
                {
                    elapsed = 0d;
                }

                return mediaTimeAtSnapshot + elapsed * playbackRate;
            }
        }

        public void SetPlaying(bool value)
        {
            TakeOwnership();
            mediaTimeAtSnapshot = CurrentTime;
            serverTimeAtSnapshot = Networking.GetServerTimeInSeconds();
            playing = value;
            revision++;
            RequestSerialization();
        }

        public void Seek(float seconds)
        {
            TakeOwnership();
            mediaTimeAtSnapshot = Mathf.Max(0f, seconds);
            serverTimeAtSnapshot = Networking.GetServerTimeInSeconds();
            revision++;
            RequestSerialization();
        }

        public void SetPlaybackRate(float rate)
        {
            TakeOwnership();
            mediaTimeAtSnapshot = CurrentTime;
            serverTimeAtSnapshot = Networking.GetServerTimeInSeconds();
            playbackRate = Mathf.Clamp(rate, 0.25f, 4f);
            revision++;
            RequestSerialization();
        }

        public float CalculateDrift(float localPlaybackTime)
        {
            return localPlaybackTime - (float)CurrentTime;
        }

        public bool RequiresHardCorrection(float localPlaybackTime, float thresholdSeconds)
        {
            return Mathf.Abs(CalculateDrift(localPlaybackTime)) >= Mathf.Max(0.05f, thresholdSeconds);
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
