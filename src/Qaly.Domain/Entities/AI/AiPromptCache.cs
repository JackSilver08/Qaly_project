using System;

namespace Qaly.Domain.Entities;

public class AiPromptCache : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public string CacheKey { get; set; } = null!;
    public string JobType { get; set; } = null!;
    public string SchemaId { get; set; } = null!;
    public string? ProviderName { get; set; }
    public string? ModelName { get; set; }
    public string RequestHash { get; set; } = null!;
    public string ResponseJson { get; set; } = null!;
    public int HitCount { get; set; } = 0;
    public DateTimeOffset? ExpiresAt { get; set; }
}