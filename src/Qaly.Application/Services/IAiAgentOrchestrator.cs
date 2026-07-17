namespace Qaly.Application.Services;

/// <summary>
/// Provider-neutral boundary for agent orchestration. The Application layer does not
/// depend on a concrete agent framework, which keeps Erumi testable and replaceable.
/// </summary>
public interface IAiAgentOrchestrator
{
    bool IsEnabled { get; }

    Task<AiResponse> ExecuteAsync(AiRequest request, CancellationToken cancellationToken = default);
}
