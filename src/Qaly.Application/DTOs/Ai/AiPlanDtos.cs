using System;
using System.Collections.Generic;

namespace Qaly.Application.DTOs.Ai;

public record GeneratePlanRequestDto(
    string UserPrompt,
    Guid? ProjectId = null
);

public record GeneratedPlanDto(
    bool IsNewProject,
    string? ProjectName,
    string? ProjectDescription,
    List<GeneratedPlanTaskDto> Tasks
);

public record GeneratedPlanTaskDto(
    string Title,
    string? Description,
    string Priority = "Medium",
    int DueDateOffsetDays = 7,
    int? EstimatedHours = null
);

public record ConfirmPlanRequestDto(
    bool IsNewProject,
    string? ProjectName,
    string? ProjectDescription,
    Guid? ProjectId,
    List<ConfirmPlanTaskDto> Tasks
);

public record ConfirmPlanTaskDto(
    string Title,
    string? Description,
    string Priority,
    DateTimeOffset? DueDate,
    int? EstimatedHours = null
);

public record CreatePlanTaskFailureDto(
    string? Title,
    string Error
);
