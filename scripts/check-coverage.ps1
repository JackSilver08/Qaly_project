param(
    [string]$CoverageRoot = ".",
    [double]$MinimumLineRate = 0
)

$files = Get-ChildItem -Path $CoverageRoot -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue

if (-not $files -or $files.Count -eq 0) {
    Write-Error "No coverage.cobertura.xml files found under '$CoverageRoot'."
    exit 1
}

$covered = 0
$valid = 0

foreach ($file in $files) {
    [xml]$coverage = Get-Content -LiteralPath $file.FullName
    $covered += [int]$coverage.coverage.'lines-covered'
    $valid += [int]$coverage.coverage.'lines-valid'
}

if ($valid -le 0) {
    Write-Error "Coverage files did not report any valid lines."
    exit 1
}

$lineRate = [math]::Round(($covered / $valid) * 100, 2)
Write-Host "Line coverage: $lineRate% ($covered / $valid lines)"
Write-Host "Required minimum: $MinimumLineRate%"

if ($lineRate -lt $MinimumLineRate) {
    Write-Error "Coverage gate failed: $lineRate% is below $MinimumLineRate%."
    exit 1
}
