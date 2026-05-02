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

    public static string UserGroup(string userId)
        => $"user:{userId}";
}
