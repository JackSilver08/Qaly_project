using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.Services;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Interceptors;
using Qaly.Infrastructure.Data.Repositories;
using Qaly.Infrastructure.Services;
using Qaly.Infrastructure.Services.AI;
using Qaly.Infrastructure.Services.AI.Providers;
using OllamaSharp;
using Qaly.Infrastructure.Services.Privacy;

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
        services.AddScoped<INotificationTargetResolver, NotificationTargetResolver>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<IAiExportService, AiExportService>();
        services.AddScoped<ISessionService, RedisSessionService>();
        services.AddScoped<IWebhookPublisher, WebhookPublisher>();
        services.AddScoped<Qaly.Application.Common.Interfaces.IPushSender, WebPushSender>();

        // AI Core Services
        services.Configure<AiJobPlatformOptions>(configuration.GetSection(AiJobPlatformOptions.SectionName));
        services.PostConfigure<AiJobPlatformOptions>(options =>
        {
            if (bool.TryParse(configuration["AI_JOB_V4_ENABLED"], out var enabled)) options.Enabled = enabled;
            if (bool.TryParse(configuration["AI_JOB_V4_WORKER_ENABLED"], out var workerEnabled)) options.WorkerEnabled = workerEnabled;
        });
        services.Configure<PrivacyV4Options>(configuration.GetSection(PrivacyV4Options.SectionName));
        services.PostConfigure<PrivacyV4Options>(options =>
        {
            if (bool.TryParse(configuration["PRIVACY_V4_ENABLED"], out var enabled)) options.Enabled = enabled;
            if (bool.TryParse(configuration["PRIVACY_V4_WORKER_ENABLED"], out var workerEnabled)) options.WorkerEnabled = workerEnabled;
            if (bool.TryParse(configuration["PRIVACY_V4_ENFORCED"], out var enforced)) options.EnforceSensitiveIngestion = enforced;
        });
        services.AddScoped<IAiCostService, Qaly.Infrastructure.Services.AI.AiCostService>();
        services.AddScoped<IAiComplianceService, Qaly.Infrastructure.Services.AI.AiComplianceService>();
        services.AddSingleton<IPrivacyPayloadProtector, PrivacyPayloadProtector>();
        services.AddScoped<IPrivacyService, PrivacyService>();
        services.AddScoped<IPrivacyWorkStore, PrivacyWorkStore>();
        services.AddScoped<IPrivacyWorkProcessor, PrivacyWorkProcessor>();
        services.AddScoped<IPrivacyOperationsService, PrivacyOperationsService>();
        services.AddScoped<IAiSourceGuard, AiSourceGuard>();
        services.AddScoped<IAiJobDispatchStore, AiJobDispatchStore>();
        services.AddScoped<IAiJobProcessor, AiJobProcessor>();
        
        // AI Providers & Routing Infrastructure
        services.AddTransient<IAiProvider, Qaly.Infrastructure.Services.AI.Providers.OllamaProvider>();
        services.AddTransient<IAiProvider, Qaly.Infrastructure.Services.AI.Providers.OpenAIProvider>();
        services.AddTransient<IAiProvider, Qaly.Infrastructure.Services.AI.Providers.GeminiProvider>();
        services.AddScoped<AiProviderFactory>();
        services.AddSingleton<AiOutputValidator>();

        services.AddScoped<IAiGateway, Qaly.Infrastructure.Services.AI.AiGateway>();
        services.AddScoped<IAiAgentOrchestrator, MicrosoftAgentOrchestrator>();
        
        services.AddHttpClient("WebhookClient");

        // AI Services
        var ollamaUrl = configuration["Ai:OllamaUrl"] ?? "http://localhost:11434";
        var chatModel = configuration["Ai:ChatModel"] ?? "llama3.2";
        var embeddingModel = configuration["Ai:EmbeddingModel"] ?? "nomic-embed-text";

        services.AddChatClient((IChatClient)new OllamaApiClient(new Uri(ollamaUrl), chatModel));
        services.AddEmbeddingGenerator((IEmbeddingGenerator<string, Embedding<float>>)
            new OllamaApiClient(new Uri(ollamaUrl), embeddingModel));
        
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
        services.AddScoped<ToolParameterGuard>();
        


        services.AddSingleton<Microsoft.AspNetCore.Authentication.Cookies.ITicketStore, Auth.RedisTicketStore>();

        // Background Workers
        services.AddHostedService<DatabaseMigrationHostedService>();
        services.AddHostedService<EmailDigestWorker>();
        services.AddHostedService<TaskAttentionSignalWorker>();
        services.AddHostedService<ProjectTrashCleanupWorker>();
        services.AddHostedService<AiJobWorker>();
        services.AddHostedService<PrivacyWorker>();

        return services;
    }
}
