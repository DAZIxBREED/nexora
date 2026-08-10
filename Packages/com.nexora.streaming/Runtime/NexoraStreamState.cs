namespace Nexora.Streaming
{
    public static class NexoraStreamState
    {
        public const byte Idle = 0;
        public const byte Connecting = 1;
        public const byte Buffering = 2;
        public const byte Live = 3;
        public const byte Recovering = 4;
        public const byte Failed = 5;
        public const byte Stopped = 6;
    }

    public static class NexoraStreamFailure
    {
        public const byte None = 0;
        public const byte StartupTimeout = 1;
        public const byte Stalled = 2;
        public const byte BackendFault = 3;
        public const byte RecoveryExhausted = 4;
        public const byte UnsupportedSource = 5;
        public const byte PermissionDenied = 6;
    }
}
