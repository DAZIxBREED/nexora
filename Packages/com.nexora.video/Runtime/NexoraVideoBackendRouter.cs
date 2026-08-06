using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Nexora.Video
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraVideoBackendRouter : UdonSharpBehaviour
    {
        [Header("Backend targets")]
        public UdonBehaviour pcBackend;
        public UdonBehaviour androidBackend;
        public UdonBehaviour iosBackend;

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

        [HideInInspector] public VRCUrl mediaUrl;
        [HideInInspector] public float mediaTime;
        [HideInInspector] public float volume = 1f;
        [HideInInspector] public bool loop;

        private UdonBehaviour activeBackend;

        private void Start()
        {
            activeBackend = SelectBackend();
            PushSettings();
        }

        public void Load()
        {
            PushSettings();
            Send(loadEvent);
        }

        public void Play()
        {
            PushSettings();
            Send(playEvent);
        }

        public void Pause()
        {
            PushSettings();
            Send(pauseEvent);
        }

        public void Stop()
        {
            Send(stopEvent);
        }

        public void Seek()
        {
            PushSettings();
            Send(seekEvent);
        }

        public void PushSettings()
        {
            if (activeBackend == null)
            {
                activeBackend = SelectBackend();
            }

            if (activeBackend == null)
            {
                return;
            }

            activeBackend.SetProgramVariable(urlVariable, mediaUrl);
            activeBackend.SetProgramVariable(timeVariable, mediaTime);
            activeBackend.SetProgramVariable(volumeVariable, Mathf.Clamp01(volume));
            activeBackend.SetProgramVariable(loopVariable, loop);
            Send(settingsEvent);
        }

        private UdonBehaviour SelectBackend()
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

        private void Send(string eventName)
        {
            if (activeBackend != null && !string.IsNullOrEmpty(eventName))
            {
                activeBackend.SendCustomEvent(eventName);
            }
        }
    }
}
