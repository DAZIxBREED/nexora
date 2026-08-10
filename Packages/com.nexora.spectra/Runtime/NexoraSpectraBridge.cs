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
        public string epochVariable = "externalAuthorityEpoch";
        public string commandVariable = "externalCommandType";
        public string updateEvent = "OnNexoraTimeUpdate";
        public string playbackEvent = "OnNexoraPlaybackChanged";

        [HideInInspector] public int pushedRevision = -1;
        [HideInInspector] public int pushedAuthorityEpoch = -1;

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
            spectraReceiver.SetProgramVariable(timeVariable, (float)state.AcceptedExpectedMediaTime());
            spectraReceiver.SendCustomEvent(updateEvent);
        }

        private void Push()
        {
            if (state == null || spectraReceiver == null) return;

            pushedRevision = state.acceptedRevision;
            pushedAuthorityEpoch = state.acceptedAuthorityEpoch;
            spectraReceiver.SetProgramVariable(timeVariable, (float)state.AcceptedExpectedMediaTime());
            spectraReceiver.SetProgramVariable(stateVariable, state.acceptedPlaybackState);
            spectraReceiver.SetProgramVariable(revisionVariable, state.acceptedRevision);
            spectraReceiver.SetProgramVariable(epochVariable, state.acceptedAuthorityEpoch);
            spectraReceiver.SetProgramVariable(commandVariable, state.acceptedCommandType);
        }
    }
}
