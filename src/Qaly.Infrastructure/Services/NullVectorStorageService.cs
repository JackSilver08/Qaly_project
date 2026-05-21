using Qaly.Application.Common.Interfaces;

namespace Qaly.Infrastructure.Services;

public sealed class NullVectorStorageService : IVectorStorageService
{
    public Task UpsertAsync(Guid id, float[] vector, Dictionary<string, object> payload, string collectionName)
        => Task.CompletedTask;

    public Task<List<VectorSearchResult>> SearchAsync(float[] queryVector, string collectionName, VectorFilter filter, int limit = 5)
        => Task.FromResult(new List<VectorSearchResult>());

    public Task DeleteAsync(Guid id, string collectionName)
        => Task.CompletedTask;

    public Task DeleteByFilterAsync(VectorFilter filter, string collectionName)
        => Task.CompletedTask;

    public Task EnsureCollectionExistsAsync(string collectionName, ulong vectorSize)
        => Task.CompletedTask;
}
