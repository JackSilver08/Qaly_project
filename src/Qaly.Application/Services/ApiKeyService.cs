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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private const string KeyPrefix = "qaly_sk_";
    private const int MaxActiveKeysPerUser = 20;

    public ApiKeyService(
        IRepository<ApiKey> apiKeyRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _apiKeyRepo = apiKeyRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<ApiKeyCreatedDto>> CreateAsync(CreateApiKeyDto dto, CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Forbidden<ApiKeyCreatedDto>();

        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<ApiKeyCreatedDto>("Tên API Key là bắt buộc.");
        if (name.Length > 100)
            return Result.Failure<ApiKeyCreatedDto>("Tên API Key không được vượt quá 100 ký tự.");
        if (dto.ExpiresAt.HasValue && dto.ExpiresAt.Value <= DateTimeOffset.UtcNow)
            return Result.Failure<ApiKeyCreatedDto>("Thời điểm hết hạn phải ở tương lai.");

        var scopes = ApiKeyScopeCatalog.Normalize(dto.Scopes);
        if (scopes.Count == 0)
            return Result.Failure<ApiKeyCreatedDto>("Phải chọn ít nhất một phạm vi truy cập.");

        var unsupportedScopes = scopes
            .Where(scope => !ApiKeyScopeCatalog.Supported.Contains(scope))
            .ToArray();
        if (unsupportedScopes.Length > 0)
        {
            return Result.Failure<ApiKeyCreatedDto>(
                $"Phạm vi chưa được hỗ trợ: {string.Join(", ", unsupportedScopes)}.");
        }

        var now = DateTimeOffset.UtcNow;
        var activeKeyCount = await _apiKeyRepo.GetQueryable()
            .CountAsync(key =>
                key.UserId == userId.Value &&
                !key.IsRevoked &&
                (!key.ExpiresAt.HasValue || key.ExpiresAt.Value > now), ct);
        if (activeKeyCount >= MaxActiveKeysPerUser)
        {
            return Result.Failure<ApiKeyCreatedDto>(
                $"Bạn đã đạt giới hạn {MaxActiveKeysPerUser} API Key đang hoạt động. Hãy thu hồi key không còn dùng.",
                409);
        }

        // Generate a cryptographically secure random key
        var randomPart = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        var fullKey = $"{KeyPrefix}{randomPart}";

        var keyHash = HashKey(fullKey);

        var entity = new ApiKey
        {
            UserId = userId.Value,
            Name = name,
            KeyHash = keyHash,
            Prefix = fullKey[..16], // "qaly_sk_" + first 8 chars of random
            Scopes = JsonSerializer.Serialize(scopes),
            ExpiresAt = dto.ExpiresAt,
            IsRevoked = false
        };

        await _apiKeyRepo.AddAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

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
            DeserializeScopes(k.Scopes),
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
        if (key == null || key.UserId != userId)
            return Result.NotFound("Không tìm thấy API Key.");
        if (key.IsRevoked) return Result.Success();

        key.IsRevoked = true;
        key.UpdatedAt = DateTimeOffset.UtcNow;
        await _apiKeyRepo.UpdateAsync(key, ct);
        await _unitOfWork.SaveChangesAsync(ct);

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

    public static List<string> DeserializeScopes(string? serializedScopes)
    {
        if (string.IsNullOrWhiteSpace(serializedScopes)) return [];

        try
        {
            return ApiKeyScopeCatalog.Normalize(
                    JsonSerializer.Deserialize<List<string>>(serializedScopes))
                .Where(ApiKeyScopeCatalog.Supported.Contains)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
