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

    public async Task UpsertAsync(Guid id, float[] vector, Dictionary<string, object> payload, string collectionName, CancellationToken ct = default)
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

        await _client.UpsertAsync(collectionName, new[] { point }, cancellationToken: ct);
    }

    public async Task<List<VectorSearchResult>> SearchAsync(float[] queryVector, string collectionName, VectorFilter filter, int limit = 5, CancellationToken ct = default)
    {
        var qdrantFilter = BuildSearchFilter(filter);

        var results = await _client.SearchAsync(
            collectionName: collectionName,
            vector: queryVector,
            filter: qdrantFilter,
            limit: (ulong)limit,
            cancellationToken: ct);

        return results.Select(r => new VectorSearchResult(
            Guid.Parse(r.Id.Uuid),
            r.Score,
            r.Payload.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value.ToString())
        )).ToList();
    }

    public async Task DeleteAsync(Guid id, string collectionName, CancellationToken ct = default)
    {
        await _client.DeleteAsync(collectionName, id, cancellationToken: ct);
    }

    public async Task DeleteByFilterAsync(VectorFilter filter, string collectionName, CancellationToken ct = default)
    {
        var qdrantFilter = BuildFilter(filter);
        if (qdrantFilter == null) return;

        await _client.DeleteAsync(collectionName, qdrantFilter, cancellationToken: ct);
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
                    Key = "project_id", 
                    Match = new Match { Keyword = filter.ProjectId.Value.ToString() } 
                } 
            });
        }

        if (filter.TaskId.HasValue)
        {
            qdrantFilter.Must.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = "task_id",
                    Match = new Match { Keyword = filter.TaskId.Value.ToString() }
                }
            });
        }

        if (filter.ContentType != null)
        {
            qdrantFilter.Must.Add(new Condition 
            { 
                Field = new FieldCondition 
                { 
                    Key = "content_type", 
                    Match = new Match { Keyword = filter.ContentType.ToLowerInvariant() } 
                } 
            });
        }

        if (filter.IsPrivate.HasValue)
        {
            if (filter.IsPrivate == true && filter.OwnerId.HasValue)
            {
                qdrantFilter.Must.Add(new Condition { Field = new FieldCondition { Key = "is_private", Match = new Match { Boolean = true } } });
                qdrantFilter.Must.Add(new Condition { Field = new FieldCondition { Key = "owner_id", Match = new Match { Keyword = filter.OwnerId.Value.ToString() } } });
            }
            else if (filter.IsPrivate == false)
            {
                qdrantFilter.Must.Add(new Condition { Field = new FieldCondition { Key = "is_private", Match = new Match { Boolean = false } } });
            }
        }

        return qdrantFilter.Must.Count > 0 ? qdrantFilter : null;
    }

    private static Filter BuildSearchFilter(VectorFilter filter)
    {
        if (!filter.ProjectId.HasValue)
        {
            throw new ArgumentException("Vector search requires a project filter.", nameof(filter));
        }

        if (!filter.OwnerId.HasValue)
        {
            throw new ArgumentException("Vector search requires the current user filter.", nameof(filter));
        }

        var qdrantFilter = new Filter();

        // Mandatory Project Filter
        qdrantFilter.Must.Add(new Condition
        {
            Field = new FieldCondition
            {
                Key = "project_id",
                Match = new Match { Keyword = filter.ProjectId.Value.ToString() }
            }
        });

        if (filter.TaskId.HasValue)
        {
            qdrantFilter.Must.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = "task_id",
                    Match = new Match { Keyword = filter.TaskId.Value.ToString() }
                }
            });
        }

        if (!string.IsNullOrWhiteSpace(filter.ContentType))
        {
            qdrantFilter.Must.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = "content_type",
                    Match = new Match { Keyword = filter.ContentType.ToLowerInvariant() }
                }
            });
        }

        // Security: (is_private == false) OR (owner_id == currentUser)
        qdrantFilter.Must.Add(new Condition
        {
            Filter = new Filter
            {
                Should = 
                {
                    new Condition { Field = new FieldCondition { Key = "is_private", Match = new Match { Boolean = false } } },
                    new Condition { Field = new FieldCondition { Key = "owner_id", Match = new Match { Keyword = filter.OwnerId.Value.ToString() } } }
                }
            }
        });

        return qdrantFilter;
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

    public async Task EnsureCollectionExistsAsync(string collectionName, ulong vectorSize, CancellationToken ct = default)
    {
        var collections = await _client.ListCollectionsAsync(cancellationToken: ct);
        if (!collections.Contains(collectionName))
        {
            await _client.CreateCollectionAsync(collectionName, new VectorParams
            {
                Size = vectorSize,
                Distance = Distance.Cosine
            }, cancellationToken: ct);
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
