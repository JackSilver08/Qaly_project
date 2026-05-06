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
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(QalyDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                })
                .AddInterceptors(sp.GetRequiredService<VectorSyncInterceptor>()));

        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Services
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IAiExportService, AiExportService>();

        // AI Services
        var ollamaUrl = configuration["Ai:OllamaUrl"] ?? "http://localhost:11434";
        var chatModel = configuration["Ai:ChatModel"] ?? "llama3.2";
        var embeddingModel = configuration["Ai:EmbeddingModel"] ?? "nomic-embed-text";

        services.AddChatClient(new OllamaChatClient(new Uri(ollamaUrl), chatModel));
        services.AddEmbeddingGenerator(new OllamaEmbeddingGenerator(new Uri(ollamaUrl), embeddingModel));
        
        services.AddSingleton<IVectorStorageService, QdrantVectorStorageService>();
        services.AddScoped<IAiIngestionService, AiIngestionService>();
        services.AddScoped<AiTools>();

        // Background Workers
        services.AddHostedService<VectorSyncWorker>();

        return services;
    }
}
