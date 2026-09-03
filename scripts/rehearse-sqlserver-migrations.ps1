param(
    [string]$Server = "(localdb)\MSSQLLocalDB",
    [string]$User = "sa",
    [string]$Password = $env:SQLSERVER_SA_PASSWORD,
    [switch]$UseSqlAuthentication,
    [switch]$EnableBackupCompression,
    [string]$EvidenceDirectory = "docs/task/qa-evidence/recovery",
    [string]$BackupDirectory = ".backups",
    [string]$Operator = "local-rehearsal"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$stamp = Get-Date -Format "yyyyMMddHHmmss"
$sourceDatabase = "QalyMigrationRehearsal_$stamp"
$targetDatabase = "QalyRestoreRehearsal_$stamp"
$startedAt = Get-Date
$sourceCreated = $false
$previousConnection = $env:ConnectionStrings__DefaultConnection
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    throw "sqlcmd was not found. Install SQL Server command-line tools before running the migration rehearsal."
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet was not found. Install the repository SDK before running the migration rehearsal."
}

if ($UseSqlAuthentication -and [string]::IsNullOrWhiteSpace($Password)) {
    throw "Set SQLSERVER_SA_PASSWORD or pass -Password when -UseSqlAuthentication is selected."
}

foreach ($databaseName in @($sourceDatabase, $targetDatabase)) {
    if ($databaseName -notmatch '^Qaly(Migration|Restore)Rehearsal_[0-9]{14}$') {
        throw "Refusing to use database outside the rehearsal namespace: $databaseName"
    }
}

function Get-SqlArgs {
    param([string]$Query, [string]$Database = "master")

    $arguments = @("-S", $Server, "-d", $Database, "-C", "-b", "-Q", $Query)
    if ($UseSqlAuthentication) {
        $arguments += @("-U", $User, "-P", $Password)
    }
    else {
        $arguments += "-E"
    }
    return $arguments
}

function Invoke-QalySql {
    param([string]$Query, [string]$Database = "master")

    $output = & sqlcmd @(Get-SqlArgs -Query $Query -Database $Database) 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw ($output -join [Environment]::NewLine)
    }
    return $output
}

function Get-DatabaseExists {
    param([string]$DatabaseName)

    $escaped = $DatabaseName.Replace("'", "''")
    $output = Invoke-QalySql -Query "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = N'$escaped';"
    return [int](($output | Where-Object { $_ -match '^\s*\d+\s*$' } | Select-Object -First 1).Trim()) -gt 0
}

$resolvedEvidenceDirectory = Join-Path $repositoryRoot $EvidenceDirectory
$resolvedBackupDirectory = Join-Path $repositoryRoot $BackupDirectory
New-Item -ItemType Directory -Force -Path $resolvedEvidenceDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $resolvedBackupDirectory | Out-Null
$evidenceJson = Join-Path $resolvedEvidenceDirectory "$stamp-migration-restore-rehearsal.json"
$evidenceMarkdown = Join-Path $resolvedEvidenceDirectory "$stamp-migration-restore-rehearsal.md"

$evidence = [ordered]@{
    scenario = "Full EF migration chain, backup, clean restore and integrity check"
    startedAt = $startedAt.ToString("o")
    finishedAt = $null
    operator = $Operator
    server = $Server
    sourceDatabase = $sourceDatabase
    targetDatabase = $targetDatabase
    migration = "not-run"
    appliedMigrations = 0
    latestMigration = $null
    backup = "not-run"
    cleanRestore = "not-run"
    sourceCleanup = "not-run"
    durationSeconds = $null
    success = $false
    error = $null
}

try {
    if ((Get-DatabaseExists -DatabaseName $sourceDatabase) -or
        (Get-DatabaseExists -DatabaseName $targetDatabase)) {
        throw "A generated rehearsal database already exists; refusing to overwrite it."
    }

    $auth = if ($UseSqlAuthentication) {
        "User Id=$User;Password=$Password"
    }
    else {
        "Integrated Security=True"
    }
    $env:ConnectionStrings__DefaultConnection =
        "Server=$Server;Database=$sourceDatabase;$auth;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
    $env:ASPNETCORE_ENVIRONMENT = "MigrationRehearsal"

    Push-Location $repositoryRoot
    try {
        & dotnet ef database update `
            --project "src/Qaly.Infrastructure/Qaly.Infrastructure.csproj" `
            --startup-project "src/Qaly.Web/Qaly.Web.csproj" `
            --configuration Release
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet ef database update failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }

    $sourceCreated = Get-DatabaseExists -DatabaseName $sourceDatabase
    if (-not $sourceCreated) {
        throw "EF reported success but the rehearsal database was not created."
    }
    $evidence.migration = "passed"

    $migrationOutput = Invoke-QalySql -Database $sourceDatabase -Query @"
SET NOCOUNT ON;
SELECT COUNT(*) FROM __EFMigrationsHistory;
SELECT TOP (1) MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC;
"@
    $numericLine = $migrationOutput | Where-Object { $_ -match '^\s*\d+\s*$' } | Select-Object -First 1
    $migrationLine = $migrationOutput | Where-Object { $_ -match '^\s*20\d{12}_' } | Select-Object -Last 1
    $evidence.appliedMigrations = [int]$numericLine.Trim()
    $evidence.latestMigration = $migrationLine.Trim()
    if ($evidence.appliedMigrations -le 0 -or [string]::IsNullOrWhiteSpace($evidence.latestMigration)) {
        throw "The migrated database did not expose a valid EF migration history."
    }

    $backupParameters = @{
        Server = $Server
        Database = $sourceDatabase
        OutputDirectory = $resolvedBackupDirectory
    }
    if ($UseSqlAuthentication) {
        $backupParameters.User = $User
        $backupParameters.Password = $Password
    }
    else {
        $backupParameters.UseIntegratedSecurity = $true
    }
    if (-not $EnableBackupCompression) {
        $backupParameters.DisableCompression = $true
    }

    & (Join-Path $PSScriptRoot "backup-sqlserver.ps1") @backupParameters
    if ($LASTEXITCODE -ne 0) {
        throw "Backup script failed with exit code $LASTEXITCODE."
    }
    $backupFile = Get-ChildItem -LiteralPath $resolvedBackupDirectory -Filter "$sourceDatabase-*.bak" |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -eq $backupFile -or $backupFile.LastWriteTime -lt $startedAt) {
        throw "Backup completed without producing a fresh rehearsal artifact."
    }
    $evidence.backup = "passed"
    $evidence.backupSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $backupFile.FullName).Hash

    $restoreParameters = @{
        Server = $Server
        BackupFile = $backupFile.FullName
        TargetDatabase = $targetDatabase
        DropRestoredDatabase = $true
        EvidenceDirectory = $resolvedEvidenceDirectory
        Operator = $Operator
    }
    if ($UseSqlAuthentication) {
        $restoreParameters.User = $User
        $restoreParameters.Password = $Password
    }
    else {
        $restoreParameters.UseIntegratedSecurity = $true
    }

    & (Join-Path $PSScriptRoot "restore-sqlserver-clean.ps1") @restoreParameters
    if ($LASTEXITCODE -ne 0) {
        throw "Clean restore script failed with exit code $LASTEXITCODE."
    }
    if (Get-DatabaseExists -DatabaseName $targetDatabase) {
        throw "The restore rehearsal requested cleanup but the restored database still exists."
    }
    $evidence.cleanRestore = "passed"
    $evidence.success = $true
}
catch {
    $evidence.error = $_.Exception.Message
    throw
}
finally {
    $env:ConnectionStrings__DefaultConnection = $previousConnection
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment

    if ($sourceCreated -and (Get-DatabaseExists -DatabaseName $sourceDatabase)) {
        $escapedSource = $sourceDatabase.Replace("]", "]]" )
        Invoke-QalySql -Query "ALTER DATABASE [$escapedSource] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$escapedSource];" | Out-Null
        $evidence.sourceCleanup = "dropped-rehearsal-database"
    }

    $finishedAt = Get-Date
    $evidence.finishedAt = $finishedAt.ToString("o")
    $evidence.durationSeconds = [math]::Round(($finishedAt - $startedAt).TotalSeconds, 2)
    $evidence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $evidenceJson -Encoding utf8

    $status = if ($evidence.success) { "PASSED" } else { "FAILED" }
    @"
# SQL Migration and Restore Rehearsal — $status

- Server: $($evidence.server)
- Source database: $($evidence.sourceDatabase)
- Target database: $($evidence.targetDatabase)
- Full migration chain: $($evidence.migration)
- Applied migrations: $($evidence.appliedMigrations)
- Latest migration: $($evidence.latestMigration)
- Backup with checksum: $($evidence.backup)
- Backup SHA256: $($evidence.backupSha256)
- Clean restore + DBCC CHECKDB: $($evidence.cleanRestore)
- Source cleanup: $($evidence.sourceCleanup)
- Duration seconds: $($evidence.durationSeconds)
- Error: $($evidence.error)

Evidence JSON: $(Split-Path -Leaf $evidenceJson)
"@ | Set-Content -LiteralPath $evidenceMarkdown -Encoding utf8

    Write-Host "Migration rehearsal evidence written:"
    Write-Host "  $evidenceJson"
    Write-Host "  $evidenceMarkdown"
}
