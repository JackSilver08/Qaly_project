using System;

namespace Qaly.Domain.Entities;

public class AiBudgetPolicy : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public decimal DailyBudgetUsd { get; set; } = 2.0000m;
    public decimal MonthlyBudgetUsd { get; set; } = 30.0000m;
    public int WarnAtPercent { get; set; } = 80;
    public bool HardStopEnabled { get; set; } = true;
    public bool AllowCloudForSensitive { get; set; }
    public Guid? CreatedBy { get; set; }
}