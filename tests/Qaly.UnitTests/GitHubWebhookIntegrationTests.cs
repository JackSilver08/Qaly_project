using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qaly.Domain.Entities;
using Qaly.Domain.Entities.GitHub;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Integrations.GitHub;

namespace Qaly.UnitTests;

public sealed class GitHubWebhookIntegrationTests : IDisposable
{
    private const string Secret = "unit-test-webhook-secret";
    private readonly QalyDbContext _db;

    public GitHubWebhookIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase($"github-webhook-{Guid.NewGuid():N}").Options;
        _db = new QalyDbContext(options);
    }

    [Fact]
    public async Task Receiver_ValidSignature_PersistsOnce()
    {
        var receiver = Receiver();
        const string payload = """{"installation":{"id":12},"repository":{"id":34}}""";

        var first = await receiver.ReceiveAsync("delivery-1", "push", Sign(payload), payload);
        var duplicate = await receiver.ReceiveAsync("delivery-1", "push", Sign(payload), payload);

        first.Status.Should().Be(GitHubWebhookReceiveStatus.Accepted);
        duplicate.Status.Should().Be(GitHubWebhookReceiveStatus.Duplicate);
        (await _db.GitHubWebhookInbox.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Receiver_InvalidSignature_DoesNotPersist()
    {
        var result = await Receiver().ReceiveAsync("delivery-2", "push", "sha256=00", "{}");

        result.Status.Should().Be(GitHubWebhookReceiveStatus.InvalidSignature);
        (await _db.GitHubWebhookInbox.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Processor_Push_UpsertsCommitAndLinksTaskKey()
    {
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        _db.Users.Add(new User { Id = userId, FullName = "Owner", Email = "owner@test.local", PasswordHash = "x" });
        _db.Organizations.Add(new Organization { Id = organizationId, Name = "Org", OwnerId = userId });
        _db.Projects.Add(new Project { Id = projectId, Name = "Qaly", Code = "QALY", OwnerId = userId, OrganizationId = organizationId });
        _db.TaskItems.Add(new TaskItem { Id = taskId, ProjectId = projectId, ReporterId = userId, Number = 284, Title = "Payment" });
        _db.GitHubInstallations.Add(new GitHubInstallation
        {
            Id = installationId, OrganizationId = organizationId, InstallationId = 12,
            AccountId = 13, AccountLogin = "org", InstalledByUserId = userId
        });
        _db.GitHubRepositoryConnections.Add(new GitHubRepositoryConnection
        {
            Id = connectionId, OrganizationId = organizationId, ProjectId = projectId,
            GitHubInstallationId = installationId, RepositoryExternalId = 34,
            Owner = "org", Name = "repo", FullName = "org/repo"
        });
        await _db.SaveChangesAsync();
        var payload = """
        {"ref":"refs/heads/feature/QALY-284-payment","repository":{"id":34},"commits":[
          {"id":"abc123","message":"Implement QALY-284","timestamp":"2026-08-04T01:00:00Z","url":"https://github.test/commit/abc123","author":{"username":"dev","email":"dev@example.test"}}
        ]}
        """;
        var inbox = new GitHubWebhookInbox { DeliveryId = "delivery-3", EventName = "push", RepositoryExternalId = 34, Payload = payload };
        _db.GitHubWebhookInbox.Add(inbox);
        await _db.SaveChangesAsync();

        await new GitHubWebhookProcessor(_db).ProcessAsync(inbox);
        await _db.SaveChangesAsync();

        var commit = await _db.GitHubCommits.SingleAsync();
        commit.Sha.Should().Be("abc123");
        commit.AuthorEmailHash.Should().NotBe("dev@example.test");
        var link = await _db.TaskDevelopmentLinks.SingleAsync();
        link.TaskId.Should().Be(taskId);
        link.ExternalEntityId.Should().Be("34:abc123");
    }

    private GitHubWebhookReceiver Receiver() => new(_db, Options.Create(new GitHubIntegrationOptions
    {
        Enabled = true,
        WebhookSecret = Secret
    }));

    private static string Sign(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        return $"sha256={Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant()}";
    }

    public void Dispose() => _db.Dispose();
}
