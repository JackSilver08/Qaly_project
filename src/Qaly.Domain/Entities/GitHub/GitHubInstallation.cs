namespace Qaly.Domain.Entities.GitHub;

/// <summary>
/// Một lần cài đặt GitHub App vào một organization/account GitHub, gắn với một
/// tenant Qaly. Không lưu installation token lâu dài — token được tạo khi cần.
/// </summary>
public class GitHubInstallation : BaseEntity, ITenantScoped
{
    /// <summary>Tenant Qaly sở hữu kết nối này.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Installation ID do GitHub cấp (số).</summary>
    public long InstallationId { get; set; }

    /// <summary>GitHub account ID (số).</summary>
    public long AccountId { get; set; }

    /// <summary>Organization hoặc username GitHub.</summary>
    public string AccountLogin { get; set; } = string.Empty;

    /// <summary>Organization hoặc User.</summary>
    public string AccountType { get; set; } = "Organization";

    /// <summary>Người thực hiện kết nối.</summary>
    public Guid InstalledByUserId { get; set; }

    /// <summary>Active, Suspended, Removed.</summary>
    public string Status { get; set; } = "Active";

    // Navigation
    public Organization Organization { get; set; } = null!;
    public ICollection<GitHubRepositoryConnection> RepositoryConnections { get; set; } = new List<GitHubRepositoryConnection>();
}
