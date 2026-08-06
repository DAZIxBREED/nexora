using UdonSharp;
using UnityEngine;
using VRC.Udon;
using Nexora.Sync;

namespace Nexora.Spectra
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraSpectraTimelineBridge : UdonSharpBehaviour
    {
        public NexoraAuthoritativeClock clock;
        public UdonBehaviour spectraReceiver;

        [Header("Receiver contract")]
        public string timeVariable = "externalMediaTime";
        public string playingVariable = "externalPlaying";
        public string revisionVariable = "externalMediaRevision";
        public string timeEvent = "OnNexoraTimeUpdate";
        public string playbackEvent = "OnNexoraPlaybackChanged";

        [Header("Timing")]
        public float updateIntervalSeconds = 0.1f;
        public float hardCorrectionThresholdSeconds = 0.5f;

        private int lastRevision = -1;
        private bool scheduled;

        private void Start()
        {
            Schedule();
        }

        public void Tick()
        {
            scheduled = false;

            if (clock != null && spectraReceiver != null)
            {
                spectraReceiver.SetProgramVariable(timeVariable, (float)clock.CurrentTime);
                spectraReceiver.SetProgramVariable(playingVariable, clock.playing);
                spectraReceiver.SetProgramVariable(revisionVariable, clock.revision);
                spectraReceiver.SendCustomEvent(timeEvent);

                if (lastRevision != clock.revision)
                {
                    lastRevision = clock.revision;
                    spectraReceiver.SendCustomEvent(playbackEvent);
                }
            }

            Schedule();
        }

        public bool NeedsTimelineCorrection(float spectraTime)
        {
            if (clock == null)
            {
                return false;
            }

            return Mathf.Abs(spectraTime - (float)clock.CurrentTime) >= hardCorrectionThresholdSeconds;
        }

        private void Schedule()
        {
            if (scheduled)
            {
                return;
            }

            scheduled = true;
            SendCustomEventDelayedSeconds(nameof(Tick), Mathf.Max(0.05f, updateIntervalSeconds));
        }
    }
}
