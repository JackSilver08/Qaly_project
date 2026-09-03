# Clean Restore Evidence - PASSED

- Task: T1-LG-01
- Backup file: C:\Users\Lenovo\Documents\Qaly\Qaly_project\.backups\QalyMigrationRehearsal_20260902102626-20260902-102745.bak
- Backup SHA256: 25380D14C521A98250DC2B49348A0EA5A8EA14F058EA4D7B0D048BEC033888D1
- Target database: QalyRestoreRehearsal_20260902102626
- Server: (localdb)\MSSQLLocalDB
- Operator: codex-local-rehearsal
- Started: 2026-09-02T10:27:45.6405955+07:00
- Finished: 2026-09-02T10:27:46.7655053+07:00
- RTO seconds: 1.12
- Verify backup: passed
- Restore: passed
- DBCC CHECKDB: passed
- Smoke query: passed
- Cleanup: dropped-restored-database

Evidence JSON file: 20260902-102745-clean-restore-evidence.json
