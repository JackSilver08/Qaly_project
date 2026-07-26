using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
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
    private readonly IRepository<ApiKey> _apiKeyRepo;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IRepository<ApiKey> apiKeyRepo)
        : base(options, logger, encoder)
    {
        _apiKeyRepo = apiKeyRepo;
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
        if (candidate.ExpiresAt.HasValue && candidate.ExpiresAt.Value < DateTimeOffset.UtcNow)
            return AuthenticateResult.Fail("API Key đã hết hạn.");

        // Update LastUsedAt
        candidate.LastUsedAt = DateTimeOffset.UtcNow;
        await _apiKeyRepo.UpdateAsync(candidate);

        // Parse scopes
        var scopes = JsonSerializer.Deserialize<List<string>>(candidate.Scopes) ?? [];

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
