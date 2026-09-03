using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

/// <summary>
/// One resolved project role, whether built-in or defined by an organization.
/// </summary>
/// <param name="Key">Value stored on <see cref="ProjectMember.Role"/>.</param>
/// <param name="DisplayName">Label shown to users.</param>
/// <param name="BaseRole">Built-in role whose permissions apply. Authorization reads only this.</param>
/// <param name="IsCustom">True when the role came from a <see cref="ProjectRoleDefinition"/>.</param>
/// <param name="SkillTags">Skill hints used when suggesting who should take a task.</param>
public sealed record ResolvedProjectRole(
    string Key,
    string DisplayName,
    string BaseRole,
    bool IsCustom,
    IReadOnlyList<string> SkillTags);

public interface IProjectRoleCatalog
{
    /// <summary>
    /// Query source used by authorization filters that must remain server-translatable. Callers
    /// still compare only inherited built-in capabilities, never a custom role key by itself.
    /// </summary>
    IQueryable<ProjectRoleDefinition> GetDefinitionsQuery();

    /// <summary>Every role assignable in an organization: the built-ins plus its active custom roles.</summary>
    Task<IReadOnlyList<ResolvedProjectRole>> GetAssignableRolesAsync(Guid? organizationId, CancellationToken ct = default);

    /// <summary>Resolves only roles currently available for a new membership assignment.</summary>
    Task<ResolvedProjectRole?> ResolveAssignableAsync(string? role, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Resolves a stored role value. Returns null when the value matches neither a built-in role
    /// nor a custom role in the organization. Inactive roles still resolve for existing members.
    /// </summary>
    Task<ResolvedProjectRole?> ResolveAsync(string? role, Guid? organizationId, CancellationToken ct = default);
}

public class ProjectRoleCatalog : IProjectRoleCatalog
{
    private readonly IRepository<ProjectRoleDefinition> _definitionRepo;

    public ProjectRoleCatalog(IRepository<ProjectRoleDefinition> definitionRepo)
    {
        _definitionRepo = definitionRepo;
    }

    public IQueryable<ProjectRoleDefinition> GetDefinitionsQuery()
        => _definitionRepo.GetQueryable().AsNoTracking();

    public async Task<IReadOnlyList<ResolvedProjectRole>> GetAssignableRolesAsync(
        Guid? organizationId,
        CancellationToken ct = default)
    {
        var roles = ProjectRoleRules.AssignableRoles
            .Select(BuiltIn)
            .ToList();

        if (organizationId.HasValue)
        {
            var definitions = await ActiveDefinitionsQuery(organizationId.Value)
                .OrderBy(definition => definition.DisplayName)
                .ToListAsync(ct);
            roles.AddRange(definitions.Select(FromDefinition));
        }

        return roles;
    }

    public async Task<ResolvedProjectRole?> ResolveAsync(
        string? role,
        Guid? organizationId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        if (ProjectRoleRules.TryNormalizeAssignableRole(role, out var builtIn))
        {
            return BuiltIn(builtIn);
        }

        // Owner is a real role but is never assignable, so it is resolved separately.
        if (string.Equals(role.Trim(), ProjectRoleRules.Owner, StringComparison.OrdinalIgnoreCase))
        {
            return BuiltIn(ProjectRoleRules.Owner);
        }

        if (!organizationId.HasValue)
        {
            return null;
        }

        var key = NormalizeKey(role);
        // Inactive definitions are not offered for new assignments, but existing memberships
        // must continue to resolve after a role is deactivated.
        var definition = await DefinitionsQuery(organizationId.Value)
            .FirstOrDefaultAsync(item => item.Key == key, ct);

        return definition == null ? null : FromDefinition(definition);
    }

    public async Task<ResolvedProjectRole?> ResolveAssignableAsync(
        string? role,
        Guid? organizationId,
        CancellationToken ct = default)
    {
        var resolved = await ResolveAsync(role, organizationId, ct);
        if (resolved == null || !resolved.IsCustom || !organizationId.HasValue)
        {
            return resolved;
        }

        return await ActiveDefinitionsQuery(organizationId.Value)
            .AnyAsync(definition => definition.Key == resolved.Key, ct)
            ? resolved
            : null;
    }

    private IQueryable<ProjectRoleDefinition> ActiveDefinitionsQuery(Guid organizationId)
        => DefinitionsQuery(organizationId)
            .Where(definition => definition.IsActive);

    private IQueryable<ProjectRoleDefinition> DefinitionsQuery(Guid organizationId)
        => _definitionRepo.GetQueryable()
            .AsNoTracking()
            .Where(definition => definition.OrganizationId == organizationId);

    private static ResolvedProjectRole BuiltIn(string role)
        => new(role, ProjectPermissionRules.DescribeRoleVietnamese(role), role, false, []);

    private static ResolvedProjectRole FromDefinition(ProjectRoleDefinition definition)
        => new(
            definition.Key,
            definition.DisplayName,
            ProjectRoleRules.NormalizeProjectRole(definition.BaseRole),
            true,
            SplitSkillTags(definition.SkillTags));

    /// <summary>
    /// Turns free text into a stable, storable key: "Dev Backend" becomes "dev-backend".
    /// </summary>
    public static string NormalizeKey(string value)
    {
        var trimmed = value.Trim().ToLowerInvariant();
        var builder = new System.Text.StringBuilder(trimmed.Length);
        var lastWasSeparator = false;

        foreach (var character in trimmed)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator && builder.Length > 0)
            {
                builder.Append('-');
                lastWasSeparator = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    public static IReadOnlyList<string> SplitSkillTags(string? skillTags)
        => string.IsNullOrWhiteSpace(skillTags)
            ? []
            : skillTags
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(tag => tag.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToList();
}
