using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Qaly.Infrastructure.Auth;

public partial class RedisTicketStore : ITicketStore
{
    private const string KeyPrefix = "AuthTicket:";
    private static readonly ConcurrentDictionary<string, byte[]> FallbackTickets = new();
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisTicketStore> _logger;

    public RedisTicketStore(IDistributedCache cache, ILogger<RedisTicketStore> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var userId = ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        var guid = Guid.NewGuid().ToString();
        var key = $"{KeyPrefix}{userId}:{guid}";
        await RenewAsync(key, ticket);
        return key;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var options = new DistributedCacheEntryOptions();
        var expiresUtc = ticket.Properties.ExpiresUtc;

        if (expiresUtc.HasValue)
        {
            options.AbsoluteExpiration = expiresUtc.Value;
        }
        else
        {
            options.SlidingExpiration = TimeSpan.FromHours(8);
        }

        var val = Serialize(ticket);
        try
        {
            await _cache.SetAsync(key, val, options);
            FallbackTickets.TryRemove(key, out _);
        }
        catch (Exception ex)
        {
            LogTicketStoreWriteFailed(_logger, ex, key);
            FallbackTickets[key] = val;
        }
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        try
        {
            var val = await _cache.GetAsync(key);
            if (val != null)
            {
                return Deserialize(val);
            }
        }
        catch (Exception ex)
        {
            LogTicketStoreReadFailed(_logger, ex, key);
        }

        if (!FallbackTickets.TryGetValue(key, out var fallbackVal))
        {
            return null;
        }

        return Deserialize(fallbackVal);
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _cache.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            LogTicketStoreRemoveFailed(_logger, ex, key);
        }

        FallbackTickets.TryRemove(key, out _);
    }

    private static byte[] Serialize(AuthenticationTicket ticket)
    {
        return TicketSerializer.Default.Serialize(ticket);
    }

    private static AuthenticationTicket? Deserialize(byte[] data)
    {
        return TicketSerializer.Default.Deserialize(data);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Redis ticket store write failed, falling back to in-memory cache for {Key}.")]
    private static partial void LogTicketStoreWriteFailed(ILogger logger, Exception exception, string key);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Redis ticket store read failed for {Key}, using fallback cache.")]
    private static partial void LogTicketStoreReadFailed(ILogger logger, Exception exception, string key);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Redis ticket store remove failed for {Key}, clearing fallback cache.")]
    private static partial void LogTicketStoreRemoveFailed(ILogger logger, Exception exception, string key);
}
