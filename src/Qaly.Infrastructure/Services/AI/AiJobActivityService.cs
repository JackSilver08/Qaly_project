using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services.AI;

public sealed class AiJobActivityService : IAiJobActivityService
{
    private static readonly HashSet<string> AllowedStages =
    [
        AiActionActivityStages.UnderstandIntent,
        AiActionActivityStages.ResolveContext,
        AiActionActivityStages.CollectSources,
        AiActionActivityStages.RouteModel,
        AiActionActivityStages.ComposeOptions,
        AiActionActivityStages.ValidateOutput,
        AiActionActivityStages.AwaitConfirmation,
        AiActionActivityStages.ExecuteCommands,
        AiActionActivityStages.PersistReceipt,
        AiActionActivityStages.ReadBack
    ];

    private static readonly HashSet<string> AllowedStatuses =
    [
        AiActionActivityStatuses.Queued,
        AiActionActivityStatuses.Running,
        AiActionActivityStatuses.WaitingUser,
        AiActionActivityStatuses.Succeeded,
        AiActionActivityStatuses.Warning,
        AiActionActivityStatuses.Failed,
        AiActionActivityStatuses.Cancelled,
        AiActionActivityStatuses.Skipped
    ];

    private static readonly Dictionary<string, string> Labels = new()
    {
        [AiActionActivityStages.UnderstandIntent] = "Đang hiểu yêu cầu",
        [AiActionActivityStages.ResolveContext] = "Đang xác định dự án và quyền",
        [AiActionActivityStages.CollectSources] = "Đang thu thập dữ liệu được phép",
        [AiActionActivityStages.RouteModel] = "Đang chọn model phù hợp",
        [AiActionActivityStages.ComposeOptions] = "Đang soạn phương án",
        [AiActionActivityStages.ValidateOutput] = "Đang kiểm tra phương án",
        [AiActionActivityStages.AwaitConfirmation] = "Đang chờ bạn xác nhận",
        [AiActionActivityStages.ExecuteCommands] = "Đang thực hiện hành động",
        [AiActionActivityStages.PersistReceipt] = "Đang ghi nhận kết quả",
        [AiActionActivityStages.ReadBack] = "Đã hoàn tất và có thể xem lại"
    };

    private readonly QalyDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AiJobActivityService(QalyDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task AppendAsync(Guid jobId, AppendAiActionActivityDto dto, CancellationToken ct = default)
    {
        var stage = dto.Stage.Trim().ToLowerInvariant();
        var status = dto.Status.Trim().ToLowerInvariant();
        if (!AllowedStages.Contains(stage) || !AllowedStatuses.Contains(status))
        {
            throw new ArgumentException("Unknown AI action activity stage or status.", nameof(dto));
        }
        if (dto.Current.HasValue && dto.Total.HasValue &&
            (dto.Current < 0 || dto.Total < 1 || dto.Current > dto.Total))
        {
            throw new ArgumentException("Activity current/total is invalid.", nameof(dto));
        }

        var detail = NormalizeSafeDetail(dto.SafeDetailJson);
        for (var retry = 0; retry < 3; retry++)
        {
            var nextSequence = (await _db.AiJobActivityEvents
                .Where(item => item.AiJobId == jobId)
                .MaxAsync(item => (int?)item.Sequence, ct) ?? 0) + 1;
            var startedAt = dto.StartedAt ?? DateTimeOffset.UtcNow;
            var completedAt = dto.CompletedAt ?? (status is AiActionActivityStatuses.Succeeded or
                AiActionActivityStatuses.Warning or AiActionActivityStatuses.Failed or
                AiActionActivityStatuses.Cancelled or AiActionActivityStatuses.Skipped
                    ? DateTimeOffset.UtcNow
                    : null);
            int? duration = completedAt.HasValue
                ? checked((int)Math.Min(int.MaxValue, Math.Max(0, (completedAt.Value - startedAt).TotalMilliseconds)))
                : null;

            var entity = new AiJobActivityEvent
            {
                AiJobId = jobId,
                Sequence = nextSequence,
                Stage = stage,
                Status = status,
                PublicLabel = Labels[stage],
                SafeDetailJson = detail,
                Current = dto.Current,
                Total = dto.Total,
                Attempt = Math.Max(1, dto.Attempt),
                StartedAt = startedAt,
                CompletedAt = completedAt,
                DurationMs = duration,
                Retryable = dto.Retryable,
                ReceiptLink = NormalizeReceiptLink(dto.ReceiptLink)
            };
            _db.AiJobActivityEvents.Add(entity);
            try
            {
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException) when (retry < 2)
            {
                _db.Entry(entity).State = EntityState.Detached;
            }
        }
    }

    public async Task<Result<AiActionActivityFeedDto>> GetAsync(
        Guid jobId,
        int afterSequence = 0,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue) return Result.Forbidden<AiActionActivityFeedDto>();

        var job = await _db.AiJobs.AsNoTracking()
            .Include(item => item.Project)
                .ThenInclude(project => project!.Organization)
            .FirstOrDefaultAsync(item => item.Id == jobId, ct);
        if (job == null || !await CanReadJobAsync(job, userId.Value, ct))
        {
            return Result.Failure<AiActionActivityFeedDto>(
                "AI job was not found.",
                404,
                AiErrorCodes.JobNotFound);
        }

        var events = await _db.AiJobActivityEvents.AsNoTracking()
            .Where(item => item.AiJobId == jobId && item.Sequence > Math.Max(0, afterSequence))
            .OrderBy(item => item.Sequence)
            .ToListAsync(ct);
        var lastSequence = await _db.AiJobActivityEvents.AsNoTracking()
            .Where(item => item.AiJobId == jobId)
            .MaxAsync(item => (int?)item.Sequence, ct) ?? 0;

        return Result.Success(new AiActionActivityFeedDto(
            job.Id,
            job.Status,
            job.StartedAt,
            job.FinishedAt,
            lastSequence,
            job.Status is AiJobStatuses.Queued or AiJobStatuses.Running or AiJobStatuses.Retrying,
            events.Select(ToDto).ToList()));
    }

    private async Task<bool> CanReadJobAsync(AiJob job, Guid userId, CancellationToken ct)
    {
        if (job.RequestedById == userId ||
            await _db.Users.AnyAsync(user => user.Id == userId && user.Role == "Admin", ct))
        {
            return true;
        }
        if (!job.ProjectId.HasValue) return false;
        if (job.Project?.OwnerId == userId ||
            await _db.ProjectMembers.AnyAsync(member => member.ProjectId == job.ProjectId && member.UserId == userId, ct))
        {
            return true;
        }
        return job.Project?.OrganizationId.HasValue == true &&
            (job.Project.Organization?.OwnerId == userId ||
             await _db.OrganizationMembers.AnyAsync(member =>
                 member.OrganizationId == job.Project.OrganizationId && member.UserId == userId, ct));
    }

    private static AiActionActivityEventDto ToDto(AiJobActivityEvent item)
    {
        JsonElement? detail = null;
        if (!string.IsNullOrWhiteSpace(item.SafeDetailJson))
        {
            using var document = JsonDocument.Parse(item.SafeDetailJson);
            detail = document.RootElement.Clone();
        }
        return new AiActionActivityEventDto(
            item.Id,
            item.Sequence,
            item.Stage,
            item.Status,
            item.PublicLabel,
            detail,
            item.Current,
            item.Total,
            item.Attempt,
            item.StartedAt,
            item.CompletedAt,
            item.DurationMs,
            item.Retryable,
            item.ReceiptLink);
    }

    private static string? NormalizeSafeDetail(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        if (json.Length > 2000) throw new ArgumentException("Activity safe detail is too large.", nameof(json));
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
        {
            throw new ArgumentException("Activity safe detail must be a JSON object or array.", nameof(json));
        }
        return JsonSerializer.Serialize(document.RootElement);
    }

    private static string? NormalizeReceiptLink(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= 500 && normalized.StartsWith('/')
            ? normalized
            : throw new ArgumentException("Receipt link must be an internal relative URL.", nameof(value));
    }
}
