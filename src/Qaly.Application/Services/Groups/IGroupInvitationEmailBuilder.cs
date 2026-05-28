namespace Qaly.Application.Services.Groups;

public interface IGroupInvitationEmailBuilder
{
    GroupInvitationEmailContent Build(string groupName, string? inviterName, string invitationToken, DateTimeOffset expiredAt);
    string BuildAcceptInvitationLink(string invitationToken);
}

public sealed record GroupInvitationEmailContent(string Subject, string Body);
