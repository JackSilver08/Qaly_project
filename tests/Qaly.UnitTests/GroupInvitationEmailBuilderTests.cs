using FluentAssertions;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Models;
using Qaly.Application.Services.Groups;

namespace Qaly.UnitTests;

public class GroupInvitationEmailBuilderTests
{
    [Fact]
    public void Build_ShouldIncludeGroupNameInSubject()
    {
        var builder = CreateBuilder();

        var result = builder.Build("Alpha Team", "Owner User", "token-123", DateTimeOffset.UtcNow.AddDays(7));

        result.Subject.Should().Contain("Alpha Team");
    }

    [Fact]
    public void Build_ShouldIncludeGroupNameInBody()
    {
        var builder = CreateBuilder();

        var result = builder.Build("Alpha Team", "Owner User", "token-123", DateTimeOffset.UtcNow.AddDays(7));

        result.Body.Should().Contain("Alpha Team");
    }

    [Fact]
    public void Build_ShouldIncludeInviterName_WhenInviterNameProvided()
    {
        var builder = CreateBuilder();

        var result = builder.Build("Alpha Team", "Owner User", "token-123", DateTimeOffset.UtcNow.AddDays(7));

        result.Body.Should().Contain("Owner User");
    }

    [Fact]
    public void BuildAcceptInvitationLink_ShouldUseConfiguredBaseUrlPathAndUrlEncodedToken()
    {
        var builder = CreateBuilder("https://app.qaly.dev/", "/groups/invitations/accept");
        const string token = "abc+/=? token";
        var encodedToken = Uri.EscapeDataString(token);

        var link = builder.BuildAcceptInvitationLink(token);

        link.Should().Be($"https://app.qaly.dev/groups/invitations/accept?token={encodedToken}");
    }

    [Fact]
    public void Build_ShouldContainEncodedTokenInsideAcceptLink()
    {
        var builder = CreateBuilder("https://app.qaly.dev", "groups/invitations/accept");
        const string token = "abc+/=? token";
        var encodedToken = Uri.EscapeDataString(token);

        var result = builder.Build("Alpha Team", null, token, DateTimeOffset.UtcNow.AddDays(7));

        result.Body.Should().Contain($"https://app.qaly.dev/groups/invitations/accept?token={encodedToken}");
        result.Body.Should().NotContain($"token={token}");
    }

    [Fact]
    public void BuildAcceptInvitationLink_WhenFrontendBaseUrlMissing_ShouldThrowInvalidOperationException()
    {
        var builder = CreateBuilder(string.Empty, "/groups/invitations/accept");

        var action = () => builder.BuildAcceptInvitationLink("token-123");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*InvitationLink:FrontendBaseUrl*");
    }

    [Fact]
    public void BuildAcceptInvitationLink_WhenAcceptInvitationPathMissing_ShouldThrowInvalidOperationException()
    {
        var builder = CreateBuilder("https://app.qaly.dev", string.Empty);

        var action = () => builder.BuildAcceptInvitationLink("token-123");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*InvitationLink:AcceptInvitationPath*");
    }

    private static GroupInvitationEmailBuilder CreateBuilder(string frontendBaseUrl = "https://app.qaly.dev", string acceptInvitationPath = "/invitations/accept")
    {
        var options = Options.Create(new InvitationLinkOptions
        {
            FrontendBaseUrl = frontendBaseUrl,
            AcceptInvitationPath = acceptInvitationPath
        });

        return new GroupInvitationEmailBuilder(options);
    }
}
