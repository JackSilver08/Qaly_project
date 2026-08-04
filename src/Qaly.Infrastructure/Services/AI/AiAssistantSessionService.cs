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
    private readonly AiJobPlatformOptions _options;

    public AiAssistantSessionService(
        QalyDbContext db,
        ICurrentUserService currentUser,
        IProjectService projectService,
        IAiAssistantContextRegistry contextRegistry,
        IAiAssistantGoalPlanner goalPlanner,
        IErumiChatService chatService,
        IOptions<AiJobPlatformOptions> options)
    {
        _db = db;
        _currentUser = currentUser;
        _projectService = projectService;
        _contextRegistry = contextRegistry;
        _goalPlanner = goalPlanner;
        _chatService = chatService;
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

        AiAssistantGoalPlanningResultDto? planning = null;
        Result<AiAssistantExecutionContextDto> contextResult;
        if (_options.AssistantGoalPlannerEnabled)
        {
            var discoveryResult = await _contextRegistry.DiscoverAsync(request, ct);
            if (!discoveryResult.IsSuccess || discoveryResult.Data == null)
                return Result.Failure<AiAssistantTurnResponseDto>(
                    discoveryResult.Error ?? "Assistant skills could not be authorized.",
                    discoveryResult.StatusCode, discoveryResult.ErrorCode);
            var planningResult = await _goalPlanner.PlanAsync(request, discoveryResult.Data, ct);
            if (!planningResult.IsSuccess || planningResult.Data == null)
                return Result.Failure<AiAssistantTurnResponseDto>(
                    planningResult.Error ?? "Assistant goal could not be analysed.",
                    planningResult.StatusCode, planningResult.ErrorCode);
            planning = planningResult.Data;
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
                    request with { RequestedCapabilityId = planning.SelectedCapabilityId }, ct);
        }
        else
        {
            contextResult = await _contextRegistry.ResolveAsync(request, ct);
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
            Status = "running",
            ModelProfile = NormalizeModelProfile(request.ProviderHint),
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

        var serverHistory = session.Turns
            .Where(item => item.Id != turn.Id && item.Status == "completed" && !string.IsNullOrWhiteSpace(item.AssistantResponse))
            .OrderByDescending(item => item.Sequence)
            .Take(6)
            .OrderBy(item => item.Sequence)
            .SelectMany(item => new[]
            {
                new AiChatMessageDto("user", item.UserMessage),
                new AiChatMessageDto("assistant", item.AssistantResponse!)
            })
            .ToList();

        var routingStartedAt = now;
        Result<AiAssistantTurnResponseDto> coreResult;
        try
        {
            var serverRequest = request with
            {
                Message = message,
                History = serverHistory
            };
            coreResult = planning == null
                ? await _chatService.AssistantTurnAsync(serverRequest, executionContext, ct)
                : await _chatService.AssistantPlannedTurnAsync(serverRequest, executionContext, planning, ct);
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

        var completedAt = DateTimeOffset.UtcNow;
        var routingEvent = turn.ProcessEvents.Single(item => item.Sequence == 4);
        routingEvent.Status = coreResult.IsSuccess ? "completed" : "failed";
        routingEvent.CompletedAt = completedAt;
        routingEvent.DurationMs = SafeDurationMilliseconds(routingStartedAt, completedAt);
        routingEvent.Retryable = !coreResult.IsSuccess && coreResult.StatusCode >= 500;
        routingEvent.SafeErrorCode = coreResult.IsSuccess ? null : NormalizeErrorCode(coreResult.ErrorCode);

        if (!coreResult.IsSuccess || coreResult.Data == null)
        {
            turn.Status = "failed";
            turn.SafeErrorCode = NormalizeErrorCode(coreResult.ErrorCode) ?? "assistant_turn_failed";
            turn.CompletedAt = completedAt;
            var failedEvent = new AssistantProcessEvent
            {
                TurnId = turn.Id,
                Sequence = 5,
                Stage = "failed",
                Status = "failed",
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
                "assistant_turn.failed",
                "AssistantTurn",
                turn.Id,
                "failed",
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
                coreResult.Data.Answer?.Model?.Provider ??
                planning?.GoalAnalysis.ActualProvider ?? "not_reached",
            ActualModel = coreResult.Data.ResearchPlan?.ActualModel ??
                coreResult.Data.Answer?.Model?.Id ??
                planning?.GoalAnalysis.ActualModel ?? "not_reached",
            GoalAnalysis = planning?.GoalAnalysis ?? coreResult.Data.GoalAnalysis,
            WorkPlan = planning == null
                ? coreResult.Data.WorkPlan
                : AiAssistantGoalPlanningOutputContract.CompleteWorkPlan(
                    planning.WorkPlan,
                    coreResult.Data.Disposition)
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

        var hasArtifact = response.Artifact != null || response.ResearchPlan != null;
        var finalEvent = new AssistantProcessEvent
        {
            TurnId = turn.Id,
            Sequence = 5,
            Stage = hasArtifact ? "artifact" : "answer",
            Status = "completed",
            PublicLabel = response.ResearchPlan != null
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

        response = response with { ProcessEvents = MapEvents(turn.ProcessEvents) };
        turn.ResponseJson = JsonSerializer.Serialize(response with { ProcessEvents = null }, JsonOptions);
        _db.AiAuditEvents.Add(BuildAudit(
            userId,
            session.TenantId,
            session.ProjectId,
            "assistant_turn.completed",
            "AssistantTurn",
            turn.Id,
            "completed",
            turn.CorrelationId));
        await _db.SaveChangesAsync(ct);

        return Result.Success(response);
    }

    private IQueryable<AssistantSession> SessionQuery()
        => _db.AssistantSessions
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
            turns);
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
            .Select(item => new AiAssistantProcessEventDto(
                item.Sequence,
                item.Stage,
                item.Status,
                item.PublicLabel,
                item.StartedAt,
                item.CompletedAt,
                item.DurationMs,
                item.Retryable,
                item.SafeErrorCode))
            .ToList();

    private static string ComputeRequestHash(AiAssistantTurnRequestDto request)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            Message = request.Message.Trim(),
            request.Context,
            request.Mode,
            request.Language,
            request.ProviderHint,
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

    private static string NormalizeTitle(string? title)
    {
        var value = string.IsNullOrWhiteSpace(title) ? "Cuộc trò chuyện mới" : title.Trim();
        return value.Length <= MaxTitleLength ? value : value[..MaxTitleLength];
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
