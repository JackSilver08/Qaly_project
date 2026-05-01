using Microsoft.Extensions.DependencyInjection;

namespace Qaly.Application;

/// <summary>
/// Extension method để đăng ký tất cả services của Application layer.
/// Gọi trong Program.cs: builder.Services.AddApplication();
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Service registrations sẽ được thêm khi implement
        // Ví dụ:
        // services.AddScoped<IProjectService, ProjectService>();
        // services.AddScoped<ITaskService, TaskService>();
        // services.AddScoped<ICommentService, CommentService>();
        // services.AddScoped<INotificationService, NotificationService>();
        // services.AddScoped<IAuditLogService, AuditLogService>();

        return services;
    }
}
