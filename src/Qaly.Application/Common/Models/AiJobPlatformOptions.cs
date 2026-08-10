namespace Qaly.Application.Common.Models;

public sealed class AiJobPlatformOptions
{
    public const string SectionName = "AiJobsV4";

    public bool Enabled { get; set; }
    public bool WorkerEnabled { get; set; }
    public bool BudgetUiEnabled { get; set; }
    public bool TaskSkillSuggestionEnabled { get; set; }
    public bool ActionComposerEnabled { get; set; }
    public bool ActionComposerTaskCreateEnabled { get; set; }
    public bool AssistantSessionEnabled { get; set; }
    public bool AssistantContextRegistryEnabled { get; set; }
    public bool AssistantResearchPlanEnabled { get; set; }
    public bool AssistantGoalPlannerEnabled { get; set; }
    public bool AssistantReadOnlyLoopEnabled { get; set; }
    public bool AssistantProgressiveInteractionEnabled { get; set; }
    public bool ProjectLaunchBriefEnabled { get; set; }
    public bool ProjectLaunchPlanningEnabled { get; set; }
    public bool ProjectLaunchExecutionEnabled { get; set; }
    public bool ProjectOperationMonitoringEnabled { get; set; }
    public bool SafeTestOrchestratorEnabled { get; set; }
    public int SafeTestTimeoutSeconds { get; set; } = 900;
    public int PollIntervalMilliseconds { get; set; } = 1000;
    public int LeaseSeconds { get; set; } = 120;
    public int HeartbeatSeconds { get; set; } = 30;
    public int BatchSize { get; set; } = 4;
    public int MaxAttempts { get; set; } = 3;
    public int BaseRetrySeconds { get; set; } = 5;
    public bool AllowProviderDegradedMock { get; set; }
    public string[] EnabledJobTypes { get; set; } = [];
}
