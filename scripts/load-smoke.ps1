#requires -Version 7.0

[CmdletBinding()]
param(
    [string]$BaseUrl = "http://127.0.0.1:5000",
    [string]$Path = "/health/ready",
    [ValidateRange(1, 100000)]
    [int]$Requests = 200,
    [ValidateRange(1, 500)]
    [int]$Concurrency = 10,
    [ValidateRange(0, 10000)]
    [int]$WarmupRequests = 10,
    [ValidateRange(1, 300)]
    [int]$RequestTimeoutSeconds = 15,
    [ValidateRange(1, 86400)]
    [int]$RunTimeoutSeconds = 300,
    [ValidateRange(0, 100)]
    [double]$MaxFailureRatePercent = 0,
    [ValidateRange(1, 600000)]
    [double]$MaxP95Milliseconds = 1000,
    [ValidateRange(1, 600000)]
    [double]$MaxP99Milliseconds = 2500,
    [string]$Scenario = "readiness",
    [string]$EvidenceDirectory = (Join-Path $PSScriptRoot "../docs/task/qa-evidence/performance"),
    [switch]$RequireAuthentication,
    [switch]$AllowInsecureHttp,
    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-Percentile {
    param(
        [Parameter(Mandatory)]
        [double[]]$SortedValues,
        [Parameter(Mandatory)]
        [ValidateRange(0.01, 1)]
        [double]$Percentile
    )

    $index = [math]::Min(
        $SortedValues.Count - 1,
        [math]::Max(0, [math]::Ceiling($SortedValues.Count * $Percentile) - 1))
    return [math]::Round($SortedValues[$index], 2)
}

function Invoke-ProbeBatch {
    param(
        [Parameter(Mandatory)]
        [int]$Count,
        [Parameter(Mandatory)]
        [int]$Throttle,
        [Parameter(Mandatory)]
        [string]$Target,
        [Parameter(Mandatory)]
        [hashtable]$RequestHeaders,
        [Parameter(Mandatory)]
        [int]$PerRequestTimeoutSeconds,
        [Parameter(Mandatory)]
        [int]$WholeRunTimeoutSeconds
    )

    if ($Count -eq 0) {
        return @()
    }

    $targetForRunspaces = $Target
    $headersForRunspaces = $RequestHeaders
    $timeoutForRunspaces = $PerRequestTimeoutSeconds
    $inputItems = 1..$Count

    return @($inputItems | ForEach-Object -Parallel {
        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        $statusCode = 0
        $ok = $false
        $errorKind = $null
        try {
            $response = Invoke-WebRequest `
                -Uri $using:targetForRunspaces `
                -Method Get `
                -Headers $using:headersForRunspaces `
                -TimeoutSec $using:timeoutForRunspaces `
                -SkipHttpErrorCheck
            $statusCode = [int]$response.StatusCode
            $ok = $statusCode -ge 200 -and $statusCode -lt 300
            if (-not $ok) {
                $errorKind = "HttpStatus"
            }
        }
        catch [System.OperationCanceledException] {
            $errorKind = "TimeoutOrCanceled"
        }
        catch {
            $errorKind = $_.Exception.GetType().Name
        }
        finally {
            $watch.Stop()
        }

        [pscustomobject]@{
            Ok = $ok
            StatusCode = $statusCode
            ElapsedMs = [double]$watch.Elapsed.TotalMilliseconds
            ErrorKind = $errorKind
        }
    } -ThrottleLimit $Throttle -TimeoutSeconds $WholeRunTimeoutSeconds)
}

$baseUri = $null
if (-not [Uri]::TryCreate($BaseUrl, [UriKind]::Absolute, [ref]$baseUri)) {
    throw "BaseUrl must be an absolute HTTP or HTTPS URL."
}
if ($baseUri.Scheme -notin @("http", "https")) {
    throw "BaseUrl must use HTTP or HTTPS."
}
if (-not [string]::IsNullOrEmpty($baseUri.UserInfo)) {
    throw "BaseUrl cannot contain embedded credentials."
}
if ($baseUri.Scheme -eq "http" -and -not $baseUri.IsLoopback -and -not $AllowInsecureHttp) {
    throw "Remote plaintext HTTP is blocked. Use HTTPS or explicitly pass -AllowInsecureHttp for a controlled rehearsal."
}
if (-not $Path.StartsWith('/') -or $Path.Contains('?') -or $Path.Contains('#')) {
    throw "Path must be a relative route beginning with '/' and cannot contain a query string or fragment."
}

$safeScenario = ($Scenario.Trim() -replace '[^a-zA-Z0-9._-]', '-')
if ([string]::IsNullOrWhiteSpace($safeScenario)) {
    throw "Scenario must contain at least one letter or number."
}

$targetUri = [Uri]::new("$($BaseUrl.TrimEnd('/'))$Path")
$requestHeaders = @{
    "Accept" = "application/json"
    "X-Qaly-Load-Probe" = "true"
}
$authModes = [System.Collections.Generic.List[string]]::new()
if (-not [string]::IsNullOrWhiteSpace($env:QALY_LOAD_BEARER_TOKEN)) {
    $requestHeaders["Authorization"] = "Bearer $($env:QALY_LOAD_BEARER_TOKEN.Trim())"
    $authModes.Add("bearer")
}
if (-not [string]::IsNullOrWhiteSpace($env:QALY_LOAD_API_KEY)) {
    $requestHeaders["X-API-Key"] = $env:QALY_LOAD_API_KEY.Trim()
    $authModes.Add("api-key")
}
if (-not [string]::IsNullOrWhiteSpace($env:QALY_LOAD_COOKIE)) {
    $requestHeaders["Cookie"] = $env:QALY_LOAD_COOKIE.Trim()
    $authModes.Add("cookie")
}
if ($RequireAuthentication -and $authModes.Count -eq 0) {
    throw "Authenticated scenario requires QALY_LOAD_BEARER_TOKEN, QALY_LOAD_API_KEY, or QALY_LOAD_COOKIE."
}

$authSummary = if ($authModes.Count -eq 0) { "none" } else { $authModes -join "+" }
if ($ValidateOnly) {
    Write-Host "Load probe configuration valid: scenario=$safeScenario target=$($targetUri.AbsolutePath) auth=$authSummary requests=$Requests concurrency=$Concurrency."
    return
}

if ($WarmupRequests -gt 0) {
    $warmup = Invoke-ProbeBatch `
        -Count $WarmupRequests `
        -Throttle ([math]::Min($Concurrency, $WarmupRequests)) `
        -Target $targetUri.AbsoluteUri `
        -RequestHeaders $requestHeaders `
        -PerRequestTimeoutSeconds $RequestTimeoutSeconds `
        -WholeRunTimeoutSeconds $RunTimeoutSeconds
    $warmupFailures = @($warmup | Where-Object { -not $_.Ok })
    if ($warmupFailures.Count -gt 0) {
        $warmupStatus = ($warmupFailures | Group-Object StatusCode | ForEach-Object { "$($_.Name):$($_.Count)" }) -join ", "
        throw "Warmup failed ($($warmupFailures.Count)/$WarmupRequests; status $warmupStatus). Fix readiness/auth before collecting load evidence."
    }
}

$startedAt = [DateTimeOffset]::UtcNow
$runWatch = [System.Diagnostics.Stopwatch]::StartNew()
$results = Invoke-ProbeBatch `
    -Count $Requests `
    -Throttle ([math]::Min($Concurrency, $Requests)) `
    -Target $targetUri.AbsoluteUri `
    -RequestHeaders $requestHeaders `
    -PerRequestTimeoutSeconds $RequestTimeoutSeconds `
    -WholeRunTimeoutSeconds $RunTimeoutSeconds
$runWatch.Stop()
$completedAt = [DateTimeOffset]::UtcNow

if ($results.Count -ne $Requests) {
    throw "Probe returned $($results.Count) results for $Requests requests; the run is incomplete and cannot be used as evidence."
}

$failed = @($results | Where-Object { -not $_.Ok })
$latencies = [double[]]@($results | ForEach-Object { [double]$_.ElapsedMs } | Sort-Object)
$failureRate = [math]::Round(($failed.Count * 100.0) / $Requests, 3)
$elapsedSeconds = [math]::Max(0.001, $runWatch.Elapsed.TotalSeconds)
$statusCodes = [ordered]@{}
$results | Group-Object StatusCode | Sort-Object Name | ForEach-Object {
    $statusCodes[[string]$_.Name] = $_.Count
}
$failureKinds = [ordered]@{}
$failed | Group-Object ErrorKind | Sort-Object Name | ForEach-Object {
    $failureKinds[[string]$_.Name] = $_.Count
}

$p50 = Get-Percentile -SortedValues $latencies -Percentile 0.50
$p95 = Get-Percentile -SortedValues $latencies -Percentile 0.95
$p99 = Get-Percentile -SortedValues $latencies -Percentile 0.99
$passed = $failureRate -le $MaxFailureRatePercent -and
    $p95 -le $MaxP95Milliseconds -and
    $p99 -le $MaxP99Milliseconds
$commit = try { (git rev-parse HEAD 2>$null).Trim() } catch { "unknown" }

$report = [ordered]@{
    schemaVersion = 1
    evidenceType = "qaly-http-load-smoke"
    scenario = $safeScenario
    sourceCommit = $commit
    startedAtUtc = $startedAt.ToString("O")
    completedAtUtc = $completedAt.ToString("O")
    target = [ordered]@{
        origin = $targetUri.GetLeftPart([UriPartial]::Authority)
        path = $targetUri.AbsolutePath
        authentication = $authSummary
    }
    profile = [ordered]@{
        requests = $Requests
        concurrency = $Concurrency
        warmupRequests = $WarmupRequests
        requestTimeoutSeconds = $RequestTimeoutSeconds
        runTimeoutSeconds = $RunTimeoutSeconds
    }
    result = [ordered]@{
        passed = $passed
        successes = $Requests - $failed.Count
        failures = $failed.Count
        failureRatePercent = $failureRate
        throughputRequestsPerSecond = [math]::Round($Requests / $elapsedSeconds, 2)
        elapsedSeconds = [math]::Round($elapsedSeconds, 3)
        latencyMilliseconds = [ordered]@{
            p50 = $p50
            p95 = $p95
            p99 = $p99
            max = [math]::Round($latencies[-1], 2)
        }
        statusCodes = $statusCodes
        failureKinds = $failureKinds
    }
    thresholds = [ordered]@{
        maxFailureRatePercent = $MaxFailureRatePercent
        maxP95Milliseconds = $MaxP95Milliseconds
        maxP99Milliseconds = $MaxP99Milliseconds
    }
    truthBoundary = "This is target-runtime evidence only when the origin identifies the deployed release candidate and sourceCommit matches that deployment."
}

$resolvedEvidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory)
[IO.Directory]::CreateDirectory($resolvedEvidenceDirectory) | Out-Null
$stamp = $startedAt.ToString("yyyyMMdd-HHmmss")
$jsonPath = Join-Path $resolvedEvidenceDirectory "$stamp-$safeScenario.json"
$markdownPath = Join-Path $resolvedEvidenceDirectory "$stamp-$safeScenario.md"
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding utf8NoBOM

$verdict = if ($passed) { "PASS" } else { "FAIL" }
$markdown = @"
# Qaly load smoke — $safeScenario

- Verdict: **$verdict**
- Started (UTC): $($report.startedAtUtc)
- Source commit: $commit
- Target: $($report.target.origin)$($report.target.path)
- Authentication supplied: $authSummary (credential values are never recorded)
- Profile: $Requests requests, concurrency $Concurrency, $WarmupRequests warmup
- Result: $($report.result.successes) success, $($report.result.failures) failure ($failureRate%), $($report.result.throughputRequestsPerSecond) req/s
- Latency: p50 $p50 ms, p95 $p95 ms, p99 $p99 ms, max $($report.result.latencyMilliseconds.max) ms
- Thresholds: failure <= $MaxFailureRatePercent%, p95 <= $MaxP95Milliseconds ms, p99 <= $MaxP99Milliseconds ms

This artifact is target-runtime evidence only when the target is the deployed release candidate and the reported source commit matches that deployment.
"@
$markdown | Set-Content -LiteralPath $markdownPath -Encoding utf8NoBOM

Write-Host "Load smoke $($verdict): $Requests requests, concurrency $Concurrency, $($report.result.throughputRequestsPerSecond) req/s, p95=${p95}ms, p99=${p99}ms, failures=$($failed.Count) ($failureRate%)."
Write-Host "Evidence: $jsonPath"
Write-Host "Evidence: $markdownPath"

if (-not $passed) {
    exit 2
}
