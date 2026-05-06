namespace Qaly.Application.Common.Interfaces;

public interface IVectorStorageService
{
    /// <summary>
    /// Upsert a point into the vector database.
    /// </summary>
    Task UpsertAsync(Guid id, float[] vector, Dictionary<string, object> payload, string collectionName);

    /// <summary>
    /// Search for the most relevant points in the vector database with mandatory filtering.
    /// </summary>
    Task<List<VectorSearchResult>> SearchAsync(float[] queryVector, string collectionName, VectorFilter? filter = null, int limit = 5);

    /// <summary>
    /// Delete a point by ID.
    /// </summary>
    Task DeleteAsync(Guid id, string collectionName);

    /// <summary>
    /// Delete points by filter.
    /// </summary>
    Task DeleteByFilterAsync(VectorFilter filter, string collectionName);

    /// <summary>
    /// Ensure a collection exists in the vector database.
    /// </summary>
    Task EnsureCollectionExistsAsync(string collectionName, ulong vectorSize);
}

public record VectorSearchResult(Guid Id, float Score, Dictionary<string, object> Payload);

public record VectorFilter
{
    public Guid? ProjectId { get; init; }
    public Guid? TaskId { get; init; }
    public Guid? OwnerId { get; init; }
    public bool? IsPrivate { get; init; }
    public string? Visibility { get; init; } // private, member, public
    public string? ContentType { get; init; }
    public List<Guid>? AllowedUserIds { get; init; } // For private visibility check
}
