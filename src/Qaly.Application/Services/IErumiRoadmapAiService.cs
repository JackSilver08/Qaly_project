using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

/// <summary>
/// Service interface governing the AI-powered Project Roadmap and Timeline capabilities.
/// Supports Fast Action triggers, WBS decomposition, risk auditing, What-If simulation,
/// and strict permission-guarded snapshot diff approval and rollback.
/// </summary>
public interface IErumiRoadmapAiService
{
    /// <summary>
    /// Free-form chat interaction with Erumi AI to discuss and generate roadmap proposals.
    /// </summary>
    Task<Result<ErumiRoadmapChatResponseDto>> ChatAndProposeRoadmapAsync(ErumiRoadmapChatRequestDto dto, CancellationToken ct = default);

    /// <summary>
    /// Executes a 1-Click Fast Access action (e.g. ExpandPhase, AuditRisks, AutoBalance, BreakdownWBS, Forecast).
    /// </summary>
    Task<Result<ErumiRoadmapDiffProposalDto>> ExecuteFastActionAsync(ErumiRoadmapActionRequestDto dto, CancellationToken ct = default);

    /// <summary>
    /// Simulates a What-If scenario in an isolated sandbox without modifying database entities.
    /// </summary>
    Task<Result<ErumiRoadmapSimulationResultDto>> SimulateScenarioAsync(ErumiRoadmapActionRequestDto dto, CancellationToken ct = default);

    /// <summary>
    /// Generates an executive brief suitable for management and client presentation.
    /// </summary>
    Task<Result<ErumiRoadmapExecutiveBriefDto>> GenerateExecutiveBriefAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Approves and applies an AI proposal diff into the real project database.
    /// Strictly restricted to Project Owners and authorized Project Admins.
    /// </summary>
    Task<Result> ApproveRoadmapProposalAsync(ApproveErumiRoadmapProposalDto dto, CancellationToken ct = default);

    /// <summary>
    /// Rolls back an approved snapshot within the retention window (72 hours).
    /// Strictly restricted to Project Owners and authorized Project Admins.
    /// </summary>
    Task<Result> RollbackRoadmapSnapshotAsync(RollbackErumiRoadmapSnapshotDto dto, CancellationToken ct = default);
}
