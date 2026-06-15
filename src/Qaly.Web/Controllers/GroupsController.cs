using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.DTOs.Groups;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Common.Models;
using Qaly.Application.Services;
using Qaly.Web.Hubs;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/groups")]
public class GroupsController : BaseApiController
{
    private readonly IGroupsService _groupsService;
    private readonly IGroupAttachmentService _groupAttachmentService;
    private readonly IHubContext<GroupHub> _groupHub;

    public GroupsController(
        IGroupsService groupsService,
        IGroupAttachmentService groupAttachmentService,
        IHubContext<GroupHub> groupHub)
    {
        _groupsService = groupsService;
        _groupAttachmentService = groupAttachmentService;
        _groupHub = groupHub;
    }

    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var result = await _groupsService.GetMineAsync(page, pageSize, search, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _groupsService.GetByIdAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateGroupRequest request, CancellationToken ct)
    {
        var result = await _groupsService.CreateAsync(request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateGroupRequest request, CancellationToken ct)
    {
        var result = await _groupsService.UpdateAsync(id, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _groupsService.DeleteAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid id, CancellationToken ct)
    {
        var result = await _groupsService.GetMembersAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{id:guid}/invitations")]
    public async Task<IActionResult> GetInvitations(Guid id, [FromQuery] string? status = null, CancellationToken ct = default)
    {
        var result = await _groupsService.GetInvitationsAsync(id, status, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id:guid}/invitations")]
    public async Task<IActionResult> CreateInvitation(Guid id, [FromBody] CreateGroupInvitationRequest request, CancellationToken ct)
    {
        if (request == null)
        {
            return BadRequest(new { error = "Vui lòng cung cấp thông tin lời mời." });
        }

        var result = await _groupsService.CreateInvitationAsync(id, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id:guid}/polls")]
    public async Task<IActionResult> CreatePoll(Guid id, [FromBody] CreateGroupPollRequest request, CancellationToken ct)
    {
        var result = await _groupsService.CreatePollAsync(id, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("{groupId:guid}/polls/{pollId:guid}")]
    public async Task<IActionResult> UpdatePoll(Guid groupId, Guid pollId, [FromBody] UpdateGroupPollRequest request, CancellationToken ct)
    {
        var result = await _groupsService.UpdatePollAsync(groupId, pollId, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{groupId:guid}/polls/{pollId:guid}")]
    public async Task<IActionResult> DeletePoll(Guid groupId, Guid pollId, CancellationToken ct)
    {
        var result = await _groupsService.DeletePollAsync(groupId, pollId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{groupId:guid}/polls/{pollId:guid}/vote")]
    public async Task<IActionResult> VotePoll(Guid groupId, Guid pollId, [FromBody] VoteGroupPollRequest request, CancellationToken ct)
    {
        var result = await _groupsService.VotePollAsync(groupId, pollId, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("{groupId:guid}/polls/{pollId:guid}/close")]
    public async Task<IActionResult> ClosePoll(Guid groupId, Guid pollId, CancellationToken ct)
    {
        var result = await _groupsService.ClosePollAsync(groupId, pollId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{groupId:guid}/polls/{pollId:guid}/results")]
    public async Task<IActionResult> GetPollResults(Guid groupId, Guid pollId, CancellationToken ct)
    {
        var result = await _groupsService.GetPollResultsAsync(groupId, pollId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("invitations/{token}/accept")]
    public async Task<IActionResult> AcceptInvitation(string token, CancellationToken ct)
    {
        var result = await _groupsService.AcceptInvitationAsync(token, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("invitations/{token}/reject")]
    public async Task<IActionResult> RejectInvitation(string token, CancellationToken ct)
    {
        var result = await _groupsService.RejectInvitationAsync(token, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddExistingMember(Guid id, AddGroupMemberRequest request, CancellationToken ct)
    {
        var result = await _groupsService.AddExistingMemberAsync(id, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("{id:guid}/members/{userId:guid}")]
    [HttpPatch("{id:guid}/members/{userId:guid}/role")]
    public async Task<IActionResult> UpdateMemberRole(Guid id, Guid userId, UpdateGroupMemberRoleRequest request, CancellationToken ct)
    {
        var result = await _groupsService.UpdateMemberRoleAsync(id, userId, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        var result = await _groupsService.RemoveMemberAsync(id, userId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await _groupsService.GetMessagesAsync(id, page, pageSize, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid id, SendGroupMessageRequest request, CancellationToken ct)
    {
        var result = await _groupsService.CreateMessageAsync(id, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file == null)
        {
            return BadRequest(new { error = "Vui lòng chọn tệp." });
        }

        await using var stream = file.OpenReadStream();
        var result = await _groupAttachmentService.UploadAsync(
            id,
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id:guid}/avatar")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadAvatar(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "Vui lòng chọn ảnh đại diện." });
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return BadRequest(new { error = "Ảnh đại diện phải có dung lượng không quá 5 MB." });
        }

        var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/gif",
            "image/webp"
        };
        if (!allowedTypes.Contains(file.ContentType))
        {
            return BadRequest(new { error = "Ảnh đại diện phải có định dạng JPG, PNG, GIF hoặc WEBP." });
        }

        if (!await _groupsService.CanManageGroupAsync(id, ct))
        {
            return Forbid();
        }

        await using var stream = file.OpenReadStream();
        var upload = await _groupAttachmentService.UploadAsync(
            id,
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            ct);
        if (!upload.IsSuccess || upload.Data == null)
        {
            return StatusCode(upload.StatusCode, upload);
        }

        var avatarUrl = $"/api/groups/{id}/attachments/{upload.Data.Id}";
        var result = await _groupsService.UpdateAvatarAsync(id, avatarUrl, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadAttachment(
        Guid id,
        Guid attachmentId,
        [FromQuery] bool download = false,
        CancellationToken ct = default)
    {
        var result = await _groupAttachmentService.DownloadAsync(id, attachmentId, ct);
        if (!result.IsSuccess || result.Data == null)
        {
            return StatusCode(result.StatusCode, result);
        }

        var canPreview = result.Data.ContentType is
            "image/jpeg" or
            "image/png" or
            "image/gif" or
            "image/webp";

        return download || !canPreview
            ? File(result.Data.Stream, result.Data.ContentType, result.Data.FileName, enableRangeProcessing: true)
            : File(result.Data.Stream, result.Data.ContentType, enableRangeProcessing: true);
    }

    [HttpPatch("{id:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> UpdateMessage(
        Guid id,
        Guid messageId,
        UpdateGroupMessageRequest request,
        CancellationToken ct)
    {
        var result = await _groupsService.UpdateMessageAsync(id, messageId, request, ct);
        await BroadcastMessageChangedAsync(id, result, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id:guid}/messages/{messageId:guid}/recall")]
    public async Task<IActionResult> RecallMessage(Guid id, Guid messageId, CancellationToken ct)
    {
        var result = await _groupsService.RecallMessageAsync(id, messageId, ct);
        await BroadcastMessageChangedAsync(id, result, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("{id:guid}/messages/{messageId:guid}/pin")]
    public async Task<IActionResult> SetMessagePin(
        Guid id,
        Guid messageId,
        SetGroupMessagePinRequest request,
        CancellationToken ct)
    {
        var result = await _groupsService.SetMessagePinAsync(id, messageId, request, ct);
        await BroadcastMessageChangedAsync(id, result, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id:guid}/messages/{messageId:guid}/reactions")]
    public async Task<IActionResult> ReactToMessage(
        Guid id,
        Guid messageId,
        ReactToGroupMessageRequest request,
        CancellationToken ct)
    {
        var result = await _groupsService.ToggleMessageReactionAsync(id, messageId, request, ct);
        await BroadcastMessageChangedAsync(id, result, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{id:guid}/messages/{messageId:guid}/for-me")]
    public async Task<IActionResult> HideMessageForMe(Guid id, Guid messageId, CancellationToken ct)
    {
        var result = await _groupsService.HideMessageForCurrentUserAsync(id, messageId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id:guid}/create-project")]
    public async Task<IActionResult> CreateProjectFromGroup(Guid id, CreateProjectFromGroupRequest request, CancellationToken ct)
    {
        var result = await _groupsService.CreateProjectFromGroupAsync(id, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id:guid}/meetings/start")]
    public async Task<IActionResult> StartMeeting(Guid id, CancellationToken ct)
    {
        var result = await _groupsService.StartMeetingSessionAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    private async Task BroadcastMessageChangedAsync(
        Guid groupId,
        Result<GroupMessageDto> result,
        CancellationToken ct)
    {
        if (!result.IsSuccess || result.Data == null)
        {
            return;
        }

        await _groupHub.Clients
            .Group(GroupHub.WorkGroup(groupId))
            .SendAsync("groupMessageChanged", result.Data, ct);
    }

    [HttpGet("{id:guid}/meetings/active")]
    public async Task<IActionResult> GetActiveMeeting(Guid id, CancellationToken ct)
    {
        var result = await _groupsService.GetActiveMeetingSessionAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{groupId:guid}/meetings/{meetingId:guid}/join")]
    public async Task<IActionResult> JoinMeeting(Guid groupId, Guid meetingId, CancellationToken ct)
    {
        var result = await _groupsService.JoinMeetingSessionAsync(groupId, meetingId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{groupId:guid}/meetings/{meetingId:guid}/end")]
    public async Task<IActionResult> EndMeeting(Guid groupId, Guid meetingId, CancellationToken ct)
    {
        var result = await _groupsService.EndMeetingSessionAsync(groupId, meetingId, ct);
        return StatusCode(result.StatusCode, result);
    }
}
