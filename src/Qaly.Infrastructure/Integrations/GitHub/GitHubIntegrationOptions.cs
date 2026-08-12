namespace Qaly.Infrastructure.Integrations.GitHub;

public sealed class GitHubIntegrationOptions
{
    public const string SectionName = "GitHub";

    public bool Enabled { get; set; }
    public bool WorkerEnabled { get; set; }
    public string WebhookSecret { get; set; } = string.Empty;
    public long AppId { get; set; }
    public string AppSlug { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string PrivateKeyPath { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://api.github.com";
    public int PollIntervalMilliseconds { get; set; } = 1000;
    public int BatchSize { get; set; } = 10;
    public int MaxAttempts { get; set; } = 5;
}
