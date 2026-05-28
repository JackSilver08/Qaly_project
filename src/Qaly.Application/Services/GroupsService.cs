using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Groups;
using Qaly.Application.DTOs.Project;
using Qaly.Application.Services.Groups;
using Qaly.Domain.Entities;
using Qaly.Domain.Enums;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public partial class GroupsService : IGroupsService
{
    private readonly IRepository<WorkGroup> _groupRepo;
    private readonly IRepository<WorkGroupMember> _memberRepo;
    private readonly IRepository<GroupInvitation> _invitationRepo;
    private readonly IRepository<GroupPoll> _pollRepo;
    private readonly IRepository<GroupPollOption> _pollOptionRepo;
    private readonly IRepository<GroupPollVote> _pollVoteRepo;
    private readonly IRepository<GroupMessage> _messageRepo;
    private readonly IRepository<Organization> _organizationRepo;
    private readonly IRepository<OrganizationMember> _organizationMemberRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IProjectService _projectService;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;
    private readonly IEmailService _emailService;
    private readonly IGroupInvitationEmailBuilder _groupInvitationEmailBuilder;
    private readonly IGroupPollRealtimePublisher _groupPollRealtimePublisher;
    private readonly ILogger<GroupsService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GroupsService(
        IRepository<WorkGroup> groupRepo,
        IRepository<WorkGroupMember> memberRepo,
        IRepository<GroupInvitation> invitationRepo,
        IRepository<GroupPoll> pollRepo,
        IRepository<GroupPollOption> pollOptionRepo,
        IRepository<GroupPollVote> pollVoteRepo,
        IRepository<GroupMessage> messageRepo,
        IRepository<Organization> organizationRepo,
        IRepository<OrganizationMember> organizationMemberRepo,
        IRepository<User> userRepo,
        IProjectService projectService,
        INotificationService notificationService,
        IAuditLogService auditLogService,
        IEmailService emailService,
        IGroupInvitationEmailBuilder groupInvitationEmailBuilder,
        IGroupPollRealtimePublisher groupPollRealtimePublisher,
        ILogger<GroupsService> logger,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _groupRepo = groupRepo;
        _memberRepo = memberRepo;
        _invitationRepo = invitationRepo;
        _pollRepo = pollRepo;
        _pollOptionRepo = pollOptionRepo;
        _pollVoteRepo = pollVoteRepo;
        _messageRepo = messageRepo;
        _organizationRepo = organizationRepo;
        _organizationMemberRepo = organizationMemberRepo;
        _userRepo = userRepo;
        _projectService = projectService;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
        _emailService = emailService;
        _groupInvitationEmailBuilder = groupInvitationEmailBuilder;
        _groupPollRealtimePublisher = groupPollRealtimePublisher;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Skipped sending group invitation email because token is missing. InvitationId: {InvitationId}, GroupId: {GroupId}.")]
    private static partial void LogSkippedMissingInvitationToken(ILogger logger, Guid invitationId, Guid groupId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Could not send group invitation email. InvitationId: {InvitationId}, GroupId: {GroupId}, RecipientEmail: {RecipientEmail}.")]
    private static partial void LogCouldNotSendGroupInvitationEmail(ILogger logger, Exception ex, Guid invitationId, Guid groupId, string recipientEmail);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Could not broadcast poll updated realtime event. GroupId: {GroupId}, PollId: {PollId}.")]
    private static partial void LogCouldNotBroadcastPollUpdated(ILogger logger, Exception ex, Guid groupId, Guid pollId);

    public async Task<Result<PagedResult<GroupDto>>> GetMineAsync(int page = 1, int pageSize = 20, string? search = null, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<PagedResult<GroupDto>>();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = GroupDetailsQuery();
        if (!IsSystemAdmin())
        {
            query = query.Where(group => group.OwnerId == currentUserId || group.Members.Any(member => member.UserId == currentUserId));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim();
            query = query.Where(group =>
                group.Name.Contains(normalized) ||
                (group.Description != null && group.Description.Contains(normalized)));
        }

        var totalCount = await query.CountAsync(ct);
        var groups = await query
            .OrderByDescending(group => group.UpdatedAt ?? group.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Result.Success(new PagedResult<GroupDto>
        {
            Items = groups.Select(group => ToDto(group, currentUserId.Value)).ToList(),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<GroupDto>> GetByIdAsync(Guid groupId, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<GroupDto>();
        }

        var group = await GroupDetailsQuery()
            .FirstOrDefaultAsync(item => item.Id == groupId, ct);
        if (group == null)
        {
            return Result.NotFound<GroupDto>();
        }

        if (!await CanAccessGroupAsync(groupId, ct))
        {
            return Result.Forbidden<GroupDto>();
        }

        return Result.Success(ToDto(group, currentUserId.Value));
    }

    public async Task<Result<GroupDto>> CreateAsync(CreateGroupRequest request, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<GroupDto>();
        }

        var validation = ValidateGroup(request.Name, request.Color);
        if (!validation.IsSuccess)
        {
            return Result.Failure<GroupDto>(validation.Error!, validation.StatusCode);
        }

        if (request.OrganizationId.HasValue)
        {
            var organization = await _organizationRepo.GetByIdAsync(request.OrganizationId.Value, ct);
            if (organization == null)
            {
                return Result.Failure<GroupDto>("Organization was not found.", 404);
            }

            if (!organization.IsActive)
            {
                return Result.Failure<GroupDto>("Organization is inactive.", 400);
            }

            if (!await CanAccessOrganizationAsync(request.OrganizationId.Value, organization.OwnerId, ct))
            {
                return Result.Forbidden<GroupDto>();
            }
        }

        var group = new WorkGroup
        {
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            AvatarUrl = NormalizeOptional(request.AvatarUrl),
            Color = NormalizeOptional(request.Color),
            OrganizationId = request.OrganizationId,
            OwnerId = currentUserId.Value
        };

        await _groupRepo.AddAsync(group, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _memberRepo.AddAsync(new WorkGroupMember
        {
            WorkGroupId = group.Id,
            UserId = currentUserId.Value,
            Role = GroupRoleRules.Owner
        }, ct);

        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Create", nameof(WorkGroup), group.Id.ToString(), new { group.Name }, ct);

        return await GetByIdAsync(group.Id, ct);
    }

    public async Task<Result<GroupDto>> UpdateAsync(Guid groupId, UpdateGroupRequest request, CancellationToken ct = default)
    {
        var group = await _groupRepo.GetByIdAsync(groupId, ct);
        if (group == null)
        {
            return Result.NotFound<GroupDto>();
        }

        if (!await CanManageGroupAsync(groupId, ct))
        {
            return Result.Forbidden<GroupDto>();
        }

        var validation = ValidateGroup(request.Name, request.Color);
        if (!validation.IsSuccess)
        {
            return Result.Failure<GroupDto>(validation.Error!, validation.StatusCode);
        }

        group.Name = request.Name.Trim();
        group.Description = NormalizeOptional(request.Description);
        group.AvatarUrl = NormalizeOptional(request.AvatarUrl);
        group.Color = NormalizeOptional(request.Color);
        group.Status = NormalizeGroupStatus(request.Status);

        await _groupRepo.UpdateAsync(group, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Update", nameof(WorkGroup), groupId.ToString(), request, ct);

        return await GetByIdAsync(groupId, ct);
    }

    public async Task<Result> DeleteAsync(Guid groupId, CancellationToken ct = default)
    {
        var group = await _groupRepo.GetByIdAsync(groupId, ct);
        if (group == null)
        {
            return Result.NotFound();
        }

        if (!await CanManageGroupAsync(groupId, ct))
        {
            return Result.Forbidden();
        }

        await _groupRepo.DeleteAsync(group, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Delete", nameof(WorkGroup), groupId.ToString(), new { group.Name }, ct);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<GroupMemberDto>>> GetMembersAsync(Guid groupId, CancellationToken ct = default)
    {
        if (!await CanAccessGroupAsync(groupId, ct))
        {
            return Result.Forbidden<IReadOnlyList<GroupMemberDto>>();
        }

        var members = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Include(member => member.User)
            .Where(member => member.WorkGroupId == groupId)
            .OrderBy(member => member.Role == GroupRoleRules.Owner ? 0 : member.Role == GroupRoleRules.Admin ? 1 : 2)
            .ThenBy(member => member.User.FullName)
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<GroupMemberDto>>(members.Select(ToMemberDto).ToList());
    }

    public async Task<Result<GroupInvitationDto>> CreateInvitationAsync(Guid groupId, CreateGroupInvitationRequest request, CancellationToken ct = default)
    {
        if (request == null)
        {
            return Result.Failure<GroupInvitationDto>("Invitation request is required.");
        }

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<GroupInvitationDto>();
        }

        var groupSummary = await _groupRepo.GetQueryable()
            .AsNoTracking()
            .Where(group => group.Id == groupId)
            .Select(group => new
            {
                group.Id,
                group.Name
            })
            .FirstOrDefaultAsync(ct);
        if (groupSummary == null)
        {
            return Result.NotFound<GroupInvitationDto>("Group was not found.");
        }

        var membershipRole = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.WorkGroupId == groupId && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        if (membershipRole == null || !GroupRoleRules.CanManage(membershipRole))
        {
            return Result.Forbidden<GroupInvitationDto>("Bạn không có quyền mời thành viên vào nhóm này.");
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return Result.Failure<GroupInvitationDto>("Email is required.");
        }

        if (!IsValidEmail(normalizedEmail))
        {
            return Result.Failure<GroupInvitationDto>("Email is invalid.");
        }

        var isMember = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .AnyAsync(member =>
                member.WorkGroupId == groupId &&
                member.User.Email == normalizedEmail, ct);
        if (isMember)
        {
            return Result.Failure<GroupInvitationDto>("Email is already a group member.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var hasPendingInvitation = await _invitationRepo.GetQueryable()
            .AsNoTracking()
            .AnyAsync(invitation =>
                invitation.GroupId == groupId &&
                invitation.Email == normalizedEmail &&
                invitation.Status == GroupInvitationStatus.Pending &&
                invitation.ExpiredAt > now, ct);

        if (hasPendingInvitation)
        {
            return Result.Failure<GroupInvitationDto>("A pending invitation already exists for this email.", 409);
        }

        var invitation = new GroupInvitation
        {
            GroupId = groupId,
            Email = normalizedEmail,
            Token = await GenerateUniqueInvitationTokenAsync(ct),
            Status = GroupInvitationStatus.Pending,
            ExpiredAt = now.AddDays(7)
        };

        await _invitationRepo.AddAsync(invitation, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("CreateInvitation", nameof(WorkGroup), groupId.ToString(), new
        {
            invitation.Id,
            invitation.Email,
            invitation.ExpiredAt
        }, ct);

        await NotifyInvitedExistingUserAsync(groupSummary.Name, invitation, ct);
        await SendInvitationEmailBestEffortAsync(groupSummary.Name, invitation, currentUserId.Value, ct);

        return Result.Created(ToInvitationDto(invitation));
    }

    public async Task<Result<GroupPollDto>> CreatePollAsync(Guid groupId, CreateGroupPollRequest request, CancellationToken ct = default)
    {
        if (groupId == Guid.Empty)
        {
            return Result.Failure<GroupPollDto>("Group id is required.");
        }

        if (request == null)
        {
            return Result.Failure<GroupPollDto>("Poll request is required.");
        }

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Failure<GroupPollDto>("Authentication is required.", 401);
        }

        var group = await _groupRepo.GetByIdAsync(groupId, ct);
        if (group == null)
        {
            return Result.NotFound<GroupPollDto>("Group was not found.");
        }

        var membershipRole = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.WorkGroupId == groupId && member.UserId == currentUserId.Value)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        if (membershipRole == null)
        {
            return Result.Forbidden<GroupPollDto>("Current user is not a group member.");
        }

        if (!GroupRoleRules.CanCreatePoll(membershipRole))
        {
            return Result.Forbidden<GroupPollDto>("Bạn không có quyền tạo bình chọn trong nhóm này.");
        }

        var normalizedQuestion = NormalizeOptional(request.Question);
        if (string.IsNullOrWhiteSpace(normalizedQuestion))
        {
            return Result.Failure<GroupPollDto>("Question is required.");
        }

        if (normalizedQuestion.Length > 500)
        {
            return Result.Failure<GroupPollDto>("Question must be 500 characters or fewer.");
        }

        if (request.Options == null || request.Options.Count < 2)
        {
            return Result.Failure<GroupPollDto>("Poll must contain at least 2 options.");
        }

        var normalizedOptions = new List<string>(request.Options.Count);
        var dedupe = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < request.Options.Count; index++)
        {
            var normalizedContent = NormalizeOptional(request.Options[index]?.Content);
            if (string.IsNullOrWhiteSpace(normalizedContent))
            {
                return Result.Failure<GroupPollDto>("Option content is required.");
            }

            if (normalizedContent.Length > 300)
            {
                return Result.Failure<GroupPollDto>("Option content must be 300 characters or fewer.");
            }

            var dedupeKey = normalizedContent.ToLowerInvariant();
            if (!dedupe.Add(dedupeKey))
            {
                return Result.Failure<GroupPollDto>("Poll options must be unique.");
            }

            normalizedOptions.Add(normalizedContent);
        }

        if (request.ExpiredAt.HasValue && request.ExpiredAt.Value <= DateTimeOffset.UtcNow)
        {
            return Result.Failure<GroupPollDto>("ExpiredAt must be in the future.");
        }

        var poll = new GroupPoll
        {
            GroupId = groupId,
            Question = normalizedQuestion,
            CreatedByUserId = currentUserId.Value,
            AllowMultiple = request.AllowMultiple,
            Status = GroupPollStatus.Open,
            ExpiredAt = request.ExpiredAt
        };

        await _pollRepo.AddAsync(poll, ct);

        var options = normalizedOptions
            .Select((content, index) => new GroupPollOption
            {
                PollId = poll.Id,
                Content = content,
                SortOrder = index + 1
            })
            .ToList();

        await _pollOptionRepo.AddRangeAsync(options, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _auditLogService.LogAsync("CreatePoll", nameof(WorkGroup), groupId.ToString(), new
        {
            PollId = poll.Id,
            poll.Question,
            poll.AllowMultiple,
            poll.ExpiredAt,
            OptionCount = options.Count
        }, ct);

        return Result.Created(ToPollDto(poll, options));
    }

    public async Task<Result<GroupPollResultsDto>> VotePollAsync(Guid groupId, Guid pollId, VoteGroupPollRequest request, CancellationToken ct = default)
    {
        if (groupId == Guid.Empty)
        {
            return Result.Failure<GroupPollResultsDto>("Group id is required.");
        }

        if (pollId == Guid.Empty)
        {
            return Result.Failure<GroupPollResultsDto>("Poll id is required.");
        }

        if (request?.OptionIds == null || request.OptionIds.Count == 0)
        {
            return Result.Failure<GroupPollResultsDto>("At least one option must be selected.");
        }

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Failure<GroupPollResultsDto>("Authentication is required.", 401);
        }

        var group = await _groupRepo.GetByIdAsync(groupId, ct);
        if (group == null)
        {
            return Result.NotFound<GroupPollResultsDto>("Group was not found.");
        }

        var membershipRole = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.WorkGroupId == groupId && member.UserId == currentUserId.Value)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        if (membershipRole == null)
        {
            return Result.Forbidden<GroupPollResultsDto>("Current user is not a group member.");
        }

        var poll = await _pollRepo.GetQueryable()
            .FirstOrDefaultAsync(item => item.Id == pollId && item.GroupId == groupId, ct);
        if (poll == null)
        {
            return Result.NotFound<GroupPollResultsDto>("Poll was not found.");
        }

        if (poll.Status != GroupPollStatus.Open)
        {
            return Result.Failure<GroupPollResultsDto>("Poll is already closed.", 409);
        }

        if (poll.ExpiredAt.HasValue && poll.ExpiredAt.Value <= DateTimeOffset.UtcNow)
        {
            return Result.Failure<GroupPollResultsDto>("Poll has expired.", 409);
        }

        if (request.OptionIds.Any(optionId => optionId == Guid.Empty))
        {
            return Result.Failure<GroupPollResultsDto>("Option id is required.");
        }

        var requestedOptionIds = request.OptionIds.ToList();
        var deduplicatedOptionIds = requestedOptionIds.Distinct().ToList();
        if (requestedOptionIds.Count != deduplicatedOptionIds.Count)
        {
            return Result.Failure<GroupPollResultsDto>("Option ids must be unique.");
        }

        if (!poll.AllowMultiple && deduplicatedOptionIds.Count != 1)
        {
            return Result.Failure<GroupPollResultsDto>("This poll allows selecting exactly one option.");
        }

        var pollOptions = await _pollOptionRepo.GetQueryable()
            .AsNoTracking()
            .Where(option => option.PollId == poll.Id)
            .OrderBy(option => option.SortOrder)
            .ToListAsync(ct);

        var pollOptionIdSet = pollOptions
            .Select(option => option.Id)
            .ToHashSet();

        if (deduplicatedOptionIds.Any(optionId => !pollOptionIdSet.Contains(optionId)))
        {
            return Result.NotFound<GroupPollResultsDto>("One or more poll options were not found.");
        }

        var requestedOptionIdSet = deduplicatedOptionIds.ToHashSet();
        var existingVotes = await _pollVoteRepo.GetQueryable()
            .Where(vote => vote.PollId == poll.Id && vote.UserId == currentUserId.Value)
            .ToListAsync(ct);

        var existingOptionIdSet = existingVotes
            .Select(vote => vote.OptionId)
            .ToHashSet();

        foreach (var vote in existingVotes.Where(vote => !requestedOptionIdSet.Contains(vote.OptionId)))
        {
            await _pollVoteRepo.DeleteAsync(vote, ct);
        }

        foreach (var optionId in deduplicatedOptionIds.Where(optionId => !existingOptionIdSet.Contains(optionId)))
        {
            await _pollVoteRepo.AddAsync(new GroupPollVote
            {
                PollId = poll.Id,
                OptionId = optionId,
                UserId = currentUserId.Value
            }, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("VotePoll", nameof(WorkGroup), groupId.ToString(), new
        {
            PollId = poll.Id,
            UserId = currentUserId.Value,
            OptionIds = deduplicatedOptionIds
        }, ct);

        var pollResults = await BuildPollResultsDtoAsync(poll, pollOptions, currentUserId.Value, ct);
        var updatedAt = DateTimeOffset.UtcNow;

        try
        {
            await _groupPollRealtimePublisher.PublishPollUpdatedAsync(groupId, poll.Id, pollResults, updatedAt, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogCouldNotBroadcastPollUpdated(_logger, ex, groupId, poll.Id);
        }

        return Result.Success(pollResults);
    }

    public async Task<Result<GroupPollDto>> ClosePollAsync(Guid groupId, Guid pollId, CancellationToken ct = default)
    {
        if (groupId == Guid.Empty)
        {
            return Result.Failure<GroupPollDto>("Group id is required.");
        }

        if (pollId == Guid.Empty)
        {
            return Result.Failure<GroupPollDto>("Poll id is required.");
        }

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Failure<GroupPollDto>("Authentication is required.", 401);
        }

        var group = await _groupRepo.GetByIdAsync(groupId, ct);
        if (group == null)
        {
            return Result.NotFound<GroupPollDto>("Group was not found.");
        }

        var membershipRole = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.WorkGroupId == groupId && member.UserId == currentUserId.Value)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        if (membershipRole == null)
        {
            return Result.Forbidden<GroupPollDto>("Current user is not a group member.");
        }

        var poll = await _pollRepo.GetQueryable()
            .FirstOrDefaultAsync(item => item.Id == pollId && item.GroupId == groupId, ct);
        if (poll == null)
        {
            return Result.NotFound<GroupPollDto>("Poll was not found.");
        }

        var canClosePoll = GroupRoleRules.CanClosePoll(membershipRole, poll.CreatedByUserId == currentUserId.Value);
        if (!canClosePoll)
        {
            return Result.Forbidden<GroupPollDto>("Bạn không có quyền đóng bình chọn này.");
        }

        if (poll.Status == GroupPollStatus.Closed)
        {
            var closedPollOptions = await _pollOptionRepo.GetQueryable()
                .AsNoTracking()
                .Where(option => option.PollId == poll.Id)
                .OrderBy(option => option.SortOrder)
                .ToListAsync(ct);
            return Result.Success(ToPollDto(poll, closedPollOptions));
        }

        poll.Status = GroupPollStatus.Closed;
        poll.ClosedAt ??= DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync(ct);

        var options = await _pollOptionRepo.GetQueryable()
            .AsNoTracking()
            .Where(option => option.PollId == poll.Id)
            .OrderBy(option => option.SortOrder)
            .ToListAsync(ct);

        await _auditLogService.LogAsync("ClosePoll", nameof(WorkGroup), groupId.ToString(), new
        {
            PollId = poll.Id,
            ClosedByUserId = currentUserId.Value,
            poll.ClosedAt
        }, ct);

        return Result.Success(ToPollDto(poll, options));
    }

    public async Task<Result<GroupPollResultsDto>> GetPollResultsAsync(Guid groupId, Guid pollId, CancellationToken ct = default)
    {
        if (groupId == Guid.Empty)
        {
            return Result.Failure<GroupPollResultsDto>("Group id is required.");
        }

        if (pollId == Guid.Empty)
        {
            return Result.Failure<GroupPollResultsDto>("Poll id is required.");
        }

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Failure<GroupPollResultsDto>("Authentication is required.", 401);
        }

        var group = await _groupRepo.GetByIdAsync(groupId, ct);
        if (group == null)
        {
            return Result.NotFound<GroupPollResultsDto>("Group was not found.");
        }

        var isMember = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .AnyAsync(member => member.WorkGroupId == groupId && member.UserId == currentUserId.Value, ct);

        if (!isMember)
        {
            return Result.Forbidden<GroupPollResultsDto>("Current user is not a group member.");
        }

        var poll = await _pollRepo.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == pollId && item.GroupId == groupId, ct);
        if (poll == null)
        {
            return Result.NotFound<GroupPollResultsDto>("Poll was not found.");
        }

        var options = await _pollOptionRepo.GetQueryable()
            .AsNoTracking()
            .Where(option => option.PollId == poll.Id)
            .OrderBy(option => option.SortOrder)
            .ToListAsync(ct);

        var pollResults = await BuildPollResultsDtoAsync(poll, options, currentUserId.Value, ct);
        return Result.Success(pollResults);
    }

    public async Task<Result<GroupInvitationDto>> AcceptInvitationAsync(string token, CancellationToken ct = default)
    {
        var normalizedToken = NormalizeToken(token);
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return Result.Failure<GroupInvitationDto>("Invitation token is required.");
        }

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Failure<GroupInvitationDto>("Authentication is required.", 401);
        }

        var invitation = await _invitationRepo.GetQueryable()
            .FirstOrDefaultAsync(item => item.Token == normalizedToken, ct);
        if (invitation == null)
        {
            return Result.NotFound<GroupInvitationDto>("Invitation was not found.");
        }

        var stateValidationResult = await ValidateInvitationCanBeActionedAsync(invitation, ct);
        if (stateValidationResult != null)
        {
            return stateValidationResult;
        }

        var currentUserEmail = await GetCurrentUserNormalizedEmailAsync(currentUserId.Value, ct);
        if (string.IsNullOrWhiteSpace(currentUserEmail))
        {
            return Result.Forbidden<GroupInvitationDto>("Current user email is not available.");
        }

        if (!string.Equals(currentUserEmail, invitation.Email, StringComparison.Ordinal))
        {
            return Result.Forbidden<GroupInvitationDto>("Invitation does not belong to the current user.");
        }

        var group = await _groupRepo.GetByIdAsync(invitation.GroupId, ct);
        if (group == null)
        {
            return Result.NotFound<GroupInvitationDto>("Group was not found.");
        }

        var isMember = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .AnyAsync(member => member.WorkGroupId == invitation.GroupId && member.UserId == currentUserId.Value, ct);

        invitation.Status = GroupInvitationStatus.Accepted;

        if (!isMember)
        {
            await _memberRepo.AddAsync(new WorkGroupMember
            {
                WorkGroupId = invitation.GroupId,
                UserId = currentUserId.Value,
                Role = GroupRoleRules.Member
            }, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("AcceptInvitation", nameof(WorkGroup), invitation.GroupId.ToString(), new
        {
            invitation.Id,
            invitation.Email,
            UserId = currentUserId.Value,
            AlreadyMember = isMember
        }, ct);

        return Result.Success(ToInvitationDto(invitation));
    }

    public async Task<Result<GroupInvitationDto>> RejectInvitationAsync(string token, CancellationToken ct = default)
    {
        var normalizedToken = NormalizeToken(token);
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return Result.Failure<GroupInvitationDto>("Invitation token is required.");
        }

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Failure<GroupInvitationDto>("Authentication is required.", 401);
        }

        var invitation = await _invitationRepo.GetQueryable()
            .FirstOrDefaultAsync(item => item.Token == normalizedToken, ct);
        if (invitation == null)
        {
            return Result.NotFound<GroupInvitationDto>("Invitation was not found.");
        }

        var stateValidationResult = await ValidateInvitationCanBeActionedAsync(invitation, ct);
        if (stateValidationResult != null)
        {
            return stateValidationResult;
        }

        var currentUserEmail = await GetCurrentUserNormalizedEmailAsync(currentUserId.Value, ct);
        if (string.IsNullOrWhiteSpace(currentUserEmail))
        {
            return Result.Forbidden<GroupInvitationDto>("Current user email is not available.");
        }

        if (!string.Equals(currentUserEmail, invitation.Email, StringComparison.Ordinal))
        {
            return Result.Forbidden<GroupInvitationDto>("Invitation does not belong to the current user.");
        }

        invitation.Status = GroupInvitationStatus.Rejected;
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("RejectInvitation", nameof(WorkGroup), invitation.GroupId.ToString(), new
        {
            invitation.Id,
            invitation.Email,
            UserId = currentUserId.Value
        }, ct);

        return Result.Success(ToInvitationDto(invitation));
    }

    public async Task<Result> AddExistingMemberAsync(Guid groupId, AddGroupMemberRequest request, CancellationToken ct = default)
    {
        if (!await CanManageGroupAsync(groupId, ct))
        {
            return Result.Forbidden();
        }

        var group = await _groupRepo.GetByIdAsync(groupId, ct);
        if (group == null)
        {
            return Result.NotFound();
        }

        var user = await _userRepo.GetQueryable()
            .FirstOrDefaultAsync(item => item.Id == request.UserId && item.IsActive, ct);
        if (user == null)
        {
            return Result.Failure("User was not found.", 404);
        }

        var role = GroupRoleRules.Normalize(request.Role);
        var existing = await _memberRepo.GetQueryable()
            .FirstOrDefaultAsync(member => member.WorkGroupId == groupId && member.UserId == request.UserId, ct);

        if (existing != null)
        {
            if (group.OwnerId == request.UserId && role != GroupRoleRules.Owner)
            {
                return Result.Failure("Group owner role cannot be changed.", 400);
            }

            existing.Role = role;
            await _memberRepo.UpdateAsync(existing, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _auditLogService.LogAsync("UpdateMemberRole", nameof(WorkGroup), groupId.ToString(), new { request.UserId, role }, ct);
            return Result.Success();
        }

        await _memberRepo.AddAsync(new WorkGroupMember
        {
            WorkGroupId = groupId,
            UserId = request.UserId,
            Role = role
        }, ct);

        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("AddMember", nameof(WorkGroup), groupId.ToString(), new { request.UserId, role }, ct);
        await _notificationService.CreateAsync(
            request.UserId,
            $"You were added to group \"{group.Name}\".",
            "GroupInvite",
            "success",
            group.Id,
            nameof(WorkGroup),
            $"group:{group.Id}:member:{request.UserId}",
            ct);

        return Result.Success();
    }

    public async Task<Result> UpdateMemberRoleAsync(Guid groupId, Guid userId, UpdateGroupMemberRoleRequest request, CancellationToken ct = default)
    {
        if (groupId == Guid.Empty || userId == Guid.Empty)
        {
            return Result.Failure("Group id and user id are required.");
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Role))
        {
            return Result.Failure("Role is required.");
        }

        if (!GroupRoleRules.IsValid(request.Role))
        {
            return Result.Failure("Role is invalid.");
        }

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Failure("Authentication is required.", 401);
        }

        var group = await _groupRepo.GetByIdAsync(groupId, ct);
        if (group == null)
        {
            return Result.NotFound();
        }

        var currentMember = await _memberRepo.GetQueryable()
            .FirstOrDefaultAsync(item => item.WorkGroupId == groupId && item.UserId == currentUserId.Value, ct);
        if (currentMember == null)
        {
            return Result.Forbidden("Current user is not a group member.");
        }

        var member = await _memberRepo.GetQueryable()
            .FirstOrDefaultAsync(item => item.WorkGroupId == groupId && item.UserId == userId, ct);
        if (member == null)
        {
            return Result.Failure("Group member was not found.", 404);
        }

        if (!GroupRoleRules.CanChangeMemberRole(currentMember.Role, member.Role))
        {
            return Result.Forbidden("Bạn không có quyền thay đổi vai trò thành viên này.");
        }

        var normalizedCurrentRole = GroupRoleRules.Normalize(member.Role);
        var normalizedRequestedRole = GroupRoleRules.Normalize(request.Role);

        if (normalizedCurrentRole == normalizedRequestedRole)
        {
            return Result.Success();
        }

        if (normalizedRequestedRole == GroupRoleRules.Owner
            && !string.Equals(GroupRoleRules.Normalize(currentMember.Role), GroupRoleRules.Owner, StringComparison.Ordinal))
        {
            return Result.Forbidden("Only group owner can assign Owner role.");
        }

        if (normalizedCurrentRole == GroupRoleRules.Owner && normalizedRequestedRole != GroupRoleRules.Owner)
        {
            var ownerCount = await CountGroupOwnersAsync(groupId, ct);
            if (ownerCount <= 1)
            {
                return Result.Failure("Cannot downgrade the last owner of the group.", 409);
            }

            if (group.OwnerId == member.UserId)
            {
                return Result.Failure("Primary group owner role cannot be changed.", 409);
            }
        }

        member.Role = normalizedRequestedRole;
        await _memberRepo.UpdateAsync(member, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("UpdateMemberRole", nameof(WorkGroup), groupId.ToString(), new
        {
            userId,
            OldRole = normalizedCurrentRole,
            NewRole = member.Role
        }, ct);

        return Result.Success();
    }

    public async Task<Result> RemoveMemberAsync(Guid groupId, Guid userId, CancellationToken ct = default)
    {
        if (groupId == Guid.Empty || userId == Guid.Empty)
        {
            return Result.Failure("Group id and user id are required.");
        }

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Failure("Authentication is required.", 401);
        }

        var group = await _groupRepo.GetByIdAsync(groupId, ct);
        if (group == null)
        {
            return Result.NotFound();
        }

        var currentMember = await _memberRepo.GetQueryable()
            .FirstOrDefaultAsync(item => item.WorkGroupId == groupId && item.UserId == currentUserId.Value, ct);
        if (currentMember == null)
        {
            return Result.Forbidden("Current user is not a group member.");
        }

        var member = await _memberRepo.GetQueryable()
            .FirstOrDefaultAsync(item => item.WorkGroupId == groupId && item.UserId == userId, ct);
        if (member == null)
        {
            return Result.Failure("Group member was not found.", 404);
        }

        var isSelfAction = currentUserId.Value == userId;
        if (!GroupRoleRules.CanRemoveMember(currentMember.Role, member.Role, isSelfAction))
        {
            return Result.Forbidden("Bạn không có quyền xóa thành viên này.");
        }

        if (GroupRoleRules.Normalize(member.Role) == GroupRoleRules.Owner)
        {
            var ownerCount = await CountGroupOwnersAsync(groupId, ct);
            if (ownerCount <= 1)
            {
                return Result.Failure("Cannot remove the last owner of the group.", 409);
            }

            if (group.OwnerId == member.UserId)
            {
                return Result.Failure("Primary group owner cannot be removed from the group.", 409);
            }
        }

        await _memberRepo.DeleteAsync(member, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("RemoveMember", nameof(WorkGroup), groupId.ToString(), new
        {
            userId,
            RemovedBy = currentUserId.Value,
            IsSelfAction = isSelfAction
        }, ct);

        return Result.Success();
    }

    public async Task<Result<PagedResult<GroupMessageDto>>> GetMessagesAsync(Guid groupId, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        if (!await CanAccessGroupAsync(groupId, ct))
        {
            return Result.Forbidden<PagedResult<GroupMessageDto>>();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _messageRepo.GetQueryable()
            .AsNoTracking()
            .Include(message => message.User)
            .Where(message => message.WorkGroupId == groupId);

        var totalCount = await query.CountAsync(ct);
        var messages = await query
            .OrderByDescending(message => message.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        messages.Reverse();

        return Result.Success(new PagedResult<GroupMessageDto>
        {
            Items = messages.Select(ToMessageDto).ToList(),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<GroupMessageDto>> CreateMessageAsync(Guid groupId, SendGroupMessageRequest request, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<GroupMessageDto>();
        }

        if (!await CanAccessGroupAsync(groupId, ct))
        {
            return Result.Forbidden<GroupMessageDto>();
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Result.Failure<GroupMessageDto>("Message content is required.");
        }

        if (request.Content.Trim().Length > 4000)
        {
            return Result.Failure<GroupMessageDto>("Message content must be 4000 characters or fewer.");
        }

        var message = new GroupMessage
        {
            WorkGroupId = groupId,
            UserId = currentUserId.Value,
            Content = request.Content.Trim(),
            MessageType = NormalizeMessageType(request.MessageType)
        };

        await _messageRepo.AddAsync(message, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var saved = await _messageRepo.GetQueryable()
            .AsNoTracking()
            .Include(item => item.User)
            .FirstAsync(item => item.Id == message.Id, ct);

        return Result.Created(ToMessageDto(saved));
    }

    public async Task<Result<CreateProjectFromGroupResult>> CreateProjectFromGroupAsync(Guid groupId, CreateProjectFromGroupRequest request, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<CreateProjectFromGroupResult>();
        }

        if (!await CanManageGroupAsync(groupId, ct))
        {
            return Result.Forbidden<CreateProjectFromGroupResult>();
        }

        var group = await GroupDetailsQuery()
            .Include(item => item.Members)
                .ThenInclude(member => member.User)
            .FirstOrDefaultAsync(item => item.Id == groupId, ct);
        if (group == null)
        {
            return Result.NotFound<CreateProjectFromGroupResult>();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<CreateProjectFromGroupResult>("Project name is required.");
        }

        var projectResult = await _projectService.CreateAsync(new CreateProjectDto(
            request.Name.Trim(),
            request.Code,
            request.Description,
            null,
            request.StartDate,
            request.EndDate,
            group.OrganizationId,
            group.Id), ct);

        if (!projectResult.IsSuccess || projectResult.Data == null)
        {
            return Result.Failure<CreateProjectFromGroupResult>(projectResult.Error ?? "Project could not be created.", projectResult.StatusCode);
        }

        var addedUserIds = new List<Guid>();
        var warnings = new List<string>();

        foreach (var member in group.Members.Where(item => item.User.IsActive && item.UserId != currentUserId.Value))
        {
            var projectRole = GroupRoleRules.ToProjectRole(member.Role);
            var addResult = await _projectService.AddMemberAsync(projectResult.Data.Id, member.UserId, projectRole, ct);
            if (addResult.IsSuccess)
            {
                addedUserIds.Add(member.UserId);
            }
            else
            {
                warnings.Add($"{member.User.Email}: {addResult.Error}");
            }
        }

        var refreshedProject = await _projectService.GetByIdAsync(projectResult.Data.Id, ct);
        var project = refreshedProject.IsSuccess && refreshedProject.Data != null
            ? refreshedProject.Data
            : projectResult.Data;

        return Result.Created(new CreateProjectFromGroupResult(project, addedUserIds.Count, addedUserIds, warnings));
    }

    public async Task<bool> CanAccessGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsSystemAdmin())
        {
            return true;
        }

        return await _groupRepo.GetQueryable()
            .AnyAsync(group =>
                group.Id == groupId &&
                (group.OwnerId == currentUserId ||
                 group.Members.Any(member => member.UserId == currentUserId)), ct);
    }

    public async Task<bool> CanManageGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsSystemAdmin())
        {
            return true;
        }

        var membership = await _memberRepo.GetQueryable()
            .Where(member => member.WorkGroupId == groupId && member.UserId == currentUserId)
            .Select(member => new { member.Role })
            .FirstOrDefaultAsync(ct);

        if (membership == null)
        {
            return false;
        }

        return GroupRoleRules.CanManage(membership.Role);
    }

    private IQueryable<WorkGroup> GroupDetailsQuery()
        => _groupRepo.GetQueryable()
            .Include(group => group.Owner)
            .Include(group => group.Organization)
            .Include(group => group.Members)
                .ThenInclude(member => member.User)
            .Include(group => group.Messages)
            .Include(group => group.Polls);

    private GroupDto ToDto(WorkGroup group, Guid currentUserId)
    {
        var currentRole = group.OwnerId == currentUserId
            ? GroupRoleRules.Owner
            : group.Members.FirstOrDefault(member => member.UserId == currentUserId)?.Role;

        if (currentRole == null && IsSystemAdmin())
        {
            currentRole = GroupRoleRules.Admin;
        }

        return new GroupDto(
            group.Id,
            group.Name,
            group.Description,
            group.AvatarUrl,
            group.Color,
            group.Status,
            group.OwnerId,
            group.Owner?.FullName ?? string.Empty,
            group.OrganizationId,
            group.Organization?.Name,
            currentRole ?? GroupRoleRules.Member,
            group.Members?.Count ?? 0,
            group.Messages?.Count(message => !message.IsDeleted) ?? 0,
            group.Polls?.Count(poll => poll.Status == GroupPollStatus.Open) ?? 0,
            group.CreatedAt,
            group.UpdatedAt);
    }

    private static GroupMemberDto ToMemberDto(WorkGroupMember member)
        => new(
            member.UserId,
            member.User.FullName,
            member.User.Email,
            member.Role,
            member.JoinedAt);

    private static GroupInvitationDto ToInvitationDto(GroupInvitation invitation)
        => new(
            invitation.Id,
            invitation.GroupId,
            invitation.Email,
            invitation.Status.ToString(),
            invitation.ExpiredAt,
            invitation.CreatedAt);

    private static GroupPollDto ToPollDto(GroupPoll poll, IReadOnlyList<GroupPollOption> options)
        => new(
            poll.Id,
            poll.GroupId,
            poll.Question,
            poll.AllowMultiple,
            poll.Status.ToString(),
            poll.ExpiredAt,
            poll.CreatedByUserId,
            poll.CreatedAt,
            poll.UpdatedAt,
            options
                .OrderBy(option => option.SortOrder)
                .Select(option => new GroupPollOptionDto(option.Id, option.Content, option.SortOrder))
                .ToList());

    private async Task<GroupPollResultsDto> BuildPollResultsDtoAsync(GroupPoll poll, IReadOnlyList<GroupPollOption> options, Guid currentUserId, CancellationToken ct)
    {
        var votes = await _pollVoteRepo.GetQueryable()
            .AsNoTracking()
            .Where(vote => vote.PollId == poll.Id)
            .ToListAsync(ct);

        var voteCountByOption = votes
            .GroupBy(vote => vote.OptionId)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.Count());

        var currentUserOptionIds = votes
            .Where(vote => vote.UserId == currentUserId)
            .Select(vote => vote.OptionId)
            .Distinct()
            .ToList();

        var optionResults = options
            .OrderBy(option => option.SortOrder)
            .Select(option => new GroupPollOptionResultDto(
                option.Id,
                option.Content,
                option.SortOrder,
                voteCountByOption.GetValueOrDefault(option.Id)))
            .ToList();

        return new GroupPollResultsDto(
            poll.Id,
            poll.GroupId,
            poll.Question,
            poll.AllowMultiple,
            poll.Status.ToString(),
            poll.ExpiredAt,
            votes.Count,
            votes.Select(vote => vote.UserId).Distinct().Count(),
            optionResults,
            currentUserOptionIds);
    }

    private static GroupMessageDto ToMessageDto(GroupMessage message)
        => new(
            message.Id,
            message.WorkGroupId,
            message.UserId,
            message.User.FullName,
            message.User.AvatarUrl,
            message.IsDeleted ? string.Empty : message.Content,
            message.MessageType,
            message.IsDeleted,
            message.CreatedAt,
            message.EditedAt);

    private async Task<bool> CanAccessOrganizationAsync(Guid organizationId, Guid ownerId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsSystemAdmin() || ownerId == currentUserId)
        {
            return true;
        }

        return await _organizationMemberRepo.GetQueryable()
            .AnyAsync(member => member.OrganizationId == organizationId && member.UserId == currentUserId, ct);
    }

    private bool IsSystemAdmin()
        => ProjectRoleRules.IsSystemAdmin(_currentUserService.Role);

    private static Result ValidateGroup(string name, string? color)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("Group name is required.");
        }

        if (name.Trim().Length > 160)
        {
            return Result.Failure("Group name must be 160 characters or fewer.");
        }

        if (!string.IsNullOrWhiteSpace(color) && color.Trim().Length > 20)
        {
            return Result.Failure("Group color must be 20 characters or fewer.");
        }

        return Result.Success();
    }

    private static string NormalizeGroupStatus(string? status)
    {
        if (string.Equals(status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            return "Archived";
        }

        return "Active";
    }

    private static string NormalizeMessageType(string? messageType)
    {
        if (string.Equals(messageType, "System", StringComparison.OrdinalIgnoreCase))
        {
            return "System";
        }

        if (string.Equals(messageType, "Meeting", StringComparison.OrdinalIgnoreCase))
        {
            return "Meeting";
        }

        if (string.Equals(messageType, "Poll", StringComparison.OrdinalIgnoreCase))
        {
            return "Poll";
        }

        return "Text";
    }

    private async Task<string> GenerateUniqueInvitationTokenAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var token = GenerateSecureToken();
            var exists = await _invitationRepo.GetQueryable()
                .AsNoTracking()
                .AnyAsync(invitation => invitation.Token == token, ct);
            if (!exists)
            {
                return token;
            }
        }

        return GenerateSecureToken();
    }

    private async Task NotifyInvitedExistingUserAsync(string groupName, GroupInvitation invitation, CancellationToken ct)
    {
        var invitedUser = await _userRepo.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.IsActive && user.Email == invitation.Email, ct);

        if (invitedUser == null)
        {
            return;
        }

        var message = $"Bạn nhận được lời mời tham gia nhóm \"{groupName}\" (hết hạn {invitation.ExpiredAt:yyyy-MM-dd HH:mm} UTC).";
        await _notificationService.CreateAsync(
            invitedUser.Id,
            message,
            "GroupInvitationReceived",
            "info",
            invitation.Id,
            nameof(GroupInvitation),
            $"group:{invitation.GroupId}:invitation:{invitation.Id}:recipient:{invitedUser.Id}",
            ct);
    }

    private async Task SendInvitationEmailBestEffortAsync(string groupName, GroupInvitation invitation, Guid inviterUserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(invitation.Token))
        {
            LogSkippedMissingInvitationToken(_logger, invitation.Id, invitation.GroupId);
            return;
        }

        try
        {
            var inviterName = await _userRepo.GetQueryable()
                .AsNoTracking()
                .Where(user => user.Id == inviterUserId)
                .Select(user => user.FullName)
                .FirstOrDefaultAsync(ct);

            var emailContent = _groupInvitationEmailBuilder.Build(
                groupName,
                inviterName,
                invitation.Token,
                invitation.ExpiredAt);

            await _emailService.SendAsync(invitation.Email, emailContent.Subject, emailContent.Body, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogCouldNotSendGroupInvitationEmail(_logger, ex, invitation.Id, invitation.GroupId, invitation.Email);
        }
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var parsed = new MailAddress(email);
            return string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string NormalizeEmail(string email)
        => string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();

    private async Task<Result<GroupInvitationDto>?> ValidateInvitationCanBeActionedAsync(GroupInvitation invitation, CancellationToken ct)
    {
        if (invitation.Status != GroupInvitationStatus.Pending)
        {
            return invitation.Status == GroupInvitationStatus.Expired
                ? Result.Failure<GroupInvitationDto>("Invitation has expired.", 409)
                : Result.Failure<GroupInvitationDto>("Invitation has already been processed.", 409);
        }

        if (invitation.ExpiredAt > DateTimeOffset.UtcNow)
        {
            return null;
        }

        invitation.Status = GroupInvitationStatus.Expired;
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Failure<GroupInvitationDto>("Invitation has expired.", 409);
    }

    private async Task<string> GetCurrentUserNormalizedEmailAsync(Guid userId, CancellationToken ct)
    {
        var currentUserEmail = await _userRepo.GetQueryable()
            .AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive)
            .Select(user => user.Email)
            .FirstOrDefaultAsync(ct);

        return NormalizeEmail(currentUserEmail ?? string.Empty);
    }

    private static string NormalizeToken(string token)
        => string.IsNullOrWhiteSpace(token) ? string.Empty : token.Trim();

    private async Task<int> CountGroupOwnersAsync(Guid groupId, CancellationToken ct)
        => await _memberRepo.GetQueryable()
            .AsNoTracking()
            .CountAsync(member => member.WorkGroupId == groupId && member.Role == GroupRoleRules.Owner, ct);

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
