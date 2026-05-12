namespace Qaly.Application.DTOs.ApiKey;

public record CreateApiKeyDto(string Name, List<string>? Scopes, DateTimeOffset? ExpiresAt);

public record ApiKeyDto(
    Guid Id,
    string Name,
    string Prefix,
    List<string> Scopes,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    bool IsRevoked,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Returned only once at creation time — contains the full plaintext key.
/// </summary>
public record ApiKeyCreatedDto(
    Guid Id,
    string Name,
    string Key,
    List<string> Scopes,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt
);
