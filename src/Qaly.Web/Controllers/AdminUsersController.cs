using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize(Policy = "UserManagementAccess")]
[Route("api/admin/users")]
public class AdminUsersController : BaseApiController
{
    private readonly QalyDbContext _context;
    private readonly IAuditLogService _auditLogService;
    private readonly ISessionService _sessionService;
    private readonly IProjectService _projectService;

    public AdminUsersController(QalyDbContext context, IAuditLogService auditLogService, ISessionService sessionService, IProjectService projectService)
    {
        _context = context;
        _auditLogService = auditLogService;
        _sessionService = sessionService;
        _projectService = projectService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(user => user.FullName.Contains(term) || user.Email.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(user => user.Role == role.Trim());
        }

        if (isActive.HasValue)
        {
            query = query.Where(user => user.IsActive == isActive.Value);
        }

        var totalItems = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(user => user.Role == SystemRoleRules.Admin)
            .ThenBy(user => user.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new AdminUserDto(
                user.Id,
                user.FullName,
                user.Email,
                user.Role,
                user.IsActive,
                user.AvatarUrl,
                user.CreatedAt,
                user.OwnedProjects.Count(project => !project.IsDeleted),
                user.ProjectMemberships.Count(member => !member.Project.IsDeleted)))
            .ToListAsync(ct);

        return Ok(new PagedAdminUsersResponse(items, page, pageSize, totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken ct)
    {
        var user = await _context.Users.AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new AdminUserDto(
                candidate.Id, candidate.FullName, candidate.Email, candidate.Role, candidate.IsActive,
                candidate.AvatarUrl, candidate.CreatedAt,
                candidate.OwnedProjects.Count(project => !project.IsDeleted),
                candidate.ProjectMemberships.Count(member => !member.Project.IsDeleted)))
            .SingleOrDefaultAsync(ct);

        return user == null ? NotFound() : Ok(user);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> CreateUser(CreateAdminUserRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(email))
            return BadRequest("Họ tên và email là bắt buộc.");
        if (await _context.Users.AnyAsync(user => user.Email == email, ct))
            return Conflict("Email đã được đăng ký.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return BadRequest("Mật khẩu phải có ít nhất 8 ký tự.");
        if (!SystemRoleRules.TryNormalizeAssignableRole(request.Role, out var role))
            return BadRequest("Chỉ có thể tạo tài khoản Member hoặc Moderator.");

        var user = new User
        {
            FullName = request.FullName.Trim(), Email = email, PasswordHash = HashPassword(request.Password),
            Role = role, IsActive = request.IsActive
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("AdminCreateUser", nameof(User), user.Id.ToString(), new { user.Email, user.Role, user.IsActive }, ct);
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, ToDto(user));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, UpdateAdminUserRequest request, CancellationToken ct)
    {
        var actor = CurrentActor();
        var user = await _context.Users.FindAsync([id], ct);
        if (user == null) return NotFound();
        if (SystemRoleRules.IsAdmin(user.Role)) return Forbid();
        if (SystemRoleRules.IsModerator(actor.Role) && SystemRoleRules.IsModerator(user.Role)) return Forbid();

        var previous = new { user.FullName, user.Role, user.IsActive, user.AvatarUrl };
        var securityChanged = false;
        if (!string.IsNullOrWhiteSpace(request.FullName)) user.FullName = request.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            if (!SystemRoleRules.TryNormalizeAssignableRole(request.Role, out var normalizedRole))
                return BadRequest("Admin chỉ có thể được chuyển giao qua quy trình riêng.");
            if (!string.Equals(user.Role, normalizedRole, StringComparison.OrdinalIgnoreCase) && !SystemRoleRules.IsAdmin(actor.Role))
                return Forbid();
            securityChanged = !string.Equals(user.Role, normalizedRole, StringComparison.Ordinal);
            user.Role = normalizedRole;
        }
        if (request.IsActive.HasValue && request.IsActive.Value != user.IsActive)
        {
            user.IsActive = request.IsActive.Value;
            securityChanged = true;
        }
        if (!string.IsNullOrWhiteSpace(request.AvatarUrl)) user.AvatarUrl = request.AvatarUrl.Trim();

        await _context.SaveChangesAsync(ct);
        if (securityChanged) await _sessionService.RevokeAllUserSessionsAsync(user.Id, ct);
        await _auditLogService.LogAsync("AdminUpdateUser", nameof(User), user.Id.ToString(), new { previous, current = new { user.FullName, user.Role, user.IsActive, user.AvatarUrl } }, ct);
        return Ok(ToDto(user));
    }

    [HttpPost("{id:guid}/revoke-sessions")]
    public async Task<IActionResult> RevokeSessions(Guid id, CancellationToken ct)
    {
        var actor = CurrentActor();
        var user = await _context.Users.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.Id == id, ct);
        if (user == null) return NotFound();
        if (SystemRoleRules.IsAdmin(user.Role) || (SystemRoleRules.IsModerator(actor.Role) && SystemRoleRules.IsModerator(user.Role))) return Forbid();
        var revoked = await _sessionService.RevokeAllUserSessionsAsync(id, ct);
        await _auditLogService.LogAsync("AdminRevokeUserSessions", nameof(User), id.ToString(), new { revoked }, ct);
        return Ok(new { revoked });
    }

    [HttpGet("{id:guid}/projects")]
    public async Task<IActionResult> GetProjects(Guid id, CancellationToken ct)
    {
        if (!await _context.Users.AnyAsync(user => user.Id == id, ct)) return NotFound();
        var projects = await _context.Projects.IgnoreQueryFilters().AsNoTracking()
            .Where(project => project.OwnerId == id || project.Members.Any(member => member.UserId == id))
            .OrderBy(project => project.Name)
            .Select(project => new AdminUserProjectDto(
                project.Id, project.Name, project.Code, project.Status, project.ArchivedAt,
                project.OwnerId == id,
                project.OwnerId == id ? ProjectRoleRules.Owner : project.Members.Where(member => member.UserId == id).Select(member => member.Role).FirstOrDefault()!,
                project.Tasks.Count))
            .ToListAsync(ct);
        return Ok(projects);
    }

    [HttpPatch("{id:guid}/projects/{projectId:guid}")]
    public async Task<IActionResult> UpdateProjectMembership(Guid id, Guid projectId, UpdateUserProjectRequest request, CancellationToken ct)
    {
        if (!IsSupportedProjectRole(request.Role)) return BadRequest("Invalid project role.");
        var serviceResult = await _projectService.AddMemberAsync(projectId, id, request.Role, ct);
        if (!serviceResult.IsSuccess) return StatusCode(serviceResult.StatusCode, serviceResult.Error);
        return Ok(new { role = ProjectRoleRules.NormalizeProjectRole(request.Role) });
        /*
        if (!IsSupportedProjectRole(request.Role))
            return BadRequest("Vai trò dự án không hợp lệ.");
        var project = await _context.Projects.IgnoreQueryFilters().Include(project => project.Members)
            .SingleOrDefaultAsync(project => project.Id == projectId, ct);
        if (project == null || !await _context.Users.AnyAsync(user => user.Id == id, ct)) return NotFound();
        if (project.OwnerId == id) return BadRequest("Không thể đổi role membership của chủ sở hữu dự án.");
        var membership = project.Members.SingleOrDefault(member => member.UserId == id);
        var normalized = ProjectRoleRules.NormalizeProjectRole(request.Role);
        if (membership == null)
        {
            membership = new ProjectMember { ProjectId = projectId, UserId = id, Role = normalized };
            _context.ProjectMembers.Add(membership);
        }
        else membership.Role = normalized;
        await _context.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("AdminUpdateProjectMembership", nameof(ProjectMember), membership.Id.ToString(), new { projectId, userId = id, role = normalized }, ct);
        return Ok(new { membership.Id, membership.Role });
        */
    }

    [HttpDelete("{id:guid}/projects/{projectId:guid}")]
    public async Task<IActionResult> RemoveProjectMembership(Guid id, Guid projectId, CancellationToken ct)
    {
        var serviceResult = await _projectService.RemoveMemberAsync(projectId, id, ct);
        if (!serviceResult.IsSuccess) return StatusCode(serviceResult.StatusCode, serviceResult.Error);
        return NoContent();
        /*
        var project = await _context.Projects.IgnoreQueryFilters().SingleOrDefaultAsync(candidate => candidate.Id == projectId, ct);
        if (project == null) return NotFound();
        if (project.OwnerId == id) return BadRequest("Hãy chuyển quyền sở hữu trước khi xóa user khỏi dự án.");
        var membership = await _context.ProjectMembers.SingleOrDefaultAsync(member => member.ProjectId == projectId && member.UserId == id, ct);
        if (membership == null) return NotFound();
        _context.ProjectMembers.Remove(membership);
        await _context.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("AdminRemoveProjectMembership", nameof(ProjectMember), membership.Id.ToString(), new { projectId, userId = id }, ct);
        return NoContent();
        */
    }

    [HttpPost("transfer-admin")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> TransferAdmin(TransferAdminRequest request, CancellationToken ct)
    {
        var actor = CurrentActor();
        if (actor.Id == request.SuccessorUserId) return BadRequest("Không thể chuyển giao cho chính tài khoản hiện tại.");
        var currentAdmin = await _context.Users.SingleOrDefaultAsync(user => user.Id == actor.Id && user.Role == SystemRoleRules.Admin, ct);
        var successor = await _context.Users.SingleOrDefaultAsync(user => user.Id == request.SuccessorUserId, ct);
        if (currentAdmin == null || successor == null) return NotFound();
        if (!successor.IsActive || !SystemRoleRules.IsModerator(successor.Role))
            return BadRequest("Người kế nhiệm phải là một Moderator đang hoạt động.");

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        currentAdmin.Role = SystemRoleRules.Moderator;
        await _context.SaveChangesAsync(ct);
        successor.Role = SystemRoleRules.Admin;
        await _context.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("TransferSystemAdmin", nameof(User), successor.Id.ToString(), new { previousAdminId = currentAdmin.Id, successorId = successor.Id }, ct);
        await transaction.CommitAsync(ct);
        await _sessionService.RevokeAllUserSessionsAsync(currentAdmin.Id, ct);
        await _sessionService.RevokeAllUserSessionsAsync(successor.Id, ct);
        return Ok(new { adminUserId = successor.Id });
    }

    [HttpPost("import")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ImportUsers(ImportUsersRequest request, CancellationToken ct)
    {
        var created = 0;
        var skipped = 0;
        foreach (var item in request.Users)
        {
            var email = NormalizeEmail(item.Email);
            if (string.IsNullOrWhiteSpace(item.FullName) || string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(item.Password) || item.Password.Length < 8 ||
                !SystemRoleRules.TryNormalizeAssignableRole(item.Role, out var role) ||
                await _context.Users.AnyAsync(user => user.Email == email, ct))
            { skipped++; continue; }
            _context.Users.Add(new User { FullName = item.FullName.Trim(), Email = email, PasswordHash = HashPassword(item.Password), Role = role, IsActive = item.IsActive });
            created++;
        }
        await _context.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("AdminImportUsers", nameof(User), "bulk", new { created, skipped }, ct);
        return Ok(new { created, skipped });
    }

    private (Guid Id, string Role) CurrentActor()
        => (Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), User.FindFirstValue(ClaimTypes.Role) ?? SystemRoleRules.Member);
    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    private static bool IsSupportedProjectRole(string? role)
        => new[] { ProjectRoleRules.Manager, ProjectRoleRules.ScrumMaster, ProjectRoleRules.Developer,
            ProjectRoleRules.Tester, ProjectRoleRules.Reviewer, ProjectRoleRules.Member,
            ProjectRoleRules.Viewer, ProjectRoleRules.Customer }
            .Contains(role?.Trim(), StringComparer.OrdinalIgnoreCase);
    private static AdminUserDto ToDto(User user) => new(user.Id, user.FullName, user.Email, user.Role, user.IsActive, user.AvatarUrl, user.CreatedAt, 0, 0);
    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 10000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
}

public sealed record AdminUserDto(Guid Id, string FullName, string Email, string Role, bool IsActive, string? AvatarUrl, DateTimeOffset CreatedAt, int OwnedProjectCount, int MembershipProjectCount);
public sealed record PagedAdminUsersResponse(IReadOnlyList<AdminUserDto> Items, int Page, int PageSize, int TotalItems, int TotalPages);
public sealed record AdminUserProjectDto(Guid Id, string Name, string Code, string Status, DateTimeOffset? ArchivedAt, bool IsOwner, string Role, int TaskCount);
public sealed record CreateAdminUserRequest(string FullName, string Email, string? Password, string? Role, bool IsActive = true);
public sealed record UpdateAdminUserRequest(string? FullName, string? Role, bool? IsActive, string? AvatarUrl);
public sealed record UpdateUserProjectRequest(string Role);
public sealed record TransferAdminRequest(Guid SuccessorUserId);
public sealed record ImportUsersRequest(IReadOnlyList<CreateAdminUserRequest> Users);
