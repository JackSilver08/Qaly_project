using Qaly.Application.Common.Interfaces;

namespace Qaly.Application.Services;

public class AiService : IAiService
{
    public Task<string> SuggestTaskPriorityAsync(string taskTitle, string taskDescription, string projectContext)
    {
        return Task.FromResult("Medium");
    }

    public Task<string> GenerateProjectSummaryAsync(Guid projectId)
    {
        return Task.FromResult("AI Summary: This project is in its initial foundation phase.");
    }

    public Task<string> AnalyzeProjectRisksAsync(Guid projectId)
    {
        return Task.FromResult("AI Analysis: Project looks stable. No immediate risks detected.");
    }

    public Task<string> SuggestTaskAssignmentAsync(Guid taskId, Guid projectId)
    {
        return Task.FromResult("Assign to: Admin User (Default)");
    }

    public Task<IReadOnlyList<string>> SmartSearchAsync(string query, Guid? projectId = null)
    {
        return Task.FromResult<IReadOnlyList<string>>(new List<string> { "Matching result 1", "Matching result 2" });
    }

    public Task<IReadOnlyList<string>> GenerateSubtasksAsync(string taskTitle, string taskDescription)
    {
        return Task.FromResult<IReadOnlyList<string>>(new List<string> { "Setup environment", "Define requirements", "Implement core logic" });
    }

    public Task<string> ChatAsync(string userMessage, Guid? projectId = null)
    {
        return Task.FromResult("AI: Hello! How can I help you with your project?");
    }
}
