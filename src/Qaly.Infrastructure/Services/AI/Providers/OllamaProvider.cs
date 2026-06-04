using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
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
        var ollamaClient = new OllamaChatClient(new Uri(baseUrl), model, httpClient);
        
        var chatMessages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, request.SystemPrompt)
        };

        if (request.History != null)
        {
            foreach (var msg in request.History)
            {
                var role = string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase) 
                    ? ChatRole.Assistant : ChatRole.User;
                chatMessages.Add(new ChatMessage(role, msg.Content));
            }
        }

        chatMessages.Add(new ChatMessage(ChatRole.User, request.Prompt));

        var options = new ChatOptions
        {
            MaxOutputTokens = 1500,
            Temperature = 0.3f
        };

        var response = await ollamaClient.CompleteAsync(chatMessages, options, cancellationToken);
        var content = response.Message.Text ?? string.Empty;

        int inputTokens = response.Usage?.InputTokenCount ?? (request.Prompt.Length + request.SystemPrompt.Length) / 4;
        int outputTokens = response.Usage?.OutputTokenCount ?? content.Length / 4;
        
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
