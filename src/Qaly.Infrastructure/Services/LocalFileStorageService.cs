using Microsoft.Extensions.Configuration;
using Qaly.Application.Common.Interfaces;

namespace Qaly.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(IConfiguration configuration)
    {
        _rootPath = configuration.GetValue<string>("FileStorage:RootPath")
            ?? Path.Combine(AppContext.BaseDirectory, "uploads");
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_rootPath);

        var safeFileName = Path.GetFileName(fileName);
        var storedFileName = $"{Guid.NewGuid():N}_{safeFileName}";
        var fullPath = Path.Combine(_rootPath, storedFileName);

        await using var output = File.Create(fullPath);
        await fileStream.CopyToAsync(output, cancellationToken);

        return fullPath;
    }

    public Task<Stream> DownloadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        Stream stream = File.OpenRead(filePath);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }
}
