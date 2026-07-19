param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$BaseSettingsPath = "src/Qaly.Web/appsettings.json",
    [string]$ProductionExamplePath = "src/Qaly.Web/appsettings.Production.example.json"
)

$ErrorActionPreference = "Stop"
$failures = [System.Collections.Generic.List[string]]::new()

function Read-JsonFile {
    param([string]$RelativePath)

    $path = Join-Path $RepositoryRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing configuration file: $RelativePath"
    }

    return Get-Content -LiteralPath $path -Raw -Encoding utf8 | ConvertFrom-Json
}

function Get-ConfigLeafPaths {
    param(
        [Parameter(Mandatory = $true)]
        $Node,
        [string]$Prefix = ""
    )

    if ($null -eq $Node) {
        return @($Prefix)
    }

    if ($Node -is [System.Management.Automation.PSCustomObject]) {
        $paths = @()
        foreach ($property in $Node.PSObject.Properties) {
            $childPrefix = if ([string]::IsNullOrWhiteSpace($Prefix)) { $property.Name } else { "$Prefix`:$($property.Name)" }
            $paths += Get-ConfigLeafPaths -Node $property.Value -Prefix $childPrefix
        }
        return $paths
    }

    if ($Node -is [System.Collections.IEnumerable] -and -not ($Node -is [string])) {
        return @($Prefix)
    }

    return @($Prefix)
}

function Test-PathIgnored {
    param([string]$Path)

    $ignoredPrefixes = @(
        "Serilog:Using",
        "Serilog:Enrich"
    )

    foreach ($prefix in $ignoredPrefixes) {
        if ($Path -eq $prefix -or $Path.StartsWith("$prefix`:")) {
            return $true
        }
    }

    return $false
}

$baseSettings = Read-JsonFile $BaseSettingsPath
$productionExample = Read-JsonFile $ProductionExamplePath

$basePaths = Get-ConfigLeafPaths -Node $baseSettings |
    Where-Object { -not (Test-PathIgnored -Path $_) } |
    Sort-Object -Unique

$productionPaths = Get-ConfigLeafPaths -Node $productionExample |
    Where-Object { -not (Test-PathIgnored -Path $_) } |
    Sort-Object -Unique

$missingInProduction = $basePaths | Where-Object { $_ -notin $productionPaths }
foreach ($path in $missingInProduction) {
    $failures.Add("Production example is missing config path from appsettings.json: $path")
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Configuration parity checks passed."
