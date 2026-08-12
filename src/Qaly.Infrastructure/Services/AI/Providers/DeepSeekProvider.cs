using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Qaly.Application.Common.Models;
using Qaly.Application.Services;

namespace Qaly.Infrastructure.Services.AI.Providers;

public sealed class DeepSeekProvider : IAiProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public DeepSeekProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string ProviderName => "DeepSeek";

    public async Task<AiResponse> CompleteAsync(
        AiRequest request,
        AiProviderSetting config,
        CancellationToken cancellationToken = default)
    {
        var configuredApiKey = config.ApiKey?.Trim();
        var apiKey = IsMissingApiKey(configuredApiKey)
            ? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")?.Trim()
            : configuredApiKey;
        if (IsMissingApiKey(apiKey))
        {
            throw new InvalidOperationException("DeepSeek API key is not configured.");
        }

        var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl)
            ? "https://api.deepseek.com"
            : config.BaseUrl;
        var model = string.IsNullOrWhiteSpace(config.Model)
            ? "deepseek-chat"
            : config.Model;

        var messages = new List<object>
        {
            new { role = "system", content = request.SystemPrompt }
        };
        if (request.History != null)
        {
            messages.AddRange(request.History.Select(message => (object)new
            {
                role = message.Role.ToLowerInvariant(),
                content = message.Content
            }));
        }
        messages.Add(new { role = "user", content = request.Prompt });

        var requestBody = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = messages,
            ["temperature"] = 0.2,
            ["max_tokens"] = 3000,
            ["stream"] = false
        };
        if (!string.IsNullOrWhiteSpace(request.ExpectedSchemaId))
        {
            requestBody["thinking"] = new { type = "disabled" };
            requestBody["response_format"] = new { type = "json_object" };
        }
        else
        {
            requestBody["thinking"] = new { type = "enabled" };
            requestBody["reasoning_effort"] = "high";
        }

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var httpClient = _httpClientFactory.CreateClient("AiDeepSeek");
        var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"DeepSeek API call failed with status code {response.StatusCode}: {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(responseContent);
        var root = document.RootElement;
        var content = root.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(request.ExpectedSchemaId))
        {
            var startIndex = content.IndexOf('{');
            var endIndex = content.LastIndexOf('}');
            if (startIndex >= 0 && endIndex >= startIndex)
            {
                content = content.Substring(startIndex, endIndex - startIndex + 1);
            }
        }

        var inputTokens = 0;
        var outputTokens = 0;
        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("prompt_tokens", out var promptTokens))
            {
                inputTokens = promptTokens.GetInt32();
            }
            if (usage.TryGetProperty("completion_tokens", out var completionTokens))
            {
                outputTokens = completionTokens.GetInt32();
            }
        }

        return new AiResponse
        {
            Content = content,
            ProviderName = ProviderName,
            ModelName = model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            EstimatedCostUsd =
                (inputTokens / 1_000_000m * config.InputTokenCostPerMillion) +
                (outputTokens / 1_000_000m * config.OutputTokenCostPerMillion),
            IsMock = false,
            CacheHit = false
        };
    }

    private static bool IsMissingApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return true;
        }

        return apiKey.Equals("YOUR_DEEPSEEK_KEY", StringComparison.OrdinalIgnoreCase)
            || apiKey.Contains("<set-", StringComparison.OrdinalIgnoreCase)
            || apiKey.Contains("placeholder", StringComparison.OrdinalIgnoreCase)
            || apiKey.Equals("change-me", StringComparison.OrdinalIgnoreCase);
    }
}
