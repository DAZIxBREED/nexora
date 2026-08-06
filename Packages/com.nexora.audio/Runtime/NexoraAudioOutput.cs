using UdonSharp;
using UnityEngine;
using Nexora.Sync;

namespace Nexora.Audio
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraAudioOutput : UdonSharpBehaviour
    {
        public NexoraMediaState state;
        public AudioSource[] outputs;
        public bool localMute;

        public void ApplyVolume()
        {
            if (outputs == null || state == null) return;
            float gain = localMute ? 0f : Mathf.Clamp01(state.volume);
            int i = 0;
            while (i < outputs.Length)
            {
                if (outputs[i] != null) outputs[i].volume = gain;
                i++;
            }
        }
    }
}
