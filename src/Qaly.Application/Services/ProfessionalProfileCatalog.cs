using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Qaly.Domain.Entities;

namespace Qaly.Application.Services;

public sealed record ProfessionalProfileSeed(string Key, string Name, string Category, string Description);

/// <summary>
/// Baseline professional taxonomy. These profiles describe delivery disciplines and never grant access.
/// Organizations may add their own profiles without changing application roles.
/// </summary>
public static partial class ProfessionalProfileCatalog
{
    public const string Foundation = "Foundation";
    public const string Practitioner = "Practitioner";
    public const string Proficient = "Proficient";
    public const string Expert = "Expert";
    public const string MemberDeclared = "MemberDeclared";
    public const string ManagerConfirmed = "ManagerConfirmed";
    public const string Imported = "Imported";

    public static IReadOnlyList<ProfessionalProfileSeed> Baseline { get; } =
    [
        new("product-project-manager", "Product / Project Manager", "Product & Delivery", "Định hướng sản phẩm, phạm vi, kế hoạch, stakeholder và delivery."),
        new("business-analyst", "Business Analyst", "Product & Delivery", "Phân tích nghiệp vụ, quy trình, yêu cầu và tiêu chí nghiệm thu."),
        new("scrum-master-agile-coach", "Scrum Master / Agile Coach", "Product & Delivery", "Điều phối nhịp delivery, gỡ trở ngại và cải tiến cách làm việc."),
        new("ux-ui-product-designer", "UX/UI Product Designer", "Design", "Nghiên cứu người dùng, luồng tương tác, giao diện và design system."),
        new("frontend-engineer", "Frontend Engineer", "Engineering", "Xây dựng trải nghiệm web, accessibility và hiệu năng phía client."),
        new("backend-engineer", "Backend Engineer", "Engineering", "Thiết kế API, domain logic, dữ liệu và dịch vụ phía server."),
        new("fullstack-engineer", "Full-stack Engineer", "Engineering", "Triển khai xuyên suốt frontend, backend và tích hợp."),
        new("mobile-engineer", "Mobile Engineer", "Engineering", "Phát triển và vận hành ứng dụng di động native hoặc cross-platform."),
        new("qa-manual", "QA / Manual Tester", "Quality", "Thiết kế kịch bản, kiểm thử khám phá và xác nhận chất lượng nghiệp vụ."),
        new("qa-automation", "QA Automation Engineer", "Quality", "Xây dựng kiểm thử tự động, test infrastructure và quality gates."),
        new("test-lead", "Test Lead", "Quality", "Lập chiến lược kiểm thử, quản trị rủi ro và điều phối chất lượng."),
        new("devops-sre", "DevOps / SRE", "Platform & Operations", "CI/CD, observability, reliability, incident response và vận hành."),
        new("cloud-platform-engineer", "Cloud / Platform Engineer", "Platform & Operations", "Thiết kế nền tảng cloud, self-service và hạ tầng dùng chung."),
        new("security-engineer", "Security Engineer", "Security", "Threat modeling, security controls, kiểm thử và ứng phó sự cố."),
        new("data-engineer", "Data Engineer", "Data & AI", "Pipeline, mô hình dữ liệu, chất lượng dữ liệu và nền tảng phân tích."),
        new("data-analyst", "Data Analyst", "Data & AI", "Phân tích số liệu, metric, dashboard và hỗ trợ quyết định."),
        new("ai-ml-engineer", "AI / ML Engineer", "Data & AI", "Mô hình, evaluation, inference pipeline và AI safety."),
        new("solution-system-architect", "Solution / System Architect", "Architecture", "Kiến trúc hệ thống, trade-off kỹ thuật và ranh giới tích hợp."),
        new("technical-writer", "Technical Writer", "Enablement", "Tài liệu sản phẩm, vận hành, API và nội dung hướng dẫn."),
        new("customer-success-support", "Customer Success / Support", "Customer", "Onboarding, hỗ trợ, phản hồi khách hàng và adoption."),
    ];

    public static IReadOnlyList<ProfessionalProfileDefinition> CreateBaseline(Guid organizationId)
        => Baseline.Select(item => new ProfessionalProfileDefinition
        {
            OrganizationId = organizationId,
            Key = item.Key,
            Name = item.Name,
            Category = item.Category,
            Description = item.Description,
            IsSystemSeed = true,
            IsActive = true
        }).ToList();

    public static bool TryNormalizeProficiency(string? value, out string normalized)
    {
        normalized = value?.Trim() switch
        {
            var item when string.Equals(item, Foundation, StringComparison.OrdinalIgnoreCase) => Foundation,
            var item when string.Equals(item, Practitioner, StringComparison.OrdinalIgnoreCase) => Practitioner,
            var item when string.Equals(item, Proficient, StringComparison.OrdinalIgnoreCase) => Proficient,
            var item when string.Equals(item, Expert, StringComparison.OrdinalIgnoreCase) => Expert,
            _ => string.Empty
        };
        return normalized.Length > 0;
    }

    public static bool TryNormalizeStatus(string? value, out string normalized)
    {
        normalized = value?.Trim() switch
        {
            var item when string.Equals(item, OrganizationMemberProfessionalProfile.Declared, StringComparison.OrdinalIgnoreCase) => OrganizationMemberProfessionalProfile.Declared,
            var item when string.Equals(item, OrganizationMemberProfessionalProfile.Verified, StringComparison.OrdinalIgnoreCase) => OrganizationMemberProfessionalProfile.Verified,
            var item when string.Equals(item, OrganizationMemberProfessionalProfile.Rejected, StringComparison.OrdinalIgnoreCase) => OrganizationMemberProfessionalProfile.Rejected,
            _ => string.Empty
        };
        return normalized.Length > 0;
    }

    public static bool TryNormalizeSource(string? value, out string normalized)
    {
        normalized = value?.Trim() switch
        {
            var item when string.Equals(item, MemberDeclared, StringComparison.OrdinalIgnoreCase) => MemberDeclared,
            var item when string.Equals(item, ManagerConfirmed, StringComparison.OrdinalIgnoreCase) => ManagerConfirmed,
            var item when string.Equals(item, Imported, StringComparison.OrdinalIgnoreCase) => Imported,
            _ => string.Empty
        };
        return normalized.Length > 0;
    }

    public static string ToKey(string value)
    {
        var decomposed = (value ?? string.Empty).Trim().Normalize(NormalizationForm.FormD);
        var ascii = new string(decomposed.Where(character =>
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray());
        return KeyCleanupRegex().Replace(ascii.Normalize(NormalizationForm.FormC).ToLowerInvariant(), "-").Trim('-');
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex KeyCleanupRegex();
}
