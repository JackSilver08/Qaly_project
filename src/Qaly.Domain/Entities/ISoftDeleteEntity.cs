namespace Qaly.Domain.Entities;

public interface ISoftDeleteEntity
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
}
