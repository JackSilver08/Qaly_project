using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Qaly.Application.Common.Telemetry;
using Qaly.Web.Configuration;

namespace Qaly.Web.Extensions;

public static class QalyTelemetryExtensions
{
    public static WebApplicationBuilder AddQalyTelemetry(this WebApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection(QalyTelemetryOptions.SectionName);
        var options = section.Get<QalyTelemetryOptions>() ?? new QalyTelemetryOptions();
        builder.Services.Configure<QalyTelemetryOptions>(section);

        if (!options.OtlpEnabled)
        {
            return builder;
        }

        var validationErrors = ProductionReadinessValidator.FindTelemetryErrors(builder.Configuration);
        if (validationErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "Telemetry configuration validation failed:" + Environment.NewLine +
                string.Join(Environment.NewLine, validationErrors.Select(error => $"- {error}")));
        }

        var endpoint = new Uri(options.OtlpEndpoint, UriKind.Absolute);
        var serviceVersion = typeof(QalyTelemetryExtensions).Assembly
            .GetName()
            .Version?
            .ToString();

        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: options.ServiceName,
                serviceVersion: serviceVersion))
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(options.TraceSampleRatio)))
                .AddSource(QalyAiTelemetry.ActivitySourceName)
                .AddAspNetCoreInstrumentation(instrumentation =>
                {
                    instrumentation.RecordException = true;
                    instrumentation.Filter = context => !IsHealthProbe(context.Request.Path);
                })
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(exporter => exporter.Endpoint = endpoint))
            .WithMetrics(metrics => metrics
                .AddMeter(QalyAiTelemetry.MeterName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(exporter => exporter.Endpoint = endpoint));

        return builder;
    }

    private static bool IsHealthProbe(PathString path)
        => path.StartsWithSegments("/health/live") ||
           path.StartsWithSegments("/health/ready");
}
