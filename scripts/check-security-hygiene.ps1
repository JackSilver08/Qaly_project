param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$failures = [System.Collections.Generic.List[string]]::new()
$extensions = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@(".cs", ".cshtml", ".json", ".yml", ".yaml", ".ps1", ".mjs", ".js", ".ts", ".vue", ".props", ".targets"),
    [System.StringComparer]::OrdinalIgnoreCase)

$scanRoots = @(
    (Join-Path $RepositoryRoot "src"),
    (Join-Path $RepositoryRoot "scripts"),
    (Join-Path $RepositoryRoot ".github")
) | Where-Object { Test-Path -LiteralPath $_ }

$files = foreach ($root in $scanRoots) {
    Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
        $extensions.Contains($_.Extension) -and
        $_.FullName -notmatch '[\\/](bin|obj|node_modules|wwwroot[\\/]dist|\.test-results)[\\/]'
    }
}

$rootFiles = @("Dockerfile", "docker-compose.yml", "package.json", "package-lock.json")
foreach ($relativePath in $rootFiles) {
    $path = Join-Path $RepositoryRoot $relativePath
    if (Test-Path -LiteralPath $path) {
        $files += Get-Item -LiteralPath $path
    }
}

$secretPatterns = [ordered]@{
    "private key material" = '-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'
    "GitHub token" = '(?<![A-Za-z0-9])(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,})'
    "AWS access key" = '(?<![A-Z0-9])AKIA[0-9A-Z]{16}(?![A-Z0-9])'
    "OpenAI-compatible secret" = '(?<![A-Za-z0-9])(?:sk|rk)-(?:proj-)?[A-Za-z0-9_-]{20,}'
    "Google API key" = '(?<![A-Za-z0-9])AIza[0-9A-Za-z_-]{35}(?![A-Za-z0-9])'
    "Slack token" = '(?<![A-Za-z0-9])xox[baprs]-[A-Za-z0-9-]{20,}'
}

$sensitiveLogPlaceholder = '\{(?:Email|RecipientEmail|FullName|Phone|Address|Password|Token|Secret|ApiKey|Authorization|Cookie|Payload|Body|Content|QueryString)\}'
$logExpression = '(?:LoggerMessage|Log(?:Trace|Debug|Information|Warning|Error|Critical))'

foreach ($file in $files | Sort-Object -Property FullName -Unique) {
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding utf8
    $relativePath = [System.IO.Path]::GetRelativePath($RepositoryRoot, $file.FullName)

    foreach ($entry in $secretPatterns.GetEnumerator()) {
        if ([regex]::IsMatch($content, $entry.Value)) {
            $failures.Add("High-confidence $($entry.Key) detected in $relativePath.")
        }
    }

    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
        $lineNumber++
        if ($line -match $logExpression -and $line -match $sensitiveLogPlaceholder) {
            $failures.Add("Sensitive value appears in a log template at ${relativePath}:$lineNumber.")
        }
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Security hygiene checks passed: no high-confidence credential material or raw PII log placeholders detected."
