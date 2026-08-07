using UdonSharp;
using UnityEngine;

namespace Nexora.Sync
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraSyncHealth : UdonSharpBehaviour
    {
        public NexoraAuthoritativeClock clock;
        public NexoraAuthorityLease authorityLease;

        [Header("Thresholds")]
        public float warningDriftSeconds = 0.35f;
        public float criticalDriftSeconds = 1.25f;

        [HideInInspector] public float lastMeasuredDrift;
        [HideInInspector] public byte healthState;
        [HideInInspector] public bool authorityStale;

        public const byte Healthy = 0;
        public const byte Warning = 1;
        public const byte Critical = 2;

        public void Evaluate(float localPlaybackTime)
        {
            lastMeasuredDrift = clock == null ? 0f : clock.CalculateDrift(localPlaybackTime);
            authorityStale = authorityLease != null && authorityLease.IsStale();

            float absoluteDrift = Mathf.Abs(lastMeasuredDrift);
            if (authorityStale || absoluteDrift >= criticalDriftSeconds)
            {
                healthState = Critical;
            }
            else if (absoluteDrift >= warningDriftSeconds)
            {
                healthState = Warning;
            }
            else
            {
                healthState = Healthy;
            }
        }

        public bool ShouldSeek(float localPlaybackTime)
        {
            Evaluate(localPlaybackTime);
            return healthState == Critical && clock != null;
        }

        public float AuthoritativeTime()
        {
            return clock == null ? 0f : (float)clock.CurrentTime;
        }
    }
}
