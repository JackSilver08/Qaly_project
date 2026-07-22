using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public sealed record AiSourceGuardResult(bool IsAllowed, string? ErrorCode = null, string? ErrorMessage = null);
public sealed record AiSourceCaptureResult(
    bool IsAllowed,
    IReadOnlyList<AiJobSourceInputDto> Sources,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public interface IAiSourceGuard
{
    Task<AiSourceCaptureResult> CaptureAsync(
        Guid projectId,
        Guid userId,
        IReadOnlyList<AiJobSourceInputDto> sources,
        CancellationToken cancellationToken = default);

    Task<AiSourceGuardResult> ValidateAsync(
        Guid projectId,
        Guid userId,
        IReadOnlyList<AiJobSourceInputDto> sources,
        bool enforceFreshness,
        CancellationToken cancellationToken = default);
}
