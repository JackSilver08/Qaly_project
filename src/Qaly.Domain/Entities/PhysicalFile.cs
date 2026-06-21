namespace Qaly.Domain.Entities;

public class PhysicalFile : BaseEntity
{
    public string ContentHash { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int ReferenceCount { get; set; } = 1;
}
