param(
    [string]$BaseUrl = "http://127.0.0.1:5000",
    [int]$Requests = 100,
    [int]$Concurrency = 10
)

$healthUrl = "$($BaseUrl.TrimEnd('/'))/health"
$startedAt = Get-Date
$pending = New-Object System.Collections.Queue
1..$Requests | ForEach-Object { $pending.Enqueue($_) }
$jobs = @()
$results = @()

while ($pending.Count -gt 0 -or $jobs.Count -gt 0) {
    while ($pending.Count -gt 0 -and $jobs.Count -lt $Concurrency) {
        [void]$pending.Dequeue()
        $jobs += Start-Job -ScriptBlock {
            param([string]$Url)

            $watch = [System.Diagnostics.Stopwatch]::StartNew()
            try {
                $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 15
                [pscustomobject]@{
                    Ok = $response.StatusCode -ge 200 -and $response.StatusCode -lt 300
                    StatusCode = $response.StatusCode
                    ElapsedMs = $watch.ElapsedMilliseconds
                }
            }
            catch {
                [pscustomobject]@{
                    Ok = $false
                    StatusCode = 0
                    ElapsedMs = $watch.ElapsedMilliseconds
                }
            }
        } -ArgumentList $healthUrl
    }

    $completed = Wait-Job -Job $jobs -Any -Timeout 1
    if ($completed) {
        $results += Receive-Job -Job $completed
        Remove-Job -Job $completed
        $jobs = @($jobs | Where-Object { $_.Id -ne $completed.Id })
    }
}

$failed = @($results | Where-Object { -not $_.Ok })
$elapsed = [math]::Round(((Get-Date) - $startedAt).TotalSeconds, 2)
$latencies = @($results | ForEach-Object { [double]$_.ElapsedMs } | Sort-Object)
$p95Index = [math]::Min($latencies.Count - 1, [math]::Max(0, [math]::Ceiling($latencies.Count * 0.95) - 1))
$p95 = $latencies[$p95Index]

Write-Host "Load smoke completed: $Requests requests, $Concurrency concurrency, $elapsed seconds, p95=${p95}ms, failures=$($failed.Count)."

if ($failed.Count -gt 0) {
    Write-Error "Load smoke failed with $($failed.Count) unsuccessful requests."
    exit 1
}
