namespace Qaly.Application.Common.Models;

public sealed class PrivacyV4Options
{
    public const string SectionName = "PrivacyV4";

    public bool Enabled { get; set; }
    public bool WorkerEnabled { get; set; }
    public bool EnforceSensitiveIngestion { get; set; }
    public int PollIntervalMilliseconds { get; set; } = 1000;
    public int LeaseSeconds { get; set; } = 120;
    public int BatchSize { get; set; } = 4;
    public int MaxAttempts { get; set; } = 5;
    public int BaseRetrySeconds { get; set; } = 30;
    public int MaxPayloadCharacters { get; set; } = 80_000;
    public int ExportLifetimeMinutes { get; set; } = 60;
    public int DsarDeadlineDays { get; set; } = 30;
    public int[] AllowedRetentionDays { get; set; } = [7, 30, 90, 180, 365];
}
