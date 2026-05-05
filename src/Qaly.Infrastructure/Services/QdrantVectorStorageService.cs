using Qaly.Application.Common.Interfaces;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Microsoft.Extensions.Configuration;

namespace Qaly.Infrastructure.Services;

public class QdrantVectorStorageService : IVectorStorageService, IDisposable
{
    private readonly QdrantClient _client;

    public QdrantVectorStorageService(IConfiguration configuration)
    {
        var url = configuration["Ai:QdrantUrl"] ?? "http://localhost:6333";
        _client = new QdrantClient(new Uri(url));
    }

    public async Task UpsertAsync(Guid id, float[] vector, Dictionary<string, object> payload, string collectionName)
    {
        var point = new PointStruct
        {
            Id = id,
            Vectors = vector
        };

        foreach (var kvp in payload)
        {
            point.Payload.Add(kvp.Key, kvp.Value?.ToString() ?? string.Empty);
        }

        await _client.UpsertAsync(collectionName, new[] { point });
    }

    public async Task<List<VectorSearchResult>> SearchAsync(float[] queryVector, string collectionName, int limit = 5)
    {
        var results = await _client.SearchAsync(collectionName, queryVector, limit: (ulong)limit);

        return results.Select(r => new VectorSearchResult(
            Guid.Parse(r.Id.Uuid),
            r.Score,
            r.Payload.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value.ToString())
        )).ToList();
    }

    public async Task EnsureCollectionExistsAsync(string collectionName, ulong vectorSize)
    {
        var collections = await _client.ListCollectionsAsync();
        if (!collections.Contains(collectionName))
        {
            await _client.CreateCollectionAsync(collectionName, new VectorParams
            {
                Size = vectorSize,
                Distance = Distance.Cosine
            });
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
