using Microsoft.EntityFrameworkCore;
using Qaly.Application.Services;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Seeds;

public partial class DataSeeder
{
    /// <summary>
    /// Adds a complete, production-shaped professional taxonomy and honest demo assignments.
    /// This seed never changes OrganizationMember.Role; profiles are staffing metadata only.
    /// </summary>
    private async Task<bool> EnsureProfessionalProfileDemoSeedAsync()
    {
        var changed = false;
        var organizations = await _context.Organizations.Where(item => item.IsActive).ToListAsync();
        foreach (var organization in organizations)
        {
            var existingKeys = await _context.ProfessionalProfileDefinitions
                .Where(item => item.OrganizationId == organization.Id)
                .Select(item => item.Key)
                .ToHashSetAsync(StringComparer.OrdinalIgnoreCase);
            var missing = ProfessionalProfileCatalog.CreateBaseline(organization.Id)
                .Where(item => !existingKeys.Contains(item.Key))
                .ToList();
            if (missing.Count > 0)
            {
                await _context.ProfessionalProfileDefinitions.AddRangeAsync(missing);
                changed = true;
            }
        }
        if (changed) await _context.SaveChangesAsync();

        var demoOrganization = organizations.FirstOrDefault(item => item.Code == "qaly-demo-2026");
        if (demoOrganization == null) return changed;
        var users = await _context.Users.Where(item => item.IsActive && item.Email.EndsWith("@qaly.dev"))
            .ToDictionaryAsync(item => item.Email, StringComparer.OrdinalIgnoreCase);
        if (!users.TryGetValue("admin@qaly.dev", out var verifier)) return changed;
        var definitions = await _context.ProfessionalProfileDefinitions
            .Where(item => item.OrganizationId == demoOrganization.Id && item.IsActive)
            .ToDictionaryAsync(item => item.Key, StringComparer.OrdinalIgnoreCase);
        var existingPairs = await _context.OrganizationMemberProfessionalProfiles
            .Where(item => item.OrganizationId == demoOrganization.Id)
            .Select(item => new { item.UserId, item.ProfessionalProfileDefinitionId })
            .ToListAsync();
        var existing = existingPairs.Select(item => (item.UserId, item.ProfessionalProfileDefinitionId)).ToHashSet();
        var assignments = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["admin@qaly.dev"] = ["product-project-manager", "qa-automation", "solution-system-architect"],
            ["minh.anh@qaly.dev"] = ["product-project-manager", "business-analyst", "scrum-master-agile-coach"],
            ["bao.ngoc@qaly.dev"] = ["product-project-manager", "customer-success-support", "business-analyst"],
            ["quoc.huy@qaly.dev"] = ["backend-engineer", "data-engineer"],
            ["thu.ha@qaly.dev"] = ["ux-ui-product-designer", "business-analyst"],
            ["gia.khang@qaly.dev"] = ["frontend-engineer", "fullstack-engineer"],
            ["linh.chi@qaly.dev"] = ["ai-ml-engineer", "backend-engineer"],
            ["tuan.kiet@qaly.dev"] = ["data-analyst", "frontend-engineer"],
            ["mai.phuong@qaly.dev"] = ["qa-manual", "customer-success-support"],
            ["thanh.tam@qaly.dev"] = ["devops-sre", "cloud-platform-engineer"],
            ["viet.long@qaly.dev"] = ["backend-engineer", "security-engineer"],
            ["yen.nhi@qaly.dev"] = ["technical-writer", "qa-manual"]
        };
        var now = DateTimeOffset.UtcNow;
        foreach (var (email, keys) in assignments)
        {
            if (!users.TryGetValue(email, out var user)) continue;
            foreach (var key in keys)
            {
                if (!definitions.TryGetValue(key, out var definition) || existing.Contains((user.Id, definition.Id))) continue;
                await _context.OrganizationMemberProfessionalProfiles.AddAsync(new OrganizationMemberProfessionalProfile
                {
                    OrganizationId = demoOrganization.Id,
                    UserId = user.Id,
                    ProfessionalProfileDefinitionId = definition.Id,
                    Proficiency = email is "admin@qaly.dev" or "minh.anh@qaly.dev" ? ProfessionalProfileCatalog.Expert : ProfessionalProfileCatalog.Proficient,
                    VerificationStatus = OrganizationMemberProfessionalProfile.Verified,
                    Source = ProfessionalProfileCatalog.ManagerConfirmed,
                    EffectiveFrom = now.AddMonths(-6),
                    VerifiedByUserId = verifier.Id,
                    VerifiedAt = now.AddDays(-30),
                    Note = "Dữ liệu demo đã xác minh để minh họa staffing; không cấp quyền truy cập."
                });
                existing.Add((user.Id, definition.Id));
                changed = true;
            }
        }
        if (changed) await _context.SaveChangesAsync();
        return changed;
    }
}
