using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Qaly.Application.Common.Models;
using Qaly.Application.Services;

namespace Qaly.Infrastructure.Services.AI.Providers;

public class OpenAIProvider : IAiProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public OpenAIProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string ProviderName => "OpenAI";

    public async Task<AiResponse> CompleteAsync(AiRequest request, AiProviderSetting config, CancellationToken cancellationToken = default)
    {
        var apiKey = string.IsNullOrWhiteSpace(config.ApiKey) ? Environment.GetEnvironmentVariable("OPENAI_API_KEY") : config.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_OPENAI_KEY")
        {
            throw new InvalidOperationException("OpenAI API Key is not configured.");
        }

        var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? "https://api.openai.com/v1" : config.BaseUrl;
        var model = string.IsNullOrWhiteSpace(config.Model) ? "gpt-4o" : config.Model;

        var messages = new List<object>
        {
            new { role = "system", content = request.SystemPrompt }
        };

        if (request.History != null)
        {
            foreach (var msg in request.History)
            {
                messages.Add(new { role = msg.Role.ToLowerInvariant(), content = msg.Content });
            }
        }

        messages.Add(new { role = "user", content = request.Prompt });

        var requestBody = new
        {
            model = model,
            messages = messages,
            temperature = 0.3,
            max_tokens = 1500
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var httpClient = _httpClientFactory.CreateClient("AiOpenAI");
        var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"OpenAI API call failed with status code {response.StatusCode}: {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(responseContent);
        var root = document.RootElement;

        var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        var usage = root.GetProperty("usage");
        int inputTokens = usage.GetProperty("prompt_tokens").GetInt32();
        int outputTokens = usage.GetProperty("completion_tokens").GetInt32();

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
