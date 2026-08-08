namespace Nexora.Video
{
    public static class NexoraBackendFault
    {
        public const byte None = 0;
        public const byte NotReady = 1;
        public const byte LoadFailed = 2;
        public const byte PlaybackFailed = 3;
        public const byte Stalled = 4;
        public const byte UnsupportedMedia = 5;
        public const byte PlatformBackendMissing = 6;
        public const byte RecoveryExhausted = 7;
    }
}
