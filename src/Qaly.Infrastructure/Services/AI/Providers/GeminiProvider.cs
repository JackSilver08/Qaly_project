using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Qaly.Application.Common.Models;
using Qaly.Application.Services;

namespace Qaly.Infrastructure.Services.AI.Providers;

public class GeminiProvider : IAiProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public GeminiProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string ProviderName => "Gemini";

    public async Task<AiResponse> CompleteAsync(AiRequest request, AiProviderSetting config, CancellationToken cancellationToken = default)
    {
        var apiKey = string.IsNullOrWhiteSpace(config.ApiKey) ? Environment.GetEnvironmentVariable("GEMINI_API_KEY") : config.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_GEMINI_KEY")
        {
            throw new InvalidOperationException("Gemini API Key is not configured.");
        }

        var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? "https://generativelanguage.googleapis.com/v1beta" : config.BaseUrl;
        var model = string.IsNullOrWhiteSpace(config.Model) ? "gemini-1.5-flash" : config.Model;

        var contents = new List<object>();

        if (request.History != null)
        {
            foreach (var msg in request.History)
            {
                var role = string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase) ? "model" : "user";
                contents.Add(new
                {
                    role = role,
                    parts = new[] { new { text = msg.Content } }
                });
            }
        }

        contents.Add(new
        {
            role = "user",
            parts = new[] { new { text = request.Prompt } }
        });

        var requestBody = new
        {
            contents = contents,
            systemInstruction = new
            {
                parts = new[] { new { text = request.SystemPrompt } }
            },
            generationConfig = new
            {
                temperature = 0.3,
                maxOutputTokens = 1500
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var url = $"{baseUrl.TrimEnd('/')}/models/{model}:generateContent?key={apiKey}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var httpClient = _httpClientFactory.CreateClient("AiGemini");
        var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Gemini API call failed with status code {response.StatusCode}: {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(responseContent);
        var root = document.RootElement;

        var content = root.GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        int inputTokens = (request.Prompt.Length + request.SystemPrompt.Length) / 4;
        int outputTokens = content.Length / 4;

        if (root.TryGetProperty("usageMetadata", out var usageMetadata))
        {
            if (usageMetadata.TryGetProperty("promptTokenCount", out var ptc))
                inputTokens = ptc.GetInt32();
            if (usageMetadata.TryGetProperty("candidatesTokenCount", out var ctc))
                outputTokens = ctc.GetInt32();
        }

        decimal inputCost = (inputTokens / 1000000m) * config.InputTokenCostPerMillion;
        decimal outputCost = (outputTokens / 1000000m) * config.OutputTokenCostPerMillion;
        decimal cost = inputCost + outputCost;

        return new AiResponse
        {
            Content = content,
            ProviderName = ProviderName,
            ModelName = model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            EstimatedCostUsd = cost,
            IsMock = false,
            CacheHit = false
        };
    }
}
