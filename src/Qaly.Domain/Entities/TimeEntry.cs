using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Qaly.Domain.Entities;

public class TimeEntry : BaseEntity
{
    public Guid TaskId { get; set; }
    public TaskItem Task { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    public int? ManualMinutes { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    [NotMapped]
    public int TotalMinutes
    {
        get
        {
            if (ManualMinutes.HasValue) return ManualMinutes.Value;
            if (EndedAt.HasValue) return (int)(EndedAt.Value - StartedAt).TotalMinutes;
            return (int)(DateTimeOffset.UtcNow - StartedAt).TotalMinutes;
        }
    }
}
