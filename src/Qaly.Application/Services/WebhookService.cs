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
    private readonly IRepository<WebhookSubscription> _webhookRepo;
    private readonly IRepository<Project> _projectRepo;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITaskAccessPolicy _taskAccessPolicy;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWebhookPublisher _webhookPublisher;

    public WebhookService(
        IRepository<WebhookSubscription> _webhookRepo,
        IRepository<Project> projectRepo,
        ICurrentUserService currentUserService,
        ITaskAccessPolicy taskAccessPolicy,
        IUnitOfWork unitOfWork,
        IWebhookPublisher webhookPublisher)
    {
        this._webhookRepo = _webhookRepo;
        _projectRepo = projectRepo;
        _currentUserService = currentUserService;
        _taskAccessPolicy = taskAccessPolicy;
        _unitOfWork = unitOfWork;
        _webhookPublisher = webhookPublisher;
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
            w.IsActive,
            w.CreatedAt
        ));

        return Result.Success(dtos);
    }

    public async Task<Result<WebhookDto>> CreateAsync(CreateWebhookDto dto, CancellationToken ct = default)
    {
        var validation = ValidateWebhook(dto.PayloadUrl, dto.Events);
        if (!validation.IsSuccess)
        {
            return Result.Failure<WebhookDto>(validation.Error!, validation.StatusCode);
        }

        var project = await _projectRepo.GetByIdAsync(dto.ProjectId, ct);
        if (project == null) return Result.NotFound<WebhookDto>();

        if (!await _taskAccessPolicy.CanManageWebhooksAsync(dto.ProjectId, project.OwnerId, ct))
        {
            return Result.Forbidden<WebhookDto>();
        }

        var webhook = new WebhookSubscription
        {
            ProjectId = dto.ProjectId,
            PayloadUrl = dto.PayloadUrl,
            Secret = dto.Secret,
            Events = JsonSerializer.Serialize(dto.Events ?? Array.Empty<string>())
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
            dto.Events ?? Array.Empty<string>(),
            webhook.IsActive,
            webhook.CreatedAt
        ));
    }

    public async Task<Result<WebhookDto>> UpdateAsync(Guid projectId, Guid id, UpdateWebhookDto dto, CancellationToken ct = default)
    {
        var validation = ValidateWebhook(dto.PayloadUrl, dto.Events);
        if (!validation.IsSuccess)
        {
            return Result.Failure<WebhookDto>(validation.Error!, validation.StatusCode);
        }

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

        webhook.PayloadUrl = dto.PayloadUrl;
        webhook.Secret = dto.Secret;
        webhook.Events = JsonSerializer.Serialize(dto.Events ?? Array.Empty<string>());
        webhook.IsActive = dto.IsActive;

        await _webhookRepo.UpdateAsync(webhook, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new WebhookDto(
            webhook.Id,
            webhook.ProjectId,
            webhook.PayloadUrl,
            dto.Events ?? Array.Empty<string>(),
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

    public async Task<Result> TriggerTestAsync(Guid projectId, Guid id, CancellationToken ct = default)
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

        await _webhookPublisher.DispatchToWebhookAsync(webhook.Id, "ping", new { message = "Test webhook from Qaly" }, ct);
        return Result.Success();
    }

    private static Result ValidateWebhook(string payloadUrl, string[]? events)
    {
        if (string.IsNullOrWhiteSpace(payloadUrl))
        {
            return Result.Failure("Payload URL is required.");
        }

        if (!Uri.TryCreate(payloadUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Failure("Payload URL must be a valid http or https URL.");
        }

        if (events == null || events.Length == 0 || events.Any(e => string.IsNullOrWhiteSpace(e)))
        {
            return Result.Failure("At least one webhook event must be selected.");
        }

        return Result.Success();
    }

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
