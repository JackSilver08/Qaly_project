using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services;
using Qaly.Application.Services.Groups;
using Qaly.Application.Services.Meetings;
using Qaly.Application.Services.Tasks;

namespace Qaly.Application;

/// <summary>
/// Extension method Ä‘á»ƒ Ä‘Äƒng kÃ½ táº¥t cáº£ services cá»§a Application layer.
/// Gá»i trong Program.cs: builder.Services.AddApplication();
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IGroupPollRealtimePublisher, NullGroupPollRealtimePublisher>();
        services.AddScoped<IGroupMeetingRealtimePublisher, NullGroupMeetingRealtimePublisher>();

        // Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IGroupsService, GroupsService>();
        services.AddScoped<IGroupAttachmentService, GroupAttachmentService>();
        services.AddScoped<IGroupInvitationEmailBuilder, GroupInvitationEmailBuilder>();
        services.AddScoped<ILiveKitTokenService, LiveKitTokenService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<ITaskAccessPolicy, TaskAccessPolicy>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ITimeTrackingService, TimeTrackingService>();
        services.AddScoped<ITaskPrioritySuggestionService, TaskPrioritySuggestionService>();
        services.AddScoped<IAiService, AiService>();
        services.AddScoped<IErumiChatService, ErumiChatService>();
        services.AddScoped<IAiWorkflowService, AiWorkflowService>();
        services.AddScoped<IGroupAiService, GroupAiService>();
        services.AddScoped<IWebhookService, WebhookService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IApiKeyService, ApiKeyService>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IFileImportService, FileImportService>();
        services.AddScoped<IWikiService, WikiService>();
        services.AddScoped<IMeetingImportService, MeetingImportService>();
        services.AddScoped<IDashboardSummaryService, DashboardSummaryService>();

        return services;
    }
}
