namespace Qaly.Domain.Entities;

public class GroupAttachment : BaseEntity
{
    public Guid WorkGroupId { get; set; }
    public Guid UploadedById { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSize { get; set; }

    public WorkGroup WorkGroup { get; set; } = null!;
    public User UploadedBy { get; set; } = null!;
}
