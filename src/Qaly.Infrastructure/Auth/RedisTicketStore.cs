using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace Qaly.Infrastructure.Auth;

public partial class RedisTicketStore : ITicketStore
{
    private const string KeyPrefix = "AuthTicket:";
    private static readonly TimeSpan CacheOperationTimeout = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan CircuitOpenDuration = TimeSpan.FromSeconds(15);
    private static readonly ConcurrentDictionary<string, byte[]> FallbackTickets = new();
    private static readonly ConcurrentDictionary<string, CircuitBreakerState> CircuitBreakers = new();
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
            await ExecuteWithTimeoutAsync(() => _cache.SetAsync(key, val, options), key, isRead: false);
            FallbackTickets.TryRemove(key, out _);
            ResetCircuit(key);
        }
        catch (Exception ex)
        {
            RecordFailure(key);
            LogTicketStoreWriteFailed(_logger, ex, key);
            FallbackTickets[key] = val;
        }
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        try
        {
            var val = await ExecuteWithTimeoutAsync(() => _cache.GetAsync(key), key, isRead: true);
            if (val != null)
            {
                ResetCircuit(key);
                return Deserialize(val);
            }
        }
        catch (Exception ex)
        {
            RecordFailure(key);
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
            await ExecuteWithTimeoutAsync(() => _cache.RemoveAsync(key), key, isRead: false);
            ResetCircuit(key);
        }
        catch (Exception ex)
        {
            RecordFailure(key);
            LogTicketStoreRemoveFailed(_logger, ex, key);
        }

        FallbackTickets.TryRemove(key, out _);
    }

    private static async Task<T> ExecuteWithTimeoutAsync<T>(Func<Task<T>> operation, string key, bool isRead)
    {
        if (IsCircuitOpen(key))
        {
            throw new TimeoutException($"Redis circuit open for {key}");
        }

        var operationTask = operation();
        var completedTask = await Task.WhenAny(operationTask, Task.Delay(CacheOperationTimeout));
        if (completedTask != operationTask)
        {
            throw new TimeoutException($"Redis { (isRead ? "read" : "write") } timed out for {key}");
        }

        return await operationTask;
    }

    private static async Task ExecuteWithTimeoutAsync(Func<Task> operation, string key, bool isRead)
    {
        if (IsCircuitOpen(key))
        {
            throw new TimeoutException($"Redis circuit open for {key}");
        }

        var operationTask = operation();
        var completedTask = await Task.WhenAny(operationTask, Task.Delay(CacheOperationTimeout));
        if (completedTask != operationTask)
        {
            throw new TimeoutException($"Redis { (isRead ? "read" : "write") } timed out for {key}");
        }

        await operationTask;
    }

    private static bool IsCircuitOpen(string key)
    {
        var breaker = CircuitBreakers.GetOrAdd(key, _ => new CircuitBreakerState());
        return breaker.IsOpen;
    }

    private static void RecordFailure(string key)
    {
        var breaker = CircuitBreakers.GetOrAdd(key, _ => new CircuitBreakerState());
        breaker.RecordFailure();
    }

    private static void ResetCircuit(string key)
    {
        if (CircuitBreakers.TryGetValue(key, out var breaker))
        {
            breaker.Reset();
        }
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

    private sealed class CircuitBreakerState
    {
        private int _failureCount;
        private DateTimeOffset _openUntil = DateTimeOffset.MinValue;

        public bool IsOpen => _openUntil > DateTimeOffset.UtcNow;

        public void RecordFailure()
        {
            var nextFailures = Interlocked.Increment(ref _failureCount);
            if (nextFailures >= 3)
            {
                _openUntil = DateTimeOffset.UtcNow.Add(CircuitOpenDuration);
            }
        }

        public void Reset()
        {
            Interlocked.Exchange(ref _failureCount, 0);
            _openUntil = DateTimeOffset.MinValue;
        }
    }
}
