using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Qaly.Infrastructure.Auth;
using Qaly.Infrastructure.Services;
using StackExchange.Redis;
using System.Security.Claims;

namespace Qaly.UnitTests;

#pragma warning disable CA1707 // Identifiers should not contain underscores — Test method naming convention
public class SecurityCriticalTests
{
    [Fact]
    public async Task RedisTicketStore_StoreAsync_UsesUserIdInKey()
    {
        var cache = new Mock<IDistributedCache>();
        var store = new RedisTicketStore(cache.Object, Mock.Of<ILogger<RedisTicketStore>>());
        var userId = Guid.NewGuid().ToString();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        var key = await store.StoreAsync(ticket);

        key.Should().StartWith($"AuthTicket:{userId}:");
        cache.Verify(c => c.SetAsync(key, It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default), Times.Once);
    }

    [Fact]
    public async Task RedisSessionService_RevokeAllUserSessionsAsync_CallsRedisDelete()
    {
        var redis = new Mock<IConnectionMultiplexer>();
        var server = new Mock<IServer>();
        var db = new Mock<IDatabase>();
        var config = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();

        var userId = Guid.NewGuid();
        var pattern = $"Qaly_AuthTicket:{userId}:*";
        var keys = new RedisKey[] { "key1", "key2" };

        redis.Setup(r => r.GetEndPoints(false)).Returns(new System.Net.EndPoint[] { new System.Net.DnsEndPoint("localhost", 6379) });
        redis.Setup(r => r.GetServer(It.IsAny<System.Net.EndPoint>(), null)).Returns(server.Object);
        redis.Setup(r => r.GetDatabase(-1, null)).Returns(db.Object);
        server.Setup(s => s.Keys(
            It.IsAny<int>(),
            pattern,
            It.IsAny<int>(),
            It.IsAny<long>(),
            It.IsAny<int>(),
            It.IsAny<CommandFlags>()))
            .Returns(keys);

        var service = new RedisSessionService(redis.Object, config.Object);

        await service.RevokeAllUserSessionsAsync(userId);

        db.Verify(d => d.KeyDeleteAsync(keys, CommandFlags.None), Times.Once);
    }
}
#pragma warning restore CA1707
