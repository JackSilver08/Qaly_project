using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Qaly.Infrastructure.Integrations.GitHub;

public sealed record GitHubInstallationInfo(long InstallationId, long AccountId, string AccountLogin, string AccountType);
public sealed record GitHubRepositoryInfo(long Id, string Owner, string Name, string FullName, string DefaultBranch, bool IsPrivate);

public interface IGitHubAppClient
{
    string GetInstallationUrl(string state);
    Task<GitHubInstallationInfo> GetInstallationAsync(long installationId, CancellationToken ct = default);
    Task<IReadOnlyList<GitHubRepositoryInfo>> GetRepositoriesAsync(long installationId, CancellationToken ct = default);
    Task<GitHubRepositoryInfo?> GetRepositoryAsync(long installationId, long repositoryId, CancellationToken ct = default);
}

public sealed class GitHubAppClient : IGitHubAppClient
{
    private readonly HttpClient _http;
    private readonly IOptions<GitHubIntegrationOptions> _options;

    public GitHubAppClient(HttpClient http, IOptions<GitHubIntegrationOptions> options)
    {
        _http = http;
        _options = options;
    }

    public string GetInstallationUrl(string state)
    {
        var slug = _options.Value.AppSlug;
        if (string.IsNullOrWhiteSpace(slug)) throw new InvalidOperationException("GitHub App chưa được cấu hình App Slug.");
        return $"https://github.com/apps/{Uri.EscapeDataString(slug)}/installations/new?state={Uri.EscapeDataString(state)}";
    }

    public async Task<GitHubInstallationInfo> GetInstallationAsync(long installationId, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Get, $"/app/installations/{installationId}", CreateAppJwt());
        using var response = await _http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var account = json.RootElement.GetProperty("account");
        return new(installationId, account.GetProperty("id").GetInt64(),
            account.GetProperty("login").GetString() ?? string.Empty,
            account.GetProperty("type").GetString() ?? "Organization");
    }

    public async Task<IReadOnlyList<GitHubRepositoryInfo>> GetRepositoriesAsync(long installationId, CancellationToken ct = default)
    {
        var token = await CreateInstallationTokenAsync(installationId, ct);
        var results = new List<GitHubRepositoryInfo>();
        for (var page = 1; ; page++)
        {
            using var request = Request(HttpMethod.Get, $"/installation/repositories?per_page=100&page={page}", token);
            using var response = await _http.SendAsync(request, ct);
            await EnsureSuccessAsync(response, ct);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var repositories = json.RootElement.GetProperty("repositories");
            foreach (var repository in repositories.EnumerateArray())
            {
                var owner = repository.GetProperty("owner").GetProperty("login").GetString() ?? string.Empty;
                results.Add(new GitHubRepositoryInfo(repository.GetProperty("id").GetInt64(), owner,
                    repository.GetProperty("name").GetString() ?? string.Empty,
                    repository.GetProperty("full_name").GetString() ?? string.Empty,
                    repository.GetProperty("default_branch").GetString() ?? "main",
                    repository.GetProperty("private").GetBoolean()));
            }
            if (repositories.GetArrayLength() < 100) break;
        }
        return results;
    }

    public async Task<GitHubRepositoryInfo?> GetRepositoryAsync(long installationId, long repositoryId, CancellationToken ct = default)
    {
        var token = await CreateInstallationTokenAsync(installationId, ct);
        using var request = Request(HttpMethod.Get, $"/repositories/{repositoryId}", token);
        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var repository = json.RootElement;
        return new GitHubRepositoryInfo(repository.GetProperty("id").GetInt64(),
            repository.GetProperty("owner").GetProperty("login").GetString() ?? string.Empty,
            repository.GetProperty("name").GetString() ?? string.Empty,
            repository.GetProperty("full_name").GetString() ?? string.Empty,
            repository.GetProperty("default_branch").GetString() ?? "main",
            repository.GetProperty("private").GetBoolean());
    }

    private async Task<string> CreateInstallationTokenAsync(long installationId, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Post, $"/app/installations/{installationId}/access_tokens", CreateAppJwt());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return json.RootElement.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("GitHub did not return an installation token.");
    }

    private string CreateAppJwt()
    {
        var options = _options.Value;
        var privateKey = ResolvePrivateKey(options);
        if (options.AppId <= 0 || string.IsNullOrWhiteSpace(privateKey))
            throw new InvalidOperationException("GitHub App ID/private key is not configured.");
        var now = DateTimeOffset.UtcNow;
        var header = Base64Url("""{"alg":"RS256","typ":"JWT"}""");
        var payload = Base64Url(JsonSerializer.Serialize(new
        {
            iat = now.AddSeconds(-30).ToUnixTimeSeconds(),
            exp = now.AddMinutes(9).ToUnixTimeSeconds(),
            iss = options.AppId.ToString(System.Globalization.CultureInfo.InvariantCulture)
        }));
        var unsigned = $"{header}.{payload}";
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKey.Replace("\\n", "\n", StringComparison.Ordinal));
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{unsigned}.{Base64Url(signature)}";
    }

    private static string ResolvePrivateKey(GitHubIntegrationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.PrivateKey))
            return options.PrivateKey;

        if (string.IsNullOrWhiteSpace(options.PrivateKeyPath))
            return string.Empty;

        if (!File.Exists(options.PrivateKeyPath))
            throw new InvalidOperationException("GitHub App private key file was not found.");

        return File.ReadAllText(options.PrivateKeyPath);
    }

    private HttpRequestMessage Request(HttpMethod method, string path, string bearer)
    {
        var baseUrl = _options.Value.ApiBaseUrl.TrimEnd('/');
        var request = new HttpRequestMessage(method, $"{baseUrl}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd("Qaly-GitHub-App/1.0");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException($"GitHub API returned {(int)response.StatusCode}: {body}", null, response.StatusCode);
    }

    private static string Base64Url(string value) => Base64Url(Encoding.UTF8.GetBytes(value));
    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
