namespace Qaly.Application.Common.Interfaces;

public interface ITaskPrioritySuggestionService
{
    Task<string> SuggestAsync(string taskTitle, string taskDescription, string projectContext);
}
