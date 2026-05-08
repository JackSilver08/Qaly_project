using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Qaly.Domain.Entities;

public class WikiPage : BaseEntity
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    // Foreign keys
    public Guid ProjectId { get; set; }
    public Guid AuthorId { get; set; }

    // Navigation properties
    public Project Project { get; set; } = null!;
    public User Author { get; set; } = null!;

    public new DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
