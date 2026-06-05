namespace Qaly.Application.Common.Models;

public sealed class LiveKitOptions
{
    public const string SectionName = "LiveKit";

    public string ServerUrl { get; set; } = "ws://localhost:7880";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public int TokenTtlMinutes { get; set; } = 120;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ServerUrl)
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(ApiSecret)
        && !ApiKey.Contains('<', StringComparison.Ordinal)
        && !ApiSecret.Contains('<', StringComparison.Ordinal);
}
