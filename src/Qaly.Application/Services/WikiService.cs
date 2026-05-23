using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Wiki;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class WikiService : IWikiService
{
    private readonly IRepository<WikiPage> _wikiRepo;
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<User> _userRepo;
    private readonly ITaskAccessPolicy _taskAccessPolicy;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;

    public WikiService(
        IRepository<WikiPage> wikiRepo,
        IRepository<Project> projectRepo,
        IRepository<User> userRepo,
        ITaskAccessPolicy taskAccessPolicy,
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService)
    {
        _wikiRepo = wikiRepo;
        _projectRepo = projectRepo;
        _userRepo = userRepo;
        _taskAccessPolicy = taskAccessPolicy;
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
    }

    public async Task<Result<IEnumerable<WikiPageDto>>> GetByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null) return Result.NotFound<IEnumerable<WikiPageDto>>();

        if (!await _taskAccessPolicy.CanReadWikiAsync(projectId, project.OwnerId, ct))
        {
            return Result.Forbidden<IEnumerable<WikiPageDto>>("Bạn không có quyền xem wiki của dự án này.");
        }

        var query = _wikiRepo.GetQueryable()
            .Where(p => p.ProjectId == projectId);

        if (!await _taskAccessPolicy.CanReadInternalWikiAsync(projectId, project.OwnerId, ct))
        {
            // Users who cannot read internal wiki should only see public and customer_safe pages
            query = query.Where(p => p.Visibility == "public" || p.Visibility == "customer_safe");
        }

        var pages = await query
            .Include(p => p.Author)
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new WikiPageDto(
                p.Id,
                p.Title,
                p.Content,
                p.Visibility,
                p.Author.FullName,
                p.UpdatedAt
            ))
            .ToListAsync(ct);

        return Result.Success<IEnumerable<WikiPageDto>>(pages);
    }

    public async Task<Result<WikiPageDto>> CreateAsync(Guid projectId, CreateWikiPageDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return Result.Failure<WikiPageDto>("Wiki title is required.", 400);
        }

        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null) return Result.NotFound<WikiPageDto>();

        if (!await _taskAccessPolicy.CanWriteWikiAsync(projectId, project.OwnerId, ct))
        {
            return Result.Forbidden<WikiPageDto>("Bạn không có quyền tạo trang wiki trong dự án này.");
        }

        var currentUserId = _taskAccessPolicy.CurrentUserId;
        if (currentUserId == null) return Result.Forbidden<WikiPageDto>();

        var page = new WikiPage
        {
            ProjectId = projectId,
            Title = dto.Title.Trim(),
            Content = dto.Content ?? string.Empty,
            IsPublic = string.Equals(dto.Visibility, "public", StringComparison.OrdinalIgnoreCase),
            Visibility = string.IsNullOrWhiteSpace(dto.Visibility) ? "internal" : dto.Visibility.Trim(),
            AuthorId = currentUserId.Value,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _wikiRepo.AddAsync(page, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Create", nameof(WikiPage), page.Id.ToString(), new { projectId, page.Title }, ct);

        var author = await _userRepo.GetByIdAsync(currentUserId.Value, ct);

        return Result.Success(new WikiPageDto(
            page.Id,
            page.Title,
            page.Content,
            page.Visibility,
            author?.FullName ?? "Unknown",
            page.UpdatedAt
        ));
    }

    public async Task<Result<WikiPageDto>> UpdateAsync(Guid projectId, Guid id, UpdateWikiPageDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return Result.Failure<WikiPageDto>("Wiki title is required.", 400);
        }

        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null) return Result.NotFound<WikiPageDto>();

        if (!await _taskAccessPolicy.CanWriteWikiAsync(projectId, project.OwnerId, ct))
        {
            return Result.Forbidden<WikiPageDto>("Bạn không có quyền chỉnh sửa wiki của dự án này.");
        }

        var page = await _wikiRepo.GetByIdAsync(id, ct);
        if (page == null || page.ProjectId != projectId) return Result.NotFound<WikiPageDto>();

        page.Title = dto.Title.Trim();
        page.Content = dto.Content ?? string.Empty;
        page.Visibility = string.IsNullOrWhiteSpace(dto.Visibility) ? "internal" : dto.Visibility.Trim();
        page.IsPublic = string.Equals(page.Visibility, "public", StringComparison.OrdinalIgnoreCase);
        page.UpdatedAt = DateTimeOffset.UtcNow;

        await _wikiRepo.UpdateAsync(page, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Update", nameof(WikiPage), page.Id.ToString(), new { projectId, page.Title }, ct);

        var author = await _userRepo.GetByIdAsync(page.AuthorId, ct);

        return Result.Success(new WikiPageDto(
            page.Id,
            page.Title,
            page.Content,
            page.Visibility,
            author?.FullName ?? "Unknown",
            page.UpdatedAt
        ));
    }

    public async Task<Result> DeleteAsync(Guid projectId, Guid id, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null) return Result.NotFound();

        if (!await _taskAccessPolicy.CanManageProjectAsync(projectId, project.OwnerId, ct))
        {
            return Result.Forbidden("Bạn không có quyền xóa trang wiki của dự án này.");
        }

        var page = await _wikiRepo.GetByIdAsync(id, ct);
        if (page == null || page.ProjectId != projectId) return Result.NotFound();

        await _wikiRepo.DeleteAsync(page, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Delete", nameof(WikiPage), id.ToString(), new { projectId, page.Title }, ct);

        return Result.Success();
    }
}
