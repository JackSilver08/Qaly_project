using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Qaly.Web.Hubs;

/// <summary>
/// SignalR Hub phát sự kiện Realtime Permission Invalidation.
/// Ngay khi Admin sửa ma trận phân quyền, Hub gửi event "OnPermissionsInvalidated"
/// tới Channel tương ứng để Client refetch Pinia permission store tức thì.
/// </summary>
[Authorize]
public class PermissionHub : Hub
{
    public async Task JoinProjectPermissionGroup(string projectId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"project_permissions_{projectId}");
    }

    public async Task LeaveProjectPermissionGroup(string projectId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"project_permissions_{projectId}");
    }
}
