using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.DTOs.Vote;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Web.Auth;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/votes")]
public class VotesController : ControllerBase
{
    private readonly QalyDbContext _context;

    public VotesController(QalyDbContext context)
    {
        _context = context;
    }

    [HttpPost("{targetType}/{targetId:guid}")]
    public async Task<IActionResult> Vote(string targetType, Guid targetId, VoteRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var normalizedTargetType = NormalizeTargetType(targetType);
        if (normalizedTargetType == null)
        {
            return BadRequest("Target type must be Task or Comment.");
        }

        var value = Math.Clamp(request.Value, -1, 1);
        if (!await CanAccessTargetAsync(normalizedTargetType, targetId, userId.Value, ct))
        {
            return Forbid();
        }

        var existing = await _context.Votes
            .FirstOrDefaultAsync(vote =>
                vote.TargetType == normalizedTargetType &&
                vote.TargetId == targetId &&
                vote.UserId == userId.Value,
                ct);

        if (value == 0)
        {
            if (existing != null)
            {
                _context.Votes.Remove(existing);
            }
        }
        else if (existing == null)
        {
            _context.Votes.Add(new Vote
            {
                TargetType = normalizedTargetType,
                TargetId = targetId,
                UserId = userId.Value,
                Value = value
            });
        }
        else
        {
            existing.Value = value;
        }

        await _context.SaveChangesAsync(ct);
        var summary = await RefreshCountsAsync(normalizedTargetType, targetId, userId.Value, ct);
        return Ok(summary);
    }

    private async Task<bool> CanAccessTargetAsync(string targetType, Guid targetId, Guid userId, CancellationToken ct)
    {
        if (User.IsInRole("Admin"))
        {
            return true;
        }

        TaskItem? task = targetType == "Task"
            ? await _context.TaskItems.Include(item => item.Project).FirstOrDefaultAsync(item => item.Id == targetId, ct)
            : await _context.TaskComments
                .Include(comment => comment.TaskItem)
                .ThenInclude(item => item.Project)
                .Where(comment => comment.Id == targetId)
                .Select(comment => comment.TaskItem)
                .FirstOrDefaultAsync(ct);

        if (task == null)
        {
            return false;
        }

        var isProjectMember = task.Project.OwnerId == userId ||
            await _context.ProjectMembers.AnyAsync(member => member.ProjectId == task.ProjectId && member.UserId == userId, ct);
        if (!isProjectMember)
        {
            return false;
        }

        if (!task.IsPrivate)
        {
            return true;
        }

        var isManager = task.Project.OwnerId == userId ||
            await _context.ProjectMembers.AnyAsync(member =>
                member.ProjectId == task.ProjectId &&
                member.UserId == userId &&
                (member.Role == "Owner" || member.Role == "Manager" || member.Role == "Admin"),
                ct);

        return isManager || task.ReporterId == userId || task.AssigneeId == userId;
    }

    private async Task<VoteSummaryDto> RefreshCountsAsync(string targetType, Guid targetId, Guid userId, CancellationToken ct)
    {
        var votes = await _context.Votes
            .Where(vote => vote.TargetType == targetType && vote.TargetId == targetId)
            .ToListAsync(ct);

        var upvotes = votes.Count(vote => vote.Value > 0);
        var downvotes = votes.Count(vote => vote.Value < 0);

        if (targetType == "Task")
        {
            var task = await _context.TaskItems.FindAsync([targetId], ct);
            if (task != null)
            {
                task.UpvoteCount = upvotes;
                task.DownvoteCount = downvotes;
            }
        }
        else
        {
            var comment = await _context.TaskComments.FindAsync([targetId], ct);
            if (comment != null)
            {
                comment.UpvoteCount = upvotes;
                comment.DownvoteCount = downvotes;
            }
        }

        await _context.SaveChangesAsync(ct);

        return new VoteSummaryDto(
            targetType,
            targetId,
            upvotes,
            downvotes,
            upvotes - downvotes,
            votes.FirstOrDefault(vote => vote.UserId == userId)?.Value ?? 0);
    }

    private static string? NormalizeTargetType(string targetType)
        => targetType.Trim().ToLowerInvariant() switch
        {
            "task" or "tasks" => "Task",
            "comment" or "comments" => "Comment",
            _ => null
        };
}
