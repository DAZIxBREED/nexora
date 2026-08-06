using UdonSharp;
using UnityEngine;
using Nexora.Core;

namespace Nexora.Streaming
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraStreamHealth : UdonSharpBehaviour
    {
        public NexoraModuleHost moduleHost;
        public float reconnectDelaySeconds = 3f;
        public int maximumRetries = 5;
        [HideInInspector] public int retryCount;
        [HideInInspector] public bool healthy;

        public void MarkHealthy()
        {
            healthy = true;
            retryCount = 0;
        }

        public void MarkFailed()
        {
            healthy = false;
            retryCount++;
            if (moduleHost != null) moduleHost.Broadcast(NexoraEvent.Error);
        }
    }
}
