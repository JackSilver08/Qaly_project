namespace Qaly.Application.Common.Interfaces;

/// <summary>
/// Resolves credentials for the fixed local demo accounts. Implementations must
/// return <see langword="null"/> outside environments where demo seeding is allowed.
/// </summary>
public interface ISeedCredentialProvider
{
    string? GetPassword(string normalizedEmail);
}
