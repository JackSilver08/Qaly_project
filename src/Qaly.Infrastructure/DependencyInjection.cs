using Microsoft.EntityFrameworkCore;
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
using Qaly.Infrastructure.Integrations.GitHub;

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
        var useInMemoryDatabase = configuration.GetValue<bool>("UseInMemoryDatabase");
        var defaultConnection = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<QalyDbContext>((sp, options) =>
        {
            if (useInMemoryDatabase)
            {
                options.UseInMemoryDatabase("QalyInMemory");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(defaultConnection))
                {
                    throw new InvalidOperationException(
                        "ConnectionStrings:DefaultConnection is required when UseInMemoryDatabase is false.");
                }

                options.UseSqlServer(defaultConnection, sqlServer =>
                {
                    sqlServer.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    sqlServer.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                });
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
        services.AddScoped<WebhookOutboxProcessor>();
        services.AddScoped<WebhookOperationsRetentionService>();
        services.AddSingleton<IWebhookDnsResolver, SystemWebhookDnsResolver>();
        services.AddSingleton<IWebhookEndpointPolicy, WebhookEndpointPolicy>();
        services.AddScoped<Qaly.Application.Common.Interfaces.IPushSender, WebPushSender>();
        services.Configure<GitHubIntegrationOptions>(configuration.GetSection(GitHubIntegrationOptions.SectionName));
        services.AddScoped<IGitHubWebhookReceiver, GitHubWebhookReceiver>();
        services.AddScoped<IGitHubWebhookProcessor, GitHubWebhookProcessor>();
        services.AddScoped<IGitHubWebhookInboxStore, GitHubWebhookInboxStore>();
        services.AddHttpClient<IGitHubAppClient, GitHubAppClient>();
        services.AddScoped<IGitHubInstallationService, GitHubInstallationService>();
        services.AddScoped<IGitHubProjectManagementService, GitHubProjectManagementService>();
        services.AddScoped<Qaly.Application.Services.GitHub.IGitHubRepositoryProvider, GitHubRepositoryProvider>();

        // AI Core Services
        services.Configure<AiJobPlatformOptions>(configuration.GetSection(AiJobPlatformOptions.SectionName));
        services.PostConfigure<AiJobPlatformOptions>(options =>
        {
            if (bool.TryParse(configuration["AI_JOB_V4_ENABLED"], out var enabled)) options.Enabled = enabled;
            if (bool.TryParse(configuration["AI_JOB_V4_WORKER_ENABLED"], out var workerEnabled)) options.WorkerEnabled = workerEnabled;
            if (bool.TryParse(configuration["AI_BUDGET_UI_ENABLED"], out var budgetUiEnabled)) options.BudgetUiEnabled = budgetUiEnabled;
            if (bool.TryParse(configuration["AI_TASK_SKILL_SUGGESTION_ENABLED"], out var taskSkillSuggestionEnabled)) options.TaskSkillSuggestionEnabled = taskSkillSuggestionEnabled;
            if (bool.TryParse(configuration["AI_ACTION_COMPOSER_ENABLED"], out var actionComposerEnabled)) options.ActionComposerEnabled = actionComposerEnabled;
            if (bool.TryParse(configuration["AI_ACTION_COMPOSER_TASK_CREATE_ENABLED"], out var actionComposerTaskCreateEnabled)) options.ActionComposerTaskCreateEnabled = actionComposerTaskCreateEnabled;
            if (bool.TryParse(configuration["AI_ASSISTANT_SESSION_ENABLED"], out var assistantSessionEnabled)) options.AssistantSessionEnabled = assistantSessionEnabled;
            if (bool.TryParse(configuration["AI_ASSISTANT_CONTEXT_REGISTRY_ENABLED"], out var assistantContextRegistryEnabled)) options.AssistantContextRegistryEnabled = assistantContextRegistryEnabled;
            if (bool.TryParse(configuration["AI_ASSISTANT_RESEARCH_PLAN_ENABLED"], out var assistantResearchPlanEnabled)) options.AssistantResearchPlanEnabled = assistantResearchPlanEnabled;
            if (bool.TryParse(configuration["AI_ASSISTANT_GOAL_PLANNER_ENABLED"], out var assistantGoalPlannerEnabled)) options.AssistantGoalPlannerEnabled = assistantGoalPlannerEnabled;
            if (bool.TryParse(configuration["AI_ASSISTANT_READ_ONLY_LOOP_ENABLED"], out var assistantReadOnlyLoopEnabled)) options.AssistantReadOnlyLoopEnabled = assistantReadOnlyLoopEnabled;
            if (bool.TryParse(configuration["AI_ASSISTANT_PROGRESSIVE_INTERACTION_ENABLED"], out var assistantProgressiveInteractionEnabled)) options.AssistantProgressiveInteractionEnabled = assistantProgressiveInteractionEnabled;
            if (bool.TryParse(configuration["AI_PROJECT_LAUNCH_BRIEF_ENABLED"], out var projectLaunchBriefEnabled)) options.ProjectLaunchBriefEnabled = projectLaunchBriefEnabled;
            if (bool.TryParse(configuration["AI_PROJECT_LAUNCH_PLANNING_ENABLED"], out var projectLaunchPlanningEnabled)) options.ProjectLaunchPlanningEnabled = projectLaunchPlanningEnabled;
            if (bool.TryParse(configuration["AI_PROJECT_LAUNCH_EXECUTION_ENABLED"], out var projectLaunchExecutionEnabled)) options.ProjectLaunchExecutionEnabled = projectLaunchExecutionEnabled;
            if (bool.TryParse(configuration["AI_PROJECT_OPERATION_MONITORING_ENABLED"], out var projectOperationMonitoringEnabled)) options.ProjectOperationMonitoringEnabled = projectOperationMonitoringEnabled;
            if (bool.TryParse(configuration["AI_SAFE_TEST_ORCHESTRATOR_ENABLED"], out var safeTestOrchestratorEnabled)) options.SafeTestOrchestratorEnabled = safeTestOrchestratorEnabled;
            if (bool.TryParse(configuration["AI_NATIVE_DOMAIN_ACTIONS_ENABLED"], out var nativeDomainActionsEnabled)) options.NativeDomainActionsEnabled = nativeDomainActionsEnabled;
        });
        services.Configure<PrivacyV4Options>(configuration.GetSection(PrivacyV4Options.SectionName));
        services.Configure<VectorSyncOptions>(configuration.GetSection(VectorSyncOptions.SectionName));
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
        services.AddScoped<IAiJobActivityService, AiJobActivityService>();
        services.AddScoped<IAiActionPlanValidator, AiActionPlanValidator>();
        services.AddScoped<IAiAssistantSessionService, AiAssistantSessionService>();
        services.AddScoped<IAiAssistantContextRegistry, AiAssistantContextRegistry>();
        services.AddScoped<IAiNativeActionService, AiNativeActionService>();
        services.AddScoped<IOrganizationWorkRulebookService, OrganizationWorkRulebookService>();
        services.AddScoped<IProjectLaunchService, ProjectLaunchService>();
        services.AddScoped<IProjectLaunchOrchestratorService, ProjectLaunchOrchestratorService>();
        services.AddScoped<IAiSafeTestOrchestratorService, AiSafeTestOrchestratorService>();
        services.AddScoped<IAiJobDispatchStore, AiJobDispatchStore>();
        services.AddScoped<IAiJobProcessor, AiJobProcessor>();
        
        // AI Providers & Routing Infrastructure
        services.AddTransient<IAiProvider, Qaly.Infrastructure.Services.AI.Providers.OllamaProvider>();
        services.AddTransient<IAiProvider, Qaly.Infrastructure.Services.AI.Providers.DeepSeekProvider>();
        services.AddTransient<IAiProvider, Qaly.Infrastructure.Services.AI.Providers.OpenAIProvider>();
        services.AddTransient<IAiProvider, Qaly.Infrastructure.Services.AI.Providers.GeminiProvider>();
        services.AddScoped<AiProviderFactory>();
        services.AddSingleton<AiOutputValidator>();

        services.AddScoped<IAiGateway, Qaly.Infrastructure.Services.AI.AiGateway>();
        services.AddScoped<IAiAgentOrchestrator, MicrosoftAgentOrchestrator>();
        
        services.AddHttpClient("WebhookClient")
            .ConfigurePrimaryHttpMessageHandler(WebhookConnectionGuard.CreateHandler);

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
        services.AddScoped<IVectorSyncOutboxStore, VectorSyncOutboxStore>();
        services.AddScoped<AiTools>();
        services.AddScoped<ToolParameterGuard>();
        


        services.AddSingleton<Microsoft.AspNetCore.Authentication.Cookies.ITicketStore, Auth.RedisTicketStore>();

        // Background Workers
        services.AddHostedService<DatabaseMigrationHostedService>();
        services.AddHostedService<EmailDigestWorker>();
        services.AddHostedService<TaskAttentionSignalWorker>();
        services.AddHostedService<ProjectTrashCleanupWorker>();
        services.AddHostedService<ProjectVisibilityHealthCheckWorker>();
        services.AddHostedService<AiJobWorker>();
        services.AddHostedService<PrivacyWorker>();
        services.AddHostedService<GitHubWebhookWorker>();
        services.AddHostedService<ProjectOperationMonitorWorker>();
        services.AddHostedService<WebhookOutboxWorker>();
        services.AddHostedService<WebhookOperationsRetentionWorker>();

        return services;
    }
}
