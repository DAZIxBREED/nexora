using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;

namespace Nexora.Video
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraVRCVideoBackend : UdonSharpBehaviour
    {
        [Header("VRChat player")]
        public BaseVRCVideoPlayer player;
        public NexoraVideoBackendRouter router;
        public AudioSource[] audioOutputs;

        [Header("Backend mode")]
        [Tooltip("Enable for AVPro/live-stream backends. Uses PlayURL instead of LoadURL for URL acquisition.")]
        public bool preferPlayUrl;
        [Tooltip("VRChat globally rate-limits video URL requests. Keep this at or above 5 seconds.")]
        public float minimumUrlRequestInterval = 5.1f;
        public float timeReportInterval = 0.25f;

        [Header("Router contract inputs")]
        public VRCUrl nexoraUrl;
        public float nexoraTime;
        public float nexoraVolume = 1f;
        public bool nexoraLoop;

        [Header("Runtime telemetry")]
        [HideInInspector] public bool ready;
        [HideInInspector] public bool loading;
        [HideInInspector] public bool desiredPlaying;
        [HideInInspector] public float duration;
        [HideInInspector] public int loadGeneration;
        [HideInInspector] public int successfulStarts;
        [HideInInspector] public int errorCount;

        private bool loadQueued;
        private bool seekPending;
        private float pendingSeek;
        private float lastUrlRequestAt = -1000f;
        private bool timeTickScheduled;

        private void Start()
        {
            minimumUrlRequestInterval = Mathf.Max(5.05f, minimumUrlRequestInterval);
            timeReportInterval = Mathf.Max(0.1f, timeReportInterval);
            ApplyAudioSettings();
            ScheduleTimeTick();

            if (router != null)
            {
                router.ReportBackendNotReady();
            }
        }

        public void NexoraLoad()
        {
            desiredPlaying = false;
            QueueLoad();
        }

        public void NexoraPlay()
        {
            desiredPlaying = true;
            if (player == null)
            {
                ReportPlayerError("NexoraPlay called without a VRChat video player.");
                return;
            }

            if (ready)
            {
                ApplyPendingSeek();
                player.Play();
            }
            else if (!loading)
            {
                QueueLoad();
            }
        }

        public void NexoraPause()
        {
            desiredPlaying = false;
            if (player == null)
            {
                ReportPlayerError("NexoraPause called without a VRChat video player.");
                return;
            }

            if (ready)
            {
                player.Pause();
            }
        }

        public void NexoraStop()
        {
            desiredPlaying = false;
            loadQueued = false;
            loading = false;
            ready = false;
            seekPending = false;

            if (player != null)
            {
                player.Stop();
            }

            if (router != null)
            {
                router.ReportBackendNotReady();
                router.ReportBackendTime(0f);
            }
        }

        public void NexoraSeek()
        {
            pendingSeek = Mathf.Max(0f, nexoraTime);
            seekPending = true;
            ApplyPendingSeek();
        }

        public void NexoraApplySettings()
        {
            ApplyAudioSettings();
            if (ready)
            {
                ApplyPendingSeek();
            }
        }

        public void NexoraRecover()
        {
            desiredPlaying = true;
            ready = false;
            loading = false;
            seekPending = true;
            pendingSeek = Mathf.Max(0f, nexoraTime);

            if (player != null)
            {
                player.Stop();
            }

            QueueLoad();
        }

        public void ProcessQueuedLoad()
        {
            if (!loadQueued)
            {
                return;
            }

            float elapsed = Time.realtimeSinceStartup - lastUrlRequestAt;
            if (elapsed < minimumUrlRequestInterval)
            {
                SendCustomEventDelayedSeconds(nameof(ProcessQueuedLoad), minimumUrlRequestInterval - elapsed);
                return;
            }

            loadQueued = false;
            BeginLoad();
        }

        public void TimeReportTick()
        {
            timeTickScheduled = false;

            if (ready && player != null && router != null)
            {
                router.ReportBackendTime(Mathf.Max(0f, player.GetTime()));
            }

            ScheduleTimeTick();
        }

        public override void OnVideoReady()
        {
            loading = false;
            ready = true;
            duration = player == null ? 0f : player.GetDuration();
            ApplyAudioSettings();
            ApplyPendingSeek();

            if (router != null)
            {
                router.ReportBackendReady();
                if (player != null)
                {
                    router.ReportBackendTime(Mathf.Max(0f, player.GetTime()));
                }
            }

            if (desiredPlaying && player != null && !preferPlayUrl)
            {
                player.Play();
            }
        }

        public override void OnVideoStart()
        {
            loading = false;
            ready = true;
            successfulStarts++;
            ApplyPendingSeek();

            if (router != null)
            {
                router.ReportBackendReady();
                if (player != null)
                {
                    router.ReportBackendTime(Mathf.Max(0f, player.GetTime()));
                }
            }

            if (!desiredPlaying && player != null)
            {
                player.Pause();
            }
        }

        public override void OnVideoPlay()
        {
            ready = true;
            if (router != null)
            {
                router.ReportBackendReady();
            }
        }

        public override void OnVideoPause()
        {
            if (router != null && player != null)
            {
                router.ReportBackendTime(Mathf.Max(0f, player.GetTime()));
            }
        }

        public override void OnVideoEnd()
        {
            ready = false;
            if (nexoraLoop && player != null)
            {
                pendingSeek = 0f;
                seekPending = true;
                ready = true;
                ApplyPendingSeek();
                if (desiredPlaying)
                {
                    player.Play();
                }
                return;
            }

            desiredPlaying = false;
            if (router != null)
            {
                router.ReportBackendNotReady();
            }
        }

        public override void OnVideoLoop()
        {
            if (router != null && player != null)
            {
                router.ReportBackendTime(Mathf.Max(0f, player.GetTime()));
            }
        }

        public override void OnVideoError(VideoError videoError)
        {
            loading = false;
            ready = false;
            errorCount++;

            if (router == null)
            {
                return;
            }

            if (videoError == VideoError.InvalidURL)
            {
                router.ReportFault(NexoraBackendFault.LoadFailed, "VRChat video backend rejected an invalid URL.");
            }
            else if (videoError == VideoError.AccessDenied)
            {
                router.ReportFault(NexoraBackendFault.LoadFailed, "VRChat denied access to the video URL. Check trusted URL settings and host allowlisting.");
            }
            else if (videoError == VideoError.RateLimited)
            {
                router.ReportFault(NexoraBackendFault.LoadFailed, "VRChat rate-limited the video URL request.");
                QueueLoad();
            }
            else if (videoError == VideoError.PlayerError)
            {
                router.ReportFault(NexoraBackendFault.PlaybackFailed, "VRChat video backend reported a player error.");
            }
            else
            {
                router.ReportFault(NexoraBackendFault.PlaybackFailed, "VRChat video backend reported an unknown error.");
            }
        }

        private void QueueLoad()
        {
            if (VRCUrl.IsNullOrEmpty(nexoraUrl))
            {
                if (router != null)
                {
                    router.ReportFault(NexoraBackendFault.LoadFailed, "Nexora cannot load an empty media URL.");
                }
                return;
            }

            loadQueued = true;
            ProcessQueuedLoad();
        }

        private void BeginLoad()
        {
            if (player == null)
            {
                ReportPlayerError("Nexora backend has no BaseVRCVideoPlayer assigned.");
                return;
            }

#if UNITY_ANDROID
            string raw = nexoraUrl.Get();
            if (!string.IsNullOrEmpty(raw) && !raw.StartsWith("https://"))
            {
                if (router != null)
                {
                    router.ReportFault(NexoraBackendFault.UnsupportedMedia, "Android/Quest video URLs must use HTTPS.");
                }
                return;
            }
#endif

            lastUrlRequestAt = Time.realtimeSinceStartup;
            loadGeneration++;
            loading = true;
            ready = false;
            seekPending = true;
            pendingSeek = Mathf.Max(0f, nexoraTime);

            if (router != null)
            {
                router.ReportBackendNotReady();
            }

            if (preferPlayUrl)
            {
                player.PlayURL(nexoraUrl);
            }
            else
            {
                player.LoadURL(nexoraUrl);
            }
        }

        private void ApplyPendingSeek()
        {
            if (!ready || player == null || !seekPending)
            {
                return;
            }

            float target = Mathf.Max(0f, pendingSeek);
            if (duration > 0f && target > duration)
            {
                target = duration;
            }

            player.SetTime(target);
            seekPending = false;

            if (router != null)
            {
                router.ReportBackendTime(target);
            }
        }

        private void ApplyAudioSettings()
        {
            if (audioOutputs == null)
            {
                return;
            }

            float value = Mathf.Clamp01(nexoraVolume);
            int i = 0;
            while (i < audioOutputs.Length)
            {
                if (audioOutputs[i] != null)
                {
                    audioOutputs[i].volume = value;
                }
                i++;
            }
        }

        private void ScheduleTimeTick()
        {
            if (timeTickScheduled)
            {
                return;
            }

            timeTickScheduled = true;
            SendCustomEventDelayedSeconds(nameof(TimeReportTick), timeReportInterval);
        }

        private void ReportPlayerError(string message)
        {
            loading = false;
            ready = false;
            errorCount++;
            if (router != null)
            {
                router.ReportFault(NexoraBackendFault.PlatformBackendMissing, message);
            }
        }
    }
}
