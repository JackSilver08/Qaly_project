namespace Qaly.Infrastructure.Services;

public sealed class VectorSyncOptions
{
    public const string SectionName = "VectorSync";

    public int PollIntervalMilliseconds { get; set; } = 5000;
    public int BatchSize { get; set; } = 20;
    public int MaxAttempts { get; set; } = 5;
    public int LeaseSeconds { get; set; } = 120;
    public int HeartbeatSeconds { get; set; } = 30;
    public int BaseRetrySeconds { get; set; } = 5;
}
