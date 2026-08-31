using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services.AI;

public sealed class AiAssistantSessionService : IAiAssistantSessionService
{
    private const int MaxTitleLength = 160;
    private const int MaxMessageLength = 8000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly QalyDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IProjectService _projectService;
    private readonly IAiAssistantContextRegistry _contextRegistry;
    private readonly IAiAssistantGoalPlanner _goalPlanner;
    private readonly IErumiChatService _chatService;
    private readonly IAiSafeTestOrchestratorService _safeTestOrchestrator;
    private readonly IAiCostService? _costService;
    private readonly AiJobPlatformOptions _options;

    public AiAssistantSessionService(
        QalyDbContext db,
        ICurrentUserService currentUser,
        IProjectService projectService,
        IAiAssistantContextRegistry contextRegistry,
        IAiAssistantGoalPlanner goalPlanner,
        IErumiChatService chatService,
        IAiSafeTestOrchestratorService safeTestOrchestrator,
        IOptions<AiJobPlatformOptions> options,
        IAiCostService? costService = null)
    {
        _db = db;
        _currentUser = currentUser;
        _projectService = projectService;
        _contextRegistry = contextRegistry;
        _goalPlanner = goalPlanner;
        _chatService = chatService;
        _safeTestOrchestrator = safeTestOrchestrator;
        _costService = costService;
        _options = options.Value;
    }

    public async Task<Result<AiAssistantSessionDto>> CreateAsync(
        CreateAiAssistantSessionRequestDto request,
        CancellationToken ct = default)
    {
        var enabled = EnsureEnabled<AiAssistantSessionDto>();
        if (enabled != null) return enabled;
        if (_currentUser.UserId is not Guid userId)
        {
            return Result.Forbidden<AiAssistantSessionDto>();
        }

        Guid? tenantId = null;
        var projectId = request.Context?.ProjectId;
        if (projectId.HasValue)
        {
            var projectResult = await _projectService.GetByIdAsync(projectId.Value, ct);
            if (!projectResult.IsSuccess || projectResult.Data == null)
            {
                return Result.NotFound<AiAssistantSessionDto>();
            }

            tenantId = projectResult.Data.OrganizationId;
        }

        var now = DateTimeOffset.UtcNow;
        var session = new AssistantSession
        {
            OwnerUserId = userId,
            TenantId = tenantId,
            ProjectId = projectId,
            Title = NormalizeTitle(request.Title),
            Status = "active",
            Version = 0,
            LastSequence = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.AssistantSessions.Add(session);
        _db.AiAuditEvents.Add(BuildAudit(
            userId,
            tenantId,
            projectId,
            "assistant_session.created",
            "AssistantSession",
            session.Id,
            "accepted"));
        await _db.SaveChangesAsync(ct);

        return Result.Created(MapSession(session));
    }

    public async Task<Result<AiAssistantSessionDto>> GetAsync(Guid sessionId, CancellationToken ct = default)
    {
        var enabled = EnsureEnabled<AiAssistantSessionDto>();
        if (enabled != null) return enabled;
        if (_currentUser.UserId is not Guid userId)
        {
            return Result.Forbidden<AiAssistantSessionDto>();
        }

        var session = await SessionQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sessionId && item.OwnerUserId == userId, ct);

        return session == null
            ? Result.NotFound<AiAssistantSessionDto>()
            : Result.Success(MapSession(session));
    }

    public async Task<Result<AiAssistantSessionDto?>> GetRecentAsync(CancellationToken ct = default)
    {
        var enabled = EnsureEnabled<AiAssistantSessionDto?>();
        if (enabled != null) return enabled;
        if (_currentUser.UserId is not Guid userId)
        {
            return Result.Forbidden<AiAssistantSessionDto?>();
        }

        var session = await SessionQuery()
            .AsNoTracking()
            .Where(item => item.OwnerUserId == userId && item.Status == "active" && item.ArchivedAt == null)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return Result.Success(session == null ? null : MapSession(session));
    }

    public async Task<Result<IReadOnlyList<AiAssistantSessionSummaryDto>>> ListAsync(
        bool includeArchived = false,
        CancellationToken ct = default)
    {
        var enabled = EnsureEnabled<IReadOnlyList<AiAssistantSessionSummaryDto>>();
        if (enabled != null) return enabled;
        if (_currentUser.UserId is not Guid userId)
            return Result.Forbidden<IReadOnlyList<AiAssistantSessionSummaryDto>>();

        var query = _db.AssistantSessions.AsNoTracking()
            .Where(item => item.OwnerUserId == userId && item.Status != "deleted");
        if (!includeArchived)
            query = query.Where(item => item.Status == "active" && item.ArchivedAt == null);

        var sessions = await query
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Select(item => new AiAssistantSessionSummaryDto(
                item.Id,
                item.Title,
                item.Status,
                item.Version,
                item.ProjectId,
                item.CreatedAt,
                item.UpdatedAt,
                item.ArchivedAt,
                item.Turns.Count,
                item.Turns.OrderByDescending(turn => turn.Sequence)
                    .Select(turn => turn.AssistantResponse ?? turn.UserMessage)
                    .FirstOrDefault()))
            .Take(100)
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<AiAssistantSessionSummaryDto>>(sessions);
    }

    public async Task<Result<AiAssistantSessionDto>> RenameAsync(
        Guid sessionId,
        UpdateAiAssistantSessionRequestDto request,
        CancellationToken ct = default)
    {
        var sessionResult = await GetOwnedMutableSessionAsync(sessionId, request.ExpectedVersion, ct);
        if (!sessionResult.IsSuccess || sessionResult.Data == null)
            return Result.Failure<AiAssistantSessionDto>(sessionResult.Error ?? "Assistant session not found.", sessionResult.StatusCode, sessionResult.ErrorCode);
        var session = sessionResult.Data;
        session.Title = NormalizeTitle(request.Title);
        session.Version++;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success(MapSession(session));
    }

    public async Task<Result<AiAssistantSessionDto>> UpdateScopeAsync(
        Guid sessionId,
        UpdateAiAssistantSessionScopeRequestDto request,
        CancellationToken ct = default)
    {
        var enabled = EnsureEnabled<AiAssistantSessionDto>();
        if (enabled != null) return enabled;
        var sessionResult = await GetOwnedMutableSessionAsync(sessionId, request.ExpectedVersion, ct);
        if (!sessionResult.IsSuccess || sessionResult.Data == null)
            return Result.Failure<AiAssistantSessionDto>(
                sessionResult.Error ?? "Assistant session not found.",
                sessionResult.StatusCode,
                sessionResult.ErrorCode);

        var session = sessionResult.Data;
        Guid? tenantId = null;
        if (request.ProjectId.HasValue)
        {
            var projectResult = await _projectService.GetByIdAsync(request.ProjectId.Value, ct);
            if (!projectResult.IsSuccess || projectResult.Data == null)
                return Result.NotFound<AiAssistantSessionDto>();
            tenantId = projectResult.Data.OrganizationId;
        }

        if (session.ProjectId == request.ProjectId && session.TenantId == tenantId)
            return Result.Success(MapSession(session));

        session.ProjectId = request.ProjectId;
        session.TenantId = tenantId;
        session.Version++;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        _db.AiAuditEvents.Add(BuildAudit(
            session.OwnerUserId,
            session.TenantId,
            session.ProjectId,
            "assistant_session.scope_changed",
            "AssistantSession",
            session.Id,
            "accepted"));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<AiAssistantSessionDto>(
                "The assistant session changed. Reload before updating its scope.",
                409,
                "assistant_session_stale");
        }

        return Result.Success(MapSession(session));
    }

    public async Task<Result<AiAssistantSessionDto>> ArchiveAsync(
        Guid sessionId,
        long expectedVersion,
        CancellationToken ct = default)
    {
        var sessionResult = await GetOwnedMutableSessionAsync(sessionId, expectedVersion, ct);
        if (!sessionResult.IsSuccess || sessionResult.Data == null)
            return Result.Failure<AiAssistantSessionDto>(sessionResult.Error ?? "Assistant session not found.", sessionResult.StatusCode, sessionResult.ErrorCode);
        var session = sessionResult.Data;
        session.Status = "archived";
        session.ArchivedAt = DateTimeOffset.UtcNow;
        session.ClarificationDraftJson = null;
        session.Version++;
        session.UpdatedAt = session.ArchivedAt;
        await _db.SaveChangesAsync(ct);
        return Result.Success(MapSession(session));
    }

    public async Task<Result> DeleteAsync(
        Guid sessionId,
        long expectedVersion,
        CancellationToken ct = default)
    {
        var sessionResult = await GetOwnedMutableSessionAsync(sessionId, expectedVersion, ct);
        if (!sessionResult.IsSuccess || sessionResult.Data == null)
            return Result.Failure(sessionResult.Error ?? "Assistant session not found.", sessionResult.StatusCode, sessionResult.ErrorCode);
        var session = sessionResult.Data;
        session.Status = "deleted";
        session.ArchivedAt ??= DateTimeOffset.UtcNow;
        session.ClarificationDraftJson = null;
        session.Version++;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<AiAssistantSessionDto>> SaveClarificationDraftAsync(
        Guid sessionId,
        UpdateAiAssistantClarificationDraftRequestDto request,
        CancellationToken ct = default)
    {
        var sessionResult = await GetOwnedMutableSessionAsync(sessionId, request.ExpectedVersion, ct);
        if (!sessionResult.IsSuccess || sessionResult.Data == null)
            return Result.Failure<AiAssistantSessionDto>(sessionResult.Error ?? "Assistant session not found.", sessionResult.StatusCode, sessionResult.ErrorCode);
        if (request.OriginTurnId == Guid.Empty || string.IsNullOrWhiteSpace(request.OriginalMessage) ||
            request.Questions.Count is < 1 or > 3 ||
            request.Questions.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != request.Questions.Count ||
            request.Answers.Select(item => item.QuestionId).Distinct(StringComparer.Ordinal).Count() != request.Answers.Count ||
            request.Answers.Any(answer => string.IsNullOrWhiteSpace(answer.Value) ||
                !request.Questions.Any(question => question.Id == answer.QuestionId)))
        {
            return Result.Failure<AiAssistantSessionDto>(
                "Clarification draft is invalid.", 400, "assistant_clarification_draft_invalid");
        }

        var session = sessionResult.Data;
        if (!session.Turns.Any(turn => turn.Id == request.OriginTurnId && turn.Status == "completed"))
            return Result.Failure<AiAssistantSessionDto>(
                "The clarification origin turn is unavailable.", 409, "assistant_clarification_origin_stale");
        var draft = new AiAssistantClarificationDraftDto(
            request.OriginTurnId,
            request.OriginalMessage.Trim(),
            request.RequestedCapabilityId,
            request.Questions,
            request.Answers,
            DateTimeOffset.UtcNow);
        session.ClarificationDraftJson = JsonSerializer.Serialize(draft, JsonOptions);
        session.Version++;
        session.UpdatedAt = draft.UpdatedAt;
        await _db.SaveChangesAsync(ct);
        return Result.Success(MapSession(session));
    }

    public async Task<Result<AiAssistantSessionDto>> ClearClarificationDraftAsync(
        Guid sessionId,
        long expectedVersion,
        CancellationToken ct = default)
    {
        var sessionResult = await GetOwnedMutableSessionAsync(sessionId, expectedVersion, ct);
        if (!sessionResult.IsSuccess || sessionResult.Data == null)
            return Result.Failure<AiAssistantSessionDto>(sessionResult.Error ?? "Assistant session not found.", sessionResult.StatusCode, sessionResult.ErrorCode);
        var session = sessionResult.Data;
        session.ClarificationDraftJson = null;
        session.Version++;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success(MapSession(session));
    }

    public async Task<Result<AiAssistantTurnResponseDto>> AppendTurnAsync(
        AiAssistantTurnRequestDto request,
        string idempotencyKey,
        string correlationId,
        CancellationToken ct = default)
    {
        var enabled = EnsureEnabled<AiAssistantTurnResponseDto>();
        if (enabled != null) return enabled;
        if (_currentUser.UserId is not Guid userId)
        {
            return Result.Forbidden<AiAssistantTurnResponseDto>();
        }

        var message = request.Message?.Trim() ?? string.Empty;
        if (message.Length == 0 || message.Length > MaxMessageLength)
        {
            return Result.Failure<AiAssistantTurnResponseDto>(
                $"message must contain between 1 and {MaxMessageLength} characters.",
                400,
                AiErrorCodes.InvalidRequest);
        }

        var progressiveReplies = EffectiveProgressiveReplies(request);
        if (progressiveReplies.Count > 3 ||
            progressiveReplies.Any(item => string.IsNullOrWhiteSpace(item.QuestionId) || string.IsNullOrWhiteSpace(item.Value)) ||
            progressiveReplies.Select(item => item.QuestionId).Distinct(StringComparer.Ordinal).Count() != progressiveReplies.Count)
        {
            return Result.Failure<AiAssistantTurnResponseDto>(
                "progressiveReplies must contain at most three unique, non-empty answers.",
                400,
                "assistant_progressive_replies_invalid");
        }

        if (request.SessionId is not Guid sessionId || sessionId == Guid.Empty ||
            request.ClientTurnId is not Guid clientTurnId || clientTurnId == Guid.Empty ||
            request.ExpectedVersion is not long expectedVersion || expectedVersion < 0 ||
            string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Result.Failure<AiAssistantTurnResponseDto>(
                "sessionId, expectedVersion, clientTurnId and Idempotency-Key are required.",
                400,
                AiErrorCodes.InvalidRequest);
        }

        idempotencyKey = idempotencyKey.Trim();
        if (idempotencyKey.Length > 128)
        {
            return Result.Failure<AiAssistantTurnResponseDto>(
                "Idempotency-Key is too long.",
                400,
                AiErrorCodes.InvalidRequest);
        }

        var session = await SessionQuery()
            .SingleOrDefaultAsync(item => item.Id == sessionId && item.OwnerUserId == userId, ct);
        if (session == null)
        {
            return Result.NotFound<AiAssistantTurnResponseDto>();
        }

        var requestHash = ComputeRequestHash(request);
        var duplicate = session.Turns.FirstOrDefault(item =>
            item.ClientTurnId == clientTurnId || item.IdempotencyKey == idempotencyKey);
        if (duplicate != null)
        {
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(duplicate.RequestHash),
                    Encoding.UTF8.GetBytes(requestHash)))
            {
                return Result.Failure<AiAssistantTurnResponseDto>(
                    "The client turn or idempotency key was already used for another request.",
                    409,
                    "assistant_idempotency_conflict");
            }

            var replay = MapCompletedResponse(session, duplicate, replayed: true);
            return replay == null
                ? Result.Failure<AiAssistantTurnResponseDto>(
                    "The existing turn has not completed. Reload the session before retrying.",
                    409,
                    "assistant_turn_in_progress")
                : Result.Success(replay);
        }

        if (!string.Equals(session.Status, "active", StringComparison.Ordinal) || session.ArchivedAt.HasValue)
        {
            return Result.Failure<AiAssistantTurnResponseDto>(
                "The assistant session is not active.",
                409,
                "assistant_session_inactive");
        }

        if (session.Version != expectedVersion)
        {
            return Result.Failure<AiAssistantTurnResponseDto>(
                "The assistant session changed. Reload it before sending another turn.",
                409,
                "assistant_session_stale");
        }

        if (session.ProjectId.HasValue && request.Context?.ProjectId != session.ProjectId)
        {
            return Result.NotFound<AiAssistantTurnResponseDto>();
        }

        // Session history is server-owned memory. Enrich the request before discovery and
        // planning so short follow-ups such as "thử luôn" or "tiếp tục" retain the intent
        // established by the previous turn. The client is never trusted as the source of
        // durable conversation state.
        var serverHistory = BuildServerHistory(session);
        var requestWithMemory = request with
        {
            Message = message,
            History = serverHistory
        };

        AiAssistantGoalPlanningResultDto? planning = null;
        Result<AiAssistantExecutionContextDto> contextResult;
        if (_options.AssistantGoalPlannerEnabled)
        {
            var discoveryResult = await _contextRegistry.DiscoverAsync(requestWithMemory, ct);
            if (!discoveryResult.IsSuccess || discoveryResult.Data == null)
                return Result.Failure<AiAssistantTurnResponseDto>(
                    discoveryResult.Error ?? "Assistant skills could not be authorized.",
                    discoveryResult.StatusCode, discoveryResult.ErrorCode);
            var planningResult = await _goalPlanner.PlanAsync(requestWithMemory, discoveryResult.Data, ct);
            if (!planningResult.IsSuccess || planningResult.Data == null)
                return Result.Failure<AiAssistantTurnResponseDto>(
                    planningResult.Error ?? "Assistant goal could not be analysed.",
                    planningResult.StatusCode, planningResult.ErrorCode);
            planning = planningResult.Data;

            // Native mutation capabilities have server-owned contracts and typed renderers.
            // Re-assert that route at the durable session boundary so a provider/custom
            // planner cannot downgrade a valid P16-P24 action into prose-only guidance.
            // Authorization still comes exclusively from discoveryContext; this never
            // grants a capability the current role/project did not already have.
            if (AiAssistantGoalPlanningOutputContract.TryCreateAuthorizedExecutionPlan(
                    requestWithMemory, discoveryResult.Data, out var serverExecutionPlan) &&
                serverExecutionPlan != null)
            {
                planning = serverExecutionPlan;
            }
            contextResult = string.IsNullOrWhiteSpace(planning.SelectedCapabilityId)
                ? Result.Success(planning.GoalAnalysis.Disposition == "policy_blocked"
                    ? discoveryResult.Data with
                    {
                        SourceDisclosures =
                        [
                            new AiAssistantSourceDisclosureDto(
                                "capability.context", "denied",
                                "Capability phù hợp chưa được cấp quyền trong ngữ cảnh hiện tại.",
                                ReasonCode: "capability_not_authorized")
                        ]
                    }
                    : discoveryResult.Data)
                : await _contextRegistry.ResolveAsync(
                    requestWithMemory with { RequestedCapabilityId = planning.SelectedCapabilityId }, ct);
        }
        else
        {
            contextResult = await _contextRegistry.ResolveAsync(requestWithMemory, ct);
        }
        if (!contextResult.IsSuccess || contextResult.Data == null)
        {
            return Result.Failure<AiAssistantTurnResponseDto>(
                contextResult.Error ?? "Assistant context could not be authorized.",
                contextResult.StatusCode,
                contextResult.ErrorCode);
        }
        var executionContext = contextResult.Data;
        if (planning != null && executionContext.Sources.Count > 0)
        {
            var sourceIds = executionContext.Sources.Select(source => source.SourceId).ToArray();
            planning = planning with
            {
                WorkPlan = planning.WorkPlan with
                {
                    Steps = planning.WorkPlan.Steps.Select(step => step.Kind is "call_skill" or "verify"
                        ? step with { SourceIds = sourceIds }
                        : step).ToArray()
                }
            };
        }

        var now = DateTimeOffset.UtcNow;
        var turn = new AssistantTurn
        {
            SessionId = session.Id,
            Sequence = session.LastSequence + 1,
            ClientTurnId = clientTurnId,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            UserMessage = message,
            RequestContextJson = request.Context == null ? null : JsonSerializer.Serialize(request.Context, JsonOptions),
            RequestPayloadJson = JsonSerializer.Serialize(request with { History = null, Files = null }, JsonOptions),
            Status = "running",
            ModelProfile = NormalizeModelProfile(request.ProviderHint),
            ResumedFromTurnId = request.ResumeFromTurnId,
            CorrelationId = NormalizeCorrelationId(correlationId),
            CreatedAt = now
        };

        turn.ProcessEvents.Add(new AssistantProcessEvent
        {
            TurnId = turn.Id,
            Sequence = 1,
            Stage = "accepted",
            Status = "completed",
            PublicLabel = "Đã tiếp nhận yêu cầu",
            StartedAt = now,
            CompletedAt = now,
            DurationMs = 0
        });
        turn.ProcessEvents.Add(new AssistantProcessEvent
        {
            TurnId = turn.Id,
            Sequence = 2,
            Stage = "goal_analysis",
            Status = "completed",
            PublicLabel = "Đang xác định ngữ cảnh và khả năng phù hợp",
            StartedAt = now,
            CompletedAt = now,
            DurationMs = 0
        });
        turn.ProcessEvents.Add(new AssistantProcessEvent
        {
            TurnId = turn.Id,
            Sequence = 3,
            Stage = "context_authorization",
            Status = "completed",
            PublicLabel = "Đã kiểm tra ngữ cảnh, nguồn và quyền truy cập",
            StartedAt = now,
            CompletedAt = now,
            DurationMs = 0
        });
        turn.ProcessEvents.Add(new AssistantProcessEvent
        {
            TurnId = turn.Id,
            Sequence = 4,
            Stage = "capability_handoff",
            Status = "running",
            PublicLabel = "Đang định tuyến capability AI phù hợp",
            StartedAt = now
        });

        session.LastSequence = turn.Sequence;
        if (turn.Sequence == 1 && IsDefaultSessionTitle(session.Title))
            session.Title = BuildSessionTitle(message);
        session.Version++;
        session.UpdatedAt = now;
        turn.Session = session;
        _db.AssistantTurns.Add(turn);
        _db.AiAuditEvents.Add(BuildAudit(
            userId,
            session.TenantId,
            session.ProjectId,
            "assistant_turn.accepted",
            "AssistantTurn",
            turn.Id,
            "running",
            turn.CorrelationId));
        _db.AiAuditEvents.Add(BuildAudit(
            userId,
            session.TenantId,
            session.ProjectId,
            "assistant_context.resolved",
            "AssistantTurn",
            turn.Id,
            "allowed",
            turn.CorrelationId));
        if (planning != null)
        {
            _db.AiAuditEvents.Add(BuildAudit(
                userId,
                session.TenantId,
                session.ProjectId,
                "assistant_goal.planned",
                "AssistantTurn",
                turn.Id,
                planning.GoalAnalysis.Disposition,
                turn.CorrelationId));
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<AiAssistantTurnResponseDto>(
                "The assistant session changed. Reload it before sending another turn.",
                409,
                "assistant_session_stale");
        }
        catch (DbUpdateException)
        {
            return Result.Failure<AiAssistantTurnResponseDto>(
                "The turn could not be accepted because the session changed or the request was duplicated.",
                409,
                "assistant_turn_conflict");
        }

        var routingStartedAt = now;
        Result<AiAssistantTurnResponseDto> coreResult;
        try
        {
            var serverRequest = requestWithMemory;
            var useReadOnlyLoop = _options.AssistantReadOnlyLoopEnabled &&
                planning?.SelectedCapabilityId is { Length: > 0 } selectedCapabilityId &&
                AiAssistantCapabilityCatalog.TryGet(selectedCapabilityId, out var selectedDescriptor) &&
                selectedDescriptor.RiskClass is "read_only" or "read_only_proposal" &&
                selectedDescriptor.Kind is "read" or "artifact";
            if (string.Equals(
                    planning?.SelectedCapabilityId,
                    AiAssistantContextContract.SafeTestRunCapability,
                    StringComparison.Ordinal))
            {
                var preview = await _safeTestOrchestrator.PrepareAsync(session.Id, turn.Id, ct);
                coreResult = preview.IsSuccess && preview.Data != null
                    ? Result.Success(BuildSafeTestPreviewResponse(preview.Data, planning!, executionContext))
                    : Result.Failure<AiAssistantTurnResponseDto>(
                        preview.Error ?? "Safe test preview could not be prepared.",
                        preview.StatusCode,
                        preview.ErrorCode);
            }
            else
            {
                coreResult = useReadOnlyLoop && planning != null
                ? await ExecuteReadOnlyLoopAsync(
                    serverRequest, executionContext, planning, turn, session, ct)
                : planning == null
                    ? await _chatService.AssistantTurnAsync(serverRequest, executionContext, ct)
                    : await _chatService.AssistantPlannedTurnAsync(serverRequest, executionContext, planning, ct);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            coreResult = Result.Failure<AiAssistantTurnResponseDto>(
                "The assistant provider timed out.",
                504,
                "assistant_provider_timeout");
        }
        catch
        {
            coreResult = Result.Failure<AiAssistantTurnResponseDto>(
                "The assistant provider is unavailable.",
                503,
                "assistant_provider_unavailable");
        }

        if ((!coreResult.IsSuccess || coreResult.Data == null) && IsFailSoftEligible(coreResult))
        {
            coreResult = Result.Success(BuildProviderFailSoftResponse(
                request,
                planning,
                NormalizeErrorCode(coreResult.ErrorCode) ?? "assistant_provider_unavailable"));
        }

        var completedAt = DateTimeOffset.UtcNow;
        var routingEvent = turn.ProcessEvents.Single(item => item.Sequence == 4);
        routingEvent.Status = coreResult.IsSuccess ? "completed" : "failed";
        routingEvent.CompletedAt = completedAt;
        routingEvent.DurationMs = SafeDurationMilliseconds(routingStartedAt, completedAt);
        routingEvent.Retryable = !coreResult.IsSuccess && coreResult.StatusCode >= 500;
        routingEvent.SafeErrorCode = coreResult.IsSuccess ? null : NormalizeErrorCode(coreResult.ErrorCode);

        if (!coreResult.IsSuccess || coreResult.Data == null)
        {
            turn.Status = coreResult.ErrorCode == "assistant_turn_canceled" ? "canceled" : "failed";
            turn.SafeErrorCode = NormalizeErrorCode(coreResult.ErrorCode) ?? "assistant_turn_failed";
            turn.CompletedAt = completedAt;
            var failedEvent = new AssistantProcessEvent
            {
                TurnId = turn.Id,
                Sequence = turn.ProcessEvents.Max(item => item.Sequence) + 1,
                Stage = turn.Status,
                Status = turn.Status,
                PublicLabel = "Không thể hoàn tất lượt này; yêu cầu vẫn được lưu để bạn kiểm tra lại",
                StartedAt = completedAt,
                CompletedAt = completedAt,
                DurationMs = 0,
                Retryable = coreResult.StatusCode >= 500,
                SafeErrorCode = turn.SafeErrorCode
            };
            turn.ProcessEvents.Add(failedEvent);
            _db.AssistantProcessEvents.Add(failedEvent);
            _db.AiAuditEvents.Add(BuildAudit(
                userId,
                session.TenantId,
                session.ProjectId,
                turn.Status == "canceled" ? "assistant_turn.canceled" : "assistant_turn.failed",
                "AssistantTurn",
                turn.Id,
                turn.Status,
                turn.CorrelationId,
                turn.SafeErrorCode));
            await _db.SaveChangesAsync(ct);

            return Result.Failure<AiAssistantTurnResponseDto>(
                coreResult.Error ?? "Không thể hoàn tất lượt trợ lý AI.",
                coreResult.StatusCode,
                turn.SafeErrorCode);
        }

        var response = coreResult.Data with
        {
            SessionId = session.Id,
            TurnId = turn.Id,
            Sequence = turn.Sequence,
            SessionVersion = session.Version,
            ClientTurnId = turn.ClientTurnId,
            TurnStatus = "completed",
            CorrelationId = turn.CorrelationId,
            Replayed = false,
            ModelProfile = turn.ModelProfile,
            ActualProvider = coreResult.Data.ResearchPlan?.ActualProvider ??
                coreResult.Data.ProjectLaunchPlan?.ActualProvider ??
                coreResult.Data.ProjectLaunchBrief?.ActualProvider ??
                coreResult.Data.Conversation?.ActualProvider ??
                coreResult.Data.Answer?.Model?.Provider ??
                (coreResult.Data.NativeActionDraft != null ? coreResult.Data.ActualProvider : null) ??
                planning?.GoalAnalysis.ActualProvider ?? "not_reached",
            ActualModel = coreResult.Data.ResearchPlan?.ActualModel ??
                coreResult.Data.ProjectLaunchPlan?.ActualModel ??
                coreResult.Data.ProjectLaunchBrief?.ActualModel ??
                coreResult.Data.Conversation?.ActualModel ??
                coreResult.Data.Answer?.Model?.Id ??
                (coreResult.Data.NativeActionDraft != null ? coreResult.Data.ActualModel : null) ??
                planning?.GoalAnalysis.ActualModel ?? "not_reached",
            GoalAnalysis = planning?.GoalAnalysis ?? coreResult.Data.GoalAnalysis,
            Conversation = _options.AssistantProgressiveInteractionEnabled
                ? coreResult.Data.Conversation
                : null,
            WorkPlan = coreResult.Data.WorkPlan?.Steps.Any(step => step.Kind == "retrieve") == true
                ? coreResult.Data.WorkPlan
                : planning == null
                    ? coreResult.Data.WorkPlan
                    : AiAssistantGoalPlanningOutputContract.CompleteWorkPlan(
                        planning.WorkPlan,
                        coreResult.Data.Disposition)
        };

        var quality = AiAssistantQualityEvaluator.Evaluate(response);
        if (quality.FalseMutationSuccess)
        {
            const string correction = "Kết quả tạo dữ liệu chưa có receipt read-back hợp lệ, vì vậy Qaly không xác nhận thao tác đã hoàn tất. Dữ liệu không được coi là đã tạo; hãy kiểm tra receipt hoặc chạy lại từ bước xác nhận.";
            response = response with
            {
                Disposition = "guided_answer",
                Intent = AiAssistantTurnContract.GuidedAnswerIntent,
                ExecutionPolicy = "verification_required",
                AssistantMessage = correction,
                Conversation = response.Conversation is null ? null : response.Conversation with
                {
                    Answer = correction,
                    ActionDisposition = "verification_required"
                }
            };
            quality = AiAssistantQualityEvaluator.Evaluate(response);
        }
        quality = quality with
        {
            LatencyMs = SafeDurationMilliseconds(turn.CreatedAt, completedAt),
            UsedFallback = response.ActualProvider.Contains("Local", StringComparison.OrdinalIgnoreCase) ||
                response.ActualProvider.Contains("Fallback", StringComparison.OrdinalIgnoreCase)
        };

        turn.Status = "completed";
        turn.Disposition = response.Disposition;
        turn.Intent = response.Intent;
        turn.ExecutionPolicy = response.ExecutionPolicy;
        turn.AssistantResponse = response.AssistantMessage;
        turn.SourceRefsJson = JsonSerializer.Serialize(response.SourceRefs, JsonOptions);
        turn.ActualProvider = response.ActualProvider;
        turn.ActualModel = response.ActualModel;
        turn.CompletedAt = completedAt;

        var hasArtifact = response.Artifact != null || response.ResearchPlan != null ||
            response.ProjectLaunchBrief != null || response.ProjectLaunchPlan != null ||
            response.SafeTestRunPreview != null || response.NativeActionDraft != null;
        var finalEvent = new AssistantProcessEvent
        {
            TurnId = turn.Id,
            Sequence = turn.ProcessEvents.Max(item => item.Sequence) + 1,
            Stage = hasArtifact ? "artifact" : "answer",
            Status = "completed",
            PublicLabel = response.NativeActionDraft != null
                ? "Native action draft is ready for review"
                : response.SafeTestRunPreview != null
                ? "Safe test manifest is ready for review"
                : response.ProjectLaunchPlan != null
                ? "Project launch plan is ready for review"
                : response.ResearchPlan != null
                ? "Đã chuẩn bị Research Plan có nguồn để bạn xem lại"
                : response.Artifact == null
                    ? "Đã hoàn tất phản hồi có kiểm soát"
                    : "Đã chuẩn bị bản nháp để bạn xem lại",
            StartedAt = completedAt,
            CompletedAt = completedAt,
            DurationMs = 0
        };
        turn.ProcessEvents.Add(finalEvent);
        _db.AssistantProcessEvents.Add(finalEvent);

        if (response.Artifact != null)
        {
            var artifactRef = new AssistantArtifactRef
            {
                TurnId = turn.Id,
                SchemaId = response.Artifact.SchemaId,
                SchemaVersion = "v1",
                RendererId = response.Artifact.Kind == "task_action_plan"
                    ? "task-plan-review.v1"
                    : "unknown-safe-fallback.v1"
            };
            turn.ArtifactRefs.Add(artifactRef);
            _db.AssistantArtifactRefs.Add(artifactRef);
        }
        if (response.ResearchPlan != null)
        {
            var researchArtifactRef = new AssistantArtifactRef
            {
                TurnId = turn.Id,
                SchemaId = AiAssistantResearchPlanContract.SchemaId,
                SchemaVersion = "v1",
                RendererId = AiAssistantResearchPlanContract.RendererId
            };
            turn.ArtifactRefs.Add(researchArtifactRef);
            _db.AssistantArtifactRefs.Add(researchArtifactRef);
        }
        if (response.ProjectLaunchBrief != null)
        {
            var launchArtifactRef = new AssistantArtifactRef
            {
                TurnId = turn.Id,
                SchemaId = AiProjectLaunchContract.BriefSchemaId,
                SchemaVersion = "v1",
                RendererId = AiProjectLaunchContract.RendererId
            };
            turn.ArtifactRefs.Add(launchArtifactRef);
            _db.AssistantArtifactRefs.Add(launchArtifactRef);
        }
        if (response.ProjectLaunchPlan != null)
        {
            var planArtifactRef = new AssistantArtifactRef
            {
                TurnId = turn.Id,
                SchemaId = AiProjectOrchestrationContract.PlanSchemaId,
                SchemaVersion = "v1",
                RendererId = AiProjectOrchestrationContract.PlanRendererId
            };
            turn.ArtifactRefs.Add(planArtifactRef);
            _db.AssistantArtifactRefs.Add(planArtifactRef);
        }
        if (response.SafeTestRunPreview != null)
        {
            var testArtifactRef = new AssistantArtifactRef
            {
                TurnId = turn.Id,
                SchemaId = AiSafeTestOrchestratorContract.PreviewSchemaId,
                SchemaVersion = "v1",
                RendererId = AiSafeTestOrchestratorContract.RendererId
            };
            turn.ArtifactRefs.Add(testArtifactRef);
            _db.AssistantArtifactRefs.Add(testArtifactRef);
        }
        if (response.NativeActionDraft != null)
        {
            var nativeArtifactRef = new AssistantArtifactRef
            {
                TurnId = turn.Id,
                SchemaId = response.NativeActionDraft.SchemaId,
                SchemaVersion = "v1",
                RendererId = AiNativeDomainActionContract.RendererId,
                DraftId = response.NativeActionDraft.DraftId
            };
            turn.ArtifactRefs.Add(nativeArtifactRef);
            _db.AssistantArtifactRefs.Add(nativeArtifactRef);
        }

        response = response with { ProcessEvents = MapEvents(turn.ProcessEvents) };
        turn.ResponseJson = JsonSerializer.Serialize(response with { ProcessEvents = null }, JsonOptions);
        if (progressiveReplies.Count > 0)
            session.ClarificationDraftJson = null;
        _db.AiAuditEvents.Add(BuildAudit(
            userId,
            session.TenantId,
            session.ProjectId,
            "assistant_turn.completed",
            "AssistantTurn",
            turn.Id,
            "completed",
            turn.CorrelationId));
        var qualityAudit = BuildAudit(
            userId,
            session.TenantId,
            session.ProjectId,
            "assistant_quality.evaluated",
            "AssistantTurn",
            turn.Id,
            quality.Passed ? "passed" : "failed",
            turn.CorrelationId,
            quality.FailureCodes.Count > 0 ? quality.FailureCodes[0] : null);
        qualityAudit.PolicyVersion = AiAssistantQualityContract.EvaluationSetVersion;
        qualityAudit.AfterJson = JsonSerializer.Serialize(new
        {
            quality.Useful,
            quality.DeadEnd,
            quality.FalseMutationSuccess,
            quality.ProviderIdentityConsistent,
            quality.QuestionLimitValid,
            quality.GuidanceRoutesValid,
            quality.LatencyMs,
            quality.UsedFallback
        }, JsonOptions);
        _db.AiAuditEvents.Add(qualityAudit);
        await _db.SaveChangesAsync(ct);

        return Result.Success(response);
    }

    public async Task<Result<AiAssistantQualityMetricsDto>> GetQualityMetricsAsync(CancellationToken ct = default)
    {
        var enabled = EnsureEnabled<AiAssistantQualityMetricsDto>();
        if (enabled != null) return enabled;
        if (_currentUser.UserId is not Guid userId) return Result.Forbidden<AiAssistantQualityMetricsDto>();
        var rows = await _db.AiAuditEvents.AsNoTracking()
            .Where(item => item.ActorUserId == userId && item.EventType == "assistant_quality.evaluated")
            .OrderByDescending(item => item.CreatedAt)
            .Take(500)
            .Select(item => new { item.Outcome, item.FailureCode, item.AfterJson })
            .ToListAsync(ct);
        var evaluated = rows.Count;
        var passed = rows.Count(item => item.Outcome == "passed");
        var deadEnds = rows.Count(item => item.FailureCode == "assistant_quality_dead_end");
        var falseSuccesses = rows.Count(item => item.FailureCode == "assistant_quality_false_mutation_success");
        var providerMismatches = rows.Count(item => item.FailureCode == "assistant_quality_provider_identity_mismatch");
        var qualityDetails = rows.Select(row => ReadQualityAudit(row.AfterJson)).ToArray();
        var fallbackTurns = qualityDetails.Count(item => item.UsedFallback);
        var averageLatency = qualityDetails.Length == 0 ? 0 : Math.Round(qualityDetails.Average(item => item.LatencyMs), 1);
        return Result.Success(new AiAssistantQualityMetricsDto(
            AiAssistantQualityContract.SchemaId,
            AiAssistantQualityContract.EvaluationSetVersion,
            evaluated,
            passed,
            deadEnds,
            falseSuccesses,
            providerMismatches,
            fallbackTurns,
            averageLatency,
            evaluated == 0 ? 1 : Math.Round((double)passed / evaluated, 4),
            evaluated == 0 ? 0 : Math.Round((double)deadEnds / evaluated, 4),
            DateTimeOffset.UtcNow));
    }

    private static (int LatencyMs, bool UsedFallback) ReadQualityAudit(string? afterJson)
    {
        if (string.IsNullOrWhiteSpace(afterJson)) return (0, false);
        try
        {
            using var document = JsonDocument.Parse(afterJson);
            var root = document.RootElement;
            var latency = root.TryGetProperty("latencyMs", out var latencyValue) && latencyValue.TryGetInt32(out var parsed)
                ? parsed : 0;
            var fallback = root.TryGetProperty("usedFallback", out var fallbackValue) && fallbackValue.ValueKind is JsonValueKind.True;
            return (latency, fallback);
        }
        catch (JsonException)
        {
            return (0, false);
        }
    }

    private static AiAssistantTurnResponseDto BuildSafeTestPreviewResponse(
        AiSafeTestRunPreviewDto preview,
        AiAssistantGoalPlanningResultDto planning,
        AiAssistantExecutionContextDto executionContext)
    {
        const string provider = "Qaly Local Safety Orchestrator";
        const string model = "fixed-manifest-v1";
        var answer = $"Mình đã chuẩn bị manifest kiểm thử cố định gồm {preview.Suites.Count} suite. " +
            $"Ước tính {Math.Max(1, preview.EstimatedSeconds / 60)} phút, chi phí API ngoài = 0. " +
            "Chưa có test process nào chạy; hãy xem danh sách và xác nhận rõ ràng để bắt đầu.";
        return new AiAssistantTurnResponseDto(
            AiAssistantTurnContract.SchemaId,
            "draft_ready",
            AiSafeTestOrchestratorContract.CapabilityId,
            "explicit_confirmation_required",
            answer,
            1,
            null,
            null,
            [],
            ActualProvider: provider,
            ActualModel: model,
            Capabilities: executionContext.Capabilities,
            SourceDisclosures: executionContext.SourceDisclosures,
            GoalAnalysis: planning.GoalAnalysis,
            WorkPlan: planning.WorkPlan,
            Conversation: new AiAssistantConversationTurnDto(
                AiAssistantConversationContract.SchemaId,
                "answered",
                "confirmation_required",
                answer,
                [],
                null,
                null,
                ["review_test_manifest", "confirm_test_run"],
                [],
                1,
                provider,
                model),
            SafeTestRunPreview: preview);
    }

    private static AiAssistantTurnResponseDto BuildProviderFailSoftResponse(
        AiAssistantTurnRequestDto request,
        AiAssistantGoalPlanningResultDto? planning,
        string safeErrorCode)
    {
        var capabilityId = planning?.SelectedCapabilityId;
        var answer = "Model AI tạm thời chưa phản hồi, nhưng yêu cầu của bạn đã được lưu nguyên vẹn. " +
            "Bạn có thể tiếp tục bằng các màn hình Qaly được xác nhận bên dưới hoặc thử lại lượt này; hệ thống chưa thay đổi dữ liệu nào.";
        return new AiAssistantTurnResponseDto(
            AiAssistantTurnContract.SchemaId,
            "guided_answer",
            AiAssistantTurnContract.GuidedAnswerIntent,
            "read_only_failsoft",
            answer,
            0.72,
            null,
            null,
            [],
            ActualProvider: "Qaly Local Guidance",
            ActualModel: "deterministic-failsoft-v1",
            GoalAnalysis: planning?.GoalAnalysis,
            WorkPlan: planning?.WorkPlan,
            Conversation: new AiAssistantConversationTurnDto(
                AiAssistantConversationContract.SchemaId,
                "guided",
                "temporarily_unavailable",
                answer,
                [],
                AiAssistantManualGuidanceRegistry.ForCapability(capabilityId),
                capabilityId == null ? null : new AiAssistantCapabilityGapDto(
                    capabilityId,
                    true,
                    "Khả năng xử lý bằng model đang tạm gián đoạn; dữ liệu chưa bị thay đổi.",
                    safeErrorCode),
                ["retry_saved_turn"],
                [],
                0.72,
                "Qaly Local Guidance",
                "deterministic-failsoft-v1"));
    }

    private static bool IsFailSoftEligible(Result<AiAssistantTurnResponseDto> result)
    {
        if (result.IsSuccess || result.Data != null ||
            string.Equals(result.ErrorCode, "assistant_turn_canceled", StringComparison.Ordinal))
            return false;
        if (result.StatusCode >= 500) return true;
        var code = result.ErrorCode ?? string.Empty;
        return code.Contains("provider", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("schema", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("assistant_loop", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<Result<AiAssistantSessionDto>> CancelTurnAsync(
        Guid turnId,
        long expectedVersion,
        CancellationToken ct = default)
    {
        var enabled = EnsureEnabled<AiAssistantSessionDto>();
        if (enabled != null) return enabled;
        if (_currentUser.UserId is not Guid userId) return Result.Forbidden<AiAssistantSessionDto>();
        var session = await SessionQuery().SingleOrDefaultAsync(
            item => item.OwnerUserId == userId && item.Turns.Any(turn => turn.Id == turnId), ct);
        if (session == null) return Result.NotFound<AiAssistantSessionDto>();
        if (session.Version != expectedVersion)
            return Result.Failure<AiAssistantSessionDto>(
                "The assistant session changed. Reload before canceling.", 409, "assistant_session_stale");
        var turn = session.Turns.Single(item => item.Id == turnId);
        if (turn.Status is "completed" or "failed" or "canceled") return Result.Success(MapSession(session));
        turn.CancellationRequestedAt = DateTimeOffset.UtcNow;
        turn.UpdatedAt = turn.CancellationRequestedAt;
        await _db.SaveChangesAsync(ct);
        return Result.Success(MapSession(session));
    }

    public async Task<Result<AiAssistantTurnResponseDto>> ResumeTurnAsync(
        Guid turnId,
        AiAssistantTurnControlRequestDto request,
        string idempotencyKey,
        string correlationId,
        CancellationToken ct = default)
    {
        var enabled = EnsureEnabled<AiAssistantTurnResponseDto>();
        if (enabled != null) return enabled;
        if (_currentUser.UserId is not Guid userId) return Result.Forbidden<AiAssistantTurnResponseDto>();
        var original = await _db.AssistantTurns.AsNoTracking()
            .Include(item => item.Session)
            .SingleOrDefaultAsync(item => item.Id == turnId && item.Session.OwnerUserId == userId, ct);
        if (original == null) return Result.NotFound<AiAssistantTurnResponseDto>();
        if (original.Session.Version != request.ExpectedVersion)
            return Result.Failure<AiAssistantTurnResponseDto>(
                "The assistant session changed. Reload before resuming.", 409, "assistant_session_stale");
        if (original.Status is not ("failed" or "canceled"))
            return Result.Failure<AiAssistantTurnResponseDto>(
                "Only a failed or canceled turn can be resumed.", 409, "assistant_turn_not_resumable");
        if (string.IsNullOrWhiteSpace(original.RequestPayloadJson))
            return Result.Failure<AiAssistantTurnResponseDto>(
                "This older turn has no resumable request snapshot.", 409, "assistant_turn_snapshot_missing");

        AiAssistantTurnRequestDto? snapshot;
        try { snapshot = JsonSerializer.Deserialize<AiAssistantTurnRequestDto>(original.RequestPayloadJson, JsonOptions); }
        catch (JsonException) { snapshot = null; }
        if (snapshot == null)
            return Result.Failure<AiAssistantTurnResponseDto>(
                "The resumable request snapshot is invalid.", 409, "assistant_turn_snapshot_invalid");
        var resumed = snapshot with
        {
            SessionId = original.SessionId,
            ExpectedVersion = original.Session.Version,
            ClientTurnId = request.ClientTurnId ?? Guid.NewGuid(),
            ResumeFromTurnId = original.Id,
            History = null,
            Files = null
        };
        return await AppendTurnAsync(resumed, idempotencyKey, correlationId, ct);
    }

    private async Task<Result<AiAssistantTurnResponseDto>> ExecuteReadOnlyLoopAsync(
        AiAssistantTurnRequestDto request,
        AiAssistantExecutionContextDto executionContext,
        AiAssistantGoalPlanningResultDto planning,
        AssistantTurn turn,
        AssistantSession session,
        CancellationToken ct)
    {
        if (planning.SelectedCapabilityId is not { Length: > 0 } capabilityId ||
            !AiAssistantCapabilityCatalog.TryGet(capabilityId, out var descriptor) ||
            !executionContext.HasCapability(capabilityId) ||
            descriptor.RiskClass is not ("read_only" or "read_only_proposal") ||
            descriptor.Kind is not ("read" or "artifact"))
        {
            return Result.Failure<AiAssistantTurnResponseDto>(
                "The bounded loop accepts registered read-only capabilities only.",
                403,
                "assistant_loop_capability_denied");
        }

        var sourceIds = executionContext.Sources.Select(item => item.SourceId).Distinct(StringComparer.Ordinal).ToArray();
        if (sourceIds.Any(sourceId => !descriptor.ContextSources.Contains(sourceId, StringComparer.Ordinal)) ||
            executionContext.Sources.Any(source => string.IsNullOrWhiteSpace(source.ContentHash) ||
                string.IsNullOrWhiteSpace(source.PrivacyClass) || string.IsNullOrWhiteSpace(source.TrustClass)))
        {
            return Result.Failure<AiAssistantTurnResponseDto>(
                "An authorized source is outside the selected capability contract.",
                403,
                "assistant_loop_source_denied");
        }

        var sourceRefs = executionContext.Sources.Select(item => item.SourceRef).Distinct(StringComparer.Ordinal).ToArray();
        var steps = new[]
        {
            new AiAssistantWorkPlanStepDto("R1", "retrieve", "Đọc các nguồn Qaly đã được cấp quyền", capabilityId,
                sourceIds, [], null, ["source_authorized", "privacy_preserved"], "none", "planned"),
            new AiAssistantWorkPlanStepDto("A1", "analyze", "Phân tích mục tiêu bằng capability đã đăng ký", capabilityId,
                sourceIds, ["R1"], descriptor.OutputSchemaId, ["schema_valid"], "none", "planned"),
            new AiAssistantWorkPlanStepDto("V1", "verify", "Kiểm tra schema, nguồn và giới hạn read-only", capabilityId,
                sourceIds, ["A1"], descriptor.OutputSchemaId, ["source_grounded", "no_mutation"], "none", "planned"),
            new AiAssistantWorkPlanStepDto("P1", "present", "Trình bày kết quả và bước tiếp theo", null,
                sourceIds, ["V1"], null, ["honest_status"], "none", "planned")
        };
        if (steps.Length > AiAssistantGoalPlanningContract.MaxSteps)
            return Result.Failure<AiAssistantTurnResponseDto>("Read-only plan exceeds the step bound.", 422, "assistant_loop_step_bound");

        var boundedPlan = planning.WorkPlan with
        {
            SelectedSkillIds = [capabilityId],
            Steps = steps,
            MaxSteps = AiAssistantGoalPlanningContract.MaxSteps,
            MaxAttemptsPerStep = AiAssistantGoalPlanningContract.MaxAttemptsPerStep,
            RequiresPlanApproval = false
        };
        var eventByStep = new Dictionary<string, AssistantProcessEvent>(StringComparer.Ordinal);
        foreach (var step in steps)
        {
            var processEvent = new AssistantProcessEvent
            {
                TurnId = turn.Id,
                Sequence = turn.ProcessEvents.Max(item => item.Sequence) + 1,
                Stage = step.Kind,
                Status = "queued",
                PublicLabel = step.PublicLabel,
                StartedAt = DateTimeOffset.UtcNow,
                SafeDetailJson = SerializeLoopDetail(step.StepId, 0, sourceRefs, null, null)
            };
            turn.ProcessEvents.Add(processEvent);
            _db.AssistantProcessEvents.Add(processEvent);
            eventByStep[step.StepId] = processEvent;
        }
        await _db.SaveChangesAsync(ct);

        Result<AiAssistantTurnResponseDto>? coreResult = null;
        var completedStates = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var step in steps)
        {
            var processEvent = eventByStep[step.StepId];
            if (await IsCancellationRequestedAsync(turn.Id, ct))
            {
                processEvent.Status = "blocked";
                processEvent.CompletedAt = DateTimeOffset.UtcNow;
                processEvent.DurationMs = SafeDurationMilliseconds(processEvent.StartedAt, processEvent.CompletedAt.Value);
                processEvent.SafeErrorCode = "assistant_turn_canceled";
                SkipRemainingLoopEvents(eventByStep.Values, processEvent.Sequence, "assistant_turn_canceled");
                await _db.SaveChangesAsync(ct);
                return Result.Failure<AiAssistantTurnResponseDto>(
                    "The assistant turn was canceled safely.", 409, "assistant_turn_canceled");
            }

            if (!executionContext.HasCapability(capabilityId) ||
                step.SourceIds.Any(sourceId => !sourceIds.Contains(sourceId, StringComparer.Ordinal)))
            {
                processEvent.Status = "blocked";
                processEvent.SafeErrorCode = "assistant_loop_policy_changed";
                processEvent.CompletedAt = DateTimeOffset.UtcNow;
                SkipRemainingLoopEvents(eventByStep.Values, processEvent.Sequence, processEvent.SafeErrorCode);
                await _db.SaveChangesAsync(ct);
                return Result.Failure<AiAssistantTurnResponseDto>(
                    "Authorization changed while the read-only plan was running.", 403, processEvent.SafeErrorCode);
            }

            processEvent.Status = "running";
            processEvent.StartedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            if (step.Kind == "analyze")
            {
                if (_costService != null &&
                    !await _costService.EnsureBudgetAvailableAsync(session.TenantId, session.ProjectId, ct))
                {
                    processEvent.Status = "blocked";
                    processEvent.SafeErrorCode = AiErrorCodes.BudgetExceeded;
                    processEvent.CompletedAt = DateTimeOffset.UtcNow;
                    SkipRemainingLoopEvents(eventByStep.Values, processEvent.Sequence, processEvent.SafeErrorCode);
                    await _db.SaveChangesAsync(ct);
                    return Result.Failure<AiAssistantTurnResponseDto>(
                        "The effective AI budget has been exhausted.", 429, processEvent.SafeErrorCode);
                }

                for (var attempt = 1; attempt <= AiAssistantGoalPlanningContract.MaxAttemptsPerStep; attempt++)
                {
                    processEvent.SafeDetailJson = SerializeLoopDetail(step.StepId, attempt, sourceRefs, null, null);
                    await _db.SaveChangesAsync(ct);
                    coreResult = await _chatService.AssistantPlannedTurnAsync(request, executionContext, planning, ct);
                    if (coreResult.IsSuccess && coreResult.Data != null) break;
                    processEvent.Retryable = coreResult.StatusCode >= 500;
                    processEvent.SafeErrorCode = NormalizeErrorCode(coreResult.ErrorCode);
                    if (!processEvent.Retryable || attempt == AiAssistantGoalPlanningContract.MaxAttemptsPerStep) break;
                    if (await IsCancellationRequestedAsync(turn.Id, ct)) break;
                }
                if (coreResult == null || !coreResult.IsSuccess || coreResult.Data == null)
                {
                    processEvent.Status = "failed";
                    processEvent.CompletedAt = DateTimeOffset.UtcNow;
                    processEvent.DurationMs = SafeDurationMilliseconds(processEvent.StartedAt, processEvent.CompletedAt.Value);
                    processEvent.SafeErrorCode ??= "assistant_loop_retry_exhausted";
                    SkipRemainingLoopEvents(eventByStep.Values, processEvent.Sequence, processEvent.SafeErrorCode);
                    await _db.SaveChangesAsync(ct);
                    return coreResult ?? Result.Failure<AiAssistantTurnResponseDto>(
                        "Read-only analysis failed after bounded retries.", 503, processEvent.SafeErrorCode);
                }
                // Cancellation can arrive while the provider call is in flight.
                // Recheck before accepting its answer so a fast final step cannot
                // turn an acknowledged cancel into a completed HTTP 200 response.
                if (await IsCancellationRequestedAsync(turn.Id, ct))
                {
                    processEvent.Status = "blocked";
                    processEvent.CompletedAt = DateTimeOffset.UtcNow;
                    processEvent.DurationMs = SafeDurationMilliseconds(processEvent.StartedAt, processEvent.CompletedAt.Value);
                    processEvent.SafeErrorCode = "assistant_turn_canceled";
                    SkipRemainingLoopEvents(eventByStep.Values, processEvent.Sequence, processEvent.SafeErrorCode);
                    await _db.SaveChangesAsync(ct);
                    return Result.Failure<AiAssistantTurnResponseDto>(
                        "The assistant turn was canceled safely.", 409, processEvent.SafeErrorCode);
                }
                processEvent.SafeDetailJson = SerializeLoopDetail(
                    step.StepId,
                    ReadAttempt(processEvent.SafeDetailJson),
                    sourceRefs,
                    ResolveActualProvider(coreResult.Data),
                    ResolveActualModel(coreResult.Data));
            }
            else if (step.Kind == "verify")
            {
                if (coreResult?.Data == null ||
                    !string.Equals(coreResult.Data.SchemaId, AiAssistantTurnContract.SchemaId, StringComparison.Ordinal) ||
                    coreResult.Data.SourceRefs.Any(sourceRef => !sourceRefs.Contains(sourceRef, StringComparer.Ordinal)) ||
                    coreResult.Data.Artifact?.Kind == "task_action_plan" ||
                    coreResult.Data.ExecutionPolicy.Contains("mutation", StringComparison.OrdinalIgnoreCase) ||
                    coreResult.Data.ExecutionPolicy.Contains("confirm", StringComparison.OrdinalIgnoreCase))
                {
                    processEvent.Status = "failed";
                    processEvent.SafeErrorCode = "assistant_loop_verification_failed";
                    processEvent.CompletedAt = DateTimeOffset.UtcNow;
                    SkipRemainingLoopEvents(eventByStep.Values, processEvent.Sequence, processEvent.SafeErrorCode);
                    await _db.SaveChangesAsync(ct);
                    return Result.Failure<AiAssistantTurnResponseDto>(
                        "Read-only output verification failed.", 422, processEvent.SafeErrorCode);
                }
            }

            processEvent.Status = step.Kind is "retrieve" or "verify" ? "verified" : "completed";
            processEvent.CompletedAt = DateTimeOffset.UtcNow;
            processEvent.DurationMs = SafeDurationMilliseconds(processEvent.StartedAt, processEvent.CompletedAt.Value);
            completedStates[step.StepId] = "completed";
            await _db.SaveChangesAsync(ct);
        }

        if (coreResult?.Data == null)
            return Result.Failure<AiAssistantTurnResponseDto>("Read-only loop produced no response.", 500, "assistant_loop_no_result");
        var completedPlan = boundedPlan with
        {
            Steps = boundedPlan.Steps.Select(step => step with
            {
                State = completedStates.ContainsKey(step.StepId) ? "completed" : "blocked"
            }).ToArray()
        };
        return Result.Success(coreResult.Data with { WorkPlan = completedPlan });
    }

    private async Task<bool> IsCancellationRequestedAsync(Guid turnId, CancellationToken ct)
        => await _db.AssistantTurns.AsNoTracking()
            .Where(item => item.Id == turnId)
            .Select(item => item.CancellationRequestedAt.HasValue)
            .SingleAsync(ct);

    private static void SkipRemainingLoopEvents(
        IEnumerable<AssistantProcessEvent> events,
        int afterSequence,
        string? errorCode)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var item in events.Where(item => item.Sequence > afterSequence && item.Status == "queued"))
        {
            item.Status = "skipped";
            item.CompletedAt = now;
            item.DurationMs = 0;
            item.SafeErrorCode = errorCode;
        }
    }

    private static string SerializeLoopDetail(
        string stepId,
        int attempt,
        IReadOnlyList<string> sourceRefs,
        string? provider,
        string? model)
        => JsonSerializer.Serialize(new
        {
            stepId,
            attempt,
            sourceRefs,
            actualProvider = provider,
            actualModel = model
        }, JsonOptions);

    private static int ReadAttempt(string? detailJson)
    {
        if (string.IsNullOrWhiteSpace(detailJson)) return 0;
        try
        {
            using var document = JsonDocument.Parse(detailJson);
            return document.RootElement.TryGetProperty("attempt", out var value) && value.TryGetInt32(out var attempt)
                ? attempt : 0;
        }
        catch (JsonException) { return 0; }
    }

    private static string ResolveActualProvider(AiAssistantTurnResponseDto response)
        => response.ProjectLaunchPlan?.ActualProvider ?? response.ProjectLaunchBrief?.ActualProvider ?? response.ResearchPlan?.ActualProvider ??
           response.Conversation?.ActualProvider ?? response.Answer?.Model?.Provider ?? "not_reached";

    private static string ResolveActualModel(AiAssistantTurnResponseDto response)
        => response.ProjectLaunchPlan?.ActualModel ?? response.ProjectLaunchBrief?.ActualModel ?? response.ResearchPlan?.ActualModel ??
           response.Conversation?.ActualModel ?? response.Answer?.Model?.Id ?? "not_reached";

    private IQueryable<AssistantSession> SessionQuery()
        => _db.AssistantSessions
            .AsSplitQuery()
            .Include(item => item.Turns)
                .ThenInclude(turn => turn.ProcessEvents)
            .Include(item => item.Turns)
                .ThenInclude(turn => turn.ArtifactRefs);

    private static AiAssistantSessionDto MapSession(AssistantSession session)
    {
        var turns = session.Turns
            .OrderBy(item => item.Sequence)
            .Select(item => new AiAssistantStoredTurnDto(
                item.Id,
                item.Sequence,
                item.ClientTurnId,
                item.UserMessage,
                item.Status,
                item.CorrelationId,
                item.CreatedAt,
                item.CompletedAt,
                MapCompletedResponse(session, item, replayed: false),
                MapEvents(item.ProcessEvents)))
            .ToList();

        return new AiAssistantSessionDto(
            session.Id,
            session.Title,
            session.Status,
            session.Version,
            session.ProjectId,
            session.CreatedAt,
            session.UpdatedAt,
            turns,
            ParseClarificationDraft(session.ClarificationDraftJson));
    }

    private static AiAssistantTurnResponseDto? MapCompletedResponse(
        AssistantSession session,
        AssistantTurn turn,
        bool replayed)
    {
        if (string.IsNullOrWhiteSpace(turn.ResponseJson)) return null;
        try
        {
            var response = JsonSerializer.Deserialize<AiAssistantTurnResponseDto>(turn.ResponseJson, JsonOptions);
            return response == null
                ? null
                : response with
                {
                    SessionId = session.Id,
                    TurnId = turn.Id,
                    Sequence = turn.Sequence,
                    SessionVersion = session.Version,
                    ClientTurnId = turn.ClientTurnId,
                    TurnStatus = turn.Status,
                    CorrelationId = turn.CorrelationId,
                    Replayed = replayed,
                    ProcessEvents = MapEvents(turn.ProcessEvents)
                };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<AiAssistantProcessEventDto> MapEvents(
        IEnumerable<AssistantProcessEvent> events)
        => events
            .OrderBy(item => item.Sequence)
            .Select(item =>
            {
                var detail = ParseLoopDetail(item.SafeDetailJson);
                return new AiAssistantProcessEventDto(
                    item.Sequence,
                    item.Stage,
                    item.Status,
                    item.PublicLabel,
                    item.StartedAt,
                    item.CompletedAt,
                    item.DurationMs,
                    item.Retryable,
                    item.SafeErrorCode,
                    detail.StepId,
                    detail.Attempt,
                    detail.SourceRefs,
                    detail.Provider,
                    detail.Model);
            })
            .ToList();

    private static (string? StepId, int? Attempt, IReadOnlyList<string>? SourceRefs, string? Provider, string? Model)
        ParseLoopDetail(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return (null, null, null, null, null);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var stepId = root.TryGetProperty("stepId", out var step) ? step.GetString() : null;
            int? attempt = root.TryGetProperty("attempt", out var attemptValue) && attemptValue.TryGetInt32(out var parsed)
                ? parsed : null;
            var sourceRefs = root.TryGetProperty("sourceRefs", out var sources) && sources.ValueKind == JsonValueKind.Array
                ? sources.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString()!).ToArray()
                : null;
            var provider = root.TryGetProperty("actualProvider", out var providerValue) && providerValue.ValueKind == JsonValueKind.String
                ? providerValue.GetString() : null;
            var model = root.TryGetProperty("actualModel", out var modelValue) && modelValue.ValueKind == JsonValueKind.String
                ? modelValue.GetString() : null;
            return (stepId, attempt, sourceRefs, provider, model);
        }
        catch (JsonException) { return (null, null, null, null, null); }
    }

    private static string ComputeRequestHash(AiAssistantTurnRequestDto request)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            Message = request.Message.Trim(),
            request.Context,
            request.Mode,
            request.Language,
            request.ProviderHint,
            request.ProgressiveReply,
            request.ProgressiveReplies,
            request.ResumeFromTurnId,
            Files = request.Files?.Select(file => new
            {
                file.FileName,
                file.ContentType,
                file.Size,
                file.TotalRowCount
            })
        }, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static AiAuditEvent BuildAudit(
        Guid userId,
        Guid? tenantId,
        Guid? projectId,
        string eventType,
        string entityType,
        Guid entityId,
        string outcome,
        string? requestId = null,
        string? failureCode = null)
        => new()
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ActorUserId = userId,
            EventType = eventType,
            EntityType = entityType,
            EntityGuid = entityId,
            Purpose = "assistant_session",
            DataClassification = "project_private",
            Outcome = outcome,
            RequestId = requestId,
            FailureCode = failureCode
        };

    private Result<T>? EnsureEnabled<T>()
        => _options.AssistantSessionEnabled
            ? null
            : Result.Failure<T>(
                "Durable assistant sessions are disabled.",
                503,
                "assistant_session_disabled");

    private static List<AiChatMessageDto> BuildServerHistory(AssistantSession session)
        => session.Turns
            .Where(item => item.Status == "completed" && !string.IsNullOrWhiteSpace(item.AssistantResponse))
            .OrderByDescending(item => item.Sequence)
            .Take(8)
            .OrderBy(item => item.Sequence)
            .SelectMany(item => new[]
            {
                new AiChatMessageDto("user", item.UserMessage),
                new AiChatMessageDto("assistant", item.AssistantResponse!)
            })
            .ToList();

    private static bool IsDefaultSessionTitle(string title)
        => string.Equals(title, "Cuộc trò chuyện mới", StringComparison.OrdinalIgnoreCase) ||
           title.StartsWith("Cuộc trò chuyện Trợ lý AI", StringComparison.OrdinalIgnoreCase);

    private static string BuildSessionTitle(string message)
    {
        var compact = string.Join(' ', message.Split(
            ['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries));
        if (compact.Length > 72) compact = $"{compact[..69]}…";
        return NormalizeTitle(compact);
    }

    private static string NormalizeTitle(string? title)
    {
        var value = string.IsNullOrWhiteSpace(title) ? "Cuộc trò chuyện mới" : title.Trim();
        return value.Length <= MaxTitleLength ? value : value[..MaxTitleLength];
    }

    private async Task<Result<AssistantSession>> GetOwnedMutableSessionAsync(
        Guid sessionId,
        long expectedVersion,
        CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId) return Result.Forbidden<AssistantSession>();
        var session = await SessionQuery().SingleOrDefaultAsync(
            item => item.Id == sessionId && item.OwnerUserId == userId && item.Status != "deleted", ct);
        if (session == null) return Result.NotFound<AssistantSession>();
        if (session.Version != expectedVersion)
            return Result.Failure<AssistantSession>(
                "The assistant session changed. Reload before updating it.", 409, "assistant_session_stale");
        return Result.Success(session);
    }

    private static IReadOnlyList<AiAssistantProgressiveReplyDto> EffectiveProgressiveReplies(
        AiAssistantTurnRequestDto request)
    {
        if (request.ProgressiveReplies is { Count: > 0 }) return request.ProgressiveReplies;
        return request.ProgressiveReply == null ? [] : [request.ProgressiveReply];
    }

    private static AiAssistantClarificationDraftDto? ParseClarificationDraft(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<AiAssistantClarificationDraftDto>(json, JsonOptions); }
        catch (JsonException) { return null; }
    }

    private static string NormalizeModelProfile(string? providerHint)
        => string.Equals(providerHint, "deepseek", StringComparison.OrdinalIgnoreCase)
            ? "reasoning_strong"
            : string.Equals(providerHint, "local", StringComparison.OrdinalIgnoreCase)
                ? "fast_local"
                : "auto";

    private static string NormalizeCorrelationId(string? value)
    {
        value = value?.Trim();
        if (string.IsNullOrWhiteSpace(value)) return Guid.NewGuid().ToString("N");
        return value.Length <= 120 ? value : value[..120];
    }

    private static string? NormalizeErrorCode(string? value)
    {
        value = value?.Trim();
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Length <= 100 ? value : value[..100];
    }

    private static int SafeDurationMilliseconds(DateTimeOffset startedAt, DateTimeOffset completedAt)
        => (int)Math.Clamp((completedAt - startedAt).TotalMilliseconds, 0, int.MaxValue);
}
