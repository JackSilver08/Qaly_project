using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qaly.Domain.Entities.GitHub;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Integrations.GitHub;

public enum GitHubWebhookReceiveStatus
{
    Accepted,
    Duplicate,
    InvalidRequest,
    InvalidSignature,
    Disabled
}

public sealed record GitHubWebhookReceiveResult(GitHubWebhookReceiveStatus Status, string? Error = null);

public interface IGitHubWebhookReceiver
{
    Task<GitHubWebhookReceiveResult> ReceiveAsync(
        string? deliveryId,
        string? eventName,
        string? signature,
        string payload,
        CancellationToken ct = default);
}

public sealed class GitHubWebhookReceiver : IGitHubWebhookReceiver
{
    private readonly QalyDbContext _db;
    private readonly IOptions<GitHubIntegrationOptions> _options;

    public GitHubWebhookReceiver(QalyDbContext db, IOptions<GitHubIntegrationOptions> options)
    {
        _db = db;
        _options = options;
    }

    public async Task<GitHubWebhookReceiveResult> ReceiveAsync(
        string? deliveryId,
        string? eventName,
        string? signature,
        string payload,
        CancellationToken ct = default)
    {
        var options = _options.Value;
        if (!options.Enabled)
            return new(GitHubWebhookReceiveStatus.Disabled, "GitHub integration is disabled.");

        if (string.IsNullOrWhiteSpace(deliveryId) || deliveryId.Length > 100 ||
            string.IsNullOrWhiteSpace(eventName) || eventName.Length > 100 ||
            string.IsNullOrWhiteSpace(payload))
            return new(GitHubWebhookReceiveStatus.InvalidRequest, "Missing or invalid GitHub webhook headers/payload.");

        if (string.IsNullOrWhiteSpace(options.WebhookSecret))
            return new(GitHubWebhookReceiveStatus.Disabled, "GitHub webhook secret is not configured.");

        if (!IsValidSignature(payload, signature, options.WebhookSecret))
            return new(GitHubWebhookReceiveStatus.InvalidSignature, "Invalid GitHub webhook signature.");

        if (await _db.GitHubWebhookInbox.AsNoTracking().AnyAsync(x => x.DeliveryId == deliveryId, ct))
            return new(GitHubWebhookReceiveStatus.Duplicate);

        long? installationId;
        long? repositoryId;
        try
        {
            using var document = JsonDocument.Parse(payload);
            installationId = ReadInt64(document.RootElement, "installation", "id");
            repositoryId = ReadInt64(document.RootElement, "repository", "id");
        }
        catch (JsonException)
        {
            return new(GitHubWebhookReceiveStatus.InvalidRequest, "Payload is not valid JSON.");
        }

        var inbox = new GitHubWebhookInbox
        {
            DeliveryId = deliveryId,
            EventName = eventName,
            InstallationId = installationId,
            RepositoryExternalId = repositoryId,
            Payload = payload,
            Status = "Pending",
            ReceivedAt = DateTimeOffset.UtcNow
        };
        _db.GitHubWebhookInbox.Add(inbox);

        try
        {
            await _db.SaveChangesAsync(ct);
            return new(GitHubWebhookReceiveStatus.Accepted);
        }
        catch (DbUpdateException)
        {
            _db.Entry(inbox).State = EntityState.Detached;
            if (await _db.GitHubWebhookInbox.AsNoTracking().AnyAsync(x => x.DeliveryId == deliveryId, ct))
                return new(GitHubWebhookReceiveStatus.Duplicate);
            throw;
        }
    }

    private static bool IsValidSignature(string payload, string? supplied, string secret)
    {
        const string prefix = "sha256=";
        if (string.IsNullOrWhiteSpace(supplied) || !supplied.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        byte[] suppliedBytes;
        try { suppliedBytes = Convert.FromHexString(supplied[prefix.Length..]); }
        catch (FormatException) { return false; }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return suppliedBytes.Length == expected.Length && CryptographicOperations.FixedTimeEquals(suppliedBytes, expected);
    }

    private static long? ReadInt64(JsonElement root, string parent, string child)
        => root.TryGetProperty(parent, out var parentElement) &&
           parentElement.ValueKind == JsonValueKind.Object &&
           parentElement.TryGetProperty(child, out var value) && value.TryGetInt64(out var result)
            ? result
            : null;
}
