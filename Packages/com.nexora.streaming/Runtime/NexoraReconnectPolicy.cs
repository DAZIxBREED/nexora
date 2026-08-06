using UdonSharp;
using UnityEngine;

namespace Nexora.Streaming
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraReconnectPolicy : UdonSharpBehaviour
    {
        [Header("Retry policy")]
        public float initialDelaySeconds = 1f;
        public float maximumDelaySeconds = 30f;
        public float multiplier = 2f;
        public int maximumAttempts = 8;

        [Header("Runtime state")]
        public int attempt;
        public float nextDelaySeconds;
        public bool exhausted;

        private void Start()
        {
            ResetPolicy();
        }

        public void ResetPolicy()
        {
            attempt = 0;
            exhausted = false;
            nextDelaySeconds = Mathf.Max(0.1f, initialDelaySeconds);
        }

        public float RegisterFailure()
        {
            if (exhausted)
            {
                return -1f;
            }

            attempt++;
            if (maximumAttempts > 0 && attempt >= maximumAttempts)
            {
                exhausted = true;
            }

            float delay = nextDelaySeconds;
            nextDelaySeconds = Mathf.Min(
                Mathf.Max(initialDelaySeconds, nextDelaySeconds * Mathf.Max(1f, multiplier)),
                Mathf.Max(initialDelaySeconds, maximumDelaySeconds));
            return delay;
        }

        public bool CanRetry()
        {
            return !exhausted;
        }
    }
}
