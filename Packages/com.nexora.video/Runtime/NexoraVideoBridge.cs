using UdonSharp;
using VRC.Udon;
using Nexora.Sync;

namespace Nexora.Video
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraVideoBridge : UdonSharpBehaviour
    {
        public NexoraMediaState state;
        public UdonBehaviour backend;
        public string urlVariable = "nexoraUrl";
        public string timeVariable = "nexoraTime";
        public string stateVariable = "nexoraPlaybackState";
        public string applyEvent = "NexoraApplyState";

        public void ApplyState()
        {
            if (state == null || backend == null) return;
            backend.SetProgramVariable(urlVariable, state.mediaUrl);
            backend.SetProgramVariable(timeVariable, (float)state.ExpectedMediaTime());
            backend.SetProgramVariable(stateVariable, state.playbackState);
            backend.SendCustomEvent(applyEvent);
        }
    }
}
