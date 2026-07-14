using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Qaly.Infrastructure.Services;
using StackExchange.Redis;

namespace Qaly.UnitTests;

#pragma warning disable CA1707

/// <summary>
/// T1-DH-02: Fault injection and circuit breaker tests for RedisSessionService.
/// Maps: REQ-P0-01, REQ-NFR-04, GAP-002/021 — revoke semantics and bounded timeout.
/// </summary>
public class RedisSessionServiceTests
{
    private static RedisSessionService CreateService(IConnectionMultiplexer redis)
    {
        var config = new ConfigurationBuilder().Build();
        var logger = Mock.Of<ILogger<RedisSessionService>>();
        return new RedisSessionService(redis, config, logger);
    }

    private static IConnectionMultiplexer BuildConnectedMock(IServer? serverMock = null, IDatabase? dbMock = null)
    {
        var server = serverMock ?? new Mock<IServer>().Object;
        var db = dbMock ?? new Mock<IDatabase>().Object;
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetEndPoints(It.IsAny<bool>()))
             .Returns(new System.Net.EndPoint[] { new System.Net.IPEndPoint(0, 6379) });
        redis.Setup(r => r.GetServer(It.IsAny<System.Net.EndPoint>(), It.IsAny<object?>()))
             .Returns(server);
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
             .Returns(db);
        return redis.Object;
    }

    [Fact]
    public async Task RevokeAllUserSessionsAsync_NoEndpoints_ReturnsFalse()
    {
        // Arrange — no Redis endpoints available
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetEndPoints(It.IsAny<bool>()))
             .Returns(Array.Empty<System.Net.EndPoint>());

        var service = CreateService(redis.Object);
        var userId = Guid.NewGuid();

        // Act
        var result = await service.RevokeAllUserSessionsAsync(userId);

        // Assert — no endpoints is a degraded path, returns false without crashing
        result.Should().BeFalse("no Redis endpoints should return false gracefully");
    }

    [Fact]
    public async Task RevokeAllUserSessionsAsync_WhenRedisThrows_ReturnsFalseAndLogs()
    {
        // Arrange — Redis throws exception during key scan
        var serverMock = new Mock<IServer>();
        serverMock.Setup(s => s.Keys(
            It.IsAny<int>(),
            It.IsAny<RedisValue>(),
            It.IsAny<int>(),
            It.IsAny<long>(),
            It.IsAny<int>(),
            It.IsAny<CommandFlags>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis unavailable"));

        var dbMock = new Mock<IDatabase>();
        var redis = BuildConnectedMock(serverMock.Object, dbMock.Object);
        var service = CreateService(redis);
        var userId = Guid.NewGuid();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await service.RevokeAllUserSessionsAsync(userId);
        stopwatch.Stop();

        // Assert — returns false without throwing
        result.Should().BeFalse("Redis exception should degrade gracefully");

        // Assert — bounded time ≤1s per PERF-03
        stopwatch.ElapsedMilliseconds.Should().BeLessThanOrEqualTo(1000,
            "revocation failure must be fail-fast within 1 second (PERF-03)");
    }

    [Fact]
    public async Task RevokeAllUserSessionsAsync_CircuitOpen_SkipsRedisAfterThreeFailures()
    {
        // Arrange — Redis always throws
        var callCount = 0;
        var serverMock = new Mock<IServer>();
        serverMock.Setup(s => s.Keys(
            It.IsAny<int>(),
            It.IsAny<RedisValue>(),
            It.IsAny<int>(),
            It.IsAny<long>(),
            It.IsAny<int>(),
            It.IsAny<CommandFlags>()))
            .Returns<int, RedisValue, int, long, int, CommandFlags>((db, pattern, pageSize, cursor, pageOffset, flags) =>
            {
                callCount++;
                throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis unavailable");
            });

        var redis = BuildConnectedMock(serverMock.Object);
        var service = CreateService(redis);
        var userId = Guid.NewGuid();

        // Act — trigger 3 failures
        for (var i = 0; i < 3; i++)
        {
            await service.RevokeAllUserSessionsAsync(userId);
        }

        var callsBeforeCircuitOpen = callCount;

        // 4th call — circuit should be open
        await service.RevokeAllUserSessionsAsync(userId);
        var callsAfterCircuitOpen = callCount;

        // Assert — 4th call does NOT hit Redis (circuit is open)
        callsBeforeCircuitOpen.Should().Be(3, "first 3 failures must all reach Redis");
        callsAfterCircuitOpen.Should().Be(3, "4th call is blocked by open circuit — Redis not called again");
    }

    [Fact]
    public async Task RevokeAllUserSessionsAsync_CircuitOpen_ReturnsImmediately()
    {
        // Arrange — trigger circuit open, then measure response time on open-circuit call
        var serverMock = new Mock<IServer>();
        serverMock.Setup(s => s.Keys(
            It.IsAny<int>(),
            It.IsAny<RedisValue>(),
            It.IsAny<int>(),
            It.IsAny<long>(),
            It.IsAny<int>(),
            It.IsAny<CommandFlags>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis unavailable"));

        var redis = BuildConnectedMock(serverMock.Object);
        var service = CreateService(redis);
        var userId = Guid.NewGuid();

        // Trigger 3 failures to open circuit
        for (var i = 0; i < 3; i++)
        {
            await service.RevokeAllUserSessionsAsync(userId);
        }

        // Act — 4th call on open circuit should be immediate
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await service.RevokeAllUserSessionsAsync(userId);
        stopwatch.Stop();

        // Assert — open circuit path returns false immediately
        result.Should().BeFalse("open circuit returns false without Redis call");
        stopwatch.ElapsedMilliseconds.Should().BeLessThanOrEqualTo(100,
            "open circuit must return without network delay (fail-fast)");
    }

    [Fact]
    public async Task RevokeAllUserSessionsAsync_WhenRedisAvailable_ReturnsTrueForNoKeys()
    {
        // Arrange — server returns empty key list
        var serverMock = new Mock<IServer>();
        serverMock.Setup(s => s.Keys(
            It.IsAny<int>(),
            It.IsAny<RedisValue>(),
            It.IsAny<int>(),
            It.IsAny<long>(),
            It.IsAny<int>(),
            It.IsAny<CommandFlags>()))
            .Returns(Enumerable.Empty<RedisKey>());

        var dbMock = new Mock<IDatabase>();
        var redis = BuildConnectedMock(serverMock.Object, dbMock.Object);
        var service = CreateService(redis);
        var userId = Guid.NewGuid();

        // Act
        var result = await service.RevokeAllUserSessionsAsync(userId);

        // Assert — no keys to revoke is still a success path
        result.Should().BeTrue("no keys to revoke is a valid successful revocation");
    }

    [Fact]
    public async Task RevokeAllUserSessionsAsync_WithExistingKeys_DeletesThemAndReturnsTrue()
    {
        // Arrange — server returns keys for user
        var userId = Guid.NewGuid();
        var keysToRevoke = new[]
        {
            (RedisKey)$"Qaly_AuthTicket:{userId}:session-1",
            (RedisKey)$"Qaly_AuthTicket:{userId}:session-2"
        };

        var serverMock = new Mock<IServer>();
        serverMock.Setup(s => s.Keys(
            It.IsAny<int>(),
            It.IsAny<RedisValue>(),
            It.IsAny<int>(),
            It.IsAny<long>(),
            It.IsAny<int>(),
            It.IsAny<CommandFlags>()))
            .Returns(keysToRevoke);

        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(db => db.KeyDeleteAsync(
            It.IsAny<RedisKey[]>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(keysToRevoke.Length);

        var redis = BuildConnectedMock(serverMock.Object, dbMock.Object);
        var service = CreateService(redis);

        // Act
        var result = await service.RevokeAllUserSessionsAsync(userId);

        // Assert
        result.Should().BeTrue("all sessions revoked successfully");
        dbMock.Verify(db => db.KeyDeleteAsync(
            It.Is<RedisKey[]>(keys => keys.Length == 2),
            It.IsAny<CommandFlags>()), Times.Once, "both session keys must be deleted");
    }
}

#pragma warning restore CA1707
