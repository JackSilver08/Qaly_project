using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Interceptors;
using Qaly.Infrastructure.Data.Repositories;
using Qaly.Infrastructure.Services;

namespace Qaly.Infrastructure;

/// <summary>
/// Extension method để đăng ký tất cả services của Infrastructure layer.
/// Gọi trong Program.cs: builder.Services.AddInfrastructure(configuration);
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Interceptors
        services.AddSingleton<VectorSyncInterceptor>();

        // DbContext
        services.AddDbContext<QalyDbContext>((sp, options) =>
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                options.UseInMemoryDatabase("QalyInMemory");
            }
            else
            {
                options.UseSqlServer(
                        configuration.GetConnectionString("DefaultConnection"),
                        sqlOptions =>
                        {
                            sqlOptions.MigrationsAssembly(typeof(QalyDbContext).Assembly.FullName);
                            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                        })
                    .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
            }

            options.AddInterceptors(sp.GetRequiredService<VectorSyncInterceptor>());
        });

        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IProjectDashboardSummaryRepository, ProjectDashboardSummaryRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Services
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<IAiExportService, AiExportService>();
        services.AddScoped<ISessionService, RedisSessionService>();
        services.AddScoped<IWebhookPublisher, WebhookPublisher>();

        // AI Core Services
        services.AddScoped<IAiCostService, Qaly.Infrastructure.Services.AI.AiCostService>();
        services.AddScoped<IAiComplianceService, Qaly.Infrastructure.Services.AI.AiComplianceService>();
        services.AddScoped<IAiGateway, Qaly.Infrastructure.Services.AI.AiGateway>();
        
        services.AddHttpClient("WebhookClient");

        // AI Services
        var ollamaUrl = configuration["Ai:OllamaUrl"] ?? "http://localhost:11434";
        var chatModel = configuration["Ai:ChatModel"] ?? "llama3.2";
        var embeddingModel = configuration["Ai:EmbeddingModel"] ?? "nomic-embed-text";

        services.AddChatClient(new OllamaChatClient(new Uri(ollamaUrl), chatModel));
        services.AddEmbeddingGenerator(new OllamaEmbeddingGenerator(new Uri(ollamaUrl), embeddingModel));
        
        var semanticEnabled = configuration.GetValue<bool>("Ai:SemanticEnabled");
        if (semanticEnabled)
        {
            services.AddSingleton<IVectorStorageService, QdrantVectorStorageService>();
            services.AddHostedService<VectorSyncWorker>();
        }
        else
        {
            services.AddSingleton<IVectorStorageService, NullVectorStorageService>();
        }
        services.AddScoped<IAiIngestionService, AiIngestionService>();
        services.AddScoped<AiTools>();
        


        services.AddSingleton<Microsoft.AspNetCore.Authentication.Cookies.ITicketStore, Auth.RedisTicketStore>();

        // Background Workers
        services.AddHostedService<EmailDigestWorker>();
        services.AddHostedService<TaskAttentionSignalWorker>();

        return services;
    }
}
