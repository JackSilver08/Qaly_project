using Qaly.Application;
using Qaly.Application.Common.Interfaces;
using Qaly.Infrastructure;
using Qaly.Infrastructure.Data.Seeds;
using Qaly.Web.Hubs;
using Serilog;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authentication.Cookies;
using HealthChecks.UI.Client;
using System.Net;
using System.Net.Sockets;

// Load environment variables from .env file
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);
builder.Environment.EnvironmentName = "Development"; // Force Development for debugging

// =============================================
// Serilog Configuration
// =============================================
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProcessId()
    .CreateLogger();

builder.Host.UseSerilog();

// =============================================
// Service Registration
// =============================================

// Application layer (Services, Validators)
builder.Services.AddApplication();

// Infrastructure layer (DbContext, Repositories)
builder.Services.AddInfrastructure(builder.Configuration);

// Data Seeder
builder.Services.AddScoped<DataSeeder>();

/*
if (builder.Environment.IsDevelopment())
{
    var preferredPort = 5055;
    var selectedPort = GetAvailableHttpPort(preferredPort);
    builder.WebHost.UseUrls($"http://127.0.0.1:{selectedPort}");
    Log.Information("Development HTTP port selected: {Port}", selectedPort);
}
*/

// Razor Pages
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizePage("/Index");
    options.Conventions.AddPageRoute("/Index", "dashboard");
    options.Conventions.AddPageRoute("/Index", "profile");
    options.Conventions.AddPageRoute("/Index", "projects");
    options.Conventions.AddPageRoute("/Index", "projects/archived");
    options.Conventions.AddPageRoute("/Index", "projects/{projectId}");
    options.Conventions.AddPageRoute("/Index", "projects/{projectId}/tasks/{taskId}");
    options.Conventions.AddPageRoute("/Index", "tasks");
    options.Conventions.AddPageRoute("/Index", "teams");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/Register");
    options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
});

// Cookie auth
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Qaly.Auth";
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// Controllers
builder.Services.AddControllers();

// SignalR (realtime notifications)
builder.Services.AddSignalR();
builder.Services.AddSingleton<INotificationPublisher, SignalRNotificationPublisher>();

// OpenAPI (Swagger/Scalar)
builder.Services.AddOpenApi();

// Health Checks
builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!)
    .AddRedis(builder.Configuration.GetValue<string>("Redis:ConnectionString") ?? "localhost:6379");

var app = builder.Build();

// =============================================
// Serilog Middleware
// =============================================
app.UseSerilogRequestLogging();

// =============================================
// Database Migration & Seed (Development only)
// =============================================

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
    try 
    {
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Lỗi khi seed dữ liệu. Hãy kiểm tra kết nối SQL Server.");
    }

    // Trigger AI Ingestion Sync
    var ingestionService = scope.ServiceProvider.GetRequiredService<IAiIngestionService>();
    try 
    {
        Log.Information("Bắt đầu đồng bộ dữ liệu vào Vector Database...");
        await ingestionService.SyncAllDataAsync();
        Log.Information("Đồng bộ dữ liệu AI hoàn tất.");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Không thể đồng bộ dữ liệu AI. Hãy đảm bảo Ollama và Qdrant đang chạy.");
    }
    
    // OpenAPI UI
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// =============================================
// Middleware Pipeline
// =============================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// app.MapStaticAssets();
app.MapRazorPages(); // .WithStaticAssets();
app.MapControllers();

// Health Checks Endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// SignalR hubs
app.MapHub<NotificationHub>("/hubs/notification");

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

static int GetAvailableHttpPort(int preferredPort)
{
    for (var port = preferredPort; port <= preferredPort + 50; port++)
    {
        if (IsPortAvailable(port))
        {
            return port;
        }
    }

    return preferredPort;
}

static bool IsPortAvailable(int port)
{
    try
    {
        using var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        return true;
    }
    catch (SocketException)
    {
        return false;
    }
}
