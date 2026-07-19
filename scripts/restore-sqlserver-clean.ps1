param(
    [Parameter(Mandatory = $true)]
    [string]$BackupFile,

    [string]$Server = "localhost,1433",
    [string]$TargetDatabase,
    [string]$User = "sa",
    [string]$Password = $env:SQLSERVER_SA_PASSWORD,
    [switch]$UseIntegratedSecurity,
    [switch]$ReplaceExisting,
    [switch]$DropRestoredDatabase,
    [string]$EvidenceDirectory = "docs/task/qa-evidence/recovery",
    [string]$Operator = $env:USERNAME
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    throw "sqlcmd was not found. Install SQL Server command-line tools before running restore validation."
}

$resolvedBackup = Resolve-Path -LiteralPath $BackupFile -ErrorAction Stop
$backupPath = $resolvedBackup.Path
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"

if ([string]::IsNullOrWhiteSpace($TargetDatabase)) {
    $TargetDatabase = "QalyRestoreSmoke_$($stamp.Replace('-', ''))"
}

if (-not $UseIntegratedSecurity -and [string]::IsNullOrWhiteSpace($Password)) {
    throw "Set SQLSERVER_SA_PASSWORD, pass -Password, or use -UseIntegratedSecurity."
}

New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$resolvedEvidenceDirectory = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
$evidenceJson = Join-Path $resolvedEvidenceDirectory "$stamp-clean-restore-evidence.json"
$evidenceMarkdown = Join-Path $resolvedEvidenceDirectory "$stamp-clean-restore-evidence.md"

function Get-SqlArgs {
    param([string]$Query, [string]$Database = "master")

    $args = @("-S", $Server, "-d", $Database, "-C", "-b", "-Q", $Query)
    if ($UseIntegratedSecurity) {
        $args += "-E"
    }
    else {
        $args += @("-U", $User, "-P", $Password)
    }

    return $args
}

function Invoke-QalySql {
    param([string]$Query, [string]$Database = "master")

    $output = & sqlcmd @(Get-SqlArgs -Query $Query -Database $Database) 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw ($output -join [Environment]::NewLine)
    }

    return $output
}

function Escape-SqlLiteral {
    param([string]$Value)
    return $Value.Replace("'", "''")
}

function Escape-SqlIdentifier {
    param([string]$Value)
    return $Value.Replace("]", "]]")
}

$startedAt = Get-Date
$evidence = [ordered]@{
    task = "T1-LG-01"
    scenario = "Clean SQL Server restore from backup"
    startedAt = $startedAt.ToString("o")
    finishedAt = $null
    operator = $Operator
    server = $Server
    backupFile = $backupPath
    backupSha256 = $null
    targetDatabase = $TargetDatabase
    verifyOnly = "not-run"
    restore = "not-run"
    checkDb = "not-run"
    smoke = "not-run"
    cleanup = "not-requested"
    rtoSeconds = $null
    success = $false
    errors = @()
}

try {
    $evidence.backupSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $backupPath).Hash

    $escapedBackup = Escape-SqlLiteral $backupPath
    $escapedTarget = Escape-SqlIdentifier $TargetDatabase

    $existsQuery = "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = N'$(Escape-SqlLiteral $TargetDatabase)';"
    $existsOutput = Invoke-QalySql -Query $existsQuery
    $exists = (($existsOutput | Where-Object { $_ -match '^\s*\d+\s*$' } | Select-Object -First 1) -as [int])
    if ($exists -gt 0 -and -not $ReplaceExisting) {
        throw "Target database '$TargetDatabase' already exists. Pass -ReplaceExisting to overwrite it."
    }

    Write-Host "Verifying backup checksum..."
    Invoke-QalySql -Query "RESTORE VERIFYONLY FROM DISK = N'$escapedBackup' WITH CHECKSUM;" | Out-Null
    $evidence.verifyOnly = "passed"

    $fileListQuery = @"
SET NOCOUNT ON;
CREATE TABLE #FileList (
    LogicalName nvarchar(128),
    PhysicalName nvarchar(260),
    [Type] char(1),
    FileGroupName nvarchar(128) NULL,
    Size numeric(20,0),
    MaxSize numeric(20,0),
    FileId bigint,
    CreateLSN numeric(25,0) NULL,
    DropLSN numeric(25,0) NULL,
    UniqueId uniqueidentifier,
    ReadOnlyLSN numeric(25,0) NULL,
    ReadWriteLSN numeric(25,0) NULL,
    BackupSizeInBytes bigint,
    SourceBlockSize int,
    FileGroupId int,
    LogGroupGUID uniqueidentifier NULL,
    DifferentialBaseLSN numeric(25,0) NULL,
    DifferentialBaseGUID uniqueidentifier NULL,
    IsReadOnly bit,
    IsPresent bit,
    TDEThumbprint varbinary(32) NULL,
    SnapshotUrl nvarchar(360) NULL
);
INSERT INTO #FileList EXEC('RESTORE FILELISTONLY FROM DISK = N''$escapedBackup''');
SELECT LogicalName + N'|' + [Type] FROM #FileList ORDER BY CASE [Type] WHEN 'D' THEN 0 WHEN 'L' THEN 1 ELSE 2 END, FileId;
"@

    $logicalFiles = Invoke-QalySql -Query $fileListQuery |
        Where-Object { $_ -match '\|' } |
        ForEach-Object {
            $parts = $_.Trim() -split '\|', 2
            [pscustomobject]@{ LogicalName = $parts[0]; Type = $parts[1] }
        }

    $dataLogical = ($logicalFiles | Where-Object { $_.Type -eq "D" } | Select-Object -First 1).LogicalName
    $logLogical = ($logicalFiles | Where-Object { $_.Type -eq "L" } | Select-Object -First 1).LogicalName

    if ([string]::IsNullOrWhiteSpace($dataLogical) -or [string]::IsNullOrWhiteSpace($logLogical)) {
        throw "Could not read data/log logical names from backup file."
    }

    $pathQuery = @"
SET NOCOUNT ON;
SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultDataPath')) + N'|' + CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultLogPath'));
"@
    $pathLine = Invoke-QalySql -Query $pathQuery | Where-Object { $_ -match '\|' } | Select-Object -First 1
    $paths = $pathLine.Trim() -split '\|', 2
    $dataPath = $paths[0]
    $logPath = $paths[1]

    if ([string]::IsNullOrWhiteSpace($dataPath)) {
        $dataPath = "C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\"
    }
    if ([string]::IsNullOrWhiteSpace($logPath)) {
        $logPath = $dataPath
    }

    $targetMdf = Join-Path $dataPath "$TargetDatabase.mdf"
    $targetLdf = Join-Path $logPath "$TargetDatabase.ldf"
    $replaceClause = if ($ReplaceExisting) { ", REPLACE" } else { "" }
    $restoreQuery = @"
IF DB_ID(N'$(Escape-SqlLiteral $TargetDatabase)') IS NOT NULL
BEGIN
    ALTER DATABASE [$escapedTarget] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
END;
RESTORE DATABASE [$escapedTarget]
FROM DISK = N'$escapedBackup'
WITH
    MOVE N'$(Escape-SqlLiteral $dataLogical)' TO N'$(Escape-SqlLiteral $targetMdf)',
    MOVE N'$(Escape-SqlLiteral $logLogical)' TO N'$(Escape-SqlLiteral $targetLdf)',
    RECOVERY,
    CHECKSUM,
    STATS = 10$replaceClause;
ALTER DATABASE [$escapedTarget] SET MULTI_USER;
"@

    Write-Host "Restoring backup into clean database '$TargetDatabase'..."
    Invoke-QalySql -Query $restoreQuery | Out-Null
    $evidence.restore = "passed"

    Write-Host "Running DBCC CHECKDB..."
    Invoke-QalySql -Query "DBCC CHECKDB ([$escapedTarget]) WITH NO_INFOMSGS;" | Out-Null
    $evidence.checkDb = "passed"

    $smokeQuery = @"
SET NOCOUNT ON;
SELECT
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE') AS TableCount,
    (SELECT COUNT(*) FROM __EFMigrationsHistory) AS AppliedMigrations;
"@
    $smokeOutput = Invoke-QalySql -Database $TargetDatabase -Query $smokeQuery
    $evidence.smoke = "passed"
    $evidence.smokeOutput = ($smokeOutput -join [Environment]::NewLine).Trim()

    if ($DropRestoredDatabase) {
        Write-Host "Dropping restored database '$TargetDatabase'..."
        Invoke-QalySql -Query "ALTER DATABASE [$escapedTarget] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$escapedTarget];" | Out-Null
        $evidence.cleanup = "dropped-restored-database"
    }

    $evidence.success = $true
}
catch {
    $evidence.errors = @($_.Exception.Message)
    throw
}
finally {
    $finishedAt = Get-Date
    $evidence.finishedAt = $finishedAt.ToString("o")
    $evidence.rtoSeconds = [math]::Round(($finishedAt - $startedAt).TotalSeconds, 2)

    $evidence | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $evidenceJson -Encoding utf8

    $status = if ($evidence.success) { "PASSED" } else { "FAILED" }
    $markdown = @"
# Clean Restore Evidence - $status

- Task: T1-LG-01
- Backup file: $($evidence.backupFile)
- Backup SHA256: $($evidence.backupSha256)
- Target database: $($evidence.targetDatabase)
- Server: $($evidence.server)
- Operator: $($evidence.operator)
- Started: $($evidence.startedAt)
- Finished: $($evidence.finishedAt)
- RTO seconds: $($evidence.rtoSeconds)
- Verify backup: $($evidence.verifyOnly)
- Restore: $($evidence.restore)
- DBCC CHECKDB: $($evidence.checkDb)
- Smoke query: $($evidence.smoke)
- Cleanup: $($evidence.cleanup)

Evidence JSON file: $(Split-Path -Leaf $evidenceJson)
"@
    $markdown | Set-Content -LiteralPath $evidenceMarkdown -Encoding utf8

    Write-Host "Recovery evidence written:"
    Write-Host "  $evidenceJson"
    Write-Host "  $evidenceMarkdown"
}
