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
    private static readonly HashSet<string> SupportedVisibilities = new(StringComparer.Ordinal)
    {
        "public",
        "customer_safe",
        "internal",
        "private"
    };

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
            .Where(p => p.ProjectId == projectId && SupportedVisibilities.Contains(p.Visibility));

        var canReadInternal = await _taskAccessPolicy.CanReadInternalWikiAsync(projectId, project.OwnerId, ct);
        if (!canReadInternal)
        {
            // Users who cannot read internal wiki should only see public and customer_safe pages
            query = query.Where(p => p.Visibility == "public" || p.Visibility == "customer_safe");
        }
        else if (!await _taskAccessPolicy.CanManageProjectAsync(projectId, project.OwnerId, ct))
        {
            // A private page is visible only to its author and project managers. Internal access alone
            // must not turn a private draft into a project-wide document.
            var currentUserId = _taskAccessPolicy.CurrentUserId!.Value;
            query = query.Where(p => p.Visibility != "private" || p.AuthorId == currentUserId);
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

        if (dto.Title.Trim().Length > 200)
        {
            return Result.Failure<WikiPageDto>("Tiêu đề Wiki không được vượt quá 200 ký tự.", 400);
        }

        var visibility = NormalizeVisibility(dto.Visibility);
        if (visibility == null)
        {
            return Result.Failure<WikiPageDto>("Phạm vi Wiki phải là public, customer_safe, internal hoặc private.", 400);
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
            IsPublic = visibility == "public",
            Visibility = visibility,
            AuthorId = currentUserId.Value,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _wikiRepo.AddAsync(page, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "Create",
            nameof(WikiPage),
            page.Id.ToString(),
            new { projectId, page.Title },
            ct);

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

        if (dto.Title.Trim().Length > 200)
        {
            return Result.Failure<WikiPageDto>("Tiêu đề Wiki không được vượt quá 200 ký tự.", 400);
        }

        var visibility = NormalizeVisibility(dto.Visibility);
        if (visibility == null)
        {
            return Result.Failure<WikiPageDto>("Phạm vi Wiki phải là public, customer_safe, internal hoặc private.", 400);
        }

        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null) return Result.NotFound<WikiPageDto>();

        if (!await _taskAccessPolicy.CanWriteWikiAsync(projectId, project.OwnerId, ct))
        {
            return Result.Forbidden<WikiPageDto>("Bạn không có quyền chỉnh sửa wiki của dự án này.");
        }

        var page = await _wikiRepo.GetByIdAsync(id, ct);
        if (page == null || page.ProjectId != projectId) return Result.NotFound<WikiPageDto>();

        var currentUserId = _taskAccessPolicy.CurrentUserId;
        if (page.Visibility == "private" &&
            page.AuthorId != currentUserId &&
            !await _taskAccessPolicy.CanManageProjectAsync(projectId, project.OwnerId, ct))
        {
            return Result.Forbidden<WikiPageDto>("Chỉ tác giả hoặc người quản lý dự án được sửa trang Wiki riêng tư này.");
        }

        page.Title = dto.Title.Trim();
        page.Content = dto.Content ?? string.Empty;
        page.Visibility = visibility;
        page.IsPublic = visibility == "public";
        page.UpdatedAt = DateTimeOffset.UtcNow;

        await _wikiRepo.UpdateAsync(page, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "Update",
            nameof(WikiPage),
            page.Id.ToString(),
            new { projectId, page.Title },
            ct);

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
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "Delete",
            nameof(WikiPage),
            id.ToString(),
            new { projectId, page.Title },
            ct);

        return Result.Success();
    }

    private static string? NormalizeVisibility(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? "internal"
            : value.Trim().ToLowerInvariant();
        return SupportedVisibilities.Contains(normalized) ? normalized : null;
    }
}
