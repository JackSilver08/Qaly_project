using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.UnitTests;

public class SystemRoleRulesTests
{
    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Moderator", false)]
    [InlineData("Member", false)]
    [InlineData(null, false)]
    public void CanManageUsers_ShouldOnlyAllowElevatedSystemRoles(string? role, bool expected)
        => SystemRoleRules.CanManageUsers(role).Should().Be(expected);

    [Theory]
    [InlineData("Moderator", true, "Moderator")]
    [InlineData("Member", true, "Member")]
    [InlineData(null, true, "Member")]
    [InlineData("Admin", false, "")]
    [InlineData("Owner", false, "")]
    public void TryNormalizeAssignableRole_ShouldNeverAllowAdmin(string? role, bool expected, string normalized)
    {
        SystemRoleRules.TryNormalizeAssignableRole(role, out var result).Should().Be(expected);
        result.Should().Be(normalized);
    }

    [Theory]
    [InlineData("Admin", true, "Admin")]
    [InlineData("Moderator", true, "Moderator")]
    [InlineData("Member", true, "Member")]
    [InlineData("User", true, "Member")]
    [InlineData("Owner", false, "")]
    [InlineData(null, false, "")]
    public void TryNormalizeKnownRole_FailsClosedForUnknownStoredRoles(
        string? role,
        bool expected,
        string normalized)
    {
        SystemRoleRules.TryNormalizeKnownRole(role, out var result).Should().Be(expected);
        result.Should().Be(normalized);
    }

    [Fact]
    public void UserModel_ShouldContainFilteredUniqueIndexForSingleAdmin()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new QalyDbContext(options);

        var index = context.Model.FindEntityType(typeof(User))!.GetIndexes()
            .Single(candidate => candidate.GetDatabaseName() == "UX_Users_SingleAdmin");

        index.IsUnique.Should().BeTrue();
        index.GetFilter().Should().Be("[Role] = 'Admin'");
    }

    [Fact]
    public void SystemModulePermissionModel_ShouldEnforceOneUniqueScope()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new QalyDbContext(options);

        var entity = context.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(SystemModulePermission))!;
        var roleIndex = entity.GetIndexes().Single(index =>
            index.GetDatabaseName() == "IX_SystemModulePermissions_SystemRole_ModuleKey");
        var userIndex = entity.GetIndexes().Single(index =>
            index.GetDatabaseName() == "IX_SystemModulePermissions_UserId_ModuleKey");

        roleIndex.IsUnique.Should().BeTrue();
        roleIndex.GetFilter().Should().Be("[SystemRole] IS NOT NULL AND [UserId] IS NULL");
        userIndex.IsUnique.Should().BeTrue();
        userIndex.GetFilter().Should().Be("[UserId] IS NOT NULL AND [SystemRole] IS NULL");
        entity.GetCheckConstraints().Should().ContainSingle(constraint =>
            constraint.Name == "CK_SystemModulePermissions_ExactlyOneScope");
    }
}
