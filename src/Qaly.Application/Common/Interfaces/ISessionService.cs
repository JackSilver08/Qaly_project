namespace Qaly.Application.Common.Interfaces;

public interface ISessionService
{
    Task<bool> RevokeAllUserSessionsAsync(Guid userId, CancellationToken ct = default);
}
