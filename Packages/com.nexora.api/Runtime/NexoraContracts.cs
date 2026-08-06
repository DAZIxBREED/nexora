namespace Nexora.Api
{
    public static class NexoraPlaybackState
    {
        public const byte Stopped = 0;
        public const byte Loading = 1;
        public const byte Playing = 2;
        public const byte Paused = 3;
        public const byte Error = 4;
    }

    public static class NexoraRole
    {
        public const byte Guest = 0;
        public const byte Trusted = 1;
        public const byte DJ = 2;
        public const byte Moderator = 3;
        public const byte Owner = 4;
        public const byte Master = 5;
    }
}
