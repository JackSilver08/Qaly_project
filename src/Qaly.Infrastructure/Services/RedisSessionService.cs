using Qaly.Application.Common.Interfaces;
using StackExchange.Redis;
using Microsoft.Extensions.Configuration;

namespace Qaly.Infrastructure.Services;

public class RedisSessionService : ISessionService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _instanceName;

    public RedisSessionService(IConnectionMultiplexer redis, IConfiguration configuration)
    {
        _redis = redis;
        _instanceName = "Qaly_"; // Matching Program.cs
    }

    public async Task RevokeAllUserSessionsAsync(Guid userId, CancellationToken ct = default)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var pattern = $"{_instanceName}AuthTicket:{userId}:*";
        
        var keys = server.Keys(pattern: pattern).ToArray();
        if (keys.Length > 0)
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(keys);
        }
    }
}
