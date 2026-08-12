using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;

namespace Qaly.Infrastructure.Integrations.GitHub;

public sealed record GitHubInstallationInfo(long InstallationId, long AccountId, string AccountLogin, string AccountType);
public sealed record GitHubRepositoryInfo(long Id, string Owner, string Name, string FullName, string DefaultBranch, bool IsPrivate);
public sealed record GitHubPullRequestInfo(int Number, string Title, string State, bool IsDraft, string? AuthorLogin,
    string HeadBranch, string BaseBranch, DateTimeOffset OpenedAt, DateTimeOffset? UpdatedAt,
    DateTimeOffset? MergedAt, string? MergedByLogin, string Url);
public sealed record GitHubWorkflowRunInfo(long Id, string Name, string? Title, string Branch, string Sha,
    string Status, string? Conclusion, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, string Url);
public sealed record GitHubReleaseInfo(long Id, string TagName, string? Name, bool IsDraft, bool IsPrerelease,
    DateTimeOffset? PublishedAt, string Url);

public interface IGitHubAppClient
{
    string GetInstallationUrl(string state);
    Task<GitHubInstallationInfo> GetInstallationAsync(long installationId, CancellationToken ct = default);
    Task<IReadOnlyList<GitHubRepositoryInfo>> GetRepositoriesAsync(long installationId, CancellationToken ct = default);
    Task<GitHubRepositoryInfo?> GetRepositoryAsync(long installationId, long repositoryId, CancellationToken ct = default);
    Task<IReadOnlyList<GitHubPullRequestInfo>> GetPullRequestsAsync(long installationId, string owner, string repository, CancellationToken ct = default);
    Task<IReadOnlyList<GitHubWorkflowRunInfo>> GetWorkflowRunsAsync(long installationId, string owner, string repository, CancellationToken ct = default);
    Task<IReadOnlyList<GitHubReleaseInfo>> GetReleasesAsync(long installationId, string owner, string repository, CancellationToken ct = default);
}

public sealed class GitHubAppClient : IGitHubAppClient
{
    private readonly HttpClient _http;
    private readonly IOptions<GitHubIntegrationOptions> _options;
    private readonly IHostEnvironment _environment;

    public GitHubAppClient(HttpClient http, IOptions<GitHubIntegrationOptions> options, IHostEnvironment environment)
    {
        _http = http;
        _options = options;
        _environment = environment;
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

    public async Task<IReadOnlyList<GitHubPullRequestInfo>> GetPullRequestsAsync(long installationId, string owner, string repository, CancellationToken ct = default)
    {
        using var json = await GetAsInstallationAsync(installationId,
            $"/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}/pulls?state=all&sort=updated&direction=desc&per_page=50", ct);
        return json.RootElement.EnumerateArray().Select(item => new GitHubPullRequestInfo(
            item.GetProperty("number").GetInt32(), item.GetProperty("title").GetString() ?? string.Empty,
            item.GetProperty("state").GetString() ?? "open", item.GetProperty("draft").GetBoolean(),
            item.GetProperty("user").GetProperty("login").GetString(),
            item.GetProperty("head").GetProperty("ref").GetString() ?? string.Empty,
            item.GetProperty("base").GetProperty("ref").GetString() ?? string.Empty,
            item.GetProperty("created_at").GetDateTimeOffset(), ReadNullableDate(item, "updated_at"),
            ReadNullableDate(item, "merged_at"), ReadNestedString(item, "merged_by", "login"),
            item.GetProperty("html_url").GetString() ?? string.Empty)).ToList();
    }

    public async Task<IReadOnlyList<GitHubWorkflowRunInfo>> GetWorkflowRunsAsync(long installationId, string owner, string repository, CancellationToken ct = default)
    {
        using var json = await GetAsInstallationAsync(installationId,
            $"/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}/actions/runs?per_page=50", ct);
        return json.RootElement.GetProperty("workflow_runs").EnumerateArray().Select(item => new GitHubWorkflowRunInfo(
            item.GetProperty("id").GetInt64(), item.GetProperty("name").GetString() ?? "Workflow",
            item.TryGetProperty("display_title", out var title) ? title.GetString() : null,
            item.GetProperty("head_branch").GetString() ?? string.Empty,
            item.GetProperty("head_sha").GetString() ?? string.Empty,
            item.GetProperty("status").GetString() ?? string.Empty,
            item.GetProperty("conclusion").ValueKind == JsonValueKind.Null ? null : item.GetProperty("conclusion").GetString(),
            item.GetProperty("created_at").GetDateTimeOffset(), ReadNullableDate(item, "updated_at"),
            item.GetProperty("html_url").GetString() ?? string.Empty)).ToList();
    }

    public async Task<IReadOnlyList<GitHubReleaseInfo>> GetReleasesAsync(long installationId, string owner, string repository, CancellationToken ct = default)
    {
        using var json = await GetAsInstallationAsync(installationId,
            $"/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}/releases?per_page=30", ct);
        return json.RootElement.EnumerateArray().Select(item => new GitHubReleaseInfo(
            item.GetProperty("id").GetInt64(), item.GetProperty("tag_name").GetString() ?? string.Empty,
            item.GetProperty("name").ValueKind == JsonValueKind.Null ? null : item.GetProperty("name").GetString(),
            item.GetProperty("draft").GetBoolean(), item.GetProperty("prerelease").GetBoolean(),
            ReadNullableDate(item, "published_at"), item.GetProperty("html_url").GetString() ?? string.Empty)).ToList();
    }

    private async Task<JsonDocument> GetAsInstallationAsync(long installationId, string path, CancellationToken ct)
    {
        var token = await CreateInstallationTokenAsync(installationId, ct);
        using var request = Request(HttpMethod.Get, path, token);
        using var response = await _http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    private static DateTimeOffset? ReadNullableDate(JsonElement item, string property)
        => item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetDateTimeOffset() : null;

    private static string? ReadNestedString(JsonElement item, string parent, string property)
        => item.TryGetProperty(parent, out var value) && value.ValueKind == JsonValueKind.Object
            ? value.GetProperty(property).GetString() : null;

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

    private string ResolvePrivateKey(GitHubIntegrationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.PrivateKey))
            return options.PrivateKey;

        if (string.IsNullOrWhiteSpace(options.PrivateKeyPath))
            return string.Empty;

        var path = Path.IsPathRooted(options.PrivateKeyPath)
            ? options.PrivateKeyPath
            : Path.GetFullPath(options.PrivateKeyPath, _environment.ContentRootPath);

        if (!File.Exists(path))
            throw new InvalidOperationException($"GitHub App private key file was not found at the configured path: {path}");

        return File.ReadAllText(path);
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
