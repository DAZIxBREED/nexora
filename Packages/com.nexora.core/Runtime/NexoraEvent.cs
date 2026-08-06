namespace Nexora.Core
{
    public static class NexoraEvent
    {
        public const byte Initialized = 0;
        public const byte MediaChanged = 1;
        public const byte PlaybackChanged = 2;
        public const byte TimeChanged = 3;
        public const byte VolumeChanged = 4;
        public const byte LoopChanged = 5;
        public const byte LockChanged = 6;
        public const byte AuthorityChanged = 7;
        public const byte Error = 8;
        public const byte Cue = 9;
        public const byte PlatformChanged = 10;
        public const byte PlaylistChanged = 11;
        public const byte Snapshot = 12;
    }
}
