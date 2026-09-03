using Qaly.Application.Common.Models;
using System.Net;
using System.Globalization;

namespace Qaly.Web.Configuration;

public static class ProductionReadinessValidator
{
    private static readonly string[] PlaceholderFragments =
    [
        "<set-",
        "<livekit-",
        "your_",
        "change-me",
        "qaly.example"
    ];

    public static IReadOnlyList<string> FindErrors(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (!environment.IsProduction())
        {
            return [];
        }

        var errors = new List<string>();
        RequireConcreteValue(errors, "ConnectionStrings:DefaultConnection", configuration.GetConnectionString("DefaultConnection"));
        RequireConcreteValue(errors, "Redis:ConnectionString", configuration["Redis:ConnectionString"]);
        RequireHttpsUrl(errors, "ProductionReadiness:PublicBaseUrl", configuration["ProductionReadiness:PublicBaseUrl"]);
        RequireConcreteValue(errors, "ProductionReadiness:DeploymentId", configuration["ProductionReadiness:DeploymentId"]);

        var allowedHosts = configuration["AllowedHosts"]?.Trim();
        if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts == "*")
        {
            errors.Add("AllowedHosts must contain the deployed host names and cannot be '*'.");
        }

        if (configuration.GetValue<bool>("UseInMemoryDatabase"))
        {
            errors.Add("UseInMemoryDatabase must be false in Production.");
        }

        if (configuration.GetValue<bool>("Seed:UseRichDemoSeed"))
        {
            errors.Add("Seed:UseRichDemoSeed must be false in Production.");
        }

        RejectConfiguredSecret(errors, "Seed:AdminPassword", configuration["Seed:AdminPassword"]);
        RejectConfiguredSecret(errors, "Seed:DefaultUserPassword", configuration["Seed:DefaultUserPassword"]);

        var keysPath = configuration["DataProtection:KeysPath"];
        RequireConcreteValue(errors, "DataProtection:KeysPath", keysPath);
        if (!string.IsNullOrWhiteSpace(keysPath) && !Path.IsPathFullyQualified(keysPath))
        {
            errors.Add("DataProtection:KeysPath must be an absolute path mounted on durable shared storage.");
        }

        RequireHttpsUrl(errors, "InvitationLink:FrontendBaseUrl", configuration["InvitationLink:FrontendBaseUrl"]);
        ValidateAi(errors, configuration);
        ValidateVectorSync(errors, configuration);
        ValidatePrivacy(errors, configuration);
        ValidateGitHub(errors, configuration);
        ValidatePush(errors, configuration);
        ValidateLiveKit(errors, configuration);
        ValidateOperationalHealth(errors, configuration);
        ValidateReverseProxy(errors, configuration);
        errors.AddRange(FindTelemetryErrors(configuration));

        return errors;
    }

    public static IReadOnlyList<string> FindTelemetryErrors(IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>($"{QalyTelemetryOptions.SectionName}:OtlpEnabled"))
        {
            return [];
        }

        var errors = new List<string>();
        var prefix = QalyTelemetryOptions.SectionName;
        RequireConcreteValue(errors, $"{prefix}:ServiceName", configuration[$"{prefix}:ServiceName"]);

        var endpointText = configuration[$"{prefix}:OtlpEndpoint"];
        if (IsMissingOrPlaceholder(endpointText) ||
            !Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(endpoint.UserInfo))
        {
            errors.Add($"{prefix}:OtlpEndpoint must be a concrete absolute HTTP or HTTPS URL without embedded credentials when OTLP is enabled.");
        }
        else if (endpoint.Scheme == Uri.UriSchemeHttp &&
                 !configuration.GetValue<bool>($"{prefix}:AllowInsecureOtlp"))
        {
            errors.Add($"{prefix}:AllowInsecureOtlp must be true to acknowledge a plaintext OTLP endpoint.");
        }

        var sampleRatioText = configuration[$"{prefix}:TraceSampleRatio"];
        if (!double.TryParse(
                sampleRatioText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var sampleRatio) ||
            sampleRatio <= 0 ||
            sampleRatio > 1)
        {
            errors.Add($"{prefix}:TraceSampleRatio must be a number greater than zero and at most one.");
        }

        return errors;
    }

    public static void ThrowIfInvalid(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var errors = FindErrors(configuration, environment);
        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Production readiness validation failed:" + Environment.NewLine +
            string.Join(Environment.NewLine, errors.Select(error => $"- {error}")));
    }

    private static void ValidateAi(List<string> errors, IConfiguration configuration)
    {
        var enabled = configuration.GetValue<bool>($"{AiJobPlatformOptions.SectionName}:Enabled");
        var decision = configuration["ProductionReadiness:AiDecision"]?.Trim().ToLowerInvariant();
        if (decision is not ("enabled" or "disabled-accepted"))
        {
            errors.Add("ProductionReadiness:AiDecision must be 'enabled' or 'disabled-accepted'.");
        }
        else if (decision == "enabled" && !enabled)
        {
            errors.Add("AiDecision is 'enabled' but AiJobsV4:Enabled is false.");
        }
        else if (decision == "disabled-accepted" && enabled)
        {
            errors.Add("AiDecision is 'disabled-accepted' but AiJobsV4:Enabled is true.");
        }

        var featureKeys = new[]
        {
            "BudgetUiEnabled", "TaskSkillSuggestionEnabled", "ActionComposerEnabled",
            "ActionComposerTaskCreateEnabled", "AssistantSessionEnabled",
            "AssistantContextRegistryEnabled", "AssistantResearchPlanEnabled",
            "AssistantGoalPlannerEnabled", "AssistantReadOnlyLoopEnabled",
            "AssistantProgressiveInteractionEnabled", "ProjectLaunchBriefEnabled",
            "ProjectLaunchPlanningEnabled", "ProjectLaunchExecutionEnabled",
            "ProjectOperationMonitoringEnabled", "SafeTestOrchestratorEnabled",
            "NativeDomainActionsEnabled"
        };
        if (!enabled && featureKeys.Any(key => configuration.GetValue<bool>($"AiJobsV4:{key}")))
        {
            errors.Add("AiJobsV4 feature flags cannot be enabled while AiJobsV4:Enabled is false.");
        }

        if (!enabled)
        {
            return;
        }

        if (!configuration.GetValue<bool>("AiJobsV4:WorkerEnabled") &&
            !configuration.GetValue<bool>("AiJobsV4:AllowEnqueueWhenWorkerDisabled"))
        {
            errors.Add("AiJobsV4 is enabled but its worker is disabled and enqueue bypass is not explicitly allowed.");
        }
        if (configuration.GetValue<bool>("AiJobsV4:WorkerEnabled"))
        {
            ValidateDurableWorker(
                errors,
                configuration,
                AiJobPlatformOptions.SectionName);
        }

        var provider = configuration["AiSettings:Provider"]?.Trim();
        if (string.IsNullOrWhiteSpace(provider))
        {
            errors.Add("AiSettings:Provider is required when AI is enabled.");
            return;
        }

        if (!string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase))
        {
            RequireConcreteValue(errors, $"AiSettings:{provider}:ApiKey", configuration[$"AiSettings:{provider}:ApiKey"]);
        }
    }

    private static void ValidatePrivacy(List<string> errors, IConfiguration configuration)
    {
        var enabled = configuration.GetValue<bool>($"{PrivacyV4Options.SectionName}:Enabled");
        var enforced = configuration.GetValue<bool>($"{PrivacyV4Options.SectionName}:EnforceSensitiveIngestion");
        var worker = configuration.GetValue<bool>($"{PrivacyV4Options.SectionName}:WorkerEnabled");
        var decision = configuration["ProductionReadiness:PrivacyDecision"]?.Trim().ToLowerInvariant();

        if (decision is not ("enforced" or "disabled-accepted"))
        {
            errors.Add("ProductionReadiness:PrivacyDecision must be 'enforced' or 'disabled-accepted'.");
        }
        else if (decision == "enforced" && (!enabled || !enforced || !worker))
        {
            errors.Add("PrivacyDecision is 'enforced' but PrivacyV4 Enabled, EnforceSensitiveIngestion and WorkerEnabled are not all true.");
        }
        else if (decision == "disabled-accepted" && (enabled || enforced || worker))
        {
            errors.Add("PrivacyDecision is 'disabled-accepted' but one or more PrivacyV4 controls are enabled.");
        }

        if (enabled && worker)
        {
            ValidateDurableWorker(
                errors,
                configuration,
                PrivacyV4Options.SectionName);
        }
    }

    private static void ValidateVectorSync(List<string> errors, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Ai:SemanticEnabled"))
        {
            return;
        }

        RequireConcreteValue(errors, "Ai:QdrantUrl", configuration["Ai:QdrantUrl"]);
        RequireRange(errors, configuration, "VectorSync:PollIntervalMilliseconds", 100, 60000);
        RequireRange(errors, configuration, "VectorSync:BatchSize", 1, 100);
        RequireRange(errors, configuration, "VectorSync:MaxAttempts", 1, 20);
        RequireRange(errors, configuration, "VectorSync:LeaseSeconds", 30, 900);
        RequireRange(errors, configuration, "VectorSync:HeartbeatSeconds", 5, 300);
        RequireRange(errors, configuration, "VectorSync:BaseRetrySeconds", 1, 300);
        var leaseSeconds = configuration.GetValue<int>("VectorSync:LeaseSeconds");
        var heartbeatSeconds = configuration.GetValue<int>("VectorSync:HeartbeatSeconds");
        if (leaseSeconds is >= 30 and <= 900 &&
            heartbeatSeconds is >= 5 and <= 300 &&
            heartbeatSeconds >= leaseSeconds)
        {
            errors.Add("VectorSync:HeartbeatSeconds must be less than VectorSync:LeaseSeconds.");
        }
    }

    private static void ValidateDurableWorker(
        List<string> errors,
        IConfiguration configuration,
        string section)
    {
        RequireRange(errors, configuration, $"{section}:PollIntervalMilliseconds", 100, 60000);
        RequireRange(errors, configuration, $"{section}:BatchSize", 1, 100);
        RequireRange(errors, configuration, $"{section}:MaxAttempts", 1, 20);
        RequireRange(errors, configuration, $"{section}:LeaseSeconds", 30, 900);
        RequireRange(errors, configuration, $"{section}:HeartbeatSeconds", 5, 300);
        RequireRange(errors, configuration, $"{section}:BaseRetrySeconds", 1, 300);
        var leaseSeconds = configuration.GetValue<int>($"{section}:LeaseSeconds");
        var heartbeatSeconds = configuration.GetValue<int>($"{section}:HeartbeatSeconds");
        if (leaseSeconds is >= 30 and <= 900 &&
            heartbeatSeconds is >= 5 and <= 300 &&
            heartbeatSeconds >= leaseSeconds)
        {
            errors.Add($"{section}:HeartbeatSeconds must be less than {section}:LeaseSeconds.");
        }
    }

    private static void ValidateGitHub(List<string> errors, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("GitHub:Enabled"))
        {
            return;
        }

        if (!configuration.GetValue<bool>("GitHub:WorkerEnabled"))
        {
            errors.Add("GitHub is enabled but GitHub:WorkerEnabled is false.");
        }
        if (configuration.GetValue<long>("GitHub:AppId") <= 0)
        {
            errors.Add("GitHub:AppId must be greater than zero when GitHub is enabled.");
        }
        RequireConcreteValue(errors, "GitHub:AppSlug", configuration["GitHub:AppSlug"]);
        RequireConcreteValue(errors, "GitHub:WebhookSecret", configuration["GitHub:WebhookSecret"]);
        if (IsMissingOrPlaceholder(configuration["GitHub:PrivateKey"]) &&
            IsMissingOrPlaceholder(configuration["GitHub:PrivateKeyPath"]))
        {
            errors.Add("GitHub requires either a concrete PrivateKey or PrivateKeyPath.");
        }

        RequireHttpsUrl(errors, "GitHub:ApiBaseUrl", configuration["GitHub:ApiBaseUrl"]);
        RequireRange(errors, configuration, "GitHub:PollIntervalMilliseconds", 100, 60000);
        RequireRange(errors, configuration, "GitHub:BatchSize", 1, 100);
        RequireRange(errors, configuration, "GitHub:MaxAttempts", 1, 20);
        RequireRange(errors, configuration, "GitHub:LeaseSeconds", 30, 900);
        RequireRange(errors, configuration, "GitHub:HeartbeatSeconds", 5, 300);
        RequireRange(errors, configuration, "GitHub:BaseRetrySeconds", 1, 300);
        var leaseSeconds = configuration.GetValue<int>("GitHub:LeaseSeconds");
        var heartbeatSeconds = configuration.GetValue<int>("GitHub:HeartbeatSeconds");
        if (leaseSeconds is >= 30 and <= 900 &&
            heartbeatSeconds is >= 5 and <= 300 &&
            heartbeatSeconds >= leaseSeconds)
        {
            errors.Add("GitHub:HeartbeatSeconds must be less than GitHub:LeaseSeconds.");
        }
    }

    private static void ValidatePush(List<string> errors, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Push:Enabled"))
        {
            return;
        }

        RequireConcreteValue(errors, "Push:VapidPublicKey", configuration["Push:VapidPublicKey"]);
        RequireConcreteValue(errors, "Push:VapidPrivateKey", configuration["Push:VapidPrivateKey"]);
        RequireConcreteValue(errors, "Push:VapidSubject", configuration["Push:VapidSubject"]);
    }

    private static void ValidateLiveKit(List<string> errors, IConfiguration configuration)
    {
        var values = new[]
        {
            configuration["LiveKit:ServerUrl"],
            configuration["LiveKit:ApiKey"],
            configuration["LiveKit:ApiSecret"]
        };
        if (values.All(string.IsNullOrWhiteSpace))
        {
            return;
        }

        RequireSecureWebSocketUrl(errors, "LiveKit:ServerUrl", values[0]);
        RequireConcreteValue(errors, "LiveKit:ApiKey", values[1]);
        RequireConcreteValue(errors, "LiveKit:ApiSecret", values[2]);
    }

    private static void ValidateOperationalHealth(List<string> errors, IConfiguration configuration)
    {
        RequireRange(errors, configuration, "OperationalHealth:WebhookOutboxPendingThreshold", 1, 100000);
        RequireRange(errors, configuration, "OperationalHealth:WebhookOutboxMaxPendingAgeMinutes", 1, 1440);
        RequireRange(errors, configuration, "OperationalHealth:WebhookDeliveryRetentionDays", 7, 365);
        RequireRange(errors, configuration, "OperationalHealth:WebhookProcessedOutboxRetentionDays", 7, 365);
        RequireRange(errors, configuration, "OperationalHealth:VectorOutboxPendingThreshold", 1, 100000);
        RequireRange(errors, configuration, "OperationalHealth:VectorOutboxMaxPendingAgeMinutes", 1, 1440);
        RequireRange(errors, configuration, "OperationalHealth:AiJobPendingThreshold", 1, 100000);
        RequireRange(errors, configuration, "OperationalHealth:AiJobMaxPendingAgeMinutes", 1, 1440);
        RequireRange(errors, configuration, "OperationalHealth:PrivacyWorkPendingThreshold", 1, 100000);
        RequireRange(errors, configuration, "OperationalHealth:PrivacyWorkMaxPendingAgeMinutes", 1, 1440);
        RequireRange(errors, configuration, "Security:LoginRequestsPerMinute", 5, 1000);
        RequireRange(errors, configuration, "Security:RegistrationRequestsPerHour", 1, 100);
        RequireRange(errors, configuration, "Hosting:ShutdownTimeoutSeconds", 5, 300);
    }

    private static void ValidateReverseProxy(List<string> errors, IConfiguration configuration)
    {
        var mode = configuration["ReverseProxy:Mode"]?.Trim().ToLowerInvariant();
        if (mode is not ("direct" or "trusted-proxy"))
        {
            errors.Add("ReverseProxy:Mode must be 'direct' or 'trusted-proxy'.");
            return;
        }

        var configuredProxies = configuration
            .GetSection("ReverseProxy:KnownProxies")
            .Get<string[]>() ?? [];
        RequireRange(errors, configuration, "ReverseProxy:ForwardLimit", 1, 5);
        if (mode == "direct")
        {
            if (configuredProxies.Length > 0)
            {
                errors.Add("ReverseProxy:KnownProxies must be empty when ReverseProxy:Mode is 'direct'.");
            }
            return;
        }

        if (configuredProxies.Length == 0)
        {
            errors.Add("ReverseProxy:KnownProxies must contain at least one concrete IP when ReverseProxy:Mode is 'trusted-proxy'.");
            return;
        }

        if (configuredProxies.Any(value => !IPAddress.TryParse(value, out _)))
        {
            errors.Add("Every ReverseProxy:KnownProxies value must be a concrete IP address.");
        }

        if (configuredProxies.Distinct(StringComparer.OrdinalIgnoreCase).Count() != configuredProxies.Length)
        {
            errors.Add("ReverseProxy:KnownProxies cannot contain duplicate addresses.");
        }
    }

    private static void RequireRange(
        List<string> errors,
        IConfiguration configuration,
        string key,
        int minimum,
        int maximum)
    {
        var raw = configuration[key];
        if (!int.TryParse(raw, out var value) || value < minimum || value > maximum)
        {
            errors.Add($"{key} must be an integer from {minimum} through {maximum}.");
        }
    }

    private static void RejectConfiguredSecret(List<string> errors, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{key} must not be configured in Production; provision users through an external secret-safe bootstrap flow.");
        }
    }

    private static void RequireConcreteValue(List<string> errors, string key, string? value)
    {
        if (IsMissingOrPlaceholder(value))
        {
            errors.Add($"{key} is required and cannot contain a placeholder value.");
        }
    }

    private static void RequireHttpsUrl(List<string> errors, string key, string? value)
    {
        if (IsMissingOrPlaceholder(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add($"{key} must be a concrete absolute HTTPS URL.");
        }
    }

    private static void RequireSecureWebSocketUrl(List<string> errors, string key, string? value)
    {
        if (IsMissingOrPlaceholder(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "wss")
        {
            errors.Add($"{key} must be a concrete absolute WSS URL when LiveKit is configured.");
        }
    }

    private static bool IsMissingOrPlaceholder(string? value)
        => string.IsNullOrWhiteSpace(value) ||
           PlaceholderFragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
