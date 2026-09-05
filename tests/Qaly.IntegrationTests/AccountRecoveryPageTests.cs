using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Qaly.IntegrationTests;

#pragma warning disable CA1707
public partial class AccountRecoveryPageTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;
    public AccountRecoveryPageTests(IntegrationTestFactory factory) => _factory = factory;

    private HttpClient CreateAnonymousClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-Test-Auth", "None");
        return client;
    }

    [Fact]
    public async Task Login_LinksToAnonymousRecoveryPage()
    {
        using var client = CreateAnonymousClient();
        var login = await client.GetStringAsync("/Account/Login");
        login.Should().Contain("href=\"/Account/ForgotPassword\"");
        using var page = await client.GetAsync("/Account/ForgotPassword");
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());
        html.Should().Contain("Quên mật khẩu?");
        html.Should().Contain("chưa được bật");
        html.Should().Contain("href=\"/Account/Login\"");
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-email")]
    public async Task Recovery_RejectsInvalidEmail(string email)
    {
        using var client = CreateAnonymousClient();
        using var response = await PostRecovery(client, email);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        html.Should().Contain("Vui lòng nhập email");
        html.Should().NotContain("Yêu cầu đã được soạn");
    }

    [Fact]
    public async Task Recovery_PreparesRequestWithoutClaimingEmailDeliveryOrPasswordChange()
    {
        using var client = CreateAnonymousClient();
        using var response = await PostRecovery(client, "someone@example.com");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        html.Should().Contain("Yêu cầu đã được soạn — chưa gửi đi");
        html.Should().Contain("someone@example.com");
        html.Should().Contain("Mật khẩu của bạn chưa thay đổi");
        html.Should().NotContain("đã gửi email");
    }

    private static async Task<HttpResponseMessage> PostRecovery(HttpClient client, string email)
    {
        var page = await client.GetStringAsync("/Account/ForgotPassword");
        var token = WebUtility.HtmlDecode(AntiforgeryToken().Match(page).Groups[1].Value);
        token.Should().NotBeNullOrEmpty();
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email, ["__RequestVerificationToken"] = token
        });
        return await client.PostAsync("/Account/ForgotPassword", content);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryToken();
}
