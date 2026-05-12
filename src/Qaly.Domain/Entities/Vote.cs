namespace Qaly.Domain.Entities;

public class Vote : BaseEntity
{
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public Guid UserId { get; set; }
    public int Value { get; set; }

    public User User { get; set; } = null!;
}
