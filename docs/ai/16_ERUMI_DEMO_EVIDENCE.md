# Erumi AI/RAG Demo Evidence

This file records the repeatable checks added for the AI/RAG, permissions, smoke, load, backup, and export gaps.

## Automated Evidence

Run locally from the repository root:

```powershell
npm run build
dotnet build --configuration Release --no-restore
dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj --configuration Release --collect:"XPlat Code Coverage" --results-directory .test-results/unit
pwsh ./scripts/check-coverage.ps1 -CoverageRoot .test-results/unit -MinimumLineRate 8
dotnet test tests/Qaly.IntegrationTests/Qaly.IntegrationTests.csproj --configuration Release --collect:"XPlat Code Coverage" --results-directory .test-results/integration
pwsh ./scripts/check-coverage.ps1 -CoverageRoot .test-results/integration -MinimumLineRate 5
```

CI now runs the same coverage gates and a Playwright smoke path that logs in as the seeded admin, opens `/teams`, creates a group, and sends a real group chat message through the backend.

## AI/RAG Guardrails

Covered by `tests/Qaly.UnitTests/AiGatewayEvidenceTests.cs`:

- Sensitive cloud AI request without consent returns `ComplianceMock` and writes an `AiAuditEvent`.
- Daily budget hard stop returns `BudgetMock`.
- Golden dataset cache hit returns the fixture response and writes `AiUsageLedger` with `CacheHit = true`.
- Qdrant vector search throws before provider access when `ProjectId` or current `OwnerId` is missing.

Golden dataset fixture: `tests/Qaly.UnitTests/Fixtures/ai-golden-dataset.json`.

## Permission And Tenant Evidence

Covered by `tests/Qaly.UnitTests/TaskAccessPolicyIsolationTests.cs` and existing wiki visibility tests:

- Cross-project tasks do not pass the task visibility filter.
- Private tasks are hidden unless the user is owner/reporter/assignee.
- Customer users only see `public` and `customer_safe` wiki pages.

## Soft Delete Evidence

Covered by `tests/Qaly.UnitTests/SoftDeleteQueryFilterTests.cs`:

- Repository delete marks supported entities with `IsDeleted` and `DeletedAt`.
- Global EF query filters hide soft-deleted records.
- Audit and ledger entities are not soft-deleted.

Migration: `AddSoftDeleteColumns`.

## Production Smoke, Load, Backup

Use these scripts against staging or production:

```powershell
pwsh ./scripts/smoke-test.ps1 -BaseUrl https://your-qaly-host
pwsh ./scripts/load-smoke.ps1 -BaseUrl https://your-qaly-host -Requests 200 -Concurrency 20
pwsh ./scripts/backup-sqlserver.ps1 -Server "sql-host,1433" -Database QalyDb -User sa -Password "<secret>" -OutputDirectory .backups
```

The smoke script checks `/health`; the load smoke script repeats `/health` with concurrency and fails on any unsuccessful response; the backup script creates a SQL Server `.bak` with checksum.
