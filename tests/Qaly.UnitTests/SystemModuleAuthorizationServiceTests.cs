using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

public sealed class SystemModuleAuthorizationServiceTests
{
    [Theory]
    [InlineData("Admin", true, AiNativeSystemTier.Full)]
    [InlineData("Member", true, AiNativeSystemTier.Full)]
    [InlineData("User", true, AiNativeSystemTier.Full)]
    [InlineData("Moderator", true, AiNativeSystemTier.SummaryOnly)]
    [InlineData("UnknownRole", false, AiNativeSystemTier.Restricted)]
    [InlineData(null, false, AiNativeSystemTier.Restricted)]
    public void ResolveDefault_AiHubUsesExplicitRolePolicy(
        string? role,
        bool allowed,
        AiNativeSystemTier tier)
    {
        var result = SystemModulePermissionRules.ResolveDefault(
            role,
            SystemModulePermissionRules.AiHub);

        result.IsAllowed.Should().Be(allowed);
        result.AiTier.Should().Be(tier);
        result.Source.Should().Be(role is "UnknownRole" or null ? "default_unknown" : "role_default");
    }

    [Fact]
    public void ResolveDefault_UnknownModuleFailsClosed()
    {
        var result = SystemModulePermissionRules.ResolveDefault("Admin", "ShellAccess");

        result.IsAllowed.Should().BeFalse();
        result.AiTier.Should().Be(AiNativeSystemTier.Restricted);
    }

    [Fact]
    public async Task ResolveAsync_UserDenyOverridesPermissiveRole()
    {
        await using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.SystemModulePermissions.AddRange(
            Row(null, "Member", true, "Full"),
            Row(userId, null, false, "Full"));
        await db.SaveChangesAsync();

        var result = await CreateService(db).ResolveAsync(
            userId,
            "Member",
            SystemModulePermissionRules.AiHub);

        result.IsAllowed.Should().BeFalse();
        result.AiTier.Should().Be(AiNativeSystemTier.Restricted);
        result.Source.Should().Be("user_override");
    }

    [Fact]
    public async Task ResolveAsync_UserAllowIsMoreSpecificThanRoleDeny()
    {
        await using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.SystemModulePermissions.AddRange(
            Row(null, "Member", false, "Restricted"),
            Row(userId, null, true, "SummaryOnly"));
        await db.SaveChangesAsync();

        var result = await CreateService(db).ResolveAsync(
            userId,
            "Member",
            SystemModulePermissionRules.AiHub);

        result.IsAllowed.Should().BeTrue();
        result.AiTier.Should().Be(AiNativeSystemTier.SummaryOnly);
        result.Source.Should().Be("user_override");
    }

    [Fact]
    public async Task ResolveAsync_DuplicateRowsUseMostRestrictiveResult()
    {
        await using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.SystemModulePermissions.AddRange(
            Row(userId, null, true, "Full"),
            Row(userId, null, true, "not-a-tier"));
        await db.SaveChangesAsync();

        var result = await CreateService(db).ResolveAsync(
            userId,
            "Member",
            SystemModulePermissionRules.AiHub);

        result.IsAllowed.Should().BeTrue();
        result.AiTier.Should().Be(AiNativeSystemTier.Restricted);
    }

    private static SystemModulePermission Row(
        Guid? userId,
        string? role,
        bool allowed,
        string tier)
        => new()
        {
            UserId = userId,
            SystemRole = role,
            ModuleKey = SystemModulePermissionRules.AiHub,
            IsAllowed = allowed,
            AiTier = tier
        };

    private static SystemModuleAuthorizationService CreateService(QalyDbContext db)
        => new(new GenericRepository<SystemModulePermission>(db));

    private static QalyDbContext CreateContext()
        => new(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
