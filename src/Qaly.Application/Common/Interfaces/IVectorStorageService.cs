namespace Qaly.Application.Common.Interfaces;

public interface IVectorStorageService
{
    /// <summary>
    /// Upsert a point into the vector database.
    /// </summary>
    Task UpsertAsync(Guid id, float[] vector, Dictionary<string, object> payload, string collectionName);

    /// <summary>
    /// Search for the most relevant points in the vector database.
    /// </summary>
    Task<List<VectorSearchResult>> SearchAsync(float[] queryVector, string collectionName, int limit = 5);

    /// <summary>
    /// Ensure a collection exists in the vector database.
    /// </summary>
    Task EnsureCollectionExistsAsync(string collectionName, ulong vectorSize);
}

public record VectorSearchResult(Guid Id, float Score, Dictionary<string, object> Payload);
