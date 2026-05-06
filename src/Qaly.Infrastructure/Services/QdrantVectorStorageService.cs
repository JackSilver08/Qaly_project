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
            point.Payload.Add(kvp.Key, ToValue(kvp.Value));
        }

        await _client.UpsertAsync(collectionName, new[] { point });
    }

    public async Task<List<VectorSearchResult>> SearchAsync(float[] queryVector, string collectionName, VectorFilter? filter = null, int limit = 5)
    {
        Filter? qdrantFilter = BuildFilter(filter);

        var results = await _client.SearchAsync(
            collectionName: collectionName,
            vector: queryVector,
            filter: qdrantFilter,
            limit: (ulong)limit);

        return results.Select(r => new VectorSearchResult(
            Guid.Parse(r.Id.Uuid),
            r.Score,
            r.Payload.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value.ToString())
        )).ToList();
    }

    public async Task DeleteAsync(Guid id, string collectionName)
    {
        await _client.DeleteAsync(collectionName, id);
    }

    public async Task DeleteByFilterAsync(VectorFilter filter, string collectionName)
    {
        var qdrantFilter = BuildFilter(filter);
        if (qdrantFilter == null) return;

        await _client.DeleteAsync(collectionName, qdrantFilter);
    }

    private static Filter? BuildFilter(VectorFilter? filter)
    {
        if (filter == null) return null;

        var qdrantFilter = new Filter();

        if (filter.ProjectId.HasValue)
        {
            qdrantFilter.Must.Add(new Condition 
            { 
                Field = new FieldCondition 
                { 
                    Key = "ProjectId", 
                    Match = new Match { Keyword = filter.ProjectId.Value.ToString() } 
                } 
            });
        }

        if (filter.ContentType != null)
        {
            qdrantFilter.Must.Add(new Condition 
            { 
                Field = new FieldCondition 
                { 
                    Key = "ContentType", 
                    Match = new Match { Keyword = filter.ContentType } 
                } 
            });
        }

        // Security Filter: Visibility & Privacy
        // (Visibility == 'public') OR (Visibility == 'member' AND UserInProject) OR (Visibility == 'private' AND OwnerId == currentUserId)
        // Simplified for RAG: Usually we filter by ProjectId (must) and then filter out Private tasks unless user has access.
        
        if (filter.IsPrivate.HasValue)
        {
            if (filter.IsPrivate == true && filter.OwnerId.HasValue)
            {
                // If we specifically want private items for a user
                qdrantFilter.Must.Add(new Condition { Field = new FieldCondition { Key = "IsPrivate", Match = new Match { Boolean = true } } });
                qdrantFilter.Must.Add(new Condition { Field = new FieldCondition { Key = "OwnerId", Match = new Match { Keyword = filter.OwnerId.Value.ToString() } } });
            }
            else if (filter.IsPrivate == false)
            {
                qdrantFilter.Must.Add(new Condition { Field = new FieldCondition { Key = "IsPrivate", Match = new Match { Boolean = false } } });
            }
        }

        return qdrantFilter.Must.Count > 0 ? qdrantFilter : null;
    }

    private static Value ToValue(object? value)
    {
        return value switch
        {
            null => new Value { NullValue = NullValue.NullValue },
            string s => new Value { StringValue = s },
            bool b => new Value { BoolValue = b },
            int i => new Value { IntegerValue = i },
            long l => new Value { IntegerValue = l },
            float f => new Value { DoubleValue = f },
            double d => new Value { DoubleValue = d },
            Guid g => new Value { StringValue = g.ToString() },
            _ => new Value { StringValue = value.ToString() ?? string.Empty }
        };
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
