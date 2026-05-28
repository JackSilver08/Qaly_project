using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Models;

namespace Qaly.Application.Services.Groups;

public class GroupInvitationEmailBuilder : IGroupInvitationEmailBuilder
{
    private readonly InvitationLinkOptions _invitationLinkOptions;

    public GroupInvitationEmailBuilder(IOptions<InvitationLinkOptions> invitationLinkOptions)
    {
        _invitationLinkOptions = invitationLinkOptions?.Value ?? throw new ArgumentNullException(nameof(invitationLinkOptions));
    }

    [SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "Email timestamp uses an explicit UTC format.")]
    public GroupInvitationEmailContent Build(string groupName, string? inviterName, string invitationToken, DateTimeOffset expiredAt)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            throw new InvalidOperationException("Group name is required to build invitation email.");
        }

        var acceptLink = BuildAcceptInvitationLink(invitationToken);
        var subject = $"Bạn được mời tham gia nhóm {groupName}";

        var builder = new StringBuilder();
        builder.AppendLine("Xin chào,");
        builder.AppendLine();
        builder.AppendLine($"Bạn nhận được lời mời tham gia nhóm \"{groupName}\" trên Qaly.");
        if (!string.IsNullOrWhiteSpace(inviterName))
        {
            builder.AppendLine($"Người mời: {inviterName.Trim()}");
        }

        builder.AppendLine($"Lời mời hết hạn vào: {expiredAt:yyyy-MM-dd HH:mm} UTC.");
        builder.AppendLine();
        builder.AppendLine("Truy cập link sau để chấp nhận lời mời:");
        builder.AppendLine(acceptLink);
        builder.AppendLine();
        builder.AppendLine("Nếu bạn không mong đợi lời mời này, bạn có thể bỏ qua email.");

        return new GroupInvitationEmailContent(subject, builder.ToString().TrimEnd());
    }

    public string BuildAcceptInvitationLink(string invitationToken)
    {
        if (string.IsNullOrWhiteSpace(invitationToken))
        {
            throw new InvalidOperationException("Invitation token is required.");
        }

        var frontendBaseUrl = _invitationLinkOptions.GetNormalizedFrontendBaseUrl();
        if (string.IsNullOrWhiteSpace(frontendBaseUrl))
        {
            throw new InvalidOperationException("InvitationLink:FrontendBaseUrl is missing or empty.");
        }

        var acceptInvitationPath = _invitationLinkOptions.GetNormalizedAcceptInvitationPath();
        if (string.IsNullOrWhiteSpace(acceptInvitationPath))
        {
            throw new InvalidOperationException("InvitationLink:AcceptInvitationPath is missing or empty.");
        }

        var encodedToken = Uri.EscapeDataString(invitationToken.Trim());
        return $"{frontendBaseUrl}{acceptInvitationPath}?token={encodedToken}";
    }
}
