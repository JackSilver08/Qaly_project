namespace Qaly.Application.Common.Models;

public class AiGatewaySettings
{
    private int _providerTimeoutSeconds = 30;
    private int _schemaRepairAttempts = 2;

    public const string SectionName = "AiSettings";

    public string Provider { get; set; } = "Ollama";
    public string[] FallbackProviders { get; set; } = ["Ollama", "OpenAI", "Gemini"];
    public bool OfflineMode { get; set; }
    public bool AllowProviderDegradedMock { get; set; }
    public bool AllowLocalSensitiveProcessing { get; set; }
    public int ProviderTimeoutSeconds
    {
        get => _providerTimeoutSeconds;
        set => _providerTimeoutSeconds = value;
    }

    public int SchemaRepairAttempts
    {
        get => _schemaRepairAttempts;
        set => _schemaRepairAttempts = value;
    }

    // Backward-compatible aliases for the configuration names introduced on main.
    public int TimeoutSeconds
    {
        get => ProviderTimeoutSeconds;
        set => ProviderTimeoutSeconds = value;
    }

    public int MaxRetries
    {
        get => SchemaRepairAttempts;
        set => SchemaRepairAttempts = value;
    }

    public AiProviderSetting Ollama { get; set; } = new();
    public AiProviderSetting OpenAI { get; set; } = new();
    public AiProviderSetting Gemini { get; set; } = new();
}

public class AiProviderSetting
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public decimal InputTokenCostPerMillion { get; set; } = 0m;
    public decimal OutputTokenCostPerMillion { get; set; } = 0m;
}
