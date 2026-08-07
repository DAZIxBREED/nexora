using UdonSharp;
using UnityEngine;
using VRC.Udon;

namespace Nexora.Video
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraBackendContractGuard : UdonSharpBehaviour
    {
        public NexoraVideoBackendRouter router;
        public UdonBehaviour pcBackend;
        public UdonBehaviour androidBackend;
        public UdonBehaviour iosBackend;

        [Header("Validation")]
        public bool requirePlatformBackend = true;
        public bool valid;
        public string lastError;

        private void Start()
        {
            ValidateCurrentPlatform();
        }

        public void ValidateCurrentPlatform()
        {
            valid = true;
            lastError = "";

#if UNITY_IOS
            if (requirePlatformBackend && iosBackend == null)
            {
                Fail("Missing iOS video backend");
                return;
            }
#elif UNITY_ANDROID
            if (requirePlatformBackend && androidBackend == null)
            {
                Fail("Missing Android/Quest video backend");
                return;
            }
#else
            if (requirePlatformBackend && pcBackend == null)
            {
                Fail("Missing PC/PCVR video backend");
                return;
            }
#endif

            if (router == null)
            {
                Fail("Missing NexoraVideoBackendRouter");
                return;
            }

            router.RefreshBackend();
        }

        private void Fail(string message)
        {
            valid = false;
            lastError = message;
            Debug.LogError("[Nexora.Video] " + message);
        }
    }
}
