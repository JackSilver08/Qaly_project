using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.Common.Models;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

#pragma warning disable CA1707
public class AuditLogRecentWorkspaceActivityIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IntegrationTestFactory _factory;

    public AuditLogRecentWorkspaceActivityIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetRecentWorkspaceActivity_WhenTwoOrganizationsExist_DoesNotLeakCrossTenantAuditLogs()
    {
        var organizationAId = Guid.NewGuid();
        var organizationBId = Guid.NewGuid();
        var ownerAId = Guid.NewGuid();
        var ownerBId = Guid.NewGuid();
        var memberAId = Guid.NewGuid();
        var memberBId = Guid.NewGuid();
        var projectAId = Guid.NewGuid();
        var projectBId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await EnsureUserExists(ownerAId, "Owner A", $"owner-a-{Guid.NewGuid():N}@qaly.dev");
        await EnsureUserExists(ownerBId, "Owner B", $"owner-b-{Guid.NewGuid():N}@qaly.dev");
        await EnsureUserExists(memberAId, "Member A", $"member-a-{Guid.NewGuid():N}@qaly.dev");
        await EnsureUserExists(memberBId, "Member B", $"member-b-{Guid.NewGuid():N}@qaly.dev");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();

            db.Organizations.AddRange(
                new Organization
                {
                    Id = organizationAId,
                    Name = "Organization A",
                    Code = $"org-a-{Guid.NewGuid():N}",
                    OwnerId = ownerAId,
                    IsActive = true
                },
                new Organization
                {
                    Id = organizationBId,
                    Name = "Organization B",
                    Code = $"org-b-{Guid.NewGuid():N}",
                    OwnerId = ownerBId,
                    IsActive = true
                });

            db.OrganizationMembers.AddRange(
                new OrganizationMember
                {
                    OrganizationId = organizationAId,
                    UserId = memberAId,
                    Role = "Member"
                },
                new OrganizationMember
                {
                    OrganizationId = organizationBId,
                    UserId = memberBId,
                    Role = "Member"
                });

            db.Projects.AddRange(
                new Project
                {
                    Id = projectAId,
                    Name = "Project A",
                    Code = $"project-a-{Guid.NewGuid():N}",
                    OwnerId = ownerAId,
                    OrganizationId = organizationAId
                },
                new Project
                {
                    Id = projectBId,
                    Name = "Project B",
                    Code = $"project-b-{Guid.NewGuid():N}",
                    OwnerId = ownerBId,
                    OrganizationId = organizationBId
                });

            db.AuditLogs.AddRange(
                new AuditLog
                {
                    Action = "Update",
                    EntityType = nameof(TaskComment),
                    EntityId = Guid.NewGuid().ToString(),
                    UserId = memberBId,
                    ChangesJson = "{not-valid-json",
                    Timestamp = now.AddSeconds(-30)
                },
                new AuditLog
                {
                    Action = "Update",
                    EntityType = nameof(Organization),
                    EntityId = organizationBId.ToString(),
                    UserId = ownerBId,
                    ChangesJson = """{"title":"Organization B settings"}""",
                    Timestamp = now.AddMinutes(-1)
                },
                new AuditLog
                {
                    Action = "Create",
                    EntityType = nameof(Project),
                    EntityId = projectBId.ToString(),
                    UserId = ownerBId,
                    ChangesJson = $$"""{"projectId":"{{projectBId}}","title":"Project B"}""",
                    Timestamp = now.AddMinutes(-2)
                },
                new AuditLog
                {
                    Action = "Update",
                    EntityType = nameof(User),
                    EntityId = ownerAId.ToString(),
                    UserId = ownerAId,
                    ChangesJson = """{"title":"Owner A profile"}""",
                    Timestamp = now.AddMinutes(-3)
                },
                new AuditLog
                {
                    Action = "Update",
                    EntityType = nameof(Project),
                    EntityId = projectAId.ToString(),
                    UserId = ownerAId,
                    ChangesJson = $$"""{"projectId":"{{projectAId}}","title":"Project A"}""",
                    Timestamp = now.AddMinutes(-4)
                });

            await db.SaveChangesAsync();
        }

        var responseForA = await SendRecentActivityRequestAsync(ownerAId, limit: 10);
        responseForA.StatusCode.Should().Be(HttpStatusCode.OK);
        responseForA.Body.Should().NotBeNull();
        responseForA.Body!.IsSuccess.Should().BeTrue();
        responseForA.Body.Data.Should().NotBeNull();
        responseForA.Body.Data!.Items.Should().HaveCount(2);
        responseForA.Body.Data.Items.Should().OnlyContain(item => item.UserId == ownerAId);
        responseForA.Body.Data.Items.Should().Contain(item => item.EntityType == nameof(User) && item.EntityId == ownerAId.ToString());
        responseForA.Body.Data.Items.Should().Contain(item => item.EntityType == nameof(Project) && item.EntityId == projectAId.ToString());
        responseForA.Body.Data.Items.Should().NotContain(item => item.EntityId == organizationBId.ToString());
        responseForA.Body.Data.Items.Should().NotContain(item => item.EntityId == projectBId.ToString());
        responseForA.Body.Data.Items.Should().NotContain(item => item.ChangesJson != null && item.ChangesJson.Contains(projectBId.ToString(), StringComparison.OrdinalIgnoreCase));

        var responseForB = await SendRecentActivityRequestAsync(ownerBId, limit: 10);
        responseForB.StatusCode.Should().Be(HttpStatusCode.OK);
        responseForB.Body.Should().NotBeNull();
        responseForB.Body!.IsSuccess.Should().BeTrue();
        responseForB.Body.Data.Should().NotBeNull();
        responseForB.Body.Data!.Items.Should().HaveCount(2);
        responseForB.Body.Data.Items.Should().OnlyContain(item => item.UserId == ownerBId);
        responseForB.Body.Data.Items.Should().Contain(item => item.EntityType == nameof(Organization) && item.EntityId == organizationBId.ToString());
        responseForB.Body.Data.Items.Should().Contain(item => item.EntityType == nameof(Project) && item.EntityId == projectBId.ToString());
        responseForB.Body.Data.Items.Should().NotContain(item => item.EntityId == organizationAId.ToString());
        responseForB.Body.Data.Items.Should().NotContain(item => item.EntityId == projectAId.ToString());
        responseForB.Body.Data.Items.Should().NotContain(item => item.ChangesJson != null && item.ChangesJson.Contains(projectAId.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetRecentWorkspaceActivity_ScansPastLargeHiddenTenantBatch()
    {
        var visibleOwnerId = Guid.NewGuid();
        var hiddenOwnerId = Guid.NewGuid();
        var visibleProjectId = Guid.NewGuid();
        var hiddenProjectId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow.AddYears(5);

        await EnsureUserExists(visibleOwnerId, "Visible Owner", $"visible-{Guid.NewGuid():N}@qaly.dev");
        await EnsureUserExists(hiddenOwnerId, "Hidden Owner", $"hidden-{Guid.NewGuid():N}@qaly.dev");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Projects.AddRange(
                new Project
                {
                    Id = visibleProjectId,
                    Name = "Visible older project",
                    Code = $"visible-{Guid.NewGuid():N}",
                    OwnerId = visibleOwnerId
                },
                new Project
                {
                    Id = hiddenProjectId,
                    Name = "Hidden recent project",
                    Code = $"hidden-{Guid.NewGuid():N}",
                    OwnerId = hiddenOwnerId
                });

            db.AuditLogs.AddRange(Enumerable.Range(0, 520).Select(index => new AuditLog
            {
                Action = "Update",
                EntityType = nameof(Project),
                EntityId = hiddenProjectId.ToString(),
                UserId = hiddenOwnerId,
                Timestamp = now.AddSeconds(-index)
            }));
            db.AuditLogs.AddRange(
                new AuditLog
                {
                    Action = "Update",
                    EntityType = nameof(Project),
                    EntityId = visibleProjectId.ToString(),
                    UserId = hiddenOwnerId,
                    Timestamp = now.AddHours(-2)
                },
                new AuditLog
                {
                    Action = "Create",
                    EntityType = nameof(Project),
                    EntityId = visibleProjectId.ToString(),
                    UserId = hiddenOwnerId,
                    Timestamp = now.AddHours(-3)
                });
            await db.SaveChangesAsync();
        }

        var response = await SendRecentActivityRequestAsync(visibleOwnerId, limit: 2);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Body!.Data!.Items.Should().HaveCount(2);
        response.Body.Data.Items.Should().OnlyContain(item => item.EntityId == visibleProjectId.ToString());
    }

    [Fact]
    public async Task StageAsync_PersistsOnlyWithSharedUnitOfWorkCommit()
    {
        var entityId = Guid.NewGuid().ToString();

        using (var scope = _factory.Services.CreateScope())
        {
            var audit = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await audit.StageAsync("AtomicStage", nameof(Project), entityId, new { projectId = entityId });

            using (var beforeScope = _factory.Services.CreateScope())
            {
                var beforeDb = beforeScope.ServiceProvider.GetRequiredService<QalyDbContext>();
                (await beforeDb.AuditLogs.AnyAsync(log => log.EntityId == entityId)).Should().BeFalse();
            }

            await unitOfWork.SaveChangesAsync();
        }

        using var afterScope = _factory.Services.CreateScope();
        var afterDb = afterScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await afterDb.AuditLogs.AnyAsync(log => log.EntityId == entityId && log.Action == "AtomicStage"))
            .Should().BeTrue();
    }

    private async Task<(HttpStatusCode StatusCode, ResultEnvelope<PagedResult<AuditLogDto>>? Body)> SendRecentActivityRequestAsync(Guid userId, int limit)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/audit-logs/recent?limit={limit}");
        request.Headers.Add("X-Test-UserId", userId.ToString());
        request.Headers.Add("X-Test-Role", "User");

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<ResultEnvelope<PagedResult<AuditLogDto>>>(JsonOptions);
        return (response.StatusCode, payload);
    }

    private async Task EnsureUserExists(Guid userId, string name, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        if (!db.Users.Any(user => user.Id == userId))
        {
            db.Users.Add(new User
            {
                Id = userId,
                FullName = name,
                Email = email,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }
    }

    private sealed record ResultEnvelope<T>(bool IsSuccess, T? Data, string? Error, int StatusCode);
}
#pragma warning restore CA1707
