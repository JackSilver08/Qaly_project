# P0-01 CI and Dependency Security Evidence

Status: Complete on the feature branch; hosted CI verification pending
Executed: 2026-07-11, Asia/Saigon
Baseline commit: `b9fda76c510988c46b7a3bcceb0d7209f44397aa`
Scope: P0-01 only

## 1. Verdict

P0-01 meets its working-tree completion conditions:

- frontend typecheck and production build pass;
- backend Release build passes without warnings or errors;
- 226 unit, 24 integration, and 9 web-feature tests pass;
- NuGet and npm scans report no known Critical, High, Moderate, or Low vulnerability in the resolved dependency graphs;
- the project and task routes preserve IDs, query parameters, refresh, Back behavior, and canonical replacement;
- no security exception is required for `xlsx` or any other dependency.

This is not a production-readiness declaration. The hosted GitHub required check was not executed from this working tree, so `G-NFR-01` remains `Implemented-Unverified` until that run is attached.

## 2. Baseline and worktree control

At the start of P0-01, Git had no tracked modification. The only changes were 20 untracked files under `QALY_Docs_v4.0_Draft/` created and validated in Phase B. Those files were preserved.

The repository tracks generated frontend output under `src/Qaly.Web/wwwroot/dist/`; `.gitignore` explicitly re-includes that path. The final production build artifacts are therefore retained. Diagnostic `node_modules` churn was restored to the initial worktree state. The fact that `node_modules` is already tracked remains technical debt and was not broadened into an untracking migration in this package.

## 3. Typecheck and route correction

`src/Qaly.Web/ClientApp/App.vue` now:

- uses Vue's multi-source `watch` contract instead of a heterogeneous getter result;
- constructs the `project-detail` and `project-task` route parameters explicitly, without `any` or a blind assertion;
- preserves `projectId`, `taskId`, query parameters, replacement history, and canonical route names;
- retains a route-selected task ID while project data is loading;
- activates the Tasks pane for a direct `project-task` route so the selected task opens after navigation or refresh.

No frontend unit-test framework was introduced solely for this narrow change. The recurring route behavior is covered by the runtime smoke evidence in section 7; automating that smoke in hosted CI remains a residual test-coverage item.

## 4. Dependency remediation

| Package or path | Before | After | Decision |
|---|---:|---:|---|
| `Microsoft.OpenApi` | 2.0.0 transitive | 2.7.5 explicit compatible reference | Pin the patched 2.x release used by `Microsoft.AspNetCore.OpenApi` 10.0.7. |
| `dompurify` | 3.4.5 | 3.4.11 | Compatible patch update for the rendered Markdown sanitization call sites. |
| `markdown-it` | 14.1.1 | 14.3.0 | Compatible minor update; existing rendering and build paths pass. |
| `linkify-it` | 5.0.0 transitive | 5.0.2 transitive | Resolved by the `markdown-it` update. |
| `ws` | 7.5.10 transitive | 7.5.11 transitive | `@microsoft/signalr` remains 10.0.0; its allowed transitive patch is refreshed in the lockfile. |
| `vite` | 6.4.2 | 6.4.3 | Patch the remaining development-graph advisory found by the full audit. |
| `xlsx` | npm registry 0.18.5, production dependency | official SheetJS CDN 0.20.3 tarball, development dependency | Remove it from browser/runtime production dependencies and retain it only for two trusted local report scripts. |
| `@types/dompurify`, `@types/markdown-it` | production dependencies | development dependencies | Correct package classification; no runtime behavior change. |

The Microsoft advisory lists 2.7.5 as the patched 2.x release: [GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc).

The official SheetJS installation guide identifies the npm-registry `xlsx` package as stale and provides the vendor-hosted 0.20.3 tarball used here: [SheetJS NodeJS installation](https://docs.sheetjs.com/docs/getting-started/installation/nodejs/). Version 0.20.3 is above the patched thresholds for the relevant prototype-pollution and ReDoS advisories: [GHSA-4r6h-8v6p-xvw6](https://github.com/advisories/GHSA-4r6h-8v6p-xvw6) and [GHSA-5pgg-2g8v-p4x9](https://github.com/advisories/GHSA-5pgg-2g8v-p4x9).

## 5. `xlsx` decision and parity evidence

Repository call-site inspection found `xlsx` only in:

- `convert_to_xlsx.js`;
- `generate_300_testcases.js`.

The browser application does not import `xlsx`. Product spreadsheet uploads already submit the file to the backend import path. The package is therefore development-only and is not exposed to user-supplied browser parsing.

Both local scripts now call `xlsx.set_fs(fs)` as required by the ESM write path. Parity smoke results with 0.20.3:

- direct workbook buffer: 15,936 bytes;
- `TestResults.xlsx`: 17,709 bytes;
- `TestCases.xlsx`: 92,061 bytes;
- `Qaly_300_TestCases_Report.xlsx`: approximately 158,982 bytes.

The smoke directory was removed after verification. No exception, containment expiry, or deferred replacement decision is open.

## 6. Reproducible command evidence

| Command | Result |
|---|---|
| `npm ci` | Pass; 249 packages installed; npm reported 0 vulnerabilities. |
| `npm run typecheck` | Pass. |
| `npm run build` | Pass with Vite 6.4.3; 3,903 modules transformed. |
| `dotnet restore Qaly_project.slnx` | Pass. |
| `dotnet build Qaly_project.slnx --configuration Release --no-restore` | Pass; 0 warnings, 0 errors. |
| `dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj` | Pass; 226/226, 0 skipped. |
| `dotnet test tests/Qaly.IntegrationTests/Qaly.IntegrationTests.csproj` | Pass; 24/24, 0 skipped. |
| `dotnet test tests/Qaly.WebFeatureTests/Qaly.WebFeatureTests.csproj` | Pass; 9/9, 0 skipped. |
| `dotnet list Qaly_project.slnx package --vulnerable --include-transitive` | Pass; no vulnerable package reported in any project. |
| `npm audit --omit=dev` | Pass; 0 total, Critical, High, or Moderate. |
| `npm audit` | Pass; 0 total, Critical, High, or Moderate in the full graph. |
| `git diff --check` | Pass after final documentation and generated-churn cleanup. |

`npm ci` emitted a deprecation warning for `lucide-vue-next@1.0.0`; it is not a reported security advisory and is not expanded into an unrelated package migration in P0-01.

## 7. Runtime route smoke

Runtime configuration used the local Development environment, in-memory database, offline AI, and an intentionally unavailable low-timeout Redis endpoint. The smoke verified:

1. `/projects/{projectId}` opens the intended project.
2. Direct navigation and refresh preserve `?tab=tasks&p001=final-smoke`.
3. `/projects/{projectId}/tasks/{taskId}` opens exactly one matching task dialog.
4. Task direct navigation and refresh preserve `projectId`, `taskId`, and query parameters.
5. Browser Back returns to the prior project URL and closes the task dialog.
6. An invalid project ID is canonically replaced with the accessible project ID while preserving the task ID and query parameters.
7. No new browser console error was recorded during the final or canonical smoke.

Redis fallback warnings and authenticated-request latency remain the known P0-05 resilience issue. They did not prevent route verification and were not modified here.

## 8. Files and artifacts changed

Behavior and dependency files:

- `src/Qaly.Web/ClientApp/App.vue`;
- `src/Qaly.Web/Qaly.Web.csproj`;
- `package.json` and `package-lock.json`;
- `convert_to_xlsx.js` and `generate_300_testcases.js`;
- required production output under `src/Qaly.Web/wwwroot/dist/`.

Evidence and governance files:

- this document;
- `00_README_Governance.md`;
- `10_Acceptance_Checklist_v4.0.csv`;
- `11_Risk_Register_v4.0.csv`;
- `12_Roadmap_and_Migration_Plan.md`.

No source for AI jobs, privacy, scheduling, versioning, Redis resilience, large-module refactoring, or other post-P0-01 work packages was changed.

## 9. Residual risk

- Hosted GitHub CI has not yet supplied independent required-check evidence.
- Route smoke is executed evidence but is not yet an automated frontend route test in CI.
- `node_modules` is tracked historically; changing that policy requires a dedicated migration.
- The production build still contains large `vendor-markdown` and meeting chunks; bundle optimization is outside P0-01.
- Redis degraded latency and existing EF global-query-filter warnings remain assigned to P0-05.
- The non-security `lucide-vue-next` deprecation warning remains open technical debt.

## 10. Rollback

Rollback is a single P0-01 change set:

1. revert the App route/watch changes and regenerated frontend assets together;
2. revert `Qaly.Web.csproj`, `package.json`, `package-lock.json`, and the two report-script compatibility changes together;
3. run `npm ci`, restore, typecheck, both builds, all three test projects, and both vulnerability scans;
4. restore this evidence and the related acceptance/risk/roadmap records to their prior state.

Do not keep a vulnerable dependency or failing typecheck by suppressing audit, NU1903, or a required check.
