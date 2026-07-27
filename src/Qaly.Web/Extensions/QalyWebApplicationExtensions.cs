using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Qaly.Application.Common.Interfaces;
using Qaly.Infrastructure.Data.Seeds;
using Qaly.Web.Hubs;
using Qaly.Web.Middleware;
using Scalar.AspNetCore;
using Serilog;

namespace Qaly.Web.Extensions;

public static class QalyWebApplicationExtensions
{
    public static void UseQalyRequestPipeline(this WebApplication app)
    {
        app.UseSerilogRequestLogging();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/admin") &&
                context.User.Identity?.IsAuthenticated == true &&
                !context.User.IsInRole("Admin"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            await next();
        });
        app.UseAuthorization();
        app.UseAntiforgery();
        app.UseSession();
    }

    public static async Task UseQalyDevelopmentSetupAsync(this WebApplication app)
    {
        var seedInAnyEnvironment = app.Configuration.GetValue<bool>("UseInMemoryDatabase");
        if (!app.Environment.IsDevelopment() && !seedInAnyEnvironment)
        {
            return;
        }

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

        if (app.Configuration.GetValue("Ai:SyncOnStartup", false))
        {
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
        }

        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    public static void MapQalyEndpoints(this WebApplication app)
    {
        app.MapRazorPages();
        app.MapControllers();
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });
        app.MapHub<NotificationHub>("/hubs/notification");
        app.MapHub<AiHub>("/hubs/ai");
        app.MapHub<GroupHub>("/hubs/groups");

        // Preserve backend/auth 404 contracts while allowing Vue Router to
        // render its own not-found page for unknown browser routes.
        app.MapFallback("/api/{**path}", () => Results.NotFound());
        app.MapFallback("/hubs/{**path}", () => Results.NotFound());
        app.MapFallback("/Account/{**path}", () => Results.NotFound());
        app.MapFallbackToPage("/SpaFallback");
    }
}
