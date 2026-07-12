using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.GitHub;
using Qaly.Domain.Entities.GitHub;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services.GitHub;

public class GitHubRepositoryConnectionService : IGitHubRepositoryConnectionService
{
    private readonly IRepository<GitHubRepositoryConnection> _connectionRepo;
    private readonly IRepository<GitHubInstallation> _installationRepo;
    private readonly IGitHubAccessGuard _accessGuard;
    private readonly IUnitOfWork _unitOfWork;

    public GitHubRepositoryConnectionService(
        IRepository<GitHubRepositoryConnection> connectionRepo,
        IRepository<GitHubInstallation> installationRepo,
        IGitHubAccessGuard accessGuard,
        IUnitOfWork unitOfWork)
    {
        _connectionRepo = connectionRepo;
        _installationRepo = installationRepo;
        _accessGuard = accessGuard;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<GitHubRepositoryConnectionDto>>> GetByProjectAsync(
        Guid projectId, CancellationToken ct = default)
    {
        var auth = await _accessGuard.AuthorizeProjectAsync(projectId, requireManage: false, ct);
        if (!auth.IsSuccess)
        {
            return Result.Failure<IReadOnlyList<GitHubRepositoryConnectionDto>>(auth.Error!, auth.StatusCode);
        }

        var context = auth.Data!;

        var entities = await _connectionRepo.GetQueryable()
            .Where(c => c.ProjectId == projectId && c.OrganizationId == context.OrganizationId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        IReadOnlyList<GitHubRepositoryConnectionDto> items = entities.Select(Map).ToList();
        return Result.Success(items);
    }

    public async Task<Result<GitHubRepositoryConnectionDto>> CreateAsync(
        Guid projectId, CreateGitHubRepositoryConnectionDto dto, CancellationToken ct = default)
    {
        var auth = await _accessGuard.AuthorizeProjectAsync(projectId, requireManage: true, ct);
        if (!auth.IsSuccess)
        {
            return Result.Failure<GitHubRepositoryConnectionDto>(auth.Error!, auth.StatusCode);
        }

        var context = auth.Data!;

        if (dto.RepositoryExternalId <= 0
            || string.IsNullOrWhiteSpace(dto.Owner)
            || string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result.Failure<GitHubRepositoryConnectionDto>(
                "Repository ID, owner và name là bắt buộc.", 400);
        }

        // Installation phải thuộc đúng tenant của project (chặn dùng chéo tenant).
        var installation = await _installationRepo.GetQueryable()
            .FirstOrDefaultAsync(i => i.Id == dto.GitHubInstallationId
                && i.OrganizationId == context.OrganizationId, ct);
        if (installation is null)
        {
            return Result.Failure<GitHubRepositoryConnectionDto>(
                "Installation GitHub không tồn tại hoặc không thuộc organization của dự án.", 400);
        }
        if (!string.Equals(installation.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<GitHubRepositoryConnectionDto>(
                "Installation GitHub đang không ở trạng thái Active.", 400);
        }

        var existingConnection = await _connectionRepo.GetQueryable()
            .FirstOrDefaultAsync(c => c.ProjectId == projectId
                && c.OrganizationId == context.OrganizationId
                && c.RepositoryExternalId == dto.RepositoryExternalId, ct);
        if (existingConnection?.IsActive == true)
        {
            return Result.Failure<GitHubRepositoryConnectionDto>(
                "Repository này đã được kết nối với dự án.", 409);
        }

        // M1 sẽ lấy và xác minh các trường metadata này trực tiếp từ GitHub API.
        // M0.5 giữ contract hiện tại nhưng không cho phép dữ liệu rỗng/ID không hợp lệ.
        if (existingConnection is not null)
        {
            existingConnection.GitHubInstallationId = dto.GitHubInstallationId;
            existingConnection.Owner = dto.Owner.Trim();
            existingConnection.Name = dto.Name.Trim();
            existingConnection.FullName = string.IsNullOrWhiteSpace(dto.FullName)
                ? $"{existingConnection.Owner}/{existingConnection.Name}"
                : dto.FullName.Trim();
            existingConnection.DefaultBranch = string.IsNullOrWhiteSpace(dto.DefaultBranch)
                ? "main"
                : dto.DefaultBranch.Trim();
            existingConnection.IsPrivate = dto.IsPrivate;
            existingConnection.IsActive = true;
            existingConnection.IsDeleted = false;
            existingConnection.DeletedAt = null;
            await _connectionRepo.UpdateAsync(existingConnection, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Created(Map(existingConnection));
        }

        var connection = new GitHubRepositoryConnection
        {
            OrganizationId = context.OrganizationId,
            ProjectId = projectId,
            GitHubInstallationId = dto.GitHubInstallationId,
            RepositoryExternalId = dto.RepositoryExternalId,
            Owner = dto.Owner.Trim(),
            Name = dto.Name.Trim(),
            FullName = string.IsNullOrWhiteSpace(dto.FullName) ? $"{dto.Owner.Trim()}/{dto.Name.Trim()}" : dto.FullName.Trim(),
            DefaultBranch = string.IsNullOrWhiteSpace(dto.DefaultBranch) ? "main" : dto.DefaultBranch.Trim(),
            IsPrivate = dto.IsPrivate,
            IsActive = true
        };

        await _connectionRepo.AddAsync(connection, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Created(Map(connection));
    }

    public async Task<Result> RemoveAsync(Guid projectId, Guid connectionId, CancellationToken ct = default)
    {
        var auth = await _accessGuard.AuthorizeProjectAsync(projectId, requireManage: true, ct);
        if (!auth.IsSuccess)
        {
            return Result.Failure(auth.Error!, auth.StatusCode);
        }

        var context = auth.Data!;
        var connection = await _connectionRepo.GetQueryable()
            .FirstOrDefaultAsync(c => c.Id == connectionId
                && c.ProjectId == projectId
                && c.OrganizationId == context.OrganizationId, ct);

        if (connection is null)
        {
            return Result.NotFound("Không tìm thấy kết nối repository.");
        }

        // Chỉ ngừng đồng bộ để giữ lịch sử commit/PR/release. Create lại cùng repo
        // sẽ kích hoạt lại mapping thay vì tạo bản ghi mới.
        connection.IsActive = false;
        await _connectionRepo.UpdateAsync(connection, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static GitHubRepositoryConnectionDto Map(GitHubRepositoryConnection c)
        => new(
            c.Id,
            c.ProjectId,
            c.OrganizationId,
            c.GitHubInstallationId,
            c.RepositoryExternalId,
            c.Owner,
            c.Name,
            c.FullName,
            c.DefaultBranch,
            c.IsPrivate,
            c.IsActive,
            c.LastSyncedAt,
            c.CreatedAt);
}
