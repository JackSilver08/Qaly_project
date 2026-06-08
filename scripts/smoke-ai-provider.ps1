param(
    [string]$BaseUrl = "http://localhost:11434",
    [string]$Model = "llama3.2:1b",
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"
$expectedResponse = "QALY_OLLAMA_OK"
$requestBody = @{
    model = $Model
    prompt = "Reply with exactly: $expectedResponse"
    stream = $false
    options = @{
        temperature = 0
    }
} | ConvertTo-Json -Depth 4

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$response = Invoke-RestMethod `
    -Uri "$($BaseUrl.TrimEnd('/'))/api/generate" `
    -Method Post `
    -ContentType "application/json" `
    -Body $requestBody `
    -TimeoutSec $TimeoutSeconds
$stopwatch.Stop()

$actualResponse = ([string]$response.response).Trim()
if (-not $response.done -or $actualResponse -ne $expectedResponse) {
    Write-Error "AI provider smoke failed. Expected '$expectedResponse', received '$actualResponse'."
    exit 1
}

Write-Host "AI provider smoke passed with model '$Model' in $($stopwatch.ElapsedMilliseconds) ms."
