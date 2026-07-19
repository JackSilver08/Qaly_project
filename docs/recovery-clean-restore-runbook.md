# Qaly Clean Restore Runbook

## Purpose

This runbook proves that a SQL Server backup can be restored into a clean, isolated database and verified with recovery evidence. It supports task `T1-LG-01`.

The restore target is a new database by default. It does not overwrite `QalyDb` unless an operator explicitly passes a target name and `-ReplaceExisting`.

## Prerequisites

- SQL Server is reachable.
- `sqlcmd` is installed and available in `PATH`.
- The backup file exists locally or on a path visible to SQL Server.
- The operator has permission to run `RESTORE VERIFYONLY`, `RESTORE DATABASE`, and `DBCC CHECKDB`.

## Create A Backup

```powershell
$env:SQLSERVER_SA_PASSWORD = "<local-secret>"
.\scripts\backup-sqlserver.ps1 `
  -Server "localhost,1433" `
  -Database "QalyDb" `
  -User "sa" `
  -Password $env:SQLSERVER_SA_PASSWORD `
  -OutputDirectory ".backups"
```

## Restore Into A Clean Database

```powershell
$env:SQLSERVER_SA_PASSWORD = "<local-secret>"
.\scripts\restore-sqlserver-clean.ps1 `
  -Server "localhost,1433" `
  -BackupFile ".backups\QalyDb-YYYYMMDD-HHMMSS.bak" `
  -User "sa" `
  -Password $env:SQLSERVER_SA_PASSWORD
```

For LocalDB or Windows authentication:

```powershell
.\scripts\restore-sqlserver-clean.ps1 `
  -Server "(localdb)\MSSQLLocalDB" `
  -BackupFile ".backups\QalyDb-YYYYMMDD-HHMMSS.bak" `
  -UseIntegratedSecurity
```

To clean up the restored smoke database after evidence is written:

```powershell
.\scripts\restore-sqlserver-clean.ps1 `
  -BackupFile ".backups\QalyDb-YYYYMMDD-HHMMSS.bak" `
  -DropRestoredDatabase
```

## Evidence Produced

The script writes recovery evidence to:

- `docs/task/qa-evidence/recovery/*-clean-restore-evidence.json`
- `docs/task/qa-evidence/recovery/*-clean-restore-evidence.md`

Evidence includes:

- backup path and SHA256;
- SQL Server target;
- restored database name;
- backup verification result;
- restore result;
- `DBCC CHECKDB` result;
- basic schema smoke result;
- start and finish time;
- measured recovery time in seconds.

## Acceptance Criteria

The restore is accepted only when all checks pass:

1. `RESTORE VERIFYONLY ... WITH CHECKSUM` passes.
2. Restore completes into an isolated database.
3. `DBCC CHECKDB` passes.
4. Smoke query can read base tables and EF migration history.
5. Evidence JSON and Markdown are attached to the release or QA record.

## Recovery Notes

- Redis is not authoritative and does not need backup restore for business data.
- Uploaded objects need a separate storage restore plan if production object storage is used.
- Secrets are not restored from database backup; they must come from the deployment secret store.
- If a destructive migration is planned, run this clean restore before applying that migration.
