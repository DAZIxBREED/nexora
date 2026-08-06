using UdonSharp;
using UnityEngine;

namespace Nexora.Core
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraModule : UdonSharpBehaviour
    {
        public string moduleId = "com.nexora.module";
        public string moduleVersion = "0.1.0";
        public bool moduleEnabled = true;
        public bool receiveTimeTicks;
        public float requestedTickInterval = 0.25f;

        [HideInInspector] public NexoraModuleHost host;
        [HideInInspector] public byte lastEvent;
        [HideInInspector] public int lastEventRevision;
        [HideInInspector] public float tickAccumulator;

        public virtual void NexoraInitialize() { }
        public virtual void NexoraShutdown() { }
        public virtual void NexoraOnEvent() { }
        public virtual void NexoraOnTimeTick() { }

        public void EmitCue(string cueId, float cueValue)
        {
            if (host != null && moduleEnabled)
            {
                host.EmitCue(moduleId, cueId, cueValue);
            }
        }
    }
}
