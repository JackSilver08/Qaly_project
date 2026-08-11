using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Qaly.Application.Common.Models;
using Qaly.Application.Services.GitHub;
using Qaly.Domain.Entities;
using Qaly.Domain.Entities.GitHub;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Integrations.GitHub;

namespace Qaly.UnitTests;

public sealed class GitHubInstallationStatusTests : IDisposable
{
    private readonly QalyDbContext _db;
    private readonly Mock<IGitHubAccessGuard> _guard = new();
    private readonly Mock<IGitHubAppClient> _client = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly GitHubIntegrationOptions _options = new();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public GitHubInstallationStatusTests()
    {
        _db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _guard.Setup(item => item.AuthorizeProjectAsync(
                _projectId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new GitHubProjectContext(_projectId, _organizationId)));
        _currentUser.SetupGet(item => item.UserId).Returns(_userId);
    }

    [Fact]
    public async Task GetStatusAsync_WhenDisabled_DoesNotPretendConnectionExists()
    {
        _options.Enabled = false;

        var result = await CreateService().GetStatusAsync(_projectId);

        result.Data!.State.Should().Be("disabled");
        result.Data.LiveVerified.Should().BeFalse();
        _client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetStatusAsync_WhenLiveAdapterIsDisabled_ExposesPersistedSnapshotAsCached()
    {
        _options.Enabled = false;
        await AddActiveInstallationAsync();
        var installationId = await _db.GitHubInstallations.Select(item => item.Id).SingleAsync();
        _db.GitHubRepositoryConnections.Add(new GitHubRepositoryConnection
        {
            OrganizationId = _organizationId,
            ProjectId = _projectId,
            GitHubInstallationId = installationId,
            RepositoryExternalId = 42,
            Owner = "qaly",
            Name = "cached-demo",
            FullName = "qaly/cached-demo",
            LastSyncedAt = DateTimeOffset.UtcNow,
            IsActive = true
        });
        await _db.SaveChangesAsync();

        var result = await CreateService().GetStatusAsync(_projectId);

        result.Data!.State.Should().Be("cached");
        result.Data.HasInstallation.Should().BeTrue();
        result.Data.LiveVerified.Should().BeFalse();
        _client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetStatusAsync_WhenConfigurationIsIncomplete_ReturnsUnconfigured()
    {
        _options.Enabled = true;

        var result = await CreateService().GetStatusAsync(_projectId);

        result.Data!.State.Should().Be("unconfigured");
        result.Data.Configured.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatusAsync_WhenNoInstallationExists_ReturnsNotConnected()
    {
        ConfigureApp();

        var result = await CreateService().GetStatusAsync(_projectId);

        result.Data!.State.Should().Be("not_connected");
        result.Data.HasInstallation.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatusAsync_OnlyReturnsConnectedAfterLiveGitHubVerification()
    {
        ConfigureApp();
        await AddActiveInstallationAsync();
        _client.Setup(item => item.GetInstallationAsync(1234, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitHubInstallationInfo(1234, 99, "qaly", "Organization"));

        var result = await CreateService().GetStatusAsync(_projectId);

        result.Data!.State.Should().Be("connected");
        result.Data.LiveVerified.Should().BeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "invalid_credentials")]
    [InlineData(HttpStatusCode.Forbidden, "insufficient_permissions")]
    [InlineData(HttpStatusCode.TooManyRequests, "rate_limited")]
    [InlineData(HttpStatusCode.BadGateway, "unavailable")]
    public async Task GetStatusAsync_MapsProviderFailuresToHonestBusinessStates(
        HttpStatusCode statusCode,
        string expectedState)
    {
        ConfigureApp();
        await AddActiveInstallationAsync();
        _client.Setup(item => item.GetInstallationAsync(1234, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("GitHub failure", null, statusCode));

        var result = await CreateService().GetStatusAsync(_projectId);

        result.IsSuccess.Should().BeTrue("status discovery itself succeeded and must expose the provider state");
        result.Data!.State.Should().Be(expectedState);
        result.Data.LiveVerified.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatusAsync_RecognizesRateLimitReportedAsForbidden()
    {
        ConfigureApp();
        await AddActiveInstallationAsync();
        _client.Setup(item => item.GetInstallationAsync(1234, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("GitHub API rate limit exceeded", null, HttpStatusCode.Forbidden));

        var result = await CreateService().GetStatusAsync(_projectId);

        result.Data!.State.Should().Be("rate_limited");
    }

    [Fact]
    public async Task GetStatusAsync_WhenProviderIsUnavailable_UsesPersistedSnapshot()
    {
        ConfigureApp();
        await AddActiveInstallationAsync();
        var installationId = await _db.GitHubInstallations.Select(item => item.Id).SingleAsync();
        _db.GitHubRepositoryConnections.Add(new GitHubRepositoryConnection
        {
            OrganizationId = _organizationId,
            ProjectId = _projectId,
            GitHubInstallationId = installationId,
            RepositoryExternalId = 42,
            Owner = "qaly",
            Name = "cached-demo",
            FullName = "qaly/cached-demo",
            LastSyncedAt = DateTimeOffset.UtcNow,
            IsActive = true
        });
        await _db.SaveChangesAsync();
        _client.Setup(item => item.GetInstallationAsync(1234, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("GitHub unavailable", null, HttpStatusCode.BadGateway));

        var result = await CreateService().GetStatusAsync(_projectId);

        result.Data!.State.Should().Be("cached");
        result.Data.LiveVerified.Should().BeFalse();
        result.Data.Message.Should().Contain("snapshot");
    }

    private GitHubInstallationService CreateService()
        => new(_db, _guard.Object, _client.Object, _currentUser.Object, Options.Create(_options));

    private void ConfigureApp()
    {
        _options.Enabled = true;
        _options.AppId = 42;
        _options.AppSlug = "qaly-test";
        _options.PrivateKey = "configured-for-mocked-client";
    }

    private async Task AddActiveInstallationAsync()
    {
        _db.Users.Add(new User { Id = _userId, FullName = "Owner", Email = "github-owner@qaly.dev", IsActive = true });
        _db.Organizations.Add(new Organization
        {
            Id = _organizationId,
            Name = "GitHub organization",
            Code = "github-org",
            OwnerId = _userId
        });
        _db.Projects.Add(new Project
        {
            Id = _projectId,
            Name = "GitHub project",
            Code = "GH-TEST",
            OwnerId = _userId,
            OrganizationId = _organizationId
        });
        _db.GitHubInstallations.Add(new GitHubInstallation
        {
            OrganizationId = _organizationId,
            InstallationId = 1234,
            AccountId = 99,
            AccountLogin = "qaly",
            AccountType = "Organization",
            InstalledByUserId = _userId,
            Status = "Active"
        });
        await _db.SaveChangesAsync();
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
