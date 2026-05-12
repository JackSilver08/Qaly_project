using Microsoft.AspNetCore.SignalR;
using Qaly.Application.Common.Interfaces;

namespace Qaly.Web.Hubs;

public class AiHub : Hub
{
    private readonly IAiService _aiService;

    public AiHub(IAiService aiService)
    {
        _aiService = aiService;
    }

    public async Task SendMessage(string message, Guid? projectId)
    {
        var connectionId = Context.ConnectionId;
        
        try 
        {
            await foreach (var token in _aiService.ChatStreamingAsync(message, projectId))
            {
                await Clients.Caller.SendAsync("ReceiveToken", token);
            }
            await Clients.Caller.SendAsync("StreamComplete");
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("StreamError", ex.Message);
        }
    }
}
