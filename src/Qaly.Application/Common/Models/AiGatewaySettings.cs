namespace Qaly.Application.Common.Models;

public class AiGatewaySettings
{
    public const string SectionName = "AiSettings";

    public string Provider { get; set; } = "Ollama";
    public bool OfflineMode { get; set; }
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
