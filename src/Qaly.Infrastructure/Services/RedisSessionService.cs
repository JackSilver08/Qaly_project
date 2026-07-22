using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Qaly.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Qaly.Infrastructure.Services;

public class RedisSessionService : ISessionService
{
    private static readonly Action<ILogger, Guid, Exception?> LogCircuitOpen = LoggerMessage.Define<Guid>(
        LogLevel.Warning,
        new EventId(1, nameof(LogCircuitOpen)),
        "Redis session revocation skipped because the circuit is open for user {UserId}.");
    private static readonly Action<ILogger, Guid, Exception?> LogRevocationFailed = LoggerMessage.Define<Guid>(
        LogLevel.Warning,
        new EventId(2, nameof(LogRevocationFailed)),
        "Redis session revocation failed for user {UserId}; falling back to a no-op result.");
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan CircuitOpenDuration = TimeSpan.FromSeconds(15);
    private readonly IConnectionMultiplexer _redis;
    private readonly string _instanceName;
    private readonly ILogger<RedisSessionService> _logger;
    private readonly object _circuitLock = new();
    private DateTimeOffset _circuitOpenUntil = DateTimeOffset.MinValue;
    private int _failureCount;

    public RedisSessionService(IConnectionMultiplexer redis, IConfiguration configuration, ILogger<RedisSessionService> logger)
    {
        _redis = redis;
        _instanceName = "Qaly_"; // Matching Program.cs
        _logger = logger;
    }

    public async Task<bool> RevokeAllUserSessionsAsync(Guid userId, CancellationToken ct = default)
    {
        if (IsCircuitOpen())
        {
            LogCircuitOpen(_logger, userId, null);
            return false;
        }

        try
        {
            var endpoints = _redis.GetEndPoints();
            if (endpoints.Length == 0)
            {
                throw new InvalidOperationException("No Redis endpoints available.");
            }

            var server = _redis.GetServer(endpoints[0]);
            var pattern = $"{_instanceName}AuthTicket:{userId}:*";
            var keys = await ExecuteWithTimeoutAsync(() => Task.FromResult(server.Keys(pattern: pattern).ToArray()), "keys", ct);

            if (keys.Length > 0)
            {
                var db = _redis.GetDatabase();
                await ExecuteWithTimeoutAsync(() => db.KeyDeleteAsync(keys), "delete", ct);
            }

            ResetCircuit();
            return true;
        }
        catch (Exception ex)
        {
            RecordFailure();
            LogRevocationFailed(_logger, userId, ex);
            return false;
        }
    }

    private static async Task<T> ExecuteWithTimeoutAsync<T>(Func<Task<T>> operation, string operationName, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(OperationTimeout);

        var operationTask = operation();
        var completedTask = await Task.WhenAny(operationTask, Task.Delay(Timeout.InfiniteTimeSpan, timeoutCts.Token));
        if (completedTask != operationTask)
        {
            throw new TimeoutException($"Redis session operation {operationName} timed out.");
        }

        return await operationTask;
    }

    private bool IsCircuitOpen()
    {
        lock (_circuitLock)
        {
            return _circuitOpenUntil > DateTimeOffset.UtcNow;
        }
    }

    private void RecordFailure()
    {
        lock (_circuitLock)
        {
            _failureCount += 1;
            if (_failureCount >= 3)
            {
                _circuitOpenUntil = DateTimeOffset.UtcNow.Add(CircuitOpenDuration);
            }
        }
    }

    private void ResetCircuit()
    {
        lock (_circuitLock)
        {
            _failureCount = 0;
            _circuitOpenUntil = DateTimeOffset.MinValue;
        }
    }
}
