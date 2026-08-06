using VRC.Udon;
using Nexora.Core;
using Nexora.Sync;

namespace Nexora.Spectra
{
    public class NexoraSpectraBridge : NexoraModule
    {
        public NexoraMediaState state;
        public UdonBehaviour spectraReceiver;
        public string timeVariable = "externalMediaTime";
        public string stateVariable = "externalPlaybackState";
        public string revisionVariable = "externalMediaRevision";
        public string updateEvent = "OnNexoraTimeUpdate";
        public string playbackEvent = "OnNexoraPlaybackChanged";

        public override void NexoraInitialize()
        {
            receiveTimeTicks = true;
            requestedTickInterval = 0.1f;
            Push();
        }

        public override void NexoraOnEvent()
        {
            if (lastEvent == NexoraEvent.PlaybackChanged || lastEvent == NexoraEvent.Snapshot)
            {
                Push();
                if (spectraReceiver != null) spectraReceiver.SendCustomEvent(playbackEvent);
            }
        }

        public override void NexoraOnTimeTick()
        {
            if (state == null || spectraReceiver == null) return;
            spectraReceiver.SetProgramVariable(timeVariable, (float)state.ExpectedMediaTime());
            spectraReceiver.SendCustomEvent(updateEvent);
        }

        private void Push()
        {
            if (state == null || spectraReceiver == null) return;
            spectraReceiver.SetProgramVariable(timeVariable, (float)state.ExpectedMediaTime());
            spectraReceiver.SetProgramVariable(stateVariable, state.playbackState);
            spectraReceiver.SetProgramVariable(revisionVariable, state.revision);
        }
    }
}
