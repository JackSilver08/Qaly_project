# Import Result Visibility & Report Plan

Date: 2026-05-30

## Goal

Make each task-table import explain exactly what happened in that run:

- How many rows were read.
- How many tasks were imported.
- How many rows failed.
- How many rows were skipped intentionally, especially duplicates.
- Which rows need user action.

This plan also fixes small format mismatches and turns the existing import history endpoint into visible UI.

## Current Flow

Runtime files:

- Backend controller: `src/Qaly.Web/Controllers/ImportController.cs`
- Backend service: `src/Qaly.Application/Services/ImportService.cs`
- DTOs: `src/Qaly.Application/DTOs/Import/ImportDtos.cs`
- Session entity: `src/Qaly.Domain/Entities/ImportSession.cs`
- UI modal: `src/Qaly.Web/ClientApp/components/import/ImportModal.vue`
- Upload/history tab: `src/Qaly.Web/ClientApp/components/import/ImportUploadStep.vue`
- Confirm step: `src/Qaly.Web/ClientApp/components/import/ImportConfirmStep.vue`
- Undo banner: `src/Qaly.Web/ClientApp/components/import/ImportUndoBanner.vue`

Flow:

1. UI uploads file to `/api/import/parse`.
2. Backend parses headers, preview rows, total row count, sheet list, and mapping suggestions.
3. UI lets user map columns and options.
4. UI sends file again plus import request to `/api/import/execute`.
5. Backend creates `ImportSession`, imports valid rows, skips invalid/duplicate rows, updates counters, and returns `ImportResult`.
6. UI shows result and optional undo.

## Problems To Fix Now

1. `.dsv` is accepted by UI but rejected by backend.
2. `.xls` is advertised by UI but not supported by ClosedXML flow.
3. Result language mixes failed rows and intentionally skipped duplicate rows into one `SkippedCount`.
4. Toast and undo banner only show imported count, so users miss failed/skipped count.
5. Confirm step estimates from five preview rows only; it must be labelled as estimate, not exact validation.
6. Completed tab says history exists but does not call the existing `GET /api/import/sessions/{projectId}` endpoint.

## Decision: Failed vs Skipped

Use two separate concepts:

- `FailedCount`: rows that could not be imported because the data is invalid or missing required fields.
- `DuplicateSkippedCount`: rows intentionally skipped because user enabled duplicate protection.
- `SkippedCount`: backward-compatible total of all non-imported rows.

`SkippedRows` should carry a category:

- `Failed`
- `Duplicate`
- future: `Warning`, `Unsupported`, `Permission`

No database migration is required for the first implementation because `ImportSession.SkippedCount` can continue storing the total non-imported rows. A future report migration can persist per-category counts.

## Dry-Run Decision

Do not run AI categorization during dry-run validation by default.

Reason:

- A full dry-run that calls AI can cost almost the same time and resources as import.
- The current local AI path chunks rows and delays between batches, so validating 100+ rows can become slow.

Future endpoint should be:

- `POST /api/import/validate`
- Parse and validate all rows.
- Return row-level issues and exact counts.
- Do not create tasks.
- Do not call AI unless request explicitly sends `includeAiPreview=true`.
- Cache validation by `fileHash + mappingHash + optionsHash` for a short TTL.

Hash must include:

- File content hash.
- Sheet name.
- First-row-is-header.
- Mapping list.
- Skip duplicates flag.
- Default assignee/default priority.
- Target project.

## Report Model Direction

Current DTO is enough for the result screen, but not enough for long-term reports. Future `ImportSession` should gain:

- `Status`: pending, in_progress, completed, partial, failed.
- `TargetKind`: task_table, page, bundle.
- `FileType`, `FileSize`, `StartedAt`, `CompletedAt`.
- `StatsJson`: imported, failed, duplicateSkipped, warnings, newLabelsCreated, statusDistribution.
- `ErrorsJson`: row/file issue list.

For ZIP or multi-file import, add `ImportFileResult` later.

## Implementation Tasks

1. Fix accepted table extensions.
   - Add backend `.dsv` support with delimiter auto-detection.
   - Stop advertising `.xls` until a real parser exists.

2. Split result counters.
   - Add `FailedCount` and `DuplicateSkippedCount` to `ImportResult`.
   - Add `Category` to `SkippedRowDto`.
   - Keep `SkippedCount` as compatibility total.

3. Improve result UI.
   - Show `Imported`, `Failed`, `Skipped duplicate`, `Total rows`.
   - Toast should report all important counts.
   - Skipped row detail should group or label category.

4. Improve undo banner.
   - Show imported count plus failed/skipped counts for the last run.

5. Use import history.
   - Load `GET /api/import/sessions/{projectId}` when project import modal opens.
   - Render sessions in Completed tab.

6. Keep confirm honest.
   - Label counts as estimates from preview.
   - Mention exact counts appear after import until full validate endpoint is implemented.

## Later Tasks

1. Add `/api/import/validate` without AI by default.
2. Add cached validation keyed by file hash plus mapping/options hash.
3. Add downloadable failed-row CSV.
4. Persist per-category report in `ImportSession`.
5. Add partial import policy: import valid rows vs stop all on first error.
6. Add idempotency guard using file hash plus mapping/options hash.

## Acceptance Criteria For This Pass

- `.dsv` file no longer fails at backend format validation.
- UI no longer promises `.xls`.
- After import, users see imported, failed, duplicate-skipped, and total counts.
- Undo banner keeps the same 30-minute behavior and includes failed/skipped context.
- Completed tab shows recent sessions when importing inside a project.
- Existing import tests still pass.
