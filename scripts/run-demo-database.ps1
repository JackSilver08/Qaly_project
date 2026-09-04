[CmdletBinding()]
param(
    [switch]$Headed
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$utf8 = [Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = $utf8
[Console]::OutputEncoding = $utf8
$global:OutputEncoding = $utf8

$repoRoot = Split-Path -Parent $PSScriptRoot
$settingsPath = Join-Path $repoRoot 'src/Qaly.Web/appsettings.Development.json'
$dotenvPath = Join-Path $repoRoot '.env'

function Read-DotEnv([string]$Path) {
    $values = @{}
    if (-not (Test-Path -LiteralPath $Path)) {
        return $values
    }

    foreach ($rawLine in Get-Content -LiteralPath $Path) {
        $line = $rawLine.Trim()
        if (-not $line -or $line.StartsWith('#') -or -not $line.Contains('=')) {
            continue
        }

        $parts = $line.Split('=', 2)
        $value = $parts[1].Trim()
        if ($value.Length -ge 2 -and
            (($value.StartsWith('"') -and $value.EndsWith('"')) -or
             ($value.StartsWith("'") -and $value.EndsWith("'")))) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        $values[$parts[0].Trim()] = $value
    }

    return $values
}

function Get-ConnectionValue(
    [System.Data.Common.DbConnectionStringBuilder]$Builder,
    [string[]]$Names
) {
    foreach ($name in $Names) {
        if ($Builder.ContainsKey($name)) {
            return [string]$Builder[$name]
        }
    }

    return $null
}

function Test-Pbkdf2Password([string]$Candidate, [string]$StoredHash) {
    if ([string]::IsNullOrWhiteSpace($Candidate)) {
        return $false
    }

    $parts = $StoredHash.Split('.', 2)
    if ($parts.Length -ne 2) {
        return $false
    }

    try {
        $salt = [Convert]::FromBase64String($parts[0])
        $expected = [Convert]::FromBase64String($parts[1])
        $derive = [Security.Cryptography.Rfc2898DeriveBytes]::new(
            $Candidate,
            $salt,
            10000,
            [Security.Cryptography.HashAlgorithmName]::SHA256)
        try {
            $actual = $derive.GetBytes($expected.Length)
        }
        finally {
            $derive.Dispose()
        }
    }
    catch {
        return $false
    }

    if ($actual.Length -ne $expected.Length) {
        return $false
    }

    $difference = 0
    for ($index = 0; $index -lt $actual.Length; $index++) {
        $difference = $difference -bor ($actual[$index] -bxor $expected[$index])
    }

    return $difference -eq 0
}

if (-not (Test-Path -LiteralPath $settingsPath)) {
    throw "Cannot find Development settings at $settingsPath"
}

$settings = Get-Content -Raw -LiteralPath $settingsPath | ConvertFrom-Json
$connectionString = [string]$settings.ConnectionStrings.DefaultConnection
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw 'ConnectionStrings:DefaultConnection is missing from appsettings.Development.json.'
}

$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
# Windows PowerShell's adapter can treat `.ConnectionString = ...` as an indexer write
# for DbConnectionStringBuilder. Call the CLR setter explicitly so the pairs are parsed.
$connection.set_ConnectionString($connectionString)
$server = Get-ConnectionValue $connection @('Server', 'Data Source')
$database = Get-ConnectionValue $connection @('Database', 'Initial Catalog')
$integratedSecurity = Get-ConnectionValue $connection @('Trusted_Connection', 'Integrated Security')

if ([string]::IsNullOrWhiteSpace($server) -or [string]::IsNullOrWhiteSpace($database)) {
    throw 'The Development SQL connection string must contain Server and Database.'
}

$sqlcmdCommand = Get-Command sqlcmd -ErrorAction SilentlyContinue
if ($sqlcmdCommand) {
    $sqlcmd = $sqlcmdCommand.Source
}
else {
    $knownSqlcmd = 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE'
    if (-not (Test-Path -LiteralPath $knownSqlcmd)) {
        throw 'sqlcmd is required to resolve the demo account and project from QalyDB.'
    }
    $sqlcmd = $knownSqlcmd
}

$sqlArguments = @('-S', $server, '-d', $database, '-C', '-b', '-h', '-1', '-W')
$usesIntegratedSecurity = $integratedSecurity -match '^(?i:true|yes|sspi)$'
$previousSqlcmdPassword = $env:SQLCMDPASSWORD
if ($usesIntegratedSecurity) {
    $sqlArguments += '-E'
}
else {
    $sqlUser = Get-ConnectionValue $connection @('User ID', 'UID', 'User')
    $sqlPassword = Get-ConnectionValue $connection @('Password', 'PWD')
    if ([string]::IsNullOrWhiteSpace($sqlUser) -or [string]::IsNullOrWhiteSpace($sqlPassword)) {
        throw 'The Development SQL connection string must use integrated security or contain SQL credentials.'
    }
    $env:SQLCMDPASSWORD = $sqlPassword
    $sqlArguments += @('-U', $sqlUser)
}

function Invoke-SqlScalar([string]$Query) {
    $output = @(& $script:sqlcmd @script:sqlArguments -Q "SET NOCOUNT ON; $Query" 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Could not query $script:database on $script:server. $($output -join ' ')"
    }

    $lines = @($output | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ })
    if ($lines.Count -eq 0) {
        return $null
    }

    return [string]$lines[0]
}

try {
    $adminEmail = Invoke-SqlScalar @"
SELECT TOP (1) Email
FROM Users
WHERE Role = 'Admin' AND IsActive = 1
ORDER BY CreatedAt;
"@
    $adminPasswordHash = Invoke-SqlScalar @"
SELECT PasswordHash
FROM Users
WHERE Email = N'$($adminEmail.Replace("'", "''"))';
"@
    $demoProject = Invoke-SqlScalar @"
SELECT TOP (1) Name
FROM Projects
WHERE IsDeleted = 0 AND ArchivedAt IS NULL
ORDER BY CASE WHEN Code = 'qaly-workos-demo' THEN 0 ELSE 1 END, CreatedAt DESC;
"@
}
finally {
    if ($null -eq $previousSqlcmdPassword) {
        Remove-Item Env:SQLCMDPASSWORD -ErrorAction SilentlyContinue
    }
    else {
        $env:SQLCMDPASSWORD = $previousSqlcmdPassword
    }
}

if ([string]::IsNullOrWhiteSpace($adminEmail) -or
    [string]::IsNullOrWhiteSpace($adminPasswordHash) -or
    [string]::IsNullOrWhiteSpace($demoProject)) {
    throw 'QalyDB does not contain an active admin and a non-archived demo project.'
}

$dotenv = Read-DotEnv $dotenvPath
$passwordCandidates = New-Object System.Collections.Generic.List[string]
if ($dotenv.ContainsKey('QALY_SEED_ADMIN_PASSWORD')) {
    $passwordCandidates.Add([string]$dotenv['QALY_SEED_ADMIN_PASSWORD'])
}
if ($settings.PSObject.Properties['Seed'] -and $settings.Seed.PSObject.Properties['AdminPassword']) {
    $passwordCandidates.Add([string]$settings.Seed.AdminPassword)
}

$adminPassword = $passwordCandidates |
    Where-Object { Test-Pbkdf2Password $_ $adminPasswordHash } |
    Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($adminPassword)) {
    throw @"
The admin password hash in QalyDB does not match any local seed credential.
Update the local seed configuration or reset the demo admin password before running the visual test.
"@
}

$portText = if ($env:E2E_DEMO_DATABASE_PORT) { $env:E2E_DEMO_DATABASE_PORT } else { '5097' }
$port = 0
if (-not [int]::TryParse($portText, [ref]$port) -or $port -lt 1 -or $port -gt 65535) {
    throw 'E2E_DEMO_DATABASE_PORT must be a valid TCP port.'
}

$env:E2E_BASE_URL = "http://127.0.0.1:$port"
$env:E2E_ADMIN_EMAIL = $adminEmail
$env:E2E_ADMIN_PASSWORD = $adminPassword
$env:E2E_DEMO_PROJECT = $demoProject
$env:E2E_START_LOCAL_DATABASE_SERVER = 'true'
$env:E2E_AI_MODEL = if ($env:E2E_AI_MODEL) { $env:E2E_AI_MODEL } else { 'auto' }

$startedOllama = $null
if ($env:E2E_AI_MODEL -in @('auto', 'ollama-local')) {
    $ollamaBaseUrl = [string]$settings.AiSettings.Ollama.BaseUrl
    # Follow the model the application itself is configured with. A smaller model than
    # AiSettings:Ollama:Model cannot satisfy the structured-output contract, so the turn
    # degrades to the "model chưa phản hồi" notice and the demo assertions go red for a
    # reason that has nothing to do with the product.
    $configuredOllamaModel = [string]$settings.AiSettings.Ollama.Model
    $ollamaModel = if ($env:E2E_OLLAMA_MODEL) {
        $env:E2E_OLLAMA_MODEL
    }
    elseif (-not [string]::IsNullOrWhiteSpace($configuredOllamaModel)) {
        $configuredOllamaModel
    }
    else {
        'qwen2.5:3b'
    }
    $env:AiSettings__Ollama__Model = $ollamaModel
    $env:Ai__ChatModel = $ollamaModel
    $tagsUrl = "$($ollamaBaseUrl.TrimEnd('/'))/api/tags"

    function Get-OllamaModels {
        try {
            $response = Invoke-RestMethod -Uri $script:tagsUrl -TimeoutSec 3
            return @($response.models | ForEach-Object { [string]$_.name })
        }
        catch {
            return @()
        }
    }

    $ollamaModels = @(Get-OllamaModels)
    if ($ollamaModels.Count -eq 0) {
        $ollamaCommand = Get-Command ollama.exe -ErrorAction SilentlyContinue
        if (-not $ollamaCommand) {
            throw 'The database demo uses local AI, but Ollama is not installed or available on PATH.'
        }

        $startedOllama = Start-Process -FilePath $ollamaCommand.Source -ArgumentList 'serve' -WindowStyle Hidden -PassThru
        for ($attempt = 0; $attempt -lt 30 -and $ollamaModels.Count -eq 0; $attempt++) {
            Start-Sleep -Milliseconds 500
            $ollamaModels = @(Get-OllamaModels)
        }
    }

    if ($ollamaModels.Count -eq 0) {
        if ($startedOllama -and -not $startedOllama.HasExited) {
            Stop-Process -Id $startedOllama.Id -Force -ErrorAction SilentlyContinue
        }
        throw "Ollama did not become ready at $ollamaBaseUrl."
    }
    if ($ollamaModels -notcontains $ollamaModel -and $ollamaModels -notcontains "$ollamaModel`:latest") {
        if ($startedOllama -and -not $startedOllama.HasExited) {
            Stop-Process -Id $startedOllama.Id -Force -ErrorAction SilentlyContinue
        }
        throw "Ollama model '$ollamaModel' is missing. Run: ollama pull $ollamaModel"
    }
}

Write-Host "Database : $server/$database"
Write-Host "Admin    : $adminEmail"
Write-Host 'Password : verified from local seed configuration (value hidden)'
Write-Host "Project  : $demoProject"
Write-Host "App URL  : $env:E2E_BASE_URL"
Write-Host "AI route : $env:E2E_AI_MODEL (sensitive data stays local)"
if ($env:E2E_AI_MODEL -in @('auto', 'ollama-local')) {
    Write-Host "Ollama   : $ollamaModel"
}

$playwright = Join-Path $repoRoot 'node_modules/.bin/playwright.cmd'
if (-not (Test-Path -LiteralPath $playwright)) {
    throw 'Playwright is not installed. Run npm install first.'
}

$npm = Get-Command npm.cmd -ErrorAction SilentlyContinue
if (-not $npm) {
    throw 'npm is required to build the current frontend before the database demo test.'
}

$playwrightArguments = @('test', '--config=playwright.demo.config.ts')
if ($Headed) {
    $playwrightArguments += '--headed'
}

Push-Location $repoRoot
try {
    Write-Host 'Frontend : building current Vue source'
    & $npm.Source run build
    if ($LASTEXITCODE -ne 0) {
        throw 'The frontend build failed; the demo test was not started.'
    }

    & $playwright @playwrightArguments
    exit $LASTEXITCODE
}
finally {
    Pop-Location
    if ($startedOllama -and -not $startedOllama.HasExited) {
        Stop-Process -Id $startedOllama.Id -Force -ErrorAction SilentlyContinue
    }
}
