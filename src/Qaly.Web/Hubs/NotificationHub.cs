using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Qaly.Web.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (!string.IsNullOrWhiteSpace(Context.UserIdentifier))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(Context.UserIdentifier));
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinProject(Guid projectId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, ProjectGroup(projectId.ToString()));
    }

    public async Task LeaveProject(Guid projectId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ProjectGroup(projectId.ToString()));
    }

    public static string UserGroup(string userId)
        => $"user:{userId}";

    public static string ProjectGroup(string projectId)
        => $"project:{projectId}";
}
