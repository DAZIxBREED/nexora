using UdonSharp;
using UnityEngine;

namespace Nexora.Video
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraVideoRequestGate : UdonSharpBehaviour
    {
        [Tooltip("VRChat applies this limit globally per user across video players.")]
        public float minimumIntervalSeconds = 5.1f;

        [HideInInspector] public float lastGrantAt = -1000f;
        [HideInInspector] public int grants;
        [HideInInspector] public int denials;

        private void Start()
        {
            minimumIntervalSeconds = Mathf.Max(5.05f, minimumIntervalSeconds);
        }

        public bool TryAcquire()
        {
            float now = Time.realtimeSinceStartup;
            if (now - lastGrantAt < minimumIntervalSeconds)
            {
                denials++;
                return false;
            }

            lastGrantAt = now;
            grants++;
            return true;
        }

        public float SecondsUntilAvailable()
        {
            float remaining = minimumIntervalSeconds - (Time.realtimeSinceStartup - lastGrantAt);
            return Mathf.Max(0f, remaining);
        }

        public void ResetGate()
        {
            lastGrantAt = -1000f;
        }
    }
}
