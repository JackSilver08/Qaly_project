param(
    [string]$Server = "localhost,1433",
    [string]$Database = "QalyDb",
    [string]$User = "sa",
    [string]$Password = $env:SQLSERVER_SA_PASSWORD,
    [switch]$UseIntegratedSecurity,
    [switch]$DisableCompression,
    [string]$OutputDirectory = ".backups"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    Write-Error "sqlcmd was not found. Install SQL Server command-line tools before running backups."
    exit 1
}

if ($Database.Length -gt 128 -or $Database -notmatch '^[A-Za-z0-9_]+$') {
    throw "Database must contain only letters, numbers, and underscores and be at most 128 characters."
}

if (-not $UseIntegratedSecurity -and [string]::IsNullOrWhiteSpace($Password)) {
    throw "Set SQLSERVER_SA_PASSWORD, pass -Password, or use -UseIntegratedSecurity."
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$resolvedOutputDirectory = (Resolve-Path $OutputDirectory).Path
$backupFile = Join-Path $resolvedOutputDirectory "$Database-$stamp.bak"
$escapedBackupFile = $backupFile.Replace("'", "''")
$compressionClause = if ($DisableCompression) { "" } else { ", COMPRESSION" }
$query = "BACKUP DATABASE [$Database] TO DISK = N'$escapedBackupFile' WITH INIT$compressionClause, CHECKSUM;"

$sqlArgs = @("-S", $Server, "-C", "-b", "-Q", $query)
if ($UseIntegratedSecurity) {
    $sqlArgs += "-E"
}
else {
    $sqlArgs += @("-U", $User, "-P", $Password)
}

& sqlcmd @sqlArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Backup failed for database '$Database'."
    exit $LASTEXITCODE
}

Write-Host "Backup completed: $backupFile"
