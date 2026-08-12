namespace Qaly.Application.Services.GitHub;

public sealed record SourceRepositoryMetadata(long Id, string Owner, string Name, string FullName,
    string DefaultBranch, bool IsPrivate);

public interface IGitHubRepositoryProvider
{
    Task<SourceRepositoryMetadata?> GetAsync(long installationId, long repositoryId, CancellationToken ct = default);
}
