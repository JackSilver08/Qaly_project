namespace Qaly.Application.Common.Interfaces;

public interface ISessionService
{
    Task RevokeAllUserSessionsAsync(Guid userId, CancellationToken ct = default);
}
