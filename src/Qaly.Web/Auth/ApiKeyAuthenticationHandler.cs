using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Web.Auth;

public static class ApiKeyDefaults
{
    public const string AuthenticationScheme = "ApiKey";
    public const string HeaderName = "Authorization";
}

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private static readonly Action<ILogger, Guid, Exception?> LogLastUsedPersistenceFailure =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(2401, nameof(LogLastUsedPersistenceFailure)),
            "Unable to persist LastUsedAt for API key {ApiKeyId}.");

    private readonly IRepository<ApiKey> _apiKeyRepo;
    private readonly IUnitOfWork _unitOfWork;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IRepository<ApiKey> apiKeyRepo,
        IUnitOfWork unitOfWork)
        : base(options, logger, encoder)
    {
        _apiKeyRepo = apiKeyRepo;
        _unitOfWork = unitOfWork;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyDefaults.HeaderName, out var authHeader))
            return AuthenticateResult.NoResult();

        var headerValue = authHeader.ToString();
        if (!headerValue.StartsWith("Bearer qaly_sk_", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var fullKey = headerValue["Bearer ".Length..].Trim();
        if (fullKey.Length < 16)
            return AuthenticateResult.Fail("API Key format không hợp lệ.");

        var prefix = fullKey[..16]; // "qaly_sk_" + 8 chars
        var keyHash = ApiKeyService.HashKey(fullKey);

        // Lookup by prefix + hash for fast, exact match
        var candidate = await _apiKeyRepo.GetQueryable()
            .Where(k => k.Prefix == prefix && k.KeyHash == keyHash && !k.IsRevoked && k.User.IsActive)
            .Include(k => k.User)
            .FirstOrDefaultAsync();

        if (candidate == null)
            return AuthenticateResult.Fail("API Key không hợp lệ.");

        // Check expiry
        if (candidate.ExpiresAt.HasValue && candidate.ExpiresAt.Value <= DateTimeOffset.UtcNow)
            return AuthenticateResult.Fail("API Key đã hết hạn.");

        // Persist usage without writing on every request in a busy integration.
        var now = DateTimeOffset.UtcNow;
        if (!candidate.LastUsedAt.HasValue || candidate.LastUsedAt.Value < now.AddMinutes(-5))
        {
            candidate.LastUsedAt = now;
            candidate.UpdatedAt = now;
            await _apiKeyRepo.UpdateAsync(candidate, Context.RequestAborted);
            try
            {
                await _unitOfWork.SaveChangesAsync(Context.RequestAborted);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogLastUsedPersistenceFailure(Logger, candidate.Id, exception);
            }
        }

        // Parse scopes
        var scopes = ApiKeyService.DeserializeScopes(candidate.Scopes);

        var identity = new ClaimsIdentity(ApiKeyDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, candidate.UserId.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, candidate.User.FullName));
        identity.AddClaim(new Claim(ClaimTypes.Email, candidate.User.Email));
        identity.AddClaim(new Claim(ClaimTypes.Role, candidate.User.Role));
        identity.AddClaim(new Claim("api_key_id", candidate.Id.ToString()));
        identity.AddClaim(new Claim("api_key_name", candidate.Name));

        foreach (var scope in scopes)
        {
            identity.AddClaim(new Claim("scope", scope));
        }

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyDefaults.AuthenticationScheme);

        return AuthenticateResult.Success(ticket);
    }
}
