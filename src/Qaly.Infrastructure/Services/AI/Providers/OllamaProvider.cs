using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Qaly.Application.Common.Models;
using Qaly.Application.Services;

namespace Qaly.Infrastructure.Services.AI.Providers;

public class OllamaProvider : IAiProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public OllamaProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string ProviderName => "Ollama";

    public async Task<AiResponse> CompleteAsync(AiRequest request, AiProviderSetting config, CancellationToken cancellationToken = default)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? "http://localhost:11434" : config.BaseUrl;
        var model = string.IsNullOrWhiteSpace(config.Model) ? "llama3.2:1b" : config.Model;

        var httpClient = _httpClientFactory.CreateClient("AiOllama");
        var chatMessages = new List<object>
        {
            new { role = "system", content = request.SystemPrompt }
        };

        if (request.History != null)
        {
            foreach (var msg in request.History)
            {
                var role = string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                    ? "assistant" : "user";
                chatMessages.Add(new { role, content = msg.Content });
            }
        }

        chatMessages.Add(new { role = "user", content = request.Prompt });

        using var response = await httpClient.PostAsJsonAsync(
            $"{baseUrl.TrimEnd('/')}/api/chat",
            new
            {
                model,
                messages = chatMessages,
                stream = false,
                options = new
                {
                    temperature = 0.3,
                    num_predict = 1500
                }
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var root = payload.RootElement;
        var content = root.TryGetProperty("message", out var message)
            && message.TryGetProperty("content", out var contentElement)
                ? contentElement.GetString() ?? string.Empty
                : string.Empty;

        int inputTokens = root.TryGetProperty("prompt_eval_count", out var inputCount)
            ? inputCount.GetInt32()
            : (request.Prompt.Length + request.SystemPrompt.Length) / 4;
        int outputTokens = root.TryGetProperty("eval_count", out var outputCount)
            ? outputCount.GetInt32()
            : content.Length / 4;
        
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
