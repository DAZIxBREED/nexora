using UdonSharp;
using UnityEngine;

namespace Nexora.Core
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraModuleHost : UdonSharpBehaviour
    {
        public NexoraModule[] modules;
        public float baseTickSeconds = 0.1f;

        [HideInInspector] public byte currentEvent;
        [HideInInspector] public int eventRevision;
        [HideInInspector] public string sourceModuleId;
        [HideInInspector] public string cueId;
        [HideInInspector] public float cueValue;

        private bool initialized;
        private bool tickScheduled;

        private void Start()
        {
            InitializeModules();
            ScheduleTick();
        }

        public void InitializeModules()
        {
            if (initialized) return;
            initialized = true;

            if (modules != null)
            {
                int i = 0;
                while (i < modules.Length)
                {
                    NexoraModule module = modules[i];
                    if (module != null)
                    {
                        module.host = this;
                        module.NexoraInitialize();
                    }
                    i++;
                }
            }

            Broadcast(NexoraEvent.Initialized);
        }

        public void Broadcast(byte eventType)
        {
            currentEvent = eventType;
            eventRevision++;
            if (modules == null) return;

            int i = 0;
            while (i < modules.Length)
            {
                NexoraModule module = modules[i];
                if (module != null && module.moduleEnabled)
                {
                    module.lastEvent = eventType;
                    module.lastEventRevision = eventRevision;
                    module.NexoraOnEvent();
                }
                i++;
            }
        }

        public void EmitCue(string sourceId, string emittedCueId, float emittedCueValue)
        {
            sourceModuleId = sourceId;
            cueId = emittedCueId;
            cueValue = emittedCueValue;
            Broadcast(NexoraEvent.Cue);
        }

        public void ModuleTick()
        {
            tickScheduled = false;
            if (modules != null)
            {
                int i = 0;
                while (i < modules.Length)
                {
                    NexoraModule module = modules[i];
                    if (module != null && module.moduleEnabled && module.receiveTimeTicks)
                    {
                        float interval = module.requestedTickInterval < baseTickSeconds ? baseTickSeconds : module.requestedTickInterval;
                        module.tickAccumulator += baseTickSeconds;
                        if (module.tickAccumulator >= interval)
                        {
                            module.tickAccumulator = 0f;
                            module.NexoraOnTimeTick();
                        }
                    }
                    i++;
                }
            }
            ScheduleTick();
        }

        private void ScheduleTick()
        {
            if (tickScheduled) return;
            tickScheduled = true;
            SendCustomEventDelayedSeconds(nameof(ModuleTick), baseTickSeconds);
        }
    }
}
