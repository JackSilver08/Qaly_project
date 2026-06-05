using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Qaly.Application.DTOs.Groups;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/groups")]
public class GroupsController : BaseApiController
{
    private readonly IGroupsService _groupsService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IConfiguration _configuration;

    public GroupsController(
        IGroupsService groupsService,
        IFileStorageService fileStorageService,
        IConfiguration configuration)
    {
        _groupsService = groupsService;
        _fileStorageService = fileStorageService;
        _configuration = configuration;
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
            return BadRequest(new { error = "Invitation request payload is required." });
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

    [HttpPost("{id:guid}/files")]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> UploadGroupFile(Guid id, IFormFile file, CancellationToken ct)
    {
        if (!await _groupsService.CanAccessGroupAsync(id, ct))
        {
            return Forbid();
        }

        if (file.Length == 0)
        {
            return BadRequest(new { error = "File is empty." });
        }

        await using var stream = file.OpenReadStream();
        var storedPath = await _fileStorageService.UploadAsync(stream, file.FileName, file.ContentType, ct);
        var fileKey = Path.GetFileName(storedPath);
        var safeName = Path.GetFileName(file.FileName);
        var url = Url.Action(nameof(DownloadGroupFile), values: new { id, fileKey, name = safeName }) ??
                  $"/api/groups/{id}/files/{Uri.EscapeDataString(fileKey)}?name={Uri.EscapeDataString(safeName)}";

        return Ok(new
        {
            isSuccess = true,
            data = new
            {
                name = safeName,
                url,
                sizeLabel = FormatFileSize(file.Length),
                kind = IsImage(file.ContentType, safeName) ? "image" : "file",
                contentType = file.ContentType
            }
        });
    }

    [HttpGet("{id:guid}/files/{fileKey}")]
    public async Task<IActionResult> DownloadGroupFile(Guid id, string fileKey, [FromQuery] string? name, CancellationToken ct)
    {
        if (!await _groupsService.CanAccessGroupAsync(id, ct))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(fileKey) || fileKey != Path.GetFileName(fileKey))
        {
            return BadRequest(new { error = "Invalid file key." });
        }

        var fullPath = Path.Combine(GetFileStorageRootPath(), fileKey);
        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound(new { error = "File was not found." });
        }

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(name ?? fileKey, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        var downloadName = string.IsNullOrWhiteSpace(name) ? fileKey : Path.GetFileName(name);
        return PhysicalFile(fullPath, contentType, downloadName, enableRangeProcessing: true);
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

    private string GetFileStorageRootPath()
        => _configuration.GetValue<string>("FileStorage:RootPath")
           ?? Path.Combine(AppContext.BaseDirectory, "uploads");

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{Math.Round(bytes / 1024d)} KB";
        return $"{Math.Round(bytes / 1024d / 1024d, 1)} MB";
    }

    private static bool IsImage(string? contentType, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(contentType) &&
            contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".svg";
    }
}
