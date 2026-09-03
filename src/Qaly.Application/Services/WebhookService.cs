using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Webhook;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using System.Text.Json;
using Qaly.Application.Services.Tasks;

namespace Qaly.Application.Services;

public class WebhookService : IWebhookService
{
    private static readonly HashSet<string> SupportedEvents = new(
        ["task.created", "task.updated", "task.deleted", "*"],
        StringComparer.Ordinal);
    private readonly IRepository<WebhookSubscription> _webhookRepo;
    private readonly IRepository<WebhookOutboxMessage> _outboxRepo;
    private readonly IRepository<WebhookDeliveryLog> _deliveryLogRepo;
    private readonly IRepository<Project> _projectRepo;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITaskAccessPolicy _taskAccessPolicy;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWebhookPublisher _webhookPublisher;
    private readonly IWebhookEndpointPolicy _webhookEndpointPolicy;
    private readonly IAuditLogService _auditLogService;

    public WebhookService(
        IRepository<WebhookSubscription> webhookRepo,
        IRepository<WebhookOutboxMessage> outboxRepo,
        IRepository<WebhookDeliveryLog> deliveryLogRepo,
        IRepository<Project> projectRepo,
        ICurrentUserService currentUserService,
        ITaskAccessPolicy taskAccessPolicy,
        IUnitOfWork unitOfWork,
        IWebhookPublisher webhookPublisher,
        IWebhookEndpointPolicy webhookEndpointPolicy,
        IAuditLogService auditLogService)
    {
        _webhookRepo = webhookRepo;
        _outboxRepo = outboxRepo;
        _deliveryLogRepo = deliveryLogRepo;
        _projectRepo = projectRepo;
        _currentUserService = currentUserService;
        _taskAccessPolicy = taskAccessPolicy;
        _unitOfWork = unitOfWork;
        _webhookPublisher = webhookPublisher;
        _webhookEndpointPolicy = webhookEndpointPolicy;
        _auditLogService = auditLogService;
    }

    public async Task<Result<IEnumerable<WebhookDto>>> GetByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null) return Result.NotFound<IEnumerable<WebhookDto>>();

        if (!await _taskAccessPolicy.CanManageWebhooksAsync(projectId, project.OwnerId, ct))
        {
            return Result.Forbidden<IEnumerable<WebhookDto>>();
        }

        var webhooks = await _webhookRepo.GetQueryable()
            .Where(w => w.ProjectId == projectId)
            .ToListAsync(ct);

        var dtos = webhooks.Select(w => new WebhookDto(
            w.Id,
            w.ProjectId,
            w.PayloadUrl,
            DeserializeEvents(w.Events),
            !string.IsNullOrWhiteSpace(w.Secret),
            w.IsActive,
            w.CreatedAt
        ));

        return Result.Success(dtos);
    }

    public async Task<Result<WebhookDto>> CreateAsync(CreateWebhookDto dto, CancellationToken ct = default)
    {
        var validation = ValidateWebhookEvents(dto.Events);
        if (!validation.IsSuccess)
        {
            return Result.Failure<WebhookDto>(validation.Error!, validation.StatusCode);
        }

        var endpointValidation = await _webhookEndpointPolicy.ValidateAsync(dto.PayloadUrl, ct);
        if (!endpointValidation.IsAllowed)
        {
            return Result.Failure<WebhookDto>(endpointValidation.Error!, 400);
        }
        if (endpointValidation.Endpoint!.AbsoluteUri.Length > 500)
            return Result.Failure<WebhookDto>("Payload URL không được vượt quá 500 ký tự.");
        if ((dto.Secret?.Length ?? 0) > 100)
            return Result.Failure<WebhookDto>("Khóa bí mật không được vượt quá 100 ký tự.");

        var project = await _projectRepo.GetByIdAsync(dto.ProjectId, ct);
        if (project == null) return Result.NotFound<WebhookDto>();

        if (!await _taskAccessPolicy.CanManageWebhooksAsync(dto.ProjectId, project.OwnerId, ct))
        {
            return Result.Forbidden<WebhookDto>();
        }

        var webhook = new WebhookSubscription
        {
            ProjectId = dto.ProjectId,
            PayloadUrl = endpointValidation.Endpoint.AbsoluteUri,
            Secret = dto.Secret?.Trim() ?? string.Empty,
            Events = JsonSerializer.Serialize(NormalizeEvents(dto.Events))
        };

        try
        {
            await _webhookRepo.AddAsync(webhook, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Result.Failure<WebhookDto>(
                "Unable to save webhook. Please check the project access and input values.",
                400);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<WebhookDto>(
                "Unable to create webhook because the current project state is invalid.",
                400);
        }

        return Result.Success(new WebhookDto(
            webhook.Id,
            webhook.ProjectId,
            webhook.PayloadUrl,
            NormalizeEvents(dto.Events),
            !string.IsNullOrWhiteSpace(webhook.Secret),
            webhook.IsActive,
            webhook.CreatedAt
        ));
    }

    public async Task<Result<WebhookDto>> UpdateAsync(Guid projectId, Guid id, UpdateWebhookDto dto, CancellationToken ct = default)
    {
        var validation = ValidateWebhookEvents(dto.Events);
        if (!validation.IsSuccess)
        {
            return Result.Failure<WebhookDto>(validation.Error!, validation.StatusCode);
        }

        var endpointValidation = await _webhookEndpointPolicy.ValidateAsync(dto.PayloadUrl, ct);
        if (!endpointValidation.IsAllowed)
        {
            return Result.Failure<WebhookDto>(endpointValidation.Error!, 400);
        }
        if (endpointValidation.Endpoint!.AbsoluteUri.Length > 500)
            return Result.Failure<WebhookDto>("Payload URL không được vượt quá 500 ký tự.");
        if ((dto.Secret?.Length ?? 0) > 100)
            return Result.Failure<WebhookDto>("Khóa bí mật không được vượt quá 100 ký tự.");

        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null) return Result.NotFound<WebhookDto>();

        var webhook = await _webhookRepo.GetByIdAsync(id, ct);
        if (webhook == null) return Result.NotFound<WebhookDto>();

        if (webhook.ProjectId != projectId)
        {
            return Result.NotFound<WebhookDto>();
        }

        if (!await _taskAccessPolicy.CanManageWebhooksAsync(projectId, project.OwnerId, ct))
        {
            return Result.Forbidden<WebhookDto>();
        }

        webhook.PayloadUrl = endpointValidation.Endpoint.AbsoluteUri;
        webhook.Secret = dto.Secret?.Trim() ?? string.Empty;
        webhook.Events = JsonSerializer.Serialize(NormalizeEvents(dto.Events));
        webhook.IsActive = dto.IsActive;

        await _webhookRepo.UpdateAsync(webhook, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new WebhookDto(
            webhook.Id,
            webhook.ProjectId,
            webhook.PayloadUrl,
            NormalizeEvents(dto.Events),
            !string.IsNullOrWhiteSpace(webhook.Secret),
            webhook.IsActive,
            webhook.CreatedAt
        ));
    }

    public async Task<Result> DeleteAsync(Guid projectId, Guid id, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null) return Result.NotFound();

        var webhook = await _webhookRepo.GetByIdAsync(id, ct);
        if (webhook == null) return Result.NotFound();

        if (webhook.ProjectId != projectId)
        {
            return Result.NotFound();
        }

        if (!await _taskAccessPolicy.CanManageWebhooksAsync(projectId, project.OwnerId, ct))
        {
            return Result.Forbidden();
        }

        await _webhookRepo.DeleteAsync(webhook, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<WebhookTestResultDto>> TriggerTestAsync(Guid projectId, Guid id, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null) return Result.NotFound<WebhookTestResultDto>();

        var webhook = await _webhookRepo.GetByIdAsync(id, ct);
        if (webhook == null) return Result.NotFound<WebhookTestResultDto>();

        if (webhook.ProjectId != projectId)
        {
            return Result.NotFound<WebhookTestResultDto>();
        }

        if (!await _taskAccessPolicy.CanManageWebhooksAsync(projectId, project.OwnerId, ct))
        {
            return Result.Forbidden<WebhookTestResultDto>();
        }

        var receipt = await _webhookPublisher.DispatchToWebhookAsync(
            webhook.Id,
            "ping",
            new { message = "Test webhook from Qaly" },
            ct);
        var dto = new WebhookTestResultDto(
            receipt.WebhookId,
            receipt.EventType,
            receipt.Status,
            receipt.IsDelivered,
            receipt.AttemptCount,
            receipt.ResponseStatusCode,
            receipt.DeliveryLogId,
            receipt.IdempotencyKey);

        return receipt.IsDelivered
            ? Result.Success(dto)
            : Result.Failure(
                dto,
                receipt.Status == "endpoint_blocked"
                    ? "Endpoint webhook không còn vượt qua kiểm tra an toàn."
                    : "Endpoint webhook không xác nhận nhận dữ liệu kiểm thử.",
                502,
                "webhook_delivery_failed");
    }

    public async Task<Result<WebhookOperationsDto>> GetOperationsAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null) return Result.NotFound<WebhookOperationsDto>();

        if (!await _taskAccessPolicy.CanManageWebhooksAsync(projectId, project.OwnerId, ct))
        {
            return Result.Forbidden<WebhookOperationsDto>();
        }

        var projectOutbox = _outboxRepo.GetQueryable()
            .AsNoTracking()
            .Where(message => message.ProjectId == projectId);
        var pendingCount = await projectOutbox.CountAsync(message =>
            message.ProcessedAt == null && message.DeadLetteredAt == null, ct);
        var deadLetterCount = await projectOutbox.CountAsync(message => message.DeadLetteredAt != null, ct);
        var recentOutboxEntities = await projectOutbox
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Take(50)
            .ToListAsync(ct);
        var recentOutbox = recentOutboxEntities.Select(ToOutboxDto).ToList();
        var recentDeliveries = await _deliveryLogRepo.GetQueryable()
            .AsNoTracking()
            .Where(log => log.Webhook.ProjectId == projectId)
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Take(50)
            .Select(log => new WebhookDeliveryLogDto(
                log.Id,
                log.WebhookId,
                log.EventType,
                log.IsSuccess,
                log.AttemptCount,
                log.ResponseStatusCode,
                log.DurationMs,
                log.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new WebhookOperationsDto(
            projectId,
            pendingCount,
            deadLetterCount,
            recentOutbox,
            recentDeliveries));
    }

    public async Task<Result<WebhookOutboxReplayResultDto>> ReplayDeadLetterAsync(
        Guid projectId,
        Guid outboxId,
        CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null) return Result.NotFound<WebhookOutboxReplayResultDto>();

        if (!await _taskAccessPolicy.CanManageWebhooksAsync(projectId, project.OwnerId, ct))
        {
            return Result.Forbidden<WebhookOutboxReplayResultDto>();
        }

        var message = await _outboxRepo.GetByIdAsync(outboxId, ct);
        if (message == null || message.ProjectId != projectId)
        {
            return Result.NotFound<WebhookOutboxReplayResultDto>();
        }

        if (message.ProcessedAt != null)
        {
            return Result.Failure<WebhookOutboxReplayResultDto>(
                "Webhook occurrence was already delivered and cannot be replayed.",
                409,
                "webhook_already_delivered");
        }

        if (message.DeadLetteredAt == null)
        {
            return Result.Success(new WebhookOutboxReplayResultDto(ToOutboxDto(message), false));
        }

        message.RetryCount = 0;
        message.NextAttemptAt = DateTimeOffset.UtcNow;
        message.LeaseOwner = null;
        message.LockedUntil = null;
        message.DeadLetteredAt = null;
        message.ErrorMessage = null;
        message.UpdatedAt = DateTimeOffset.UtcNow;
        await _outboxRepo.UpdateAsync(message, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "ReplayWebhookDeadLetter",
            nameof(WebhookOutboxMessage),
            message.Id.ToString(),
            new { projectId, message.EventType },
            ct);

        var canonical = await _outboxRepo.GetQueryable()
            .AsNoTracking()
            .SingleAsync(item => item.Id == outboxId && item.ProjectId == projectId, ct);

        return Result.Success(new WebhookOutboxReplayResultDto(ToOutboxDto(canonical), true));
    }

    private static WebhookOutboxItemDto ToOutboxDto(WebhookOutboxMessage message)
        => new(
            message.Id,
            message.ProjectId,
            message.EventType,
            message.ProcessedAt != null
                ? "delivered"
                : message.DeadLetteredAt != null
                    ? "dead_letter"
                    : message.LockedUntil > DateTimeOffset.UtcNow
                        ? "delivering"
                        : message.RetryCount > 0
                            ? "retry_scheduled"
                            : "pending",
            message.RetryCount,
            message.CreatedAt,
            message.NextAttemptAt,
            message.DeadLetteredAt,
            NormalizeOperatorError(message.ErrorMessage));

    private static string? NormalizeOperatorError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error)) return null;
        var normalized = string.Join(' ', error.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        return normalized.Length <= 240 ? normalized : normalized[..240] + "…";
    }

    private static Result ValidateWebhookEvents(string[]? events)
    {
        if (events == null || events.Length == 0 || events.Any(e => string.IsNullOrWhiteSpace(e)))
        {
            return Result.Failure("At least one webhook event must be selected.");
        }

        var normalized = NormalizeEvents(events);
        var unsupported = normalized.Where(eventType => !SupportedEvents.Contains(eventType)).ToArray();
        if (unsupported.Length > 0)
        {
            return Result.Failure($"Unsupported webhook events: {string.Join(", ", unsupported)}.");
        }

        return Result.Success();
    }

    private static string[] NormalizeEvents(IEnumerable<string>? events)
        => (events ?? Array.Empty<string>())
            .Select(eventType => eventType.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(eventType => eventType, StringComparer.Ordinal)
            .ToArray();

    private static string[] DeserializeEvents(string? eventsJson)
    {
        if (string.IsNullOrWhiteSpace(eventsJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(eventsJson) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
