using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
}
