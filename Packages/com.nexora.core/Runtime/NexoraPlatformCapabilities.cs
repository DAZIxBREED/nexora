using UnityEngine;

namespace Nexora.Core
{
    /// <summary>
    /// Centralized runtime capability profile used by all Nexora packages.
    /// This avoids scattering platform preprocessor logic throughout the codebase.
    /// </summary>
    public static class NexoraPlatformCapabilities
    {
        public static bool IsMobile
        {
            get
            {
#if UNITY_ANDROID || UNITY_IOS
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsQuest
        {
            get
            {
#if UNITY_ANDROID && UNITY_64
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsIOS
        {
            get
            {
#if UNITY_IOS
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsAndroid
        {
            get
            {
#if UNITY_ANDROID
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsPC
        {
            get
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX
                return true;
#else
                return false;
#endif
            }
        }

        public static int RecommendedTargetFrameRate => IsMobile ? 60 : 90;
        public static int RecommendedTextureLimit => IsMobile ? 2048 : 8192;
        public static bool SupportsAdvancedPresentation => !IsMobile;

        public static string RuntimeLabel
        {
            get
            {
                if (IsIOS) return "iOS";
                if (IsQuest) return "Meta Quest";
                if (IsAndroid) return "Android";
                if (IsPC) return "PC / PCVR";
                return Application.platform.ToString();
            }
        }
    }
}
