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
    Task<Result<GitHubIntegrationStatusDto>> GetStatusAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<string>> GetInstallUrlAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<GitHubInstallation>> CompleteAsync(Guid projectId, long installationId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<GitHubRepositoryInfo>>> GetRepositoriesAsync(Guid projectId, Guid installationId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<GitHubInstallation>>> ListAsync(Guid projectId, CancellationToken ct = default);
}

public sealed record GitHubIntegrationStatusDto(
    string State,
    bool Enabled,
    bool Configured,
    bool HasInstallation,
    bool LiveVerified,
    string Message);

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

    public async Task<Result<GitHubIntegrationStatusDto>> GetStatusAsync(Guid projectId, CancellationToken ct = default)
    {
        var auth = await _guard.AuthorizeProjectAsync(projectId, false, ct);
        if (!auth.IsSuccess) return Result.Failure<GitHubIntegrationStatusDto>(auth.Error!, auth.StatusCode);

        var configuration = _options.Value;
        if (!configuration.Enabled)
        {
            var hasCachedSnapshot = await _db.GitHubRepositoryConnections.AsNoTracking()
                .AnyAsync(item => item.OrganizationId == auth.Data!.OrganizationId &&
                                  item.ProjectId == projectId &&
                                  item.IsActive &&
                                  item.LastSyncedAt != null, ct);
            if (hasCachedSnapshot)
            {
                return Result.Success(Status(
                    "cached", enabled: false, configured: false, hasInstallation: true, liveVerified: false,
                    "GitHub live adapter đang tắt; Qaly đang hiển thị snapshot đã đồng bộ gần nhất."));
            }

            return Result.Success(Status(
                "disabled", enabled: false, configured: false, hasInstallation: false, liveVerified: false,
                "Tích hợp GitHub đang bị tắt trên máy chủ Qaly."));
        }

        if (!IsConfigured(configuration))
        {
            return Result.Success(Status(
                "unconfigured", enabled: true, configured: false, hasInstallation: false, liveVerified: false,
                "GitHub App chưa được cấu hình đầy đủ App ID, App Slug và Private Key."));
        }

        var installation = await _db.GitHubInstallations.AsNoTracking()
            .Where(item => item.OrganizationId == auth.Data!.OrganizationId && item.Status == "Active")
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (installation == null)
        {
            return Result.Success(Status(
                "not_connected", enabled: true, configured: true, hasInstallation: false, liveVerified: false,
                "GitHub App đã sẵn sàng nhưng organization chưa cài installation."));
        }

        try
        {
            await _client.GetInstallationAsync(installation.InstallationId, ct);
            return Result.Success(Status(
                "connected", enabled: true, configured: true, hasInstallation: true, liveVerified: true,
                "GitHub API và installation đã được xác minh trực tiếp."));
        }
        catch (InvalidOperationException)
        {
            return Result.Success(Status(
                "unconfigured", enabled: true, configured: false, hasInstallation: true, liveVerified: false,
                "Cấu hình GitHub App không hợp lệ hoặc private key không thể sử dụng."));
        }
        catch (HttpRequestException ex)
        {
            return Result.Success(MapApiStatus(ex, hasInstallation: true));
        }
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

        var configuration = _options.Value;
        if (!configuration.Enabled)
            return Result.Failure<GitHubInstallation>(
                "Tích hợp GitHub đang bị tắt trên máy chủ Qaly.", 503, "github.disabled");
        if (!IsConfigured(configuration))
            return Result.Failure<GitHubInstallation>(
                "GitHub App chưa được cấu hình đầy đủ.", 503, "github.unconfigured");

        GitHubInstallationInfo info;
        try
        {
            info = await _client.GetInstallationAsync(installationId, ct);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<GitHubInstallation>(
                "Cấu hình GitHub App không hợp lệ hoặc private key không thể sử dụng.", 503, "github.unconfigured");
        }
        catch (HttpRequestException ex)
        {
            return MapApiFailure<GitHubInstallation>(ex);
        }
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
        try
        {
            return Result.Success(await _client.GetRepositoriesAsync(installation.InstallationId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<IReadOnlyList<GitHubRepositoryInfo>>(
                $"Cấu hình GitHub App không hợp lệ: {ex.Message}", 503);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<IReadOnlyList<GitHubRepositoryInfo>>(
                $"Không thể lấy danh sách repository từ GitHub: {ex.Message}", 502);
        }
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

    private static bool IsConfigured(GitHubIntegrationOptions configuration)
        => configuration.AppId > 0 &&
           !string.IsNullOrWhiteSpace(configuration.AppSlug) &&
           (!string.IsNullOrWhiteSpace(configuration.PrivateKey) ||
            !string.IsNullOrWhiteSpace(configuration.PrivateKeyPath));

    private static GitHubIntegrationStatusDto Status(
        string state,
        bool enabled,
        bool configured,
        bool hasInstallation,
        bool liveVerified,
        string message)
        => new(state, enabled, configured, hasInstallation, liveVerified, message);

    private static GitHubIntegrationStatusDto MapApiStatus(HttpRequestException ex, bool hasInstallation)
    {
        if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return Status("invalid_credentials", true, true, hasInstallation, false,
                "GitHub từ chối credential của Qaly. Hãy kiểm tra App ID và Private Key.");
        if (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests || IsRateLimitError(ex))
            return Status("rate_limited", true, true, hasInstallation, false,
                "GitHub API đang giới hạn tần suất. Qaly chưa thể xác minh kết nối lúc này.");
        if (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            return Status("insufficient_permissions", true, true, hasInstallation, false,
                "GitHub App hoặc installation chưa được cấp đủ quyền đọc metadata.");
        return Status("unavailable", true, true, hasInstallation, false,
            "Không thể xác minh GitHub API do lỗi mạng hoặc dịch vụ GitHub.");
    }

    private static Result<T> MapApiFailure<T>(HttpRequestException ex)
    {
        var status = MapApiStatus(ex, hasInstallation: true);
        var statusCode = status.State == "rate_limited" ? 429 : 502;
        return Result.Failure<T>(status.Message, statusCode, $"github.{status.State}");
    }

    private static bool IsRateLimitError(HttpRequestException ex)
        => ex.StatusCode == System.Net.HttpStatusCode.Forbidden &&
           ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase);
}
