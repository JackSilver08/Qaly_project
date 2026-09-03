using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public static class SystemModulePermissionRules
{
    public const string Dashboard = "Dashboard";
    public const string OrganizationMembers = "OrganizationMembers";
    public const string Projects = "Projects";
    public const string Tasks = "Tasks";
    public const string WorkGroups = "WorkGroups";
    public const string Analytics = "Analytics";
    public const string Wiki = "Wiki";
    public const string Settings = "Settings";
    public const string AiHub = "AiHub";
    public const string OrganizationManagement = "OrganizationManagement";
    public const string ModeratorAssignments = "ModeratorAssignments";
    public const string UserManagement = "UserManagement";
    public const string AuditLogs = "AuditLogs";

    public static readonly IReadOnlyList<string> KnownModules =
    [
        OrganizationMembers,
        Dashboard,
        Projects,
        Tasks,
        WorkGroups,
        Analytics,
        Wiki,
        Settings,
        AiHub,
        OrganizationManagement,
        ModeratorAssignments,
        UserManagement,
        AuditLogs
    ];

    public static bool IsKnownModule(string? moduleKey)
        => KnownModules.Contains(moduleKey?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    public static string? NormalizeModule(string? moduleKey)
        => KnownModules.FirstOrDefault(item =>
            string.Equals(item, moduleKey?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static EffectiveSystemModuleAccess ResolveDefault(string? systemRole, string moduleKey)
    {
        var normalizedModule = NormalizeModule(moduleKey);
        if (normalizedModule == null ||
            !SystemRoleRules.TryNormalizeKnownRole(systemRole, out var normalizedRole))
        {
            return Restricted(normalizedModule ?? moduleKey, "default_unknown");
        }

        if (SystemRoleRules.IsAdmin(normalizedRole))
        {
            return new(normalizedModule, true, AiNativeSystemTier.Full, "role_default");
        }

        var isModerator = SystemRoleRules.IsModerator(normalizedRole);
        var allowed = normalizedModule switch
        {
            ModeratorAssignments or UserManagement or AuditLogs => false,
            OrganizationManagement => isModerator,
            _ => true
        };

        if (!allowed)
        {
            return Restricted(normalizedModule, "role_default");
        }

        var aiTier = normalizedModule == AiHub && isModerator
            ? AiNativeSystemTier.SummaryOnly
            : AiNativeSystemTier.Full;
        return new(normalizedModule, true, aiTier, "role_default");
    }

    public static EffectiveSystemModuleAccess ResolveRows(
        string moduleKey,
        IReadOnlyCollection<SystemModulePermission> rows,
        string source)
    {
        if (rows.Count == 0)
        {
            return Restricted(moduleKey, source);
        }

        // Duplicate rows are not expected, but historical databases may contain them. A deny or
        // malformed tier must win instead of a permissive FirstOrDefault depending on row order.
        var allowed = rows.All(item => item.IsAllowed);
        var tier = rows.Select(item => ParseTier(item.AiTier)).Min();
        return allowed
            ? new EffectiveSystemModuleAccess(moduleKey, true, tier, source)
            : Restricted(moduleKey, source);
    }

    public static AiNativeSystemTier ParseTier(string? value)
    {
        if (string.Equals(value, "Full", StringComparison.OrdinalIgnoreCase))
        {
            return AiNativeSystemTier.Full;
        }

        if (string.Equals(value, "SummaryOnly", StringComparison.OrdinalIgnoreCase))
        {
            return AiNativeSystemTier.SummaryOnly;
        }

        return AiNativeSystemTier.Restricted;
    }

    public static bool TryNormalizeTier(string? value, out string normalized)
    {
        if (string.Equals(value, "Full", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Full";
            return true;
        }

        if (string.Equals(value, "SummaryOnly", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "SummaryOnly";
            return true;
        }

        if (string.Equals(value, "Restricted", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Restricted";
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    public static string FormatTier(AiNativeSystemTier tier) => tier switch
    {
        AiNativeSystemTier.Full => "Full",
        AiNativeSystemTier.SummaryOnly => "SummaryOnly",
        _ => "Restricted"
    };

    private static EffectiveSystemModuleAccess Restricted(string moduleKey, string source)
        => new(moduleKey, false, AiNativeSystemTier.Restricted, source);
}

public sealed record EffectiveSystemModuleAccess(
    string ModuleKey,
    bool IsAllowed,
    AiNativeSystemTier AiTier,
    string Source);

public interface ISystemModuleAuthorizationService
{
    Task<EffectiveSystemModuleAccess> ResolveAsync(
        Guid userId,
        string? systemRole,
        string moduleKey,
        CancellationToken ct = default);

    Task<IReadOnlyList<EffectiveSystemModuleAccess>> ResolveAllAsync(
        Guid userId,
        string? systemRole,
        CancellationToken ct = default);
}

public sealed class SystemModuleAuthorizationService : ISystemModuleAuthorizationService
{
    private readonly IRepository<SystemModulePermission> _permissions;

    public SystemModuleAuthorizationService(IRepository<SystemModulePermission> permissions)
    {
        _permissions = permissions;
    }

    public async Task<EffectiveSystemModuleAccess> ResolveAsync(
        Guid userId,
        string? systemRole,
        string moduleKey,
        CancellationToken ct = default)
    {
        var normalizedModule = SystemModulePermissionRules.NormalizeModule(moduleKey);
        if (normalizedModule == null ||
            !SystemRoleRules.TryNormalizeKnownRole(systemRole, out var normalizedRole))
        {
            return SystemModulePermissionRules.ResolveDefault(systemRole, moduleKey);
        }

        var rows = await LoadApplicableRowsAsync(userId, systemRole, normalizedRole, ct);
        return ResolveFromLoadedRows(rows, systemRole, normalizedModule);
    }

    public async Task<IReadOnlyList<EffectiveSystemModuleAccess>> ResolveAllAsync(
        Guid userId,
        string? systemRole,
        CancellationToken ct = default)
    {
        if (!SystemRoleRules.TryNormalizeKnownRole(systemRole, out var normalizedRole))
        {
            return SystemModulePermissionRules.KnownModules
                .Select(module => SystemModulePermissionRules.ResolveDefault(systemRole, module))
                .ToList();
        }

        var rows = await LoadApplicableRowsAsync(userId, systemRole, normalizedRole, ct);
        return SystemModulePermissionRules.KnownModules
            .Select(module => ResolveFromLoadedRows(rows, systemRole, module))
            .ToList();
    }

    private static EffectiveSystemModuleAccess ResolveFromLoadedRows(
        IReadOnlyCollection<SystemModulePermission> rows,
        string? systemRole,
        string moduleKey)
    {
        var userRows = rows.Where(item =>
            item.UserId.HasValue &&
            string.Equals(item.ModuleKey, moduleKey, StringComparison.OrdinalIgnoreCase)).ToList();
        if (userRows.Count > 0)
        {
            return SystemModulePermissionRules.ResolveRows(moduleKey, userRows, "user_override");
        }

        var roleRows = rows.Where(item =>
            !item.UserId.HasValue &&
            string.Equals(item.ModuleKey, moduleKey, StringComparison.OrdinalIgnoreCase)).ToList();
        return roleRows.Count > 0
            ? SystemModulePermissionRules.ResolveRows(moduleKey, roleRows, "role_override")
            : SystemModulePermissionRules.ResolveDefault(systemRole, moduleKey);
    }

    private async Task<List<SystemModulePermission>> LoadApplicableRowsAsync(
        Guid userId,
        string? rawRole,
        string normalizedRole,
        CancellationToken ct)
    {
        var trimmedRole = rawRole?.Trim();
        return await _permissions.GetQueryable()
            .AsNoTracking()
            .Where(item => item.UserId == userId ||
                (item.UserId == null &&
                 (item.SystemRole == normalizedRole || item.SystemRole == trimmedRole)))
            .ToListAsync(ct);
    }
}
