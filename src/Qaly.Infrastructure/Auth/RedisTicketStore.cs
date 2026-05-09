using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Claims;
using System.Text.Json;

namespace Qaly.Infrastructure.Auth;

public class RedisTicketStore : ITicketStore
{
    private const string KeyPrefix = "AuthTicket:";
    private readonly IDistributedCache _cache;

    public RedisTicketStore(IDistributedCache cache)
    {
        _cache = cache;
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
        await _cache.SetAsync(key, val, options);
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        var val = await _cache.GetAsync(key);
        if (val == null) return null;

        return Deserialize(val);
    }

    public async Task RemoveAsync(string key)
    {
        await _cache.RemoveAsync(key);
    }

    private static byte[] Serialize(AuthenticationTicket ticket)
    {
        return TicketSerializer.Default.Serialize(ticket);
    }

    private static AuthenticationTicket? Deserialize(byte[] data)
    {
        return TicketSerializer.Default.Deserialize(data);
    }
}
