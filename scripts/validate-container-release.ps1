param(
    [Parameter(Mandatory = $true)]
    [string]$CandidateImage,

    [Parameter(Mandatory = $true)]
    [string]$RollbackImage,

    [string]$ContainerName = "qaly-release-validation",
    [string]$Network = "qaly-network",
    [int]$HostPort = 5011,
    [int]$TimeoutSeconds = 90,
    [string]$Database = "QalyReleaseValidation",
    [string]$SqlPassword = $env:SQLSERVER_SA_PASSWORD
)

$ErrorActionPreference = "Stop"
$smokeScript = Join-Path $PSScriptRoot "smoke-test.ps1"
$baseUrl = "http://127.0.0.1:$HostPort"

if ([string]::IsNullOrWhiteSpace($SqlPassword)) {
    throw "Set SQLSERVER_SA_PASSWORD or pass -SqlPassword before validating a container release."
}

function Remove-ValidationContainer {
    $existingContainer = docker ps -aq --filter "name=^/$ContainerName$"
    if ($existingContainer) {
        docker rm -f $ContainerName | Out-Null
    }
}

function Start-ValidationContainer {
    param([string]$Image)

    Remove-ValidationContainer

    $deploymentId = "release-validation-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"

    docker run --detach `
        --name $ContainerName `
        --network $Network `
        --publish "${HostPort}:5000" `
        --env "ASPNETCORE_ENVIRONMENT=Production" `
        --env "ASPNETCORE_URLS=http://+:5000" `
        --env "ProductionReadiness__DeploymentId=$deploymentId" `
        --env "ProductionReadiness__PublicBaseUrl=https://localhost:$HostPort" `
        --env "ProductionReadiness__AiDecision=disabled-accepted" `
        --env "ProductionReadiness__PrivacyDecision=disabled-accepted" `
        --env "DataProtection__KeysPath=/app/dp-keys" `
        --env "ConnectionStrings__DefaultConnection=Server=qaly-sqlserver,1433;Database=$Database;User Id=sa;Password=$SqlPassword;TrustServerCertificate=True;MultipleActiveResultSets=true" `
        --env "Redis__ConnectionString=qaly-redis:6379" `
        --env "Email__SmtpHost=qaly-mailhog" `
        --env "Email__SmtpPort=1025" `
        --env "InvitationLink__FrontendBaseUrl=https://localhost:$HostPort" `
        --env "AllowedHosts=localhost;127.0.0.1" `
        --env "UseInMemoryDatabase=false" `
        --env "Seed__UseRichDemoSeed=false" `
        --env "LiveKit__ServerUrl=" `
        --env "LiveKit__ApiKey=" `
        --env "LiveKit__ApiSecret=" `
        --env "AiJobsV4__Enabled=false" `
        --env "AiJobsV4__WorkerEnabled=false" `
        --env "PrivacyV4__Enabled=false" `
        --env "PrivacyV4__WorkerEnabled=false" `
        --env "PrivacyV4__EnforceSensitiveIngestion=false" `
        $Image | Out-Null

    & $smokeScript -BaseUrl $baseUrl -TimeoutSeconds $TimeoutSeconds -Probe ready
}

try {
    Write-Host "Validating candidate image '$CandidateImage'..."
    Start-ValidationContainer -Image $CandidateImage

    Write-Host "Rolling back to '$RollbackImage'..."
    Start-ValidationContainer -Image $RollbackImage

    Write-Host "Container release validation and rollback passed."
}
finally {
    Remove-ValidationContainer
}
