param(
    [string]$SolutionPath = "Qaly_project.slnx"
)

$ErrorActionPreference = "Stop"
$output = & dotnet list $SolutionPath package --vulnerable --include-transitive 2>&1
$exitCode = $LASTEXITCODE
$output | Write-Host

if ($exitCode -ne 0) {
    Write-Error "NuGet vulnerability scan failed to execute."
    exit $exitCode
}

if ($output -match "has the following vulnerable packages") {
    Write-Error "NuGet vulnerability scan found one or more vulnerable packages."
    exit 1
}

Write-Host "NuGet vulnerability scan passed."
