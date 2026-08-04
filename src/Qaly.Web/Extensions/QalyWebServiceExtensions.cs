using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Qaly.Application;
using Qaly.Application.Common.Models;
using Qaly.Application.Common.Interfaces;
using Qaly.Infrastructure;
using Qaly.Infrastructure.Data.Seeds;
using Qaly.Infrastructure.Services;
using Qaly.Web.Auth;
using Qaly.Web.Hubs;
using Qaly.Web.Middleware;
using StackExchange.Redis;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Qaly.Infrastructure.Data;

namespace Qaly.Web.Extensions;

public static class QalyWebServiceExtensions
{
    public static WebApplicationBuilder AddQalyWebServices(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;
        var cookieSecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        var redisConnection = NormalizeRedisConnection(configuration.GetValue<string>("Redis:ConnectionString"));
        var useInMemoryDistributedCache =
            configuration.GetValue<bool>("UseInMemoryDatabase");

        services.AddQalyDataProtection(builder.Environment);
        services.AddQalyRedis(redisConnection, useInMemoryDistributedCache);
        services.AddQalySession(cookieSecurePolicy);
        services.Configure<InvitationLinkOptions>(configuration.GetSection(InvitationLinkOptions.SectionName));
        services.Configure<LiveKitOptions>(configuration.GetSection(LiveKitOptions.SectionName));

        services.AddApplication();
        services.AddInfrastructure(configuration);

        services.AddExceptionHandler<CustomExceptionHandler>();
        services.AddProblemDetails();
        services.AddScoped<DataSeeder>();

        services.AddQalyRazorPages();
        services.AddQalyAuthentication(cookieSecurePolicy);
        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
            options.AddPolicy("UserManagementAccess", policy => policy.RequireRole("Admin"));
        });
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "Qaly.Csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = cookieSecurePolicy;
        });
        services.AddControllers();

        services.AddSignalR();
        services.AddSingleton<INotificationPublisher, SignalRNotificationPublisher>();
        services.AddSingleton<IGroupPollRealtimePublisher, SignalRGroupPollRealtimePublisher>();
        services.AddSingleton<IGroupMeetingRealtimePublisher, SignalRGroupMeetingRealtimePublisher>();
        services.AddSingleton<GroupMeetingPresenceTracker>();

        services.AddOpenApi();
        services.AddHealthChecks()
            .AddSqlServer(configuration.GetConnectionString("DefaultConnection")!)
            .AddRedis(redisConnection)
            .AddCheck<OutboxHealthCheck>("vector_outbox");

        return builder;
    }

    private static void AddQalyDataProtection(this IServiceCollection services, IWebHostEnvironment environment)
    {
        var dataProtectionKeysPath = Path.Combine(environment.ContentRootPath, "dp-keys");
        Directory.CreateDirectory(dataProtectionKeysPath);

        // One application name and one durable key ring keep auth/session/CSRF
        // protection consistent across preview restarts. Registering two key
        // locations makes the active key ring depend on options ordering.
        services.AddDataProtection()
            .SetApplicationName("Qaly")
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
    }

    private static void AddQalyRedis(
        this IServiceCollection services,
        string redisConnection,
        bool useInMemoryDistributedCache)
    {
        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnection));

        if (useInMemoryDistributedCache)
        {
            services.AddDistributedMemoryCache();
            return;
        }

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = "Qaly_";
        });
    }

    private static void AddQalySession(this IServiceCollection services, CookieSecurePolicy cookieSecurePolicy)
    {
        services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(30);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SecurePolicy = cookieSecurePolicy;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });
    }

    private static void AddQalyRazorPages(this IServiceCollection services)
    {
        services.AddRazorPages(options =>
        {
            options.Conventions.AuthorizePage("/Index");
            options.Conventions.AuthorizePage("/SpaFallback");
            options.Conventions.AddPageRoute("/Index", "dashboard");
            options.Conventions.AddPageRoute("/Index", "profile");
            options.Conventions.AddPageRoute("/Index", "projects");
            options.Conventions.AddPageRoute("/Index", "projects/archived");
            options.Conventions.AddPageRoute("/Index", "projects/{projectId}");
            options.Conventions.AddPageRoute("/Index", "projects/{projectId}/tasks/{taskId}");
            options.Conventions.AddPageRoute("/Index", "projects/{projectId}/wiki/{wikiId}");
            options.Conventions.AddPageRoute("/Index", "tasks");
            options.Conventions.AddPageRoute("/Index", "teams");
            options.Conventions.AddPageRoute("/Index", "analytics");
            options.Conventions.AddPageRoute("/Index", "settings");
            options.Conventions.AddPageRoute("/Index", "admin/users");
            options.Conventions.AddPageRoute("/Index", "admin/moderators");
            options.Conventions.AddPageRoute("/Index", "organizations/users");
            options.Conventions.AddPageRoute("/Index", "groups");
            options.Conventions.AddPageRoute("/Index", "groups/{groupId}");
            options.Conventions.AddPageRoute("/Index", "groups/{groupId}/meeting");
            options.Conventions.AddPageRoute("/Index", "groups/{groupId}/polls");
            options.Conventions.AddPageRoute("/Index", "groups/{groupId}/polls/{pollId}");
            options.Conventions.AllowAnonymousToPage("/Account/Login");
            options.Conventions.AllowAnonymousToPage("/Account/Register");
            options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
        });
    }

    private static void AddQalyAuthentication(this IServiceCollection services, CookieSecurePolicy cookieSecurePolicy)
    {
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "Qaly.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = cookieSecurePolicy;
                options.Cookie.SameSite = SameSiteMode.Lax;
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
                options.Events.OnValidatePrincipal = async context =>
                {
                    var userIdText = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    var issuedRole = context.Principal?.FindFirstValue(ClaimTypes.Role);
                    if (!Guid.TryParse(userIdText, out var userId))
                    {
                        context.RejectPrincipal();
                        return;
                    }

                    var db = context.HttpContext.RequestServices.GetRequiredService<QalyDbContext>();
                    var current = await db.Users.AsNoTracking()
                        .Where(user => user.Id == userId)
                        .Select(user => new { user.IsActive, user.Role })
                        .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
                    if (current == null || !current.IsActive || !string.Equals(current.Role, issuedRole, StringComparison.Ordinal))
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                };
            });

        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<ITicketStore>((options, ticketStore) => options.SessionStore = ticketStore);

        services.AddAuthentication()
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyDefaults.AuthenticationScheme,
                _ => { });
    }

    private static string NormalizeRedisConnection(string? redisConnection)
    {
        redisConnection = string.IsNullOrWhiteSpace(redisConnection)
            ? "localhost:6379"
            : redisConnection;

        var options = new[]
        {
            ("abortConnect", "false"),
            ("connectTimeout", "250"),
            ("syncTimeout", "250"),
            ("asyncTimeout", "250")
        };

        foreach (var (key, value) in options)
        {
            if (!redisConnection.Contains($"{key}=", StringComparison.OrdinalIgnoreCase))
            {
                redisConnection = $"{redisConnection},{key}={value}";
            }
        }

        return redisConnection;
    }
}
