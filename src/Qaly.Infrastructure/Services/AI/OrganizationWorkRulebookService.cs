using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services.AI;

public sealed class OrganizationWorkRulebookService : IOrganizationWorkRulebookService
{
    private const int MaxRules = 50;
    private static readonly Dictionary<string, (decimal Min, decimal Max, string Unit)> NumericRuleBounds =
        new Dictionary<string, (decimal, decimal, string)>(StringComparer.Ordinal)
        {
            ["max_active_projects"] = (1m, 50m, "projects"),
            ["max_utilization_percent"] = (10m, 100m, "percent"),
            ["focus_reserve_percent"] = (0m, 50m, "percent"),
            ["reviewer_coordination_overhead_percent"] = (0m, 50m, "percent")
        };
    private static readonly HashSet<string> BooleanRuleKeys = new(StringComparer.Ordinal)
    {
        "active_membership_required",
        "capacity_evidence_required"
    };
    private const string ManagerRolesRuleKey = "manager_roles";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly QalyDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public OrganizationWorkRulebookService(QalyDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<OrganizationWorkRuleSetDto>>> ListAsync(Guid organizationId, CancellationToken ct = default)
    {
        var access = await AuthorizeAsync(organizationId, manage: false, ct);
        if (!access.IsSuccess) return Result.Failure<IReadOnlyList<OrganizationWorkRuleSetDto>>(access.Error!, access.StatusCode, access.ErrorCode);
        var sets = await _db.OrganizationWorkRuleSets.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId)
            .OrderByDescending(item => item.Version)
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<OrganizationWorkRuleSetDto>>(sets.Select(Map).ToArray());
    }

    public async Task<Result<OrganizationWorkRuleSetDto?>> GetEffectiveAsync(Guid organizationId, CancellationToken ct = default)
    {
        var access = await AuthorizeAsync(organizationId, manage: false, ct);
        if (!access.IsSuccess) return Result.Failure<OrganizationWorkRuleSetDto?>(access.Error!, access.StatusCode, access.ErrorCode);
        var now = DateTimeOffset.UtcNow;
        var ruleSet = await _db.OrganizationWorkRuleSets.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId && item.Status == "active" &&
                (!item.EffectiveFrom.HasValue || item.EffectiveFrom <= now) &&
                (!item.EffectiveUntil.HasValue || item.EffectiveUntil > now))
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(ct);
        return Result.Success<OrganizationWorkRuleSetDto?>(ruleSet == null ? null : Map(ruleSet));
    }

    public async Task<Result<OrganizationWorkRuleSetDto>> CreateDraftAsync(
        Guid organizationId,
        CreateOrganizationWorkRuleSetRequestDto request,
        CancellationToken ct = default)
    {
        var access = await AuthorizeAsync(organizationId, manage: true, ct);
        if (!access.IsSuccess || _currentUser.UserId is not Guid userId)
            return Result.Failure<OrganizationWorkRuleSetDto>(access.Error ?? "Forbidden", access.StatusCode, access.ErrorCode);
        var validation = Validate(request);
        if (validation != null) return validation;
        var version = (await _db.OrganizationWorkRuleSets
            .Where(item => item.OrganizationId == organizationId)
            .MaxAsync(item => (int?)item.Version, ct) ?? 0) + 1;
        var entity = new OrganizationWorkRuleSet
        {
            OrganizationId = organizationId,
            Version = version,
            Status = "draft",
            EffectiveFrom = request.EffectiveFrom,
            EffectiveUntil = request.EffectiveUntil,
            RulesJson = JsonSerializer.Serialize(request.Rules, JsonOptions),
            CreatedByUserId = userId,
            Revision = 1
        };
        _db.OrganizationWorkRuleSets.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Result.Created(Map(entity));
    }

    public async Task<Result<OrganizationWorkRuleSetDto>> ActivateAsync(
        Guid organizationId,
        Guid ruleSetId,
        ActivateOrganizationWorkRuleSetRequestDto request,
        CancellationToken ct = default)
    {
        var access = await AuthorizeAsync(organizationId, manage: true, ct);
        if (!access.IsSuccess || _currentUser.UserId is not Guid userId)
            return Result.Failure<OrganizationWorkRuleSetDto>(access.Error ?? "Forbidden", access.StatusCode, access.ErrorCode);
        var entity = await _db.OrganizationWorkRuleSets.SingleOrDefaultAsync(
            item => item.Id == ruleSetId && item.OrganizationId == organizationId, ct);
        if (entity == null) return Result.NotFound<OrganizationWorkRuleSetDto>();
        if (entity.Revision != request.Revision)
            return Result.Failure<OrganizationWorkRuleSetDto>("Rulebook version changed; reload before activation.", 409, "rulebook_stale");
        if (entity.Status != "draft")
            return Result.Failure<OrganizationWorkRuleSetDto>("Only a draft Rulebook can be activated.", 409, "rulebook_not_draft");

        var now = DateTimeOffset.UtcNow;
        var active = await _db.OrganizationWorkRuleSets
            .Where(item => item.OrganizationId == organizationId && item.Status == "active")
            .ToListAsync(ct);
        foreach (var previous in active)
        {
            previous.Status = "superseded";
            previous.UpdatedAt = now;
            previous.Revision++;
        }
        entity.Status = "active";
        entity.ActivatedAt = now;
        entity.ActivatedByUserId = userId;
        entity.UpdatedAt = now;
        entity.Revision++;
        await _db.SaveChangesAsync(ct);
        return Result.Success(Map(entity));
    }

    private async Task<Result> AuthorizeAsync(Guid organizationId, bool manage, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId) return Result.Forbidden();
        var organization = await _db.Organizations.AsNoTracking()
            .Include(item => item.Members)
            .SingleOrDefaultAsync(item => item.Id == organizationId && item.IsActive, ct);
        if (organization == null) return Result.NotFound();
        var isSystemAdmin = ProjectRoleRules.IsSystemAdmin(_currentUser.Role) ||
            await _db.Users.AsNoTracking().AnyAsync(item => item.Id == userId && item.Role == ProjectRoleRules.SystemAdmin, ct);
        var membership = organization.Members.FirstOrDefault(item => item.UserId == userId);
        var readable = isSystemAdmin || organization.OwnerId == userId || membership != null;
        var manageable = isSystemAdmin || organization.OwnerId == userId || OrganizationRoleRules.CanManageOrganization(membership?.Role);
        return manage ? (manageable ? Result.Success() : Result.Forbidden()) : (readable ? Result.Success() : Result.Forbidden());
    }

    private static Result<OrganizationWorkRuleSetDto>? Validate(CreateOrganizationWorkRuleSetRequestDto request)
    {
        if (request.Rules == null || request.Rules.Count == 0 || request.Rules.Count > MaxRules)
            return Result.Failure<OrganizationWorkRuleSetDto>($"Rulebook requires 1-{MaxRules} rules.", 400, "rulebook_invalid");
        if (request.EffectiveFrom.HasValue && request.EffectiveUntil.HasValue && request.EffectiveUntil <= request.EffectiveFrom)
            return Result.Failure<OrganizationWorkRuleSetDto>("effectiveUntil must be after effectiveFrom.", 400, "rulebook_invalid");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in request.Rules)
        {
            var key = rule.RuleKey?.Trim() ?? string.Empty;
            if (key.Length == 0 || key.Length > 100 || !keys.Add(key) ||
                rule.Enforcement is not ("block" or "warn") || string.IsNullOrWhiteSpace(rule.Category))
                return Result.Failure<OrganizationWorkRuleSetDto>("Rulebook contains an invalid or duplicate rule.", 400, "rulebook_invalid");

            if (NumericRuleBounds.TryGetValue(key, out var bounds))
            {
                if (!rule.NumericValue.HasValue || rule.NumericValue.Value < bounds.Min || rule.NumericValue.Value > bounds.Max ||
                    !string.Equals(rule.Unit, bounds.Unit, StringComparison.Ordinal))
                    return Result.Failure<OrganizationWorkRuleSetDto>(
                        $"Rule '{key}' must be between {bounds.Min:0.##} and {bounds.Max:0.##} {bounds.Unit}.",
                        400,
                        "rulebook_value_out_of_range");
                if (rule.Values is { Count: > 0 })
                    return Result.Failure<OrganizationWorkRuleSetDto>($"Rule '{key}' cannot contain role values.", 400, "rulebook_invalid");
                continue;
            }

            if (BooleanRuleKeys.Contains(key))
            {
                if (rule.NumericValue.HasValue || rule.Values is { Count: > 0 })
                    return Result.Failure<OrganizationWorkRuleSetDto>($"Rule '{key}' does not accept a numeric or role value.", 400, "rulebook_invalid");
                continue;
            }

            if (key == ManagerRolesRuleKey)
            {
                var roles = rule.Values?.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
                if (roles.Length == 0 || roles.Length > 10 || rule.NumericValue.HasValue)
                    return Result.Failure<OrganizationWorkRuleSetDto>("Rule 'manager_roles' requires 1-10 role values.", 400, "rulebook_invalid");
                continue;
            }

            return Result.Failure<OrganizationWorkRuleSetDto>(
                $"Rule '{key}' is not implemented by the current staffing engine.",
                400,
                "rulebook_rule_unsupported");
        }
        return null;
    }

    private static OrganizationWorkRuleSetDto Map(OrganizationWorkRuleSet entity)
        => new(
            entity.Id,
            entity.OrganizationId,
            entity.Version,
            entity.Status,
            entity.EffectiveFrom,
            entity.EffectiveUntil,
            JsonSerializer.Deserialize<OrganizationWorkRuleDto[]>(entity.RulesJson, JsonOptions) ?? [],
            entity.CreatedAt,
            entity.ActivatedAt,
            entity.ActivatedByUserId,
            entity.Revision);
}
