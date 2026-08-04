using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.Services.GitHub;
using Qaly.Domain.Entities.GitHub;
using Qaly.Infrastructure.Data;
using Qaly.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace Qaly.Infrastructure.Integrations.GitHub;

public interface IGitHubInstallationService
{
    Task<Result<string>> GetInstallUrlAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<GitHubInstallation>> CompleteAsync(Guid projectId, long installationId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<GitHubRepositoryInfo>>> GetRepositoriesAsync(Guid projectId, Guid installationId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<GitHubInstallation>>> ListAsync(Guid projectId, CancellationToken ct = default);
}

public sealed class GitHubInstallationService : IGitHubInstallationService
{
    private readonly QalyDbContext _db;
    private readonly IGitHubAccessGuard _guard;
    private readonly IGitHubAppClient _client;
    private readonly ICurrentUserService _currentUser;
    private readonly IOptions<GitHubIntegrationOptions> _options;

    public GitHubInstallationService(QalyDbContext db, IGitHubAccessGuard guard, IGitHubAppClient client,
        ICurrentUserService currentUser, IOptions<GitHubIntegrationOptions> options)
    {
        _db = db;
        _guard = guard;
        _client = client;
        _currentUser = currentUser;
        _options = options;
    }

    public async Task<Result<string>> GetInstallUrlAsync(Guid projectId, CancellationToken ct = default)
    {
        var auth = await _guard.AuthorizeProjectAsync(projectId, true, ct);
        if (!auth.IsSuccess) return Result.Failure<string>(auth.Error!, auth.StatusCode);
        var configuration = _options.Value;
        if (!configuration.Enabled)
            return Result.Failure<string>("Tích hợp GitHub chưa được bật trên máy chủ Qaly.", 503);
        if (configuration.AppId <= 0 || string.IsNullOrWhiteSpace(configuration.AppSlug) ||
            (string.IsNullOrWhiteSpace(configuration.PrivateKey) &&
             string.IsNullOrWhiteSpace(configuration.PrivateKeyPath)))
            return Result.Failure<string>(
                "GitHub App chưa được cấu hình đầy đủ. Cần App ID, App Slug và Private Key.", 503);
        return Result.Success(_client.GetInstallationUrl(projectId.ToString("N")));
    }

    public async Task<Result<GitHubInstallation>> CompleteAsync(Guid projectId, long installationId, CancellationToken ct = default)
    {
        if (installationId <= 0) return Result.Failure<GitHubInstallation>("Installation ID không hợp lệ.", 400);
        var auth = await _guard.AuthorizeProjectAsync(projectId, true, ct);
        if (!auth.IsSuccess) return Result.Failure<GitHubInstallation>(auth.Error!, auth.StatusCode);

        var info = await _client.GetInstallationAsync(installationId, ct);
        var entity = await _db.GitHubInstallations.FirstOrDefaultAsync(x => x.InstallationId == installationId, ct);
        if (entity is not null && entity.OrganizationId != auth.Data!.OrganizationId)
            return Result.Failure<GitHubInstallation>("GitHub installation đã thuộc organization khác.", 409);

        if (entity is null)
        {
            entity = new GitHubInstallation
            {
                OrganizationId = auth.Data!.OrganizationId,
                InstallationId = info.InstallationId,
                InstalledByUserId = _currentUser.UserId!.Value
            };
            _db.GitHubInstallations.Add(entity);
        }
        entity.AccountId = info.AccountId;
        entity.AccountLogin = info.AccountLogin;
        entity.AccountType = info.AccountType;
        entity.Status = "Active";
        await _db.SaveChangesAsync(ct);
        return Result.Success(entity);
    }

    public async Task<Result<IReadOnlyList<GitHubRepositoryInfo>>> GetRepositoriesAsync(
        Guid projectId, Guid installationId, CancellationToken ct = default)
    {
        var auth = await _guard.AuthorizeProjectAsync(projectId, true, ct);
        if (!auth.IsSuccess) return Result.Failure<IReadOnlyList<GitHubRepositoryInfo>>(auth.Error!, auth.StatusCode);
        var installation = await _db.GitHubInstallations.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == installationId && x.OrganizationId == auth.Data!.OrganizationId && x.Status == "Active", ct);
        if (installation is null)
            return Result.NotFound<IReadOnlyList<GitHubRepositoryInfo>>("Không tìm thấy GitHub installation.");
        return Result.Success(await _client.GetRepositoriesAsync(installation.InstallationId, ct));
    }

    public async Task<Result<IReadOnlyList<GitHubInstallation>>> ListAsync(Guid projectId, CancellationToken ct = default)
    {
        var auth = await _guard.AuthorizeProjectAsync(projectId, false, ct);
        if (!auth.IsSuccess) return Result.Failure<IReadOnlyList<GitHubInstallation>>(auth.Error!, auth.StatusCode);
        IReadOnlyList<GitHubInstallation> items = await _db.GitHubInstallations.AsNoTracking()
            .Where(x => x.OrganizationId == auth.Data!.OrganizationId && x.Status != "Removed")
            .OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return Result.Success(items);
    }
}
