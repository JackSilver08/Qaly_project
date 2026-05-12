using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.ApiKey;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class ApiKeyService : IApiKeyService
{
    private readonly IRepository<ApiKey> _apiKeyRepo;
    private readonly ICurrentUserService _currentUserService;
    private const string KeyPrefix = "qaly_sk_";

    public ApiKeyService(
        IRepository<ApiKey> apiKeyRepo,
        ICurrentUserService currentUserService)
    {
        _apiKeyRepo = apiKeyRepo;
        _currentUserService = currentUserService;
    }

    public async Task<Result<ApiKeyCreatedDto>> CreateAsync(CreateApiKeyDto dto, CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Forbidden<ApiKeyCreatedDto>();

        // Generate a cryptographically secure random key
        var randomBytes = RandomNumberGenerator.GetBytes(24);
        var randomPart = Convert.ToBase64String(randomBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")[..32];
        var fullKey = $"{KeyPrefix}{randomPart}";

        var scopes = dto.Scopes ?? ["tasks:read", "projects:read"];
        var keyHash = HashKey(fullKey);

        var entity = new ApiKey
        {
            UserId = userId.Value,
            Name = dto.Name,
            KeyHash = keyHash,
            Prefix = fullKey[..16], // "qaly_sk_" + first 8 chars of random
            Scopes = JsonSerializer.Serialize(scopes),
            ExpiresAt = dto.ExpiresAt,
            IsRevoked = false
        };

        await _apiKeyRepo.AddAsync(entity, ct);

        return Result.Created(new ApiKeyCreatedDto(
            entity.Id,
            entity.Name,
            fullKey, // Only returned once at creation time
            scopes,
            entity.ExpiresAt,
            entity.CreatedAt
        ));
    }

    public async Task<Result<IReadOnlyList<ApiKeyDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Forbidden<IReadOnlyList<ApiKeyDto>>();

        var keys = await _apiKeyRepo.GetQueryable()
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);

        var dtos = keys.Select(k => new ApiKeyDto(
            k.Id,
            k.Name,
            k.Prefix,
            JsonSerializer.Deserialize<List<string>>(k.Scopes) ?? [],
            k.ExpiresAt,
            k.LastUsedAt,
            k.IsRevoked,
            k.CreatedAt
        )).ToList();

        return Result.Success<IReadOnlyList<ApiKeyDto>>(dtos);
    }

    public async Task<Result> RevokeAsync(Guid keyId, CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Forbidden();

        var key = await _apiKeyRepo.GetByIdAsync(keyId, ct);
        if (key == null) return Result.NotFound("Không tìm thấy API Key.");
        if (key.UserId != userId) return Result.Forbidden("Bạn không sở hữu API Key này.");

        key.IsRevoked = true;
        await _apiKeyRepo.UpdateAsync(key, ct);

        return Result.Success();
    }

    /// <summary>
    /// Hash API key using SHA-256. API keys are cryptographically random so
    /// a fast hash is sufficient (no need for BCrypt's slow intentional hashing).
    /// </summary>
    public static string HashKey(string key)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexStringLower(hashBytes);
    }

    /// <summary>
    /// Verify an API key against a stored SHA-256 hash.
    /// </summary>
    internal static bool VerifyKey(string key, string storedHash)
    {
        return string.Equals(HashKey(key), storedHash, StringComparison.OrdinalIgnoreCase);
    }
}
