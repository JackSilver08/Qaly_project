using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public sealed class AgentRunService : IAgentRunService
{
    private const string SchemaId = "Qaly.AgentRun.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IRepository<AiJobItem> _runRepo;
    private readonly IAiWorkflowService _workflow;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public AgentRunService(
        IRepository<AiJobItem> runRepo,
        IAiWorkflowService workflow,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _runRepo = runRepo;
        _workflow = workflow;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AgentRunDto>> StartAsync(StartAgentRunDto request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid userId)
            return Result.Forbidden<AgentRunDto>();
        if (string.IsNullOrWhiteSpace(request.Goal))
            return Result.Failure<AgentRunDto>("goal is required.", 400);

        var goal = request.Goal.Trim();
        var events = new List<AgentRunEventDto>
        {
            Event("run.started", "Đã tiếp nhận mục tiêu.", 5),
            Event("context.loading", "Đang đọc dữ liệu và quyền trong dự án.", 20),
            Event("plan.started", "Đang lập kế hoạch thực thi.", 40)
        };

        var workflowResult = await _workflow.CreateJobAsync(new CreateAiJobDto(
            "erumi_autonomous_tasks",
            request.ProjectId,
            "agent_run",
            null,
            "microsoft-agent-framework",
            false,
            goal), ct);

        if (!workflowResult.IsSuccess || workflowResult.Data?.DraftId is not Guid draftId)
            return Result.Failure<AgentRunDto>(workflowResult.Error ?? "Agent could not create a plan.", workflowResult.StatusCode);

        events.Add(Event("plan.completed", "Đã tạo kế hoạch task có cấu trúc.", 70));
        events.Add(Event("approval.required", "Kế hoạch sẵn sàng và đang chờ bạn xác nhận.", 75));

        var state = new AgentRunState(goal, 75, "Chờ xác nhận", draftId, events);
        var run = new AiJobItem
        {
            ProjectId = request.ProjectId,
            RequestedBy = userId,
            JobType = "erumi_agent_run",
            SchemaId = SchemaId,
            Status = "awaiting_approval",
            Priority = 100,
            Sensitive = false,
            ProviderHint = "microsoft-agent-framework",
            PayloadJson = JsonSerializer.Serialize(new { goal }, JsonOptions),
            ResultJson = JsonSerializer.Serialize(state, JsonOptions),
            EstimatedCostUsd = workflowResult.Data.EstimatedCostUsd,
            StartedAt = DateTimeOffset.UtcNow,
            MaxRetry = 1
        };

        await _runRepo.AddAsync(run, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Created(ToDto(run, state));
    }

    public async Task<Result<AgentRunDto>> GetAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await FindOwnedRunAsync(runId, ct);
        if (run == null) return Result.Failure<AgentRunDto>("Agent run was not found.", 404);
        return Result.Success(ToDto(run, ReadState(run)));
    }

    public async Task<Result<AiDraftConfirmResultDto>> ApproveAsync(
        Guid runId,
        ApproveAgentRunDto request,
        CancellationToken ct = default)
    {
        var run = await FindOwnedRunAsync(runId, ct);
        if (run == null) return Result.Failure<AiDraftConfirmResultDto>("Agent run was not found.", 404);
        if (!string.Equals(run.Status, "awaiting_approval", StringComparison.OrdinalIgnoreCase))
            return Result.Failure<AiDraftConfirmResultDto>("Agent run is not waiting for approval.", 409);

        var state = ReadState(run);
        if (state.DraftId is not Guid draftId)
            return Result.Failure<AiDraftConfirmResultDto>("Agent run has no executable draft.", 409);

        state.Events.Add(Event("execution.started", "Đang thực thi kế hoạch đã duyệt.", 85));
        run.Status = "running";
        run.ResultJson = JsonSerializer.Serialize(state with { Progress = 85, CurrentStep = "Đang thực thi" }, JsonOptions);
        await _runRepo.UpdateAsync(run, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var result = await _workflow.ConfirmDraftAsync(draftId, new ConfirmAiDraftDto(
            request.EditedPayloadJson,
            request.Action,
            request.Note ?? "Handled by Erumi agent run"), ct);

        if (!result.IsSuccess)
        {
            run.Status = "failed";
            run.ErrorCode = "EXECUTION_FAILED";
            run.ErrorMessage = result.Error;
            state.Events.Add(Event("run.failed", result.Error ?? "Không thể thực thi kế hoạch.", 85));
        }
        else
        {
            var rejected = string.Equals(request.Action, "reject", StringComparison.OrdinalIgnoreCase);
            run.Status = rejected ? "canceled" : "succeeded";
            run.FinishedAt = DateTimeOffset.UtcNow;
            state.Events.Add(Event(
                rejected ? "run.canceled" : "verification.completed",
                rejected ? "Kế hoạch đã được hủy." : "Đã thực thi và kiểm tra kết quả.",
                100));
        }

        run.ResultJson = JsonSerializer.Serialize(state with
        {
            Progress = result.IsSuccess ? 100 : 85,
            CurrentStep = result.IsSuccess ? "Hoàn tất" : "Thực thi thất bại"
        }, JsonOptions);
        await _runRepo.UpdateAsync(run, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    private async Task<AiJobItem?> FindOwnedRunAsync(Guid runId, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId) return null;
        return await _runRepo.GetQueryable()
            .FirstOrDefaultAsync(run => run.Id == runId && run.RequestedBy == userId && run.JobType == "erumi_agent_run", ct);
    }

    private static AgentRunState ReadState(AiJobItem run)
    {
        try
        {
            return JsonSerializer.Deserialize<AgentRunState>(run.ResultJson ?? "{}", JsonOptions)
                ?? new AgentRunState(string.Empty, 0, run.Status, null, []);
        }
        catch (JsonException)
        {
            return new AgentRunState(string.Empty, 0, run.Status, null, []);
        }
    }

    private static AgentRunDto ToDto(AiJobItem run, AgentRunState state) => new(
        run.Id,
        run.ProjectId ?? Guid.Empty,
        state.Goal,
        run.Status,
        state.Progress,
        state.CurrentStep,
        string.Equals(run.Status, "awaiting_approval", StringComparison.OrdinalIgnoreCase),
        state.DraftId,
        state.Events,
        run.ErrorMessage);

    private static AgentRunEventDto Event(string type, string message, int progress)
        => new(type, message, DateTimeOffset.UtcNow, progress);

    private sealed record AgentRunState(
        string Goal,
        int Progress,
        string CurrentStep,
        Guid? DraftId,
        List<AgentRunEventDto> Events);
}
