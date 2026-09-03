# Clean Restore Evidence - PASSED

- Task: T1-LG-01
- Backup file: C:\Users\Lenovo\Documents\Qaly\Qaly_project\.backups\QalyMigrationRehearsal_20260903065913-20260903-065938.bak
- Backup SHA256: 9FC7AC6CB31234281737A2836CBEDC8C0E4D6A6DCFA9D047782997E6D1430229
- Target database: QalyRestoreRehearsal_20260903065913
- Server: (localdb)\MSSQLLocalDB
- Operator: codex-production-readiness
- Started: 2026-09-03T06:59:38.9147093+07:00
- Finished: 2026-09-03T06:59:40.1847697+07:00
- RTO seconds: 1.27
- Verify backup: passed
- Restore: passed
- DBCC CHECKDB: passed
- Smoke query: passed
- Cleanup: dropped-restored-database

Evidence JSON file: 20260903-065938-clean-restore-evidence.json
