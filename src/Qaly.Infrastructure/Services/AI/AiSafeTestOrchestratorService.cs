using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services.AI;

public sealed class AiSafeTestOrchestratorService : IAiSafeTestOrchestratorService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyList<SuiteManifest> Suites =
    [
        new("ai-native-unit", "AI-native contract and policy tests", "tests/Qaly.UnitTests/Qaly.UnitTests.csproj",
            "FullyQualifiedName~AiAssistant|FullyQualifiedName~AiNative|FullyQualifiedName~AiProjectLaunch", 150),
        new("assistant-integration", "Durable Assistant API and real EF read-back", "tests/Qaly.IntegrationTests/Qaly.IntegrationTests.csproj",
            "FullyQualifiedName~AiAssistantTurnApiTests", 180),
        new("ai-web-contract", "AI web feature contracts", "tests/Qaly.WebFeatureTests/Qaly.WebFeatureTests.csproj",
            "FullyQualifiedName~Ai", 120)
    ];

    private readonly QalyDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IHostEnvironment _environment;
    private readonly AiJobPlatformOptions _options;

    public AiSafeTestOrchestratorService(
        QalyDbContext db,
        ICurrentUserService currentUser,
        IHostEnvironment environment,
        IOptions<AiJobPlatformOptions> options)
    {
        _db = db;
        _currentUser = currentUser;
        _environment = environment;
        _options = options.Value;
    }

    public async Task<Result<AiSafeTestRunPreviewDto>> PrepareAsync(
        Guid sessionId,
        Guid originTurnId,
        CancellationToken ct = default)
    {
        var gate = EnsureAllowed<AiSafeTestRunPreviewDto>();
        if (gate != null) return gate;
        if (_currentUser.UserId is not Guid userId) return Result.Forbidden<AiSafeTestRunPreviewDto>();

        var session = await _db.AssistantSessions.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == sessionId && item.OwnerUserId == userId && item.Status == "active", ct);
        if (session == null) return Result.NotFound<AiSafeTestRunPreviewDto>();
        var ownsTurn = await _db.AssistantTurns.AsNoTracking().AnyAsync(
            item => item.Id == originTurnId && item.SessionId == sessionId, ct);
        if (!ownsTurn) return Result.NotFound<AiSafeTestRunPreviewDto>();

        var existing = await _db.AssistantTestRuns.SingleOrDefaultAsync(item => item.OriginTurnId == originTurnId, ct);
        if (existing != null) return Result.Success(MapPreview(existing));

        var run = new AssistantTestRun
        {
            OwnerUserId = userId,
            TenantId = session.TenantId,
            SessionId = sessionId,
            OriginTurnId = originTurnId,
            ManifestId = AiSafeTestOrchestratorContract.DefaultManifestId,
            Status = "review_required",
            Revision = 1
        };
        _db.AssistantTestRuns.Add(run);
        await _db.SaveChangesAsync(ct);
        return Result.Created(MapPreview(run));
    }

    public async Task<Result<AiSafeTestRunReportDto>> ConfirmAsync(
        Guid runId,
        long expectedRevision,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var gate = EnsureAllowed<AiSafeTestRunReportDto>();
        if (gate != null) return gate;
        if (_currentUser.UserId is not Guid userId) return Result.Forbidden<AiSafeTestRunReportDto>();
        idempotencyKey = idempotencyKey?.Trim() ?? string.Empty;
        if (idempotencyKey.Length is < 8 or > 180)
            return Result.Failure<AiSafeTestRunReportDto>("Idempotency-Key is required.", 400, "invalid_request");

        var run = await _db.AssistantTestRuns.SingleOrDefaultAsync(
            item => item.Id == runId && item.OwnerUserId == userId, ct);
        if (run == null) return Result.NotFound<AiSafeTestRunReportDto>();
        if (run.Status is "passed" or "failed" or "canceled")
            return string.Equals(run.IdempotencyKey, idempotencyKey, StringComparison.Ordinal)
                ? Result.Success(MapReport(run))
                : Result.Failure<AiSafeTestRunReportDto>("This test run already used another confirmation key.", 409, "safe_test_idempotency_conflict");
        if (run.Status != "review_required")
            return Result.Failure<AiSafeTestRunReportDto>("The test run is already running.", 409, "safe_test_run_in_progress");
        if (run.Revision != expectedRevision)
            return Result.Failure<AiSafeTestRunReportDto>("The test preview changed. Reload it before confirmation.", 409, "safe_test_run_stale");

        var now = DateTimeOffset.UtcNow;
        run.IdempotencyKey = idempotencyKey;
        run.ConfirmedAt = now;
        run.StartedAt = now;
        run.Status = "running";
        run.Revision++;
        await _db.SaveChangesAsync(ct);

        var events = new List<AiSafeTestRunEventDto>();
        try
        {
            var repositoryRoot = ResolveRepositoryRoot();
            foreach (var suite in Suites)
            {
                var startedAt = DateTimeOffset.UtcNow;
                var runningEvent = new AiSafeTestRunEventDto(
                    events.Count + 1, suite.Id, "running", $"Đang chạy {suite.Label}", startedAt);
                events.Add(runningEvent);
                run.EventsJson = JsonSerializer.Serialize(events, JsonOptions);
                run.Revision++;
                await _db.SaveChangesAsync(ct);

                var exit = await RunSuiteAsync(repositoryRoot, suite, ct);
                var completedAt = DateTimeOffset.UtcNow;
                events[^1] = runningEvent with
                {
                    Status = exit == 0 ? "passed" : "failed",
                    PublicLabel = exit == 0 ? $"Đã đạt {suite.Label}" : $"Không đạt {suite.Label}",
                    CompletedAt = completedAt,
                    ExitCode = exit,
                    SafeErrorCode = exit == 0 ? null : "safe_test_suite_failed"
                };
                run.EventsJson = JsonSerializer.Serialize(events, JsonOptions);
                run.Revision++;
                await _db.SaveChangesAsync(ct);
                if (exit != 0) break;
            }

            var failed = events.Count(item => item.Status == "failed");
            run.Status = failed == 0 && events.Count == Suites.Count ? "passed" : "failed";
            run.SafeErrorCode = failed == 0 ? null : "safe_test_manifest_failed";
            run.SafeSummary = failed == 0
                ? $"{events.Count}/{Suites.Count} suite trong manifest cố định đã PASS."
                : $"{events.Count(item => item.Status == "passed")}/{Suites.Count} suite PASS; dừng tại suite không đạt.";
        }
        catch (OperationCanceledException)
        {
            run.Status = "canceled";
            run.SafeErrorCode = "safe_test_canceled_or_timed_out";
            run.SafeSummary = "Test run đã dừng do hủy hoặc vượt thời gian cho phép.";
        }
        catch
        {
            run.Status = "failed";
            run.SafeErrorCode = "safe_test_runner_failed";
            run.SafeSummary = "Test runner không thể hoàn tất manifest cố định; không có command tùy ý nào được chạy.";
        }

        run.CompletedAt = DateTimeOffset.UtcNow;
        run.Revision++;
        await _db.SaveChangesAsync(CancellationToken.None);
        return Result.Success(MapReport(run));
    }

    public async Task<Result<AiSafeTestRunReportDto>> GetAsync(Guid runId, CancellationToken ct = default)
    {
        var gate = EnsureAllowed<AiSafeTestRunReportDto>();
        if (gate != null) return gate;
        if (_currentUser.UserId is not Guid userId) return Result.Forbidden<AiSafeTestRunReportDto>();
        var run = await _db.AssistantTestRuns.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == runId && item.OwnerUserId == userId, ct);
        return run == null ? Result.NotFound<AiSafeTestRunReportDto>() : Result.Success(MapReport(run));
    }

    private Result<T>? EnsureAllowed<T>()
    {
        if (!_options.SafeTestOrchestratorEnabled)
            return Result.Failure<T>("Safe test orchestration is disabled.", 403, "policy_blocked");
        if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Test"))
            return Result.Failure<T>("Test execution is forbidden outside Development/Test.", 403, "policy_blocked");
        return null;
    }

    private string ResolveRepositoryRoot()
    {
        var root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", ".."));
        if (!File.Exists(Path.Combine(root, "Qaly_project.slnx")))
            throw new InvalidOperationException("The fixed Qaly test workspace was not found.");
        return root;
    }

    private async Task<int> RunSuiteAsync(string repositoryRoot, SuiteManifest suite, CancellationToken ct)
    {
        var projectPath = Path.GetFullPath(Path.Combine(repositoryRoot, suite.Project.Replace('/', Path.DirectorySeparatorChar)));
        if (!projectPath.StartsWith(repositoryRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(projectPath))
            throw new InvalidOperationException("A fixed manifest project is invalid.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.SafeTestTimeoutSeconds, 30, 1800)));
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repositoryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("test");
        process.StartInfo.ArgumentList.Add(projectPath);
        process.StartInfo.ArgumentList.Add("--no-restore");
        process.StartInfo.ArgumentList.Add("--filter");
        process.StartInfo.ArgumentList.Add(suite.Filter);
        process.StartInfo.ArgumentList.Add("--verbosity");
        process.StartInfo.ArgumentList.Add("minimal");
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(stdout, stderr);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
    }

    private static AiSafeTestRunPreviewDto MapPreview(AssistantTestRun run)
        => new(
            AiSafeTestOrchestratorContract.PreviewSchemaId,
            run.Id,
            run.ManifestId,
            "AI-native acceptance manifest",
            run.Status,
            run.Status == "review_required",
            Suites.Sum(item => item.EstimatedSeconds),
            0m,
            Suites.Select(item => new AiSafeTestSuiteDto(
                item.Id, item.Label, item.Project, "fixed_server_filter", item.EstimatedSeconds)).ToArray(),
            run.Revision);

    private static AiSafeTestRunReportDto MapReport(AssistantTestRun run)
    {
        IReadOnlyList<AiSafeTestRunEventDto> events;
        try
        {
            events = JsonSerializer.Deserialize<AiSafeTestRunEventDto[]>(run.EventsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            events = [];
        }
        return new AiSafeTestRunReportDto(
            AiSafeTestOrchestratorContract.ReportSchemaId,
            run.Id,
            run.ManifestId,
            run.Status,
            events,
            events.Count(item => item.Status == "passed"),
            events.Count(item => item.Status == "failed"),
            run.StartedAt,
            run.CompletedAt,
            run.SafeSummary,
            run.SafeErrorCode,
            run.Revision);
    }

    private sealed record SuiteManifest(
        string Id,
        string Label,
        string Project,
        string Filter,
        int EstimatedSeconds);
}
