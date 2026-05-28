namespace Qaly.Application.Common.Models;

public class InvitationLinkOptions
{
    public const string SectionName = "InvitationLink";

    public string FrontendBaseUrl { get; set; } = string.Empty;
    public string AcceptInvitationPath { get; set; } = string.Empty;

    public string GetNormalizedFrontendBaseUrl()
        => string.IsNullOrWhiteSpace(FrontendBaseUrl)
            ? string.Empty
            : FrontendBaseUrl.Trim().TrimEnd('/');

    public string GetNormalizedAcceptInvitationPath()
    {
        if (string.IsNullOrWhiteSpace(AcceptInvitationPath))
        {
            return string.Empty;
        }

        var trimmed = AcceptInvitationPath.Trim().Trim('/');
        return trimmed.Length == 0 ? string.Empty : $"/{trimmed}";
    }
}
