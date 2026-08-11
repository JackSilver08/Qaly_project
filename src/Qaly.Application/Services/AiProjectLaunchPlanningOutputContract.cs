using System.Text.Json;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public static class AiProjectLaunchPlanningOutputContract
{
    private const int MaxSprints = 8;
    private const int MaxTasks = 80;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> RootFields = new(StringComparer.Ordinal)
    {
        "architectureProposal", "sprints", "criticalPathClientIds", "collaborationProposal", "externalDeferred", "assumptions"
    };
    private static readonly HashSet<string> SprintFields = new(StringComparer.Ordinal)
    {
        "clientId", "name", "objective", "startWeek", "durationWeeks", "exitCriteria", "tasks"
    };
    private static readonly HashSet<string> TaskFields = new(StringComparer.Ordinal)
    {
        "clientId", "title", "description", "acceptanceCriteria", "definitionOfDone", "priority", "estimatedHours",
        "requiredSkillNames", "dependencyClientIds"
    };

    public static bool TryParse(string json, out ProjectLaunchModelOutputDto? output, out string error)
    {
        output = null;
        error = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "Root must be an object.";
                return false;
            }
            if (!HasOnly(document.RootElement, RootFields))
            {
                error = "Root contains an unknown field.";
                return false;
            }

            var parsed = JsonSerializer.Deserialize<ProjectLaunchModelOutputDto>(json, JsonOptions);
            if (parsed == null || parsed.Sprints == null || parsed.Sprints.Count is < 1 or > MaxSprints)
            {
                error = $"Plan requires 1-{MaxSprints} sprints.";
                return false;
            }
            if (parsed.ArchitectureProposal == null || parsed.ArchitectureProposal.Count > 12 ||
                parsed.CriticalPathClientIds == null || parsed.CollaborationProposal == null ||
                parsed.ExternalDeferred == null || parsed.Assumptions == null)
            {
                error = "Plan collections are missing or too large.";
                return false;
            }

            var sprintElements = document.RootElement.GetProperty("sprints").EnumerateArray().ToArray();
            if (sprintElements.Any(item => item.ValueKind != JsonValueKind.Object || !HasOnly(item, SprintFields)))
            {
                error = "Sprint contains an unknown field.";
                return false;
            }
            if (sprintElements.SelectMany(item => item.GetProperty("tasks").EnumerateArray())
                .Any(item => item.ValueKind != JsonValueKind.Object || !HasOnly(item, TaskFields)))
            {
                error = "Task contains an unknown field.";
                return false;
            }

            var sprintIds = new HashSet<string>(StringComparer.Ordinal);
            var taskIds = new HashSet<string>(StringComparer.Ordinal);
            var taskCount = 0;
            foreach (var sprint in parsed.Sprints)
            {
                if (!ValidId(sprint.ClientId) || !sprintIds.Add(sprint.ClientId) ||
                    string.IsNullOrWhiteSpace(sprint.Name) || string.IsNullOrWhiteSpace(sprint.Objective) ||
                    sprint.StartWeek is < 1 or > 52 || sprint.DurationWeeks is < 1 or > 26 ||
                    sprint.ExitCriteria == null || sprint.ExitCriteria.Count is < 1 or > 12 ||
                    sprint.Tasks == null || sprint.Tasks.Count is < 1 or > 30)
                {
                    error = "Sprint is invalid.";
                    return false;
                }
                foreach (var task in sprint.Tasks)
                {
                    taskCount++;
                    if (!ValidId(task.ClientId) || !taskIds.Add(task.ClientId) ||
                        string.IsNullOrWhiteSpace(task.Title) || task.Title.Length > 200 ||
                        string.IsNullOrWhiteSpace(task.Description) || task.Description.Length > 2000 ||
                        task.AcceptanceCriteria == null || task.AcceptanceCriteria.Count is < 1 or > 12 ||
                        task.DefinitionOfDone == null || task.DefinitionOfDone.Count is < 1 or > 12 ||
                        task.EstimatedHours is < 1 or > 320 ||
                        task.RequiredSkillNames == null || task.RequiredSkillNames.Count > 12 ||
                        task.DependencyClientIds == null || task.DependencyClientIds.Count > 20 ||
                        task.Priority is not ("Low" or "Medium" or "High" or "Critical"))
                    {
                        error = "Task is invalid.";
                        return false;
                    }
                }
            }
            if (taskCount > MaxTasks)
            {
                error = $"Plan exceeds {MaxTasks} tasks.";
                return false;
            }

            var tasks = parsed.Sprints.SelectMany(item => item.Tasks).ToDictionary(item => item.ClientId, StringComparer.Ordinal);
            if (tasks.Values.Any(item => item.DependencyClientIds.Any(dependency =>
                    dependency == item.ClientId || !tasks.ContainsKey(dependency))) ||
                parsed.CriticalPathClientIds.Any(id => !tasks.ContainsKey(id)) || HasCycle(tasks))
            {
                error = "Task dependency graph is invalid or cyclic.";
                return false;
            }

            output = Normalize(parsed);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            error = exception.Message;
            return false;
        }
    }

    private static ProjectLaunchModelOutputDto Normalize(ProjectLaunchModelOutputDto value)
        => value with
        {
            ArchitectureProposal = NormalizeList(value.ArchitectureProposal, 12, 500),
            CriticalPathClientIds = NormalizeList(value.CriticalPathClientIds, MaxTasks, 80),
            CollaborationProposal = NormalizeList(value.CollaborationProposal, 12, 500),
            ExternalDeferred = NormalizeList(value.ExternalDeferred, 12, 500),
            Assumptions = NormalizeList(value.Assumptions, 20, 500),
            Sprints = value.Sprints.Select(sprint => sprint with
            {
                ClientId = sprint.ClientId.Trim(),
                Name = Limit(sprint.Name, 160),
                Objective = Limit(sprint.Objective, 800),
                ExitCriteria = NormalizeList(sprint.ExitCriteria, 12, 500),
                Tasks = sprint.Tasks.Select(task => task with
                {
                    ClientId = task.ClientId.Trim(),
                    Title = Limit(task.Title, 200),
                    Description = Limit(task.Description, 2000),
                    AcceptanceCriteria = NormalizeList(task.AcceptanceCriteria, 12, 500),
                    DefinitionOfDone = NormalizeList(task.DefinitionOfDone, 12, 500),
                    RequiredSkillNames = NormalizeList(task.RequiredSkillNames, 12, 100),
                    DependencyClientIds = NormalizeList(task.DependencyClientIds, 20, 80)
                }).ToArray()
            }).ToArray()
        };

    private static bool HasCycle(IReadOnlyDictionary<string, ProjectLaunchModelTaskDto> tasks)
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        bool Visit(string id)
        {
            if (!visiting.Add(id)) return true;
            if (visited.Contains(id)) return false;
            foreach (var dependency in tasks[id].DependencyClientIds)
                if (!visited.Contains(dependency) && Visit(dependency)) return true;
            visiting.Remove(id);
            visited.Add(id);
            return false;
        }
        return tasks.Keys.Any(id => !visited.Contains(id) && Visit(id));
    }

    private static bool HasOnly(JsonElement element, HashSet<string> allowed)
        => element.EnumerateObject().All(property => allowed.Contains(property.Name));

    private static bool ValidId(string value)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= 80 &&
            value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_');

    private static string[] NormalizeList(IReadOnlyList<string> values, int max, int maxLength)
        => values.Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => Limit(item, maxLength))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToArray();

    private static string Limit(string value, int max)
    {
        var normalized = value.Trim();
        return normalized.Length <= max ? normalized : normalized[..max];
    }
}
