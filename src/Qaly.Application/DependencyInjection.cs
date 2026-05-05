using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services;

namespace Qaly.Application;

/// <summary>
/// Extension method để đăng ký tất cả services của Application layer.
/// Gọi trong Program.cs: builder.Services.AddApplication();
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAiService, AiService>();
        services.AddSingleton<INotificationPublisher, NullNotificationPublisher>();

        return services;
    }
}
