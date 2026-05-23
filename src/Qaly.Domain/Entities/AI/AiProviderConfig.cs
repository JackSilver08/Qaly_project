using System;

namespace Qaly.Domain.Entities;

public class AiProviderConfig : BaseEntity
{
    public Guid? TenantId { get; set; }
    public string ProviderName { get; set; } = null!;
    public string ModelName { get; set; } = null!;
    public string Purpose { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
    public int PriorityOrder { get; set; } = 100;
    public int MaxInputTokens { get; set; } = 8000;
    public int MaxOutputTokens { get; set; } = 1500;
    public decimal? CostInputPer1MUsd { get; set; }
    public decimal? CostOutputPer1MUsd { get; set; }
    public string DataPolicy { get; set; } = "standard"; // standard|no_cloud_sensitive|mock_only
}