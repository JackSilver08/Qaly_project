using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Qaly.Infrastructure.Auth;

namespace Qaly.UnitTests;

#pragma warning disable CA1707

/// <summary>
/// Wall-clock timeout assertions must not compete with the rest of the unit
/// suite for worker threads. Coverage instrumentation on a two-core runner can
/// otherwise delay the test continuation well beyond the 250 ms product
/// timeout even though the Redis fallback completed correctly.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RedisTicketStoreTimingDefinition
{
    public const string Name = "Redis ticket store timing";
}

/// <summary>
/// T1-DH-02: Fault injection and circuit breaker tests for RedisTicketStore.
/// Maps: REQ-P0-01, REQ-NFR-04, GAP-021 (Redis-unavailable bounded ≤1s).
/// </summary>
[Collection(RedisTicketStoreTimingDefinition.Name)]
public class RedisTicketStoreTests
{
    private static AuthenticationTicket CreateTicket(string userId = "user-test-01")
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Role, "Member")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties
        {
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
        };
        return new AuthenticationTicket(principal, properties, CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private static RedisTicketStore CreateStore(IDistributedCache cache)
    {
        var logger = Mock.Of<ILogger<RedisTicketStore>>();
        return new RedisTicketStore(cache, logger);
    }

    [Fact]
    public async Task StoreAsync_NormalOperation_ReturnsKeyWithUserPrefix()
    {
        // Arrange
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var store = CreateStore(cache.Object);
        var ticket = CreateTicket("user-store-01");

        // Act
        var key = await store.StoreAsync(ticket);

        // Assert — key contains user ID prefix
        key.Should().StartWith("AuthTicket:user-store-01:");
    }

    [Fact]
    public async Task RetrieveAsync_NormalOperation_DeserializesTicketCorrectly()
    {
        // Arrange
        var ticket = CreateTicket("user-retrieve-01");
        var serialized = TicketSerializer.Default.Serialize(ticket);

        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(serialized);

        var store = CreateStore(cache.Object);
        var key = await store.StoreAsync(ticket);

        // Act
        var retrieved = await store.RetrieveAsync(key);

        // Assert
        retrieved.Should().NotBeNull();
        var userId = retrieved!.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        userId.Should().Be("user-retrieve-01");
    }

    [Fact]
    public async Task RetrieveAsync_WhenRedisTimesOut_ReturnsFallbackTicketInBoundedTime()
    {
        // Arrange — cache write succeeds (so fallback is populated), read times out
        var ticket = CreateTicket("user-fallback-01");

        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Read never completes — simulates timeout
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .Returns(Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith<byte[]?>(_ => null!));

        var store = CreateStore(cache.Object);
        var key = await store.StoreAsync(ticket);

        // Force write to fail so fallback is populated
        var failCache = new Mock<IDistributedCache>();
        failCache.Setup(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Redis write timed out"));
        failCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith<byte[]?>(_ => null!));

        var storeWithFallback = CreateStore(failCache.Object);
        // Store will fail → fallback populated
        var fallbackKey = await storeWithFallback.StoreAsync(ticket);

        // Now read should hit fallback in <1s
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var retrieved = await storeWithFallback.RetrieveAsync(fallbackKey);
        stopwatch.Stop();

        // Assert — fallback ticket returned
        retrieved.Should().NotBeNull("fallback ticket must be served");
        retrieved!.Principal.FindFirstValue(ClaimTypes.NameIdentifier)
            .Should().Be("user-fallback-01");

        // Assert — bounded time ≤1s per PERF-03
        stopwatch.ElapsedMilliseconds.Should().BeLessThanOrEqualTo(1000,
            "Redis-unavailable path must complete within 1 second (PERF-03)");
    }

    [Fact]
    public async Task RetrieveAsync_WhenRedisUnavailable_ReturnsNullIfNoFallback()
    {
        // Arrange — no successful store (no fallback), Redis read times out
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis unavailable"));
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("Redis unavailable"));

        var store = CreateStore(cache.Object);
        var key = "AuthTicket:unknown-user:no-session-guid";

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var retrieved = await store.RetrieveAsync(key);
        stopwatch.Stop();

        // Assert
        retrieved.Should().BeNull("no fallback stored for unknown key");
        stopwatch.ElapsedMilliseconds.Should().BeLessThanOrEqualTo(1000,
            "Redis-unavailable read must fail fast within 1 second (PERF-03)");
    }

    [Fact]
    public async Task CircuitBreaker_OpensAfterThreeConsecutiveFailures_SkipsRedisOnOpenCircuit()
    {
        // Arrange — Redis always times out (exception)
        var ticket = CreateTicket("user-circuit-01");
        var callCount = 0;

        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .ThrowsAsync(new TimeoutException("Redis write timed out"));

        var store = CreateStore(cache.Object);

        // Act — trigger 3 failures to open circuit
        var key = $"AuthTicket:user-circuit-01:{Guid.NewGuid()}";
        for (var i = 0; i < 3; i++)
        {
            await store.RenewAsync(key, ticket);
        }

        var callsBeforeCircuitOpen = callCount;

        // 4th call — circuit should be open, Redis not called
        await store.RenewAsync(key, ticket);
        var callsAfterCircuitOpen = callCount;

        // Assert — calls stop after circuit opens
        callsBeforeCircuitOpen.Should().Be(3, "all 3 pre-circuit calls reach Redis");
        callsAfterCircuitOpen.Should().Be(3, "4th call is skipped by open circuit (no extra Redis call)");
    }

    [Fact]
    public async Task CircuitBreaker_ResetsAfterExpiry_AllowsRedisAgain()
    {
        // Arrange — open the circuit, then force expiry and verify Redis is attempted again
        var ticket = CreateTicket("user-circuit-reset-01");
        var callCount = 0;

        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns<string, byte[], DistributedCacheEntryOptions, CancellationToken>((k, v, o, ct) =>
            {
                callCount++;
                throw new TimeoutException("Redis write timed out");
            });

        var store = CreateStore(cache.Object);
        var key = $"AuthTicket:user-circuit-reset-01:{Guid.NewGuid()}";

        // Trigger 3 failures → circuit opens
        for (var i = 0; i < 3; i++)
        {
            await store.RenewAsync(key, ticket);
        }

        // Circuit is open — a 4th attempt should be skipped without touching Redis
        await store.RenewAsync(key, ticket);
        var callsWhileOpen = callCount;
        callsWhileOpen.Should().Be(3, "the open circuit must short-circuit repeated Redis calls");

        // Force expiry of the breaker and verify Redis is attempted again
        ExpireCircuitForKey(key);
        await store.RenewAsync(key, ticket);
        var callsAfterExpiry = callCount;

        callsAfterExpiry.Should().BeGreaterThan(callsWhileOpen,
            "after circuit expiry, Redis write should be attempted again");
    }

    [Fact]
    public async Task RemoveAsync_WhenRedisUnavailable_StillClearsFallback()
    {
        // Arrange — write fails → fallback populated
        var ticket = CreateTicket("user-remove-01");
        var removed = false;

        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis unavailable"));
        cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .Callback(() => removed = true)
             .Returns(Task.CompletedTask);
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((byte[]?)null);

        var store = CreateStore(cache.Object);
        var key = $"AuthTicket:user-remove-01:{Guid.NewGuid()}";

        // Store fails → fallback populated
        await store.RenewAsync(key, ticket);

        // Fallback should be there
        var beforeRemove = await store.RetrieveAsync(key);
        beforeRemove.Should().NotBeNull("fallback ticket must be available before remove");

        // Act
        _ = removed; // reset tracking - RemoveAsync with unavailable cache should still clear fallback
        await store.RemoveAsync(key);

        // After remove, fallback cleared
        var afterRemove = await store.RetrieveAsync(key);
        afterRemove.Should().BeNull("fallback must be cleared after RemoveAsync");
    }

    [Fact]
    public async Task FallbackTicket_IsBounded_NotGlobalFallback()
    {
        // Fallback only covers the specific key that failed — other keys return null
        var ticket = CreateTicket("user-bounded-01");

        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis unavailable"));
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("Redis unavailable"));

        var store = CreateStore(cache.Object);
        var key = $"AuthTicket:user-bounded-01:{Guid.NewGuid()}";

        // Store fails → fallback for this key
        await store.RenewAsync(key, ticket);

        // Different key — no fallback
        var otherKey = $"AuthTicket:user-bounded-01:{Guid.NewGuid()}";
        var otherResult = await store.RetrieveAsync(otherKey);

        otherResult.Should().BeNull("fallback is key-specific, not global");
    }

    private static void ExpireCircuitForKey(string key)
    {
        var storeType = typeof(RedisTicketStore);
        var circuitBreakersField = storeType.GetField("CircuitBreakers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        circuitBreakersField.Should().NotBeNull("test needs direct access to the private breaker cache");

        var circuitBreakers = circuitBreakersField!.GetValue(null);
        circuitBreakers.Should().NotBeNull("test needs the breaker dictionary instance");

        object? breaker = null;
        foreach (var entry in (System.Collections.IEnumerable)circuitBreakers!)
        {
            var entryType = entry.GetType();
            var entryKey = entryType.GetProperty("Key")?.GetValue(entry)?.ToString();
            if (entryKey == key)
            {
                breaker = entryType.GetProperty("Value")?.GetValue(entry);
                break;
            }
        }

        breaker.Should().NotBeNull("test needs the breaker state instance");

        var openUntilField = breaker!.GetType().GetField("_openUntil", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        openUntilField.Should().NotBeNull("test needs the breaker expiry field");
        openUntilField!.SetValue(breaker, DateTimeOffset.UtcNow.AddSeconds(-1));
    }
}

#pragma warning restore CA1707
