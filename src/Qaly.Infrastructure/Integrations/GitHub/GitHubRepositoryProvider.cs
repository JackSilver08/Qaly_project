using Qaly.Application.Services.GitHub;

namespace Qaly.Infrastructure.Integrations.GitHub;

public sealed class GitHubRepositoryProvider : IGitHubRepositoryProvider
{
    private readonly IGitHubAppClient _client;
    public GitHubRepositoryProvider(IGitHubAppClient client) => _client = client;

    public async Task<SourceRepositoryMetadata?> GetAsync(long installationId, long repositoryId,
        CancellationToken ct = default)
    {
        var item = await _client.GetRepositoryAsync(installationId, repositoryId, ct);
        return item is null ? null : new SourceRepositoryMetadata(item.Id, item.Owner, item.Name,
            item.FullName, item.DefaultBranch, item.IsPrivate);
    }
}
