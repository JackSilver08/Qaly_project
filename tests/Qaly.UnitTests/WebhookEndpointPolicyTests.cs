using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Qaly.Infrastructure.Services;

namespace Qaly.UnitTests;

public sealed class WebhookEndpointPolicyTests
{
    [Theory]
    [InlineData("http://127.0.0.1/hook")]
    [InlineData("http://10.0.0.1/hook")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://172.16.0.1/hook")]
    [InlineData("http://192.168.1.1/hook")]
    [InlineData("http://[::1]/hook")]
    [InlineData("http://[fc00::1]/hook")]
    [InlineData("http://[fe80::1]/hook")]
    public async Task ValidateAsync_RejectsPrivateAndSpecialAddressLiterals(string url)
    {
        var policy = new WebhookEndpointPolicy(new StubDnsResolver());

        var result = await policy.ValidateAsync(url);

        result.IsAllowed.Should().BeFalse();
        result.Error.Should().Contain("public");
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://8.8.8.8/hook")]
    [InlineData("https://user:password@8.8.8.8/hook")]
    [InlineData("http://localhost/hook")]
    [InlineData("http://qaly.internal/hook")]
    public async Task ValidateAsync_RejectsUnsafeUrlShapes(string url)
    {
        var policy = new WebhookEndpointPolicy(new StubDnsResolver());

        var result = await policy.ValidateAsync(url);

        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_AllowsPublicLiteralWithoutDnsLookup()
    {
        var resolver = new StubDnsResolver();
        var policy = new WebhookEndpointPolicy(resolver);

        var result = await policy.ValidateAsync("https://8.8.8.8/qaly-webhook");

        result.IsAllowed.Should().BeTrue();
        result.Endpoint.Should().Be(new Uri("https://8.8.8.8/qaly-webhook"));
        resolver.RequestedHosts.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_RejectsHostnameWhenAnyDnsAnswerIsPrivate()
    {
        var resolver = new StubDnsResolver(new Dictionary<string, IPAddress[]>
        {
            ["hooks.example.com"] = [IPAddress.Parse("93.184.216.34"), IPAddress.Parse("10.20.30.40")]
        });
        var policy = new WebhookEndpointPolicy(resolver);

        var result = await policy.ValidateAsync("https://hooks.example.com/qaly");

        result.IsAllowed.Should().BeFalse();
        result.Error.Should().Contain("public IP");
    }

    [Fact]
    public async Task ValidateAsync_AllowsHostnameOnlyWhenEveryDnsAnswerIsPublic()
    {
        var resolver = new StubDnsResolver(new Dictionary<string, IPAddress[]>
        {
            ["hooks.example.com"] = [IPAddress.Parse("93.184.216.34"), IPAddress.Parse("2606:4700:4700::1111")]
        });
        var policy = new WebhookEndpointPolicy(resolver);

        var result = await policy.ValidateAsync("https://hooks.example.com/qaly");

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_RejectsUnresolvableHostname()
    {
        var policy = new WebhookEndpointPolicy(new StubDnsResolver(socketError: true));

        var result = await policy.ValidateAsync("https://missing.example/qaly");

        result.IsAllowed.Should().BeFalse();
        result.Error.Should().Contain("could not be resolved");
    }

    [Fact]
    public void CreateHandler_DisablesRedirectsAndProxyResolution()
    {
        using var handler = WebhookConnectionGuard.CreateHandler();

        handler.AllowAutoRedirect.Should().BeFalse("redirect targets must never bypass endpoint validation");
        handler.UseProxy.Should().BeFalse("a proxy must not resolve a private webhook target on Qaly's behalf");
        handler.ConnectCallback.Should().NotBeNull("the destination must be checked again at connect time");
    }

    private sealed class StubDnsResolver : IWebhookDnsResolver
    {
        private readonly IReadOnlyDictionary<string, IPAddress[]> _addresses;
        private readonly bool _socketError;

        public StubDnsResolver(
            IReadOnlyDictionary<string, IPAddress[]>? addresses = null,
            bool socketError = false)
        {
            _addresses = addresses ?? new Dictionary<string, IPAddress[]>();
            _socketError = socketError;
        }

        public List<string> RequestedHosts { get; } = [];

        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct)
        {
            RequestedHosts.Add(host);
            if (_socketError) throw new SocketException((int)SocketError.HostNotFound);
            return Task.FromResult(_addresses.TryGetValue(host, out var value) ? value : Array.Empty<IPAddress>());
        }
    }
}
