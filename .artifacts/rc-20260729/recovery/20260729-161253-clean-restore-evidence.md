# Clean Restore Evidence - PASSED

- Task: T1-LG-01
- Backup file: C:\Qaly_project\.artifacts\rc-20260729\recovery\QalyDb-RC-20260729.bak
- Backup SHA256: 7CD3A00393F61761F1465BE6B233D256927FAC665BA67CE3E547443342C32979
- Target database: QalyRestoreSmoke_RC20260729B
- Server: localhost,1434
- Operator: tuant
- Started: 2026-07-29T16:12:53.2274191+07:00
- Finished: 2026-07-29T16:12:55.8568929+07:00
- RTO seconds: 2.63
- Verify backup: passed
- Restore: passed
- DBCC CHECKDB: passed
- Smoke query: passed
- Cleanup: dropped-restored-database

Evidence JSON file: 20260729-161253-clean-restore-evidence.json
