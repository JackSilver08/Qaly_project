using Qaly.Web.Extensions;
using Serilog;

DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProcessId()
    .CreateLogger();

builder.Host.UseSerilog();
builder.AddQalyWebServices();

var app = builder.Build();

app.UseQalyRequestPipeline();
await app.UseQalyDevelopmentSetupAsync();
app.MapQalyEndpoints();

try
{
    Log.Information("Starting Qaly Web App...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
