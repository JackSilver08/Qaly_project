param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$failures = [System.Collections.Generic.List[string]]::new()

function Read-JsonFile {
    param([string]$RelativePath)

    $path = Join-Path $RepositoryRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path)) {
        $failures.Add("Missing required configuration file: $RelativePath")
        return $null
    }

    try {
        return Get-Content -LiteralPath $path -Raw -Encoding utf8 | ConvertFrom-Json
    }
    catch {
        $failures.Add("Invalid JSON in ${RelativePath}: $($_.Exception.Message)")
        return $null
    }
}

function Test-SecretValue {
    param(
        [string]$Name,
        [AllowEmptyString()]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value) -or $Value.StartsWith("<")) {
        return
    }

    $failures.Add("$Name must be empty or a placeholder in tracked appsettings files.")
}

$baseSettings = Read-JsonFile "src/Qaly.Web/appsettings.json"
$developmentSettings = Read-JsonFile "src/Qaly.Web/appsettings.Development.json"
$productionExample = Read-JsonFile "src/Qaly.Web/appsettings.Production.example.json"

foreach ($entry in @(
    @{ Name = "appsettings.json LiveKit:ApiKey"; Value = $baseSettings.LiveKit.ApiKey },
    @{ Name = "appsettings.json LiveKit:ApiSecret"; Value = $baseSettings.LiveKit.ApiSecret },
    @{ Name = "appsettings.Development.json LiveKit:ApiKey"; Value = $developmentSettings.LiveKit.ApiKey },
    @{ Name = "appsettings.Development.json LiveKit:ApiSecret"; Value = $developmentSettings.LiveKit.ApiSecret },
    @{ Name = "appsettings.Production.example.json LiveKit:ApiKey"; Value = $productionExample.LiveKit.ApiKey },
    @{ Name = "appsettings.Production.example.json LiveKit:ApiSecret"; Value = $productionExample.LiveKit.ApiSecret }
)) {
    Test-SecretValue -Name $entry.Name -Value ([string]$entry.Value)
}

$trackedEnvironmentFiles = @(
    git -C $RepositoryRoot ls-files ".env" ".env.*"
) | Where-Object {
    $_ -and $_ -ne ".env.example"
}

if ($trackedEnvironmentFiles.Count -gt 0) {
    $failures.Add("Tracked environment files are not allowed: $($trackedEnvironmentFiles -join ', ')")
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Configuration safety checks passed."
