namespace Qaly.Application.Services;

/// <summary>
/// The public API surface currently supported for personal API keys.
/// Any scope or route outside this catalog must fail closed.
/// </summary>
public static class ApiKeyScopeCatalog
{
    public const string ProjectsRead = "projects:read";
    public const string ProjectsWrite = "projects:write";
    public const string TasksRead = "tasks:read";
    public const string TasksWrite = "tasks:write";
    public const string WebhooksRead = "webhooks:read";
    public const string WebhooksWrite = "webhooks:write";

    public static readonly IReadOnlySet<string> Supported = new HashSet<string>(
        [
            ProjectsRead,
            ProjectsWrite,
            TasksRead,
            TasksWrite,
            WebhooksRead,
            WebhooksWrite
        ],
        StringComparer.Ordinal);

    public static readonly IReadOnlyList<string> DefaultScopes =
        [ProjectsRead, TasksRead];

    public static List<string> Normalize(IEnumerable<string>? scopes)
        => (scopes ?? DefaultScopes)
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(scope => scope, StringComparer.Ordinal)
            .ToList();
}
