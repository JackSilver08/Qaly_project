param(
    [string]$Server = "localhost,1433",
    [string]$Database = "QalyDb",
    [string]$User = "sa",
    [Parameter(Mandatory = $true)]
    [string]$Password,
    [string]$OutputDirectory = ".backups"
)

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    Write-Error "sqlcmd was not found. Install SQL Server command-line tools before running backups."
    exit 1
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$resolvedOutputDirectory = (Resolve-Path $OutputDirectory).Path
$backupFile = Join-Path $resolvedOutputDirectory "$Database-$stamp.bak"
$escapedBackupFile = $backupFile.Replace("'", "''")
$query = "BACKUP DATABASE [$Database] TO DISK = N'$escapedBackupFile' WITH INIT, COMPRESSION, CHECKSUM;"

& sqlcmd -S $Server -U $User -P $Password -C -Q $query
if ($LASTEXITCODE -ne 0) {
    Write-Error "Backup failed for database '$Database'."
    exit $LASTEXITCODE
}

Write-Host "Backup completed: $backupFile"
