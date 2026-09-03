param(
    [string]$BaseUrl = "http://127.0.0.1:5000",
    [int]$TimeoutSeconds = 60,
    [ValidateSet("live", "ready")]
    [string]$Probe = "ready"
)

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$healthUrl = "$($BaseUrl.TrimEnd('/'))/health/$Probe"

do {
    try {
        $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 5
        if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
            Write-Host "Smoke passed: $healthUrl returned $($response.StatusCode)."
            exit 0
        }
    }
    catch {
        Start-Sleep -Seconds 2
    }
} while ((Get-Date) -lt $deadline)

Write-Error "Smoke failed: $healthUrl did not return a healthy response within $TimeoutSeconds seconds."
exit 1
