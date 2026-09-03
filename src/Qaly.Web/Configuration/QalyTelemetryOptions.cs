namespace Qaly.Web.Configuration;

public sealed class QalyTelemetryOptions
{
    public const string SectionName = "Telemetry";

    public bool OtlpEnabled { get; set; }
    public string OtlpEndpoint { get; set; } = string.Empty;
    public bool AllowInsecureOtlp { get; set; }
    public string ServiceName { get; set; } = "qaly-web";
    public double TraceSampleRatio { get; set; } = 0.1;
}
