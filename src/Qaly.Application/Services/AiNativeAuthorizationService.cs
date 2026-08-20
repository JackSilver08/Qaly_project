using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public enum AiNativeSystemTier
{
    Restricted = 0,
    SummaryOnly = 1,
    Full = 2
}

public sealed record AiNativeProjectAuthorization(
    bool CanRead,
    bool CanManage,
    bool CanCreateTask,
    AiCapabilityTier CapabilityTier);

/// <summary>
/// Server-owned authorization boundary shared by AI discovery, draft composition and mutation.
/// User overrides win over system-role defaults; custom project roles are always resolved to their
/// active built-in base role before permissions are calculated.
/// </summary>
public interface IAiNativeAuthorizationService
{
    Task<AiNativeSystemTier> ResolveSystemTierAsync(
        Guid userId,
        string? systemRole,
        CancellationToken ct = default);

    Task<AiNativeProjectAuthorization> ResolveProjectAsync(
        Project project,
        Guid userId,
        bool isSystemAdmin,
        CancellationToken ct = default);
}

public sealed class AiNativeAuthorizationService : IAiNativeAuthorizationService
{
    private const string AiHubModule = "AiHub";

    private readonly IRepository<SystemModulePermission> _systemPermissions;
    private readonly IRepository<ProjectMember> _projectMembers;
    private readonly IRepository<OrganizationMember> _organizationMembers;
    private readonly IProjectRoleCatalog _roleCatalog;

    public AiNativeAuthorizationService(
        IRepository<SystemModulePermission> systemPermissions,
        IRepository<ProjectMember> projectMembers,
        IRepository<OrganizationMember> organizationMembers,
        IProjectRoleCatalog roleCatalog)
    {
        _systemPermissions = systemPermissions;
        _projectMembers = projectMembers;
        _organizationMembers = organizationMembers;
        _roleCatalog = roleCatalog;
    }

    public async Task<AiNativeSystemTier> ResolveSystemTierAsync(
        Guid userId,
        string? systemRole,
        CancellationToken ct = default)
    {
        var permission = await _systemPermissions.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId && item.ModuleKey == AiHubModule, ct);

        if (permission == null && !string.IsNullOrWhiteSpace(systemRole))
        {
            permission = await _systemPermissions.GetQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.UserId == null &&
                    item.SystemRole == systemRole &&
                    item.ModuleKey == AiHubModule,
                    ct);
        }

        if (permission == null)
        {
            return AiNativeSystemTier.Full;
        }

        if (!permission.IsAllowed ||
            string.Equals(permission.AiTier, "Restricted", StringComparison.OrdinalIgnoreCase))
        {
            return AiNativeSystemTier.Restricted;
        }

        return string.Equals(permission.AiTier, "SummaryOnly", StringComparison.OrdinalIgnoreCase)
            ? AiNativeSystemTier.SummaryOnly
            : AiNativeSystemTier.Full;
    }

    public async Task<AiNativeProjectAuthorization> ResolveProjectAsync(
        Project project,
        Guid userId,
        bool isSystemAdmin,
        CancellationToken ct = default)
    {
        if (isSystemAdmin || project.OwnerId == userId)
        {
            return Full();
        }

        var storedRole = project.Members.FirstOrDefault(item => item.UserId == userId)?.Role;
        if (storedRole == null)
        {
            storedRole = await _projectMembers.GetQueryable()
                .AsNoTracking()
                .Where(item => item.ProjectId == project.Id && item.UserId == userId)
                .Select(item => item.Role)
                .FirstOrDefaultAsync(ct);
        }

        if (!string.IsNullOrWhiteSpace(storedRole))
        {
            var resolved = await _roleCatalog.ResolveAsync(storedRole, project.OrganizationId, ct);
            if (resolved == null)
            {
                // A removed/unknown custom role must fail closed for writes while preserving the
                // membership read boundary until an administrator repairs the assignment.
                return new AiNativeProjectAuthorization(true, false, false, AiCapabilityTier.ReadOnly);
            }

            var permissions = ProjectPermissionRules.Resolve(
                resolved.BaseRole,
                isOwner: false,
                isSystemAdmin: false);
            return new AiNativeProjectAuthorization(
                true,
                permissions.CanManageProject,
                permissions.CanCreateTask,
                Enum.TryParse<AiCapabilityTier>(permissions.AiTier, out var tier)
                    ? tier
                    : AiCapabilityTier.ReadOnly);
        }

        if (!project.OrganizationId.HasValue)
        {
            return None();
        }

        if (project.Organization?.OwnerId == userId)
        {
            return Full();
        }

        var organizationRole = project.Organization?.Members
            .FirstOrDefault(item => item.UserId == userId)?.Role;
        if (organizationRole == null)
        {
            organizationRole = await _organizationMembers.GetQueryable()
                .AsNoTracking()
                .Where(item => item.OrganizationId == project.OrganizationId.Value && item.UserId == userId)
                .Select(item => item.Role)
                .FirstOrDefaultAsync(ct);
        }

        if (OrganizationRoleRules.CanManageOrganization(organizationRole))
        {
            return Full();
        }

        return organizationRole == null
            ? None()
            : new AiNativeProjectAuthorization(true, false, false, AiCapabilityTier.ReadOnly);
    }

    private static AiNativeProjectAuthorization Full()
        => new(true, true, true, AiCapabilityTier.Full);

    private static AiNativeProjectAuthorization None()
        => new(false, false, false, AiCapabilityTier.None);
}
