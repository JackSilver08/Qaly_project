using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Qaly.Application.Services;

namespace Qaly.Infrastructure.Services.AI;

/// <summary>
/// Microsoft Agent Framework adapter for Erumi. It intentionally starts as a
/// single-agent runtime; tools are supplied per request after project authorization.
/// </summary>
public sealed class MicrosoftAgentOrchestrator : IAiAgentOrchestrator
{
    private readonly IChatClient _chatClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _timeout;

    public MicrosoftAgentOrchestrator(
        IChatClient chatClient,
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider)
    {
        _chatClient = chatClient;
        _loggerFactory = loggerFactory;
        _serviceProvider = serviceProvider;
        IsEnabled = configuration.GetValue<bool>("Ai:AgentFramework:Enabled");
        _timeout = TimeSpan.FromSeconds(Math.Clamp(
            configuration.GetValue("Ai:AgentFramework:TimeoutSeconds", 45), 5, 120));
    }

    public bool IsEnabled { get; }

    public async Task<AiResponse> ExecuteAsync(
        AiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Prompt);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);

        var agent = _chatClient.AsAIAgent(
            instructions: request.SystemPrompt,
            name: "ErumiProjectAgent",
            description: "Qaly project operations assistant with permission-scoped tools.",
            tools: request.Tools,
            loggerFactory: _loggerFactory,
            services: _serviceProvider);

        var messages = new List<ChatMessage>();
        if (request.History != null)
        {
            foreach (var item in request.History)
            {
                var role = string.Equals(item.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                    ? ChatRole.Assistant
                    : ChatRole.User;
                messages.Add(new ChatMessage(role, item.Content));
            }
        }

        messages.Add(new ChatMessage(ChatRole.User, request.Prompt));

        var response = await agent.RunAsync(
            messages,
            session: null,
            options: new ChatClientAgentRunOptions
            {
                ChatOptions = new ChatOptions
                {
                    MaxOutputTokens = 1500,
                    Temperature = 0.3f
                }
            },
            cancellationToken: timeoutCts.Token);

        return new AiResponse
        {
            Content = response.Text,
            ProviderName = "MicrosoftAgentFramework",
            ModelName = "configured-chat-client",
            IsMock = false,
            CacheHit = false
        };
    }
}
