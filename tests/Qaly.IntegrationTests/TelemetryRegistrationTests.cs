using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Qaly.Web.Extensions;

namespace Qaly.IntegrationTests;

public sealed class TelemetryRegistrationTests
{
    [Fact]
    public void OtlpDisabled_DoesNotRegisterTelemetryProviders()
    {
        var builder = Builder(new Dictionary<string, string?>
        {
            ["Telemetry:OtlpEnabled"] = "false"
        });

        builder.AddQalyTelemetry();

        builder.Services.Should().NotContain(descriptor =>
            descriptor.ServiceType == typeof(TracerProvider) ||
            descriptor.ServiceType == typeof(MeterProvider));
    }

    [Fact]
    public void OtlpEnabledWithValidContract_RegistersTraceAndMetricProviders()
    {
        var builder = Builder(new Dictionary<string, string?>
        {
            ["Telemetry:OtlpEnabled"] = "true",
            ["Telemetry:OtlpEndpoint"] = "https://collector.internal:4317",
            ["Telemetry:AllowInsecureOtlp"] = "false",
            ["Telemetry:ServiceName"] = "qaly-integration-test",
            ["Telemetry:TraceSampleRatio"] = "0.25"
        });

        builder.AddQalyTelemetry();

        builder.Services.Should().Contain(descriptor => descriptor.ServiceType == typeof(TracerProvider));
        builder.Services.Should().Contain(descriptor => descriptor.ServiceType == typeof(MeterProvider));
    }

    private static WebApplicationBuilder Builder(IReadOnlyDictionary<string, string?> values)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(QalyTelemetryExtensions).Assembly.GetName().Name,
            EnvironmentName = Environments.Development
        });
        builder.Configuration.AddInMemoryCollection(values);
        return builder;
    }
}
