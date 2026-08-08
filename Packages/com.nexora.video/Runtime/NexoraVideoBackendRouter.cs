using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Nexora.Video
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraVideoBackendRouter : UdonSharpBehaviour
    {
        [Header("Primary backend targets")]
        public UdonBehaviour pcBackend;
        public UdonBehaviour androidBackend;
        public UdonBehaviour iosBackend;

        [Header("Fallback backend targets")]
        public UdonBehaviour pcFallbackBackend;
        public UdonBehaviour androidFallbackBackend;
        public UdonBehaviour iosFallbackBackend;
        public bool automaticFallback = true;
        public int faultsBeforeFallback = 2;

        [Header("Backend contract")]
        public string urlVariable = "nexoraUrl";
        public string timeVariable = "nexoraTime";
        public string volumeVariable = "nexoraVolume";
        public string loopVariable = "nexoraLoop";
        public string loadEvent = "NexoraLoad";
        public string playEvent = "NexoraPlay";
        public string pauseEvent = "NexoraPause";
        public string stopEvent = "NexoraStop";
        public string seekEvent = "NexoraSeek";
        public string settingsEvent = "NexoraApplySettings";
        public string recoverEvent = "NexoraRecover";

        [Header("Runtime state")]
        [HideInInspector] public VRCUrl mediaUrl;
        [HideInInspector] public float mediaTime;
        [HideInInspector] public float volume = 1f;
        [HideInInspector] public bool loop;
        [HideInInspector] public float backendReportedTime;
        [HideInInspector] public bool backendReady;
        [HideInInspector] public int backendGeneration;
        [HideInInspector] public byte faultCode;
        [HideInInspector] public string faultMessage;
        [HideInInspector] public int faultCount;
        [HideInInspector] public int recoveryCount;
        [HideInInspector] public int failoverCount;
        [HideInInspector] public int consecutiveSevereFaults;
        [HideInInspector] public bool usingFallback;

        private UdonBehaviour activeBackend;
        private string activeUrl = "";

        private void Start()
        {
            faultsBeforeFallback = Mathf.Max(1, faultsBeforeFallback);
            RefreshBackend();
            ValidateActiveBackend();
            PushSettings();
        }

        public void RefreshBackend()
        {
            UdonBehaviour selected = usingFallback ? SelectFallbackBackend() : SelectPrimaryBackend();
            if (selected == null && usingFallback)
            {
                usingFallback = false;
                selected = SelectPrimaryBackend();
            }

            if (selected != activeBackend)
            {
                activeBackend = selected;
                backendGeneration++;
                backendReady = false;
                ClearFault();
            }
        }

        public bool HasActiveBackend()
        {
            RefreshBackend();
            return activeBackend != null;
        }

        public bool ValidateActiveBackend()
        {
            RefreshBackend();
            if (activeBackend != null) return true;
            SetFault(NexoraBackendFault.PlatformBackendMissing, "No compatible Nexora video backend is assigned for this platform.", false);
            return false;
        }

        public bool HasFallbackBackend()
        {
            return SelectFallbackBackend() != null;
        }

        public void Load()
        {
            string nextUrl = VRCUrl.IsNullOrEmpty(mediaUrl) ? "" : mediaUrl.Get();
            if (nextUrl != activeUrl)
            {
                activeUrl = nextUrl;
                ResetToPrimary();
            }

            if (!ValidateActiveBackend()) return;
            PushSettings();
            Send(loadEvent);
        }

        public void Play()
        {
            if (!ValidateActiveBackend()) return;
            PushSettings();
            Send(playEvent);
        }

        public void Pause()
        {
            if (!ValidateActiveBackend()) return;
            PushSettings();
            Send(pauseEvent);
        }

        public void Stop()
        {
            if (!ValidateActiveBackend()) return;
            Send(stopEvent);
            backendReady = false;
        }

        public void Seek()
        {
            if (!ValidateActiveBackend()) return;
            PushSettings();
            Send(seekEvent);
        }

        public void Recover()
        {
            if (!ValidateActiveBackend()) return;

            if (automaticFallback && !usingFallback && consecutiveSevereFaults >= faultsBeforeFallback && HasFallbackBackend())
            {
                SwitchToFallback();
                return;
            }

            recoveryCount++;
            backendReady = false;
            PushSettings();
            Send(recoverEvent);
        }

        public void SwitchToFallback()
        {
            UdonBehaviour fallback = SelectFallbackBackend();
            if (fallback == null)
            {
                return;
            }

            UdonBehaviour previous = activeBackend;
            if (previous != null && !string.IsNullOrEmpty(stopEvent))
            {
                previous.SendCustomEvent(stopEvent);
            }

            usingFallback = true;
            activeBackend = fallback;
            backendGeneration++;
            backendReady = false;
            failoverCount++;
            consecutiveSevereFaults = 0;
            ClearFault();
            PushSettingsDirect();
            Send(recoverEvent);
        }

        public void ResetToPrimary()
        {
            UdonBehaviour primary = SelectPrimaryBackend();
            if (primary == null)
            {
                return;
            }

            if (activeBackend != null && activeBackend != primary && !string.IsNullOrEmpty(stopEvent))
            {
                activeBackend.SendCustomEvent(stopEvent);
            }

            usingFallback = false;
            activeBackend = primary;
            backendGeneration++;
            backendReady = false;
            consecutiveSevereFaults = 0;
            ClearFault();
        }

        public void ReportBackendReady()
        {
            backendReady = true;
            consecutiveSevereFaults = 0;
            ClearFault();
        }

        public void ReportBackendNotReady()
        {
            backendReady = false;
            if (faultCode == NexoraBackendFault.None)
            {
                SetFault(NexoraBackendFault.NotReady, "Backend reported not ready.", false);
            }
        }

        public void ReportBackendTime(float seconds)
        {
            backendReportedTime = Mathf.Max(0f, seconds);
        }

        public void ReportLoadFailed()
        {
            ReportFault(NexoraBackendFault.LoadFailed, "Backend failed to load media.");
        }

        public void ReportPlaybackFailed()
        {
            ReportFault(NexoraBackendFault.PlaybackFailed, "Backend playback failed.");
        }

        public void ReportUnsupportedMedia()
        {
            ReportFault(NexoraBackendFault.UnsupportedMedia, "Backend reported unsupported media.");
        }

        public void ReportFault(byte code, string message)
        {
            bool severe = code == NexoraBackendFault.LoadFailed ||
                          code == NexoraBackendFault.PlaybackFailed ||
                          code == NexoraBackendFault.UnsupportedMedia ||
                          code == NexoraBackendFault.Stalled;
            SetFault(code, message, severe);

            if (automaticFallback && severe && !usingFallback && consecutiveSevereFaults >= faultsBeforeFallback && HasFallbackBackend())
            {
                SwitchToFallback();
            }
        }

        public void ClearFault()
        {
            faultCode = NexoraBackendFault.None;
            faultMessage = "";
        }

        public void PushSettings()
        {
            RefreshBackend();
            PushSettingsDirect();
        }

        private void PushSettingsDirect()
        {
            if (activeBackend == null) return;

            activeBackend.SetProgramVariable(urlVariable, mediaUrl);
            activeBackend.SetProgramVariable(timeVariable, mediaTime);
            activeBackend.SetProgramVariable(volumeVariable, Mathf.Clamp01(volume));
            activeBackend.SetProgramVariable(loopVariable, loop);
            Send(settingsEvent);
        }

        private void SetFault(byte code, string message, bool severe)
        {
            backendReady = false;
            faultCode = code;
            faultMessage = message == null ? "" : message;
            faultCount++;
            if (severe)
            {
                consecutiveSevereFaults++;
            }
        }

        private UdonBehaviour SelectPrimaryBackend()
        {
#if UNITY_IOS
            if (iosBackend != null) return iosBackend;
#elif UNITY_ANDROID
            if (androidBackend != null) return androidBackend;
#endif
            if (pcBackend != null) return pcBackend;
            if (androidBackend != null) return androidBackend;
            return iosBackend;
        }

        private UdonBehaviour SelectFallbackBackend()
        {
#if UNITY_IOS
            if (iosFallbackBackend != null) return iosFallbackBackend;
#elif UNITY_ANDROID
            if (androidFallbackBackend != null) return androidFallbackBackend;
#endif
            if (pcFallbackBackend != null) return pcFallbackBackend;
            if (androidFallbackBackend != null) return androidFallbackBackend;
            return iosFallbackBackend;
        }

        private void Send(string eventName)
        {
            if (activeBackend != null && !string.IsNullOrEmpty(eventName))
            {
                activeBackend.SendCustomEvent(eventName);
            }
        }
    }
}
