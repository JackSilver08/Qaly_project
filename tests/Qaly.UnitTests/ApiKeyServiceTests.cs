using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.ApiKey;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

public sealed class ApiKeyServiceTests : IDisposable
{
    private readonly QalyDbContext _db;
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Guid _userId = Guid.NewGuid();

    public ApiKeyServiceTests()
    {
        _db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _db.Users.Add(new User
        {
            Id = _userId,
            FullName = "API Owner",
            Email = $"api-{_userId:N}@qaly.test",
            PasswordHash = "test",
            Role = "Member",
            IsActive = true
        });
        _db.SaveChanges();
        _currentUser.SetupGet(service => service.UserId).Returns(_userId);
    }

    [Fact]
    public async Task CreateAsync_PersistsOnlyHashAndNormalizedSupportedScopes()
    {
        var service = CreateService();

        var result = await service.CreateAsync(new CreateApiKeyDto(
            "  Deploy bot  ",
            ["tasks:write", "projects:read", "tasks:write"],
            DateTimeOffset.UtcNow.AddDays(30)));

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Data!.Key.Should().StartWith("qaly_sk_");
        result.Data.Scopes.Should().Equal("projects:read", "tasks:write");

        var stored = await _db.ApiKeys.SingleAsync();
        stored.Name.Should().Be("Deploy bot");
        stored.KeyHash.Should().Be(ApiKeyService.HashKey(result.Data.Key));
        stored.KeyHash.Should().NotContain(result.Data.Key);
        stored.Prefix.Should().Be(result.Data.Key[..16]);
        ApiKeyService.DeserializeScopes(stored.Scopes).Should().Equal("projects:read", "tasks:write");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_RejectsBlankNameWithoutMutation(string name)
    {
        var result = await CreateService().CreateAsync(new CreateApiKeyDto(name, ["projects:read"], null));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        (await _db.ApiKeys.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_RejectsUnknownScopeAndExpiredKey()
    {
        var service = CreateService();

        var unknown = await service.CreateAsync(new CreateApiKeyDto("Unknown", ["admin:all"], null));
        var expired = await service.CreateAsync(new CreateApiKeyDto(
            "Expired",
            ["projects:read"],
            DateTimeOffset.UtcNow.AddMinutes(-1)));

        unknown.IsSuccess.Should().BeFalse();
        unknown.Error.Should().Contain("admin:all");
        expired.IsSuccess.Should().BeFalse();
        (await _db.ApiKeys.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RevokeAsync_PersistsRevocationAndIsIdempotent()
    {
        var service = CreateService();
        var created = await service.CreateAsync(new CreateApiKeyDto("Agent", ["tasks:read"], null));

        (await service.RevokeAsync(created.Data!.Id)).IsSuccess.Should().BeTrue();
        (await service.RevokeAsync(created.Data.Id)).IsSuccess.Should().BeTrue();

        (await _db.ApiKeys.SingleAsync()).IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_EnforcesActiveKeyLimit()
    {
        for (var index = 0; index < 20; index++)
        {
            _db.ApiKeys.Add(new ApiKey
            {
                UserId = _userId,
                Name = $"Key {index}",
                KeyHash = Guid.NewGuid().ToString("N"),
                Prefix = $"qaly_sk_{index:D8}",
                Scopes = "[\"projects:read\"]"
            });
        }
        await _db.SaveChangesAsync();

        var result = await CreateService().CreateAsync(new CreateApiKeyDto("Overflow", ["projects:read"], null));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        (await _db.ApiKeys.CountAsync()).Should().Be(20);
    }

    private ApiKeyService CreateService()
        => new(
            new GenericRepository<ApiKey>(_db),
            new UnitOfWork(_db),
            _currentUser.Object);

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
