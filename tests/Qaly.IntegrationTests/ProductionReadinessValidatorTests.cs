using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Qaly.Web.Configuration;
using Qaly.Web.Extensions;

namespace Qaly.IntegrationTests;

public sealed class ProductionReadinessValidatorTests
{
    [Fact]
    public void Development_DoesNotApplyProductionDeploymentGate()
    {
        var errors = ProductionReadinessValidator.FindErrors(
            Configuration([]),
            new TestEnvironment(Environments.Development));

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Production_DefaultOrPlaceholderConfiguration_FailsWithActionableKeys()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "*",
            ["InvitationLink:FrontendBaseUrl"] = "https://app.qaly.example",
            ["AiSettings:Provider"] = "DeepSeek",
            ["AiSettings:DeepSeek:ApiKey"] = "YOUR_DEEPSEEK_KEY"
        });

        var errors = ProductionReadinessValidator.FindErrors(
            configuration,
            new TestEnvironment(Environments.Production));

        errors.Should().Contain(error => error.Contains("ConnectionStrings:DefaultConnection", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("AllowedHosts", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("DataProtection:KeysPath", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("AiDecision", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("PrivacyDecision", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("InvitationLink:FrontendBaseUrl", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_ExplicitDisabledDecisionsAndDurableInfrastructure_Pass()
    {
        var configuration = Configuration(ValidProductionConfiguration());

        var errors = ProductionReadinessValidator.FindErrors(
            configuration,
            new TestEnvironment(Environments.Production));

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Production_EnabledAiWithPlaceholderCredential_FailsClosed()
    {
        var values = ValidProductionConfiguration();
        EnableAi(values);
        values["AiSettings:Provider"] = "DeepSeek";
        values["AiSettings:DeepSeek:ApiKey"] = "YOUR_DEEPSEEK_KEY";

        var errors = ProductionReadinessValidator.FindErrors(
            Configuration(values),
            new TestEnvironment(Environments.Production));

        errors.Should().ContainSingle(error =>
            error.Contains("AiSettings:DeepSeek:ApiKey", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("AiJobsV4:PollIntervalMilliseconds", "99")]
    [InlineData("AiJobsV4:BatchSize", "0")]
    [InlineData("AiJobsV4:MaxAttempts", "21")]
    [InlineData("AiJobsV4:LeaseSeconds", "29")]
    [InlineData("AiJobsV4:HeartbeatSeconds", "4")]
    [InlineData("AiJobsV4:HeartbeatSeconds", "120")]
    [InlineData("AiJobsV4:BaseRetrySeconds", "0")]
    public void Production_EnabledAiWorkerWithUnsafeLeaseConfiguration_FailsClosed(
        string key,
        string value)
    {
        var values = ValidProductionConfiguration();
        EnableAi(values);
        values[key] = value;

        var errors = ProductionReadinessValidator.FindErrors(
            Configuration(values),
            new TestEnvironment(Environments.Production));

        errors.Should().Contain(error => error.Contains(key, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("PrivacyV4:PollIntervalMilliseconds", "99")]
    [InlineData("PrivacyV4:BatchSize", "0")]
    [InlineData("PrivacyV4:MaxAttempts", "21")]
    [InlineData("PrivacyV4:LeaseSeconds", "29")]
    [InlineData("PrivacyV4:HeartbeatSeconds", "4")]
    [InlineData("PrivacyV4:HeartbeatSeconds", "120")]
    [InlineData("PrivacyV4:BaseRetrySeconds", "0")]
    public void Production_EnabledPrivacyWorkerWithUnsafeLeaseConfiguration_FailsClosed(
        string key,
        string value)
    {
        var values = ValidProductionConfiguration();
        EnablePrivacy(values);
        values[key] = value;

        var errors = ProductionReadinessValidator.FindErrors(
            Configuration(values),
            new TestEnvironment(Environments.Production));

        errors.Should().Contain(error => error.Contains(key, StringComparison.Ordinal));
    }

    [Fact]
    public void Production_TrustedProxyModeRequiresConcreteUniqueProxyIps()
    {
        var values = ValidProductionConfiguration();
        values["ReverseProxy:Mode"] = "trusted-proxy";
        values["ReverseProxy:KnownProxies:0"] = "<trusted-proxy-ip>";
        values["ReverseProxy:KnownProxies:1"] = "<trusted-proxy-ip>";

        var errors = ProductionReadinessValidator.FindErrors(
            Configuration(values),
            new TestEnvironment(Environments.Production));

        errors.Should().Contain(error => error.Contains("concrete IP", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("duplicate", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_TrustedProxyModeWithConcreteIp_Passes()
    {
        var values = ValidProductionConfiguration();
        values["ReverseProxy:Mode"] = "trusted-proxy";
        values["ReverseProxy:KnownProxies:0"] = "10.42.0.10";

        var errors = ProductionReadinessValidator.FindErrors(
            Configuration(values),
            new TestEnvironment(Environments.Production));

        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Telemetry:OtlpEndpoint", "")]
    [InlineData("Telemetry:OtlpEndpoint", "collector.internal:4317")]
    [InlineData("Telemetry:OtlpEndpoint", "https://user:secret@collector.internal:4317")]
    [InlineData("Telemetry:TraceSampleRatio", "0")]
    [InlineData("Telemetry:TraceSampleRatio", "1.01")]
    [InlineData("Telemetry:TraceSampleRatio", "not-a-number")]
    [InlineData("Telemetry:ServiceName", "")]
    public void Production_EnabledOtlpWithInvalidConfiguration_FailsClosed(string key, string value)
    {
        var values = ValidProductionConfiguration();
        values["Telemetry:OtlpEnabled"] = "true";
        values[key] = value;

        var errors = ProductionReadinessValidator.FindErrors(
            Configuration(values),
            new TestEnvironment(Environments.Production));

        errors.Should().Contain(error => error.Contains(key, StringComparison.Ordinal));
    }

    [Fact]
    public void Production_PlaintextOtlpRequiresExplicitAcknowledgement()
    {
        var values = ValidProductionConfiguration();
        values["Telemetry:OtlpEnabled"] = "true";
        values["Telemetry:OtlpEndpoint"] = "http://collector.internal:4317";

        var errors = ProductionReadinessValidator.FindErrors(
            Configuration(values),
            new TestEnvironment(Environments.Production));

        errors.Should().ContainSingle(error =>
            error.Contains("Telemetry:AllowInsecureOtlp", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_EnabledHttpsOtlpWithValidSampling_Passes()
    {
        var values = ValidProductionConfiguration();
        values["Telemetry:OtlpEnabled"] = "true";
        values["Telemetry:OtlpEndpoint"] = "https://collector.internal:4317";
        values["Telemetry:TraceSampleRatio"] = "0.25";

        var errors = ProductionReadinessValidator.FindErrors(
            Configuration(values),
            new TestEnvironment(Environments.Production));

        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("OperationalHealth:WebhookOutboxPendingThreshold", "0")]
    [InlineData("OperationalHealth:WebhookOutboxMaxPendingAgeMinutes", "1441")]
    [InlineData("OperationalHealth:WebhookDeliveryRetentionDays", "6")]
    [InlineData("OperationalHealth:WebhookProcessedOutboxRetentionDays", "366")]
    [InlineData("OperationalHealth:VectorOutboxPendingThreshold", "0")]
    [InlineData("OperationalHealth:VectorOutboxMaxPendingAgeMinutes", "1441")]
    [InlineData("OperationalHealth:AiJobPendingThreshold", "0")]
    [InlineData("OperationalHealth:AiJobMaxPendingAgeMinutes", "1441")]
    [InlineData("OperationalHealth:PrivacyWorkPendingThreshold", "0")]
    [InlineData("OperationalHealth:PrivacyWorkMaxPendingAgeMinutes", "1441")]
    [InlineData("Security:LoginRequestsPerMinute", "4")]
    [InlineData("Security:RegistrationRequestsPerHour", "101")]
    [InlineData("ReverseProxy:ForwardLimit", "6")]
    [InlineData("Hosting:ShutdownTimeoutSeconds", "4")]
    [InlineData("Hosting:ShutdownTimeoutSeconds", "301")]
    public void Production_InvalidOperationalValues_FailClosed(string key, string value)
    {
        var values = ValidProductionConfiguration();
        values[key] = value;

        var errors = ProductionReadinessValidator.FindErrors(
            Configuration(values),
            new TestEnvironment(Environments.Production));

        errors.Should().ContainSingle(error => error.Contains(key, StringComparison.Ordinal));
    }

    [Fact]
    public void HostLifecycle_UsesConfiguredBoundedShutdownTimeout()
    {
        var services = new ServiceCollection();
        services.AddQalyHostLifecycle(Configuration(new Dictionary<string, string?>
        {
            ["Hosting:ShutdownTimeoutSeconds"] = "45"
        }));

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout
            .Should().Be(TimeSpan.FromSeconds(45));
    }

    [Theory]
    [InlineData("GitHub:PollIntervalMilliseconds", "99")]
    [InlineData("GitHub:BatchSize", "0")]
    [InlineData("GitHub:MaxAttempts", "21")]
    [InlineData("GitHub:LeaseSeconds", "29")]
    [InlineData("GitHub:HeartbeatSeconds", "4")]
    [InlineData("GitHub:HeartbeatSeconds", "120")]
    [InlineData("GitHub:BaseRetrySeconds", "0")]
    public void Production_EnabledGitHubWithUnsafeWorkerLeaseConfiguration_FailsClosed(
        string key,
        string value)
    {
        var values = ValidProductionConfiguration();
        EnableGitHub(values);
        values[key] = value;

        var errors = ProductionReadinessValidator.FindErrors(
            Configuration(values),
            new TestEnvironment(Environments.Production));

        errors.Should().Contain(error => error.Contains(key, StringComparison.Ordinal));
    }

    [Fact]
    public void Production_EnabledGitHubWithBoundedWorkerLeaseConfiguration_Passes()
    {
        var values = ValidProductionConfiguration();
        EnableGitHub(values);

        var errors = ProductionReadinessValidator.FindErrors(
            Configuration(values),
            new TestEnvironment(Environments.Production));

        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("VectorSync:PollIntervalMilliseconds", "99")]
    [InlineData("VectorSync:BatchSize", "0")]
    [InlineData("VectorSync:MaxAttempts", "21")]
    [InlineData("VectorSync:LeaseSeconds", "29")]
    [InlineData("VectorSync:HeartbeatSeconds", "4")]
    [InlineData("VectorSync:HeartbeatSeconds", "120")]
    [InlineData("VectorSync:BaseRetrySeconds", "0")]
    public void Production_EnabledSemanticSearchWithUnsafeVectorWorkerConfiguration_FailsClosed(
        string key,
        string value)
    {
        var values = ValidProductionConfiguration();
        EnableVectorSync(values);
        values[key] = value;

        var errors = ProductionReadinessValidator.FindErrors(
            Configuration(values),
            new TestEnvironment(Environments.Production));

        errors.Should().Contain(error => error.Contains(key, StringComparison.Ordinal));
    }

    [Fact]
    public void Production_EnabledSemanticSearchWithBoundedVectorWorkerConfiguration_Passes()
    {
        var values = ValidProductionConfiguration();
        EnableVectorSync(values);

        var errors = ProductionReadinessValidator.FindErrors(
            Configuration(values),
            new TestEnvironment(Environments.Production));

        errors.Should().BeEmpty();
    }

    private static Dictionary<string, string?> ValidProductionConfiguration()
        => new()
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=sql.internal;Database=Qaly;Encrypt=True",
            ["Redis:ConnectionString"] = "redis.internal:6380,ssl=true",
            ["ProductionReadiness:PublicBaseUrl"] = "https://qaly.acme.test",
            ["ProductionReadiness:DeploymentId"] = "qaly-prod-ap-southeast-1",
            ["ProductionReadiness:AiDecision"] = "disabled-accepted",
            ["ProductionReadiness:PrivacyDecision"] = "disabled-accepted",
            ["AllowedHosts"] = "qaly.acme.test",
            ["UseInMemoryDatabase"] = "false",
            ["Seed:UseRichDemoSeed"] = "false",
            ["DataProtection:KeysPath"] = OperatingSystem.IsWindows()
                ? @"C:\qaly\data-protection"
                : "/var/lib/qaly/data-protection",
            ["InvitationLink:FrontendBaseUrl"] = "https://qaly.acme.test",
            ["AiJobsV4:Enabled"] = "false",
            ["AiJobsV4:WorkerEnabled"] = "false",
            ["AiJobsV4:PollIntervalMilliseconds"] = "1000",
            ["AiJobsV4:BatchSize"] = "4",
            ["AiJobsV4:MaxAttempts"] = "3",
            ["AiJobsV4:LeaseSeconds"] = "120",
            ["AiJobsV4:HeartbeatSeconds"] = "30",
            ["AiJobsV4:BaseRetrySeconds"] = "5",
            ["PrivacyV4:Enabled"] = "false",
            ["PrivacyV4:WorkerEnabled"] = "false",
            ["PrivacyV4:EnforceSensitiveIngestion"] = "false",
            ["PrivacyV4:PollIntervalMilliseconds"] = "1000",
            ["PrivacyV4:BatchSize"] = "4",
            ["PrivacyV4:MaxAttempts"] = "5",
            ["PrivacyV4:LeaseSeconds"] = "120",
            ["PrivacyV4:HeartbeatSeconds"] = "30",
            ["PrivacyV4:BaseRetrySeconds"] = "30",
            ["GitHub:Enabled"] = "false",
            ["Push:Enabled"] = "false",
            ["LiveKit:ServerUrl"] = "",
            ["LiveKit:ApiKey"] = "",
            ["LiveKit:ApiSecret"] = "",
            ["OperationalHealth:WebhookOutboxPendingThreshold"] = "200",
            ["OperationalHealth:WebhookOutboxMaxPendingAgeMinutes"] = "15",
            ["OperationalHealth:WebhookDeliveryRetentionDays"] = "30",
            ["OperationalHealth:WebhookProcessedOutboxRetentionDays"] = "30",
            ["OperationalHealth:VectorOutboxPendingThreshold"] = "200",
            ["OperationalHealth:VectorOutboxMaxPendingAgeMinutes"] = "15",
            ["OperationalHealth:AiJobPendingThreshold"] = "200",
            ["OperationalHealth:AiJobMaxPendingAgeMinutes"] = "15",
            ["OperationalHealth:PrivacyWorkPendingThreshold"] = "200",
            ["OperationalHealth:PrivacyWorkMaxPendingAgeMinutes"] = "15",
            ["Security:LoginRequestsPerMinute"] = "30",
            ["Security:RegistrationRequestsPerHour"] = "10",
            ["Hosting:ShutdownTimeoutSeconds"] = "30",
            ["ReverseProxy:Mode"] = "direct",
            ["ReverseProxy:ForwardLimit"] = "1",
            ["Telemetry:OtlpEnabled"] = "false",
            ["Telemetry:OtlpEndpoint"] = "",
            ["Telemetry:AllowInsecureOtlp"] = "false",
            ["Telemetry:ServiceName"] = "qaly-web",
            ["Telemetry:TraceSampleRatio"] = "0.1"
        };

    private static void EnableAi(Dictionary<string, string?> values)
    {
        values["ProductionReadiness:AiDecision"] = "enabled";
        values["AiJobsV4:Enabled"] = "true";
        values["AiJobsV4:WorkerEnabled"] = "true";
        values["AiSettings:Provider"] = "Ollama";
    }

    private static void EnablePrivacy(Dictionary<string, string?> values)
    {
        values["ProductionReadiness:PrivacyDecision"] = "enforced";
        values["PrivacyV4:Enabled"] = "true";
        values["PrivacyV4:WorkerEnabled"] = "true";
        values["PrivacyV4:EnforceSensitiveIngestion"] = "true";
    }

    private static void EnableGitHub(Dictionary<string, string?> values)
    {
        values["GitHub:Enabled"] = "true";
        values["GitHub:WorkerEnabled"] = "true";
        values["GitHub:AppId"] = "12345";
        values["GitHub:AppSlug"] = "qaly-integration";
        values["GitHub:WebhookSecret"] = "github-webhook-secret";
        values["GitHub:PrivateKey"] = "github-private-key";
        values["GitHub:ApiBaseUrl"] = "https://api.github.com";
        values["GitHub:PollIntervalMilliseconds"] = "1000";
        values["GitHub:BatchSize"] = "10";
        values["GitHub:MaxAttempts"] = "5";
        values["GitHub:LeaseSeconds"] = "120";
        values["GitHub:HeartbeatSeconds"] = "30";
        values["GitHub:BaseRetrySeconds"] = "5";
    }

    private static void EnableVectorSync(Dictionary<string, string?> values)
    {
        values["Ai:SemanticEnabled"] = "true";
        values["Ai:QdrantUrl"] = "http://qdrant.internal:6334";
        values["VectorSync:PollIntervalMilliseconds"] = "5000";
        values["VectorSync:BatchSize"] = "20";
        values["VectorSync:MaxAttempts"] = "5";
        values["VectorSync:LeaseSeconds"] = "120";
        values["VectorSync:HeartbeatSeconds"] = "30";
        values["VectorSync:BaseRetrySeconds"] = "5";
    }

    private static IConfiguration Configuration(IEnumerable<KeyValuePair<string, string?>> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Qaly.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
