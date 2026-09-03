using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Qaly.Application.Services;
using Qaly.Application.Services.GitHub;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Entities.GitHub;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

public sealed class TaskDevelopmentServiceTests : IDisposable
{
    private readonly QalyDbContext _db;
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly TaskDevelopmentService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _taskId = Guid.NewGuid();
    private readonly Guid _connectionId = Guid.NewGuid();

    public TaskDevelopmentServiceTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new QalyDbContext(options);
        Seed();

        _currentUser.Setup(x => x.UserId).Returns(_userId);
        _currentUser.Setup(x => x.Role).Returns("Owner");

        var guard = new GitHubAccessGuard(
            new GenericRepository<Project>(_db),
            new GenericRepository<ProjectMember>(_db),
            new GenericRepository<OrganizationMember>(_db),
            _currentUser.Object,
            new TaskAccessPolicy(
                _currentUser.Object,
                new GenericRepository<Project>(_db),
                new GenericRepository<ProjectMember>(_db),
                new GenericRepository<OrganizationMember>(_db),
                new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(_db))));

        _service = new TaskDevelopmentService(
            new GenericRepository<TaskItem>(_db),
            new GenericRepository<TaskDevelopmentLink>(_db),
            new GenericRepository<GitHubRepositoryConnection>(_db),
            new GenericRepository<GitHubCommit>(_db),
            new GenericRepository<GitHubPullRequest>(_db),
            new GenericRepository<GitHubRelease>(_db),
            new GenericRepository<GitHubWorkflowRun>(_db),
            guard);
    }

    private void Seed()
    {
        var user = new User { Id = _userId, FullName = "Owner", Email = "owner@test.local", PasswordHash = "x" };
        var organization = new Organization { Id = _organizationId, Name = "Org", OwnerId = _userId };
        var project = new Project { Id = _projectId, Name = "Qaly", Code = "QALY", OwnerId = _userId, OrganizationId = _organizationId, Organization = organization };
        var task = new TaskItem { Id = _taskId, ProjectId = _projectId, Project = project, ReporterId = _userId, Number = 284, Title = "Payment flow" };
        var connection = new GitHubRepositoryConnection
        {
            Id = _connectionId,
            OrganizationId = _organizationId,
            ProjectId = _projectId,
            Project = project,
            RepositoryExternalId = 77,
            Owner = "org",
            Name = "repo",
            FullName = "org/repo",
            DefaultBranch = "main",
            IsActive = true
        };

        _db.Users.Add(user);
        _db.Organizations.Add(organization);
        _db.Projects.Add(project);
        _db.TaskItems.Add(task);
        _db.GitHubRepositoryConnections.Add(connection);
        _db.GitHubPullRequests.AddRange(
            new GitHubPullRequest
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                RepositoryConnectionId = _connectionId,
                RepositoryConnection = connection,
                Number = 12,
                Title = "Implement QALY-284",
                State = "Open",
                HeadBranch = "feature/QALY-284-payment",
                BaseBranch = "main",
                AuthorLogin = "dev",
                IsDraft = false,
                OpenedAt = DateTimeOffset.UtcNow.AddHours(-2),
                Url = "https://github.test/pr/12"
            },
            new GitHubPullRequest
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                RepositoryConnectionId = _connectionId,
                RepositoryConnection = connection,
                Number = 13,
                Title = "Docs update",
                State = "Open",
                HeadBranch = "feature/docs",
                BaseBranch = "main",
                AuthorLogin = "writer",
                IsDraft = false,
                OpenedAt = DateTimeOffset.UtcNow.AddHours(-1),
                Url = "https://github.test/pr/13"
            });
        _db.GitHubWorkflowRuns.AddRange(
            new GitHubWorkflowRun
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                RepositoryConnectionId = _connectionId,
                RepositoryConnection = connection,
                RunExternalId = 9001,
                WorkflowName = "ci",
                DisplayTitle = "Build QALY-284",
                Branch = "feature/QALY-284-payment",
                Status = "completed",
                Conclusion = "success",
                StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
                Url = "https://github.test/run/9001"
            },
            new GitHubWorkflowRun
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                RepositoryConnectionId = _connectionId,
                RepositoryConnection = connection,
                RunExternalId = 9002,
                WorkflowName = "ci",
                DisplayTitle = "Docs build",
                Branch = "feature/docs",
                Status = "completed",
                Conclusion = "success",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
                Url = "https://github.test/run/9002"
            });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetAsync_RecognizesTaskKeyInBranchAndTitle()
    {
        var result = await _service.GetAsync(_taskId);

        result.IsSuccess.Should().BeTrue();
        var data = result.Data!;
        data.TaskKey.Should().Be("QALY-284");
        data.PullRequests.Should().HaveCount(1);
        data.PullRequests[0].Title.Should().Be("Implement QALY-284");
        data.PullRequests[0].Repository.Should().Be("org/repo");
        data.WorkflowRuns.Should().HaveCount(1);
        data.WorkflowRuns[0].WorkflowName.Should().Be("ci");
        data.Links.Should().BeEmpty();
    }
}
