namespace pqy_server.Services
{
    public sealed class AlertServiceOptions
    {
        public bool Enabled { get; set; } = true;
        public int PollIntervalSeconds { get; set; } = 5;
        public int BatchSize { get; set; } = 200;
        public int MaxConcurrentSends { get; set; } = 20;
        public int LeaseSeconds { get; set; } = 60;
        public int RetryDelaySeconds { get; set; } = 120;
        public int MaxRetryAttempts { get; set; } = 3;
    }
}
