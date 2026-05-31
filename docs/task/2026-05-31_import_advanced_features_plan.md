# Advanced Import Features Plan

Date: 2026-05-31

## Goal

Lập kế hoạch kỹ cho 3 tính năng import nâng cao còn rủi ro, theo hướng ít phá vỡ nhất:

1. Duplicate Handling mở rộng: `skip`, `overwrite`, `create-new`.
2. Partial Retry cho failed rows: sửa inline và retry từng dòng lỗi.
3. Import từ URL / Google Sheet: paste link và import không cần tải file thủ công.

Nguyên tắc triển khai:

- Không phá luồng import hiện tại: upload file -> parse -> mapping -> confirm -> execute -> result.
- Ưu tiên thêm API/DTO mới hoặc field optional thay vì đổi nghĩa field cũ.
- Mọi thay đổi dữ liệu task hiện hữu phải có audit trail và kiểm soát concurrency.
- Những phần rủi ro cao như URL import/background job phải bật theo feature flag hoặc triển khai sau khi có nền tảng lưu session/job ổn định.

## Current Import Capabilities

Runtime chính:

- Controller: `src/Qaly.Web/Controllers/ImportController.cs`
- Service: `src/Qaly.Application/Services/ImportService.cs`
- DTO: `src/Qaly.Application/DTOs/Import/ImportDtos.cs`
- Session entity: `src/Qaly.Domain/Entities/ImportSession.cs`
- Mapping UI: `src/Qaly.Web/ClientApp/components/import/ImportMappingStep.vue`
- Confirm UI: `src/Qaly.Web/ClientApp/components/import/ImportConfirmStep.vue`
- Upload/history UI: `src/Qaly.Web/ClientApp/components/import/ImportUploadStep.vue`
- Modal/result UI: `src/Qaly.Web/ClientApp/components/import/ImportModal.vue`
- Unit tests: `tests/Qaly.UnitTests/ImportEnhancementTests.cs`

Hiện đã có:

- Parse preview cho CSV, XLSX, TSV, DSV/TXT delimiter detection, PSV, JSON.
- Sheet selector cho XLSX.
- `firstRowIsHeader`.
- Field Mapping UI: user map cột file vào `Title`, `Description`, `Status`, `Priority`, `DueDate`, `EstimatedHours`, `Labels`, `Assignee`, hoặc `Skip`.
- Auto-suggest mapping theo header tiếng Anh/tiếng Việt.
- Validate bắt buộc có `Title`.
- Chống map trùng một target field.
- Default assignee, assign to current user nếu assignee trống.
- Default priority.
- Default Kanban status/column khi status trống.
- AI categorization cho status/priority/labels còn thiếu, có heuristic trước khi gọi AI.
- Duplicate handling mức cơ bản: `SkipDuplicates` bỏ qua task trùng title trong project.
- Import vào project hiện có hoặc tạo project mới.
- Bulk insert task/label trong một lần save.
- SortOrder append cuối từng cột Kanban.
- Import result có `ImportedCount`, `SkippedCount`, `FailedCount`, `DuplicateSkippedCount`, `NewLabelsCreated`, `UnmappedStatuses`, `StatusDistribution`, `SkippedRows`.
- Undo import trong 30 phút bằng `ImportSessionId`.
- Import history theo project qua `GET /api/import/sessions/{projectId}` và tab Completed.
- Template download CSV/XLSX.
- Document import phase đầu: markdown/text/html vào Wiki page.

Hiện chưa có hoặc chưa đủ:

- Duplicate `overwrite` hoặc `create-new`.
- Dry-run validation toàn bộ file với danh sách duplicate chính xác trước khi execute.
- Lưu chi tiết row payload/errors trong database để retry an toàn sau khi modal đóng.
- Retry failed rows từ result screen.
- Import source dạng URL/Google Sheet.
- Background job cho import dài hoặc nguồn mạng.
- Idempotency key/file hash để tránh import lặp do user bấm lại.

## Priority 1: Duplicate Handling Mở Rộng

### 1. Risks And Prerequisites

Rủi ro kỹ thuật:

- `overwrite` sẽ update task hiện hữu, không còn là import chỉ tạo mới. Cần audit trail rõ ràng.
- Task có `RowVersion`; nếu update không kiểm soát có thể ghi đè thay đổi mới của user khác.
- Task hiện có nhiều quan hệ: labels, assignee, comments, attachments, dependencies. Overwrite không được xóa nhầm dữ liệu phụ.
- Duplicate theo title có thể false positive. Hai task cùng title nhưng khác assignee/deadline vẫn có thể là hai task hợp lệ.
- Create-new cần đặt title/metadata rõ ràng để user hiểu vì sao bị nhân bản.
- Preview duplicate phải nhất quán với execute, tránh preview báo overwrite nhưng execute lại tạo mới do dữ liệu thay đổi giữa hai bước.

Điều kiện tiên quyết:

- Có policy duplicate rõ:
  - Match key phase đầu: normalized title trong cùng project.
  - Sau này mở rộng: external id/import key nếu file có cột ID.
- Có audit service dùng được cho `ImportOverwrite`.
- Có endpoint validate/dry-run hoặc logic preview duplicate trước execute.
- Có test cho concurrency và label merge.
- Có UX confirm danh sách duplicate để user không overwrite mù.

Khuyến nghị ít rủi ro:

- Phase 1 chỉ hỗ trợ duplicate mode toàn batch: `Skip`, `CreateNew`, `Overwrite`.
- Chưa làm per-row action ở phase đầu.
- `Overwrite` chỉ update các field được map và không null hóa field không map.
- Labels dùng merge, không replace, trừ khi sau này có option riêng.
- Không overwrite comments/attachments/dependencies.

### 2. Backend Design

DTO:

```csharp
public enum ImportDuplicateMode
{
    Skip,
    CreateNew,
    Overwrite
}

public record ImportRequest(
    Guid? ProjectId,
    string? NewProjectName,
    List<ColumnMapping> Mappings,
    bool FirstRowIsHeader,
    bool SkipDuplicates,
    string? SheetName,
    Guid? DefaultAssigneeId = null,
    bool AssignToMeIfEmpty = false,
    string? DefaultPriority = null,
    bool EnableAiCategorization = false,
    string? DefaultStatus = null,
    string DuplicateMode = "Skip"
);
```

Giữ `SkipDuplicates` để backward-compatible, nhưng UI mới sẽ gửi `DuplicateMode`. Mapping:

- `SkipDuplicates=true` và `DuplicateMode` null -> `Skip`.
- `SkipDuplicates=false` và `DuplicateMode` null -> `CreateNew` như behavior cũ.

DTO result mở rộng:

```csharp
public record DuplicateResolutionDto(
    int RowIndex,
    string Title,
    Guid ExistingTaskId,
    string ExistingTaskTitle,
    string Action,
    string? ConflictReason
);
```

Thêm vào `ImportResult`:

- `int OverwrittenCount`
- `int CreatedDuplicateCount`
- `List<DuplicateResolutionDto> DuplicateResolutions`

Service layer:

- Tách method:
  - `ResolveDuplicateMode(ImportRequest request)`
  - `FindDuplicateTaskAsync(projectId, title, ct)`
  - `ApplyOverwriteAsync(existingTask, parsedRow, mappedFields, sessionId, ct)`
  - `CreateImportedTaskAsync(...)`
- Khi mode `Overwrite`:
  - Load existing task by normalized title.
  - Update only mapped fields:
    - Title: giữ title mới nếu map title.
    - Description: update nếu map description; nếu cell trống thì chỉ clear khi có option `ClearEmptyMappedValues=true` ở tương lai. Phase đầu không clear.
    - Status/Priority/DueDate/EstimatedHours/Assignee: update nếu có data hoặc default value.
    - Labels: merge labels.
  - Set `UpdatedAt`.
  - Set `ImportSessionId`? Không nên đổi `ImportSessionId` của task gốc nếu field này đại diện session tạo task. Thay vào đó thêm audit details hoặc model mới sau.
  - Audit:
    - Action: `ImportOverwrite`.
    - Entity: `TaskItem`.
    - Payload: importSessionId, changed fields, old/new values, rowIndex.

Data model:

Phase ít rủi ro không cần migration bắt buộc. Nếu muốn trace đầy đủ:

```csharp
public class ImportSessionTaskChange : BaseEntity
{
    public Guid ImportSessionId { get; set; }
    public Guid TaskItemId { get; set; }
    public int RowIndex { get; set; }
    public string Action { get; set; } = "Created"; // Created, SkippedDuplicate, Overwritten, CreatedDuplicate
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}
```

Khuyến nghị:

- Phase 1A: không migration, audit log + result only.
- Phase 1B: thêm `ImportSessionTaskChange` nếu cần report/rollback overwrite.

API:

- Giữ `POST /api/import/execute`.
- Optional thêm `POST /api/import/validate` sau:
  - Trả duplicate list trước khi execute.
  - Không tạo task.
  - Không gọi AI mặc định.

### 3. Frontend Design

UI flow:

- Trong `ImportMappingStep.vue`, thay checkbox "Bỏ qua task trùng tên" bằng select/segmented control:
  - Skip duplicates
  - Create new cards
  - Overwrite existing cards
- Default là `Skip` nếu project đã có task; nếu tạo project mới có thể default `CreateNew` vì không có duplicate.
- Trong `ImportConfirmStep.vue`:
  - Hiển thị badge mode duplicate.
  - Nếu chưa có `/validate`, ghi rõ duplicate exact count chỉ hiển thị sau import.
  - Sau khi có `/validate`, hiển thị bảng duplicate preview: row, title, existing task, planned action.
- Result screen:
  - Thêm stat `Overwritten`, `Created duplicates`.
  - Detail list nhóm duplicate by action.

State:

- `duplicateMode = ref<'Skip' | 'CreateNew' | 'Overwrite'>('Skip')`
- Convert `skipDuplicates` cũ:
  - `skipDuplicates = duplicateMode === 'Skip'`
  - gửi thêm `duplicateMode`.

### 4. Implementation Steps

1. Thêm `DuplicateMode` optional vào DTO request.
2. Refactor `ImportService` để duplicate handling nằm trong switch rõ ràng.
3. Implement `CreateNew` rõ nghĩa:
   - Nếu duplicate vẫn tạo task mới.
   - Tăng `CreatedDuplicateCount`.
   - Add `DuplicateResolutionDto`.
4. Implement `Overwrite` bản an toàn:
   - Update only mapped fields.
   - Merge labels.
   - Không đụng comments/attachments/dependencies.
   - Audit từng task update.
5. Cập nhật result counters.
6. Cập nhật UI mapping step.
7. Cập nhật confirm/result UI.
8. Thêm unit tests.
9. Sau khi ổn, thêm `/api/import/validate` để preview duplicate trước execute.
10. Nếu cần rollback overwrite, thêm migration `ImportSessionTaskChanges`.

### 5. Edge Cases

- File có 2 dòng cùng title mới, nhưng DB chưa có title đó.
- DB có task title trùng khác chữ hoa/thường hoặc khoảng trắng.
- Existing task đang soft-deleted.
- Existing task user hiện tại không có quyền update.
- Overwrite status vi phạm workflow rule.
- Overwrite assignee không thuộc project.
- Overwrite due date parse fail.
- Duplicate row thiếu title.
- Label trong file trùng label existing khác casing.
- User import 2 lần cùng file do double click.
- Task bị update giữa validate và execute.

### 6. Tests

Unit tests:

- `DuplicateModeSkip_SkipsExistingTitle`.
- `DuplicateModeCreateNew_CreatesDuplicateAndCountsIt`.
- `DuplicateModeOverwrite_UpdatesOnlyMappedFields`.
- `DuplicateModeOverwrite_DoesNotClearUnmappedFields`.
- `DuplicateModeOverwrite_MergesLabels`.
- `DuplicateModeOverwrite_WhenAssigneeNotMember_ReturnsRowFailure`.
- `DuplicateModeOverwrite_WritesAuditLog`.
- `DuplicateModeBackwardCompatibility_SkipDuplicatesTrueMapsToSkip`.

Integration tests:

- `POST /api/import/execute` with `DuplicateMode=Overwrite` updates existing task.
- Unauthorized user cannot overwrite task in project.
- Concurrent update returns clear conflict once RowVersion handling is added.

## Priority 2: Partial Retry For Failed Rows

### 1. Risks And Prerequisites

Rủi ro kỹ thuật:

- Hiện `SkippedRows` chỉ trả row index/reason/category, chưa lưu raw row payload trong DB.
- Nếu modal đóng hoặc user refresh, không còn file gốc để retry.
- Retry phải dùng cùng mapping/options ban đầu hoặc user sẽ retry sai schema.
- Retry có thể tạo duplicate mới nếu một phần import đã thành công trước đó.
- Retry inline cần validate field giống execute, tránh frontend tự sửa nhưng backend hiểu khác.
- Nếu retry nhiều lần, cần idempotency để không tạo trùng.

Điều kiện tiên quyết:

- Lưu row-level errors/payload vào import session hoặc bảng con.
- Có trạng thái retry: failed, edited, retrying, retried, ignored.
- Có API retry nhận row IDs, không chỉ row indexes.
- Có service parse row payload -> task giống execute flow.
- Có UI result table đủ ổn định để edit.

Khuyến nghị ít rủi ro:

- Phase 1 chỉ retry trong cùng modal/session ngay sau import, với failed rows backend đã serialize.
- Retry chỉ hỗ trợ failed rows category `Failed`, không retry duplicate skipped.
- Retry tạo task mới, không overwrite.
- Mỗi failed row có ID riêng; retry thành công đánh dấu `Retried`.
- Không cần upload file lại.

### 2. Backend Design

Data model đề xuất:

```csharp
public class ImportSessionRow : BaseEntity
{
    public Guid ImportSessionId { get; set; }
    public int RowIndex { get; set; }
    public string Status { get; set; } = "Failed"; // Failed, Retried, Ignored
    public string Category { get; set; } = "Failed";
    public string Reason { get; set; } = string.Empty;
    public string RawRowJson { get; set; } = "[]";
    public string MappedPayloadJson { get; set; } = "{}";
    public Guid? CreatedTaskId { get; set; }
    public DateTimeOffset? RetriedAt { get; set; }
}
```

Mở rộng `ImportSession`:

- `MappingJson`
- `OptionsJson`
- `FileHash`
- `SchemaVersion`

Nếu muốn giảm migration phase đầu:

- Chỉ thêm `ErrorsJson` vào `ImportSession`.
- Nhưng về lâu dài bảng `ImportSessionRows` dễ query/test/retry hơn.

DTO:

```csharp
public record ImportFailedRowDto(
    Guid Id,
    int RowIndex,
    string Reason,
    string Category,
    Dictionary<string, string?> Values,
    string Status
);

public record RetryImportRowsRequest(
    List<RetryImportRowEditDto> Rows,
    bool SkipDuplicates = true
);

public record RetryImportRowEditDto(
    Guid ImportSessionRowId,
    Dictionary<string, string?> Values
);

public record RetryImportRowsResult(
    int RetriedCount,
    int FailedCount,
    List<SkippedRowDto> FailedRows,
    List<Guid> CreatedTaskIds
);
```

API:

- `GET /api/import/sessions/{sessionId}/failed-rows`
  - Return rows user can retry.
- `POST /api/import/sessions/{sessionId}/retry-rows`
  - Retry edited rows.
- Optional:
  - `POST /api/import/sessions/{sessionId}/failed-rows/{rowId}/ignore`

Service layer:

- Tách reusable row import function:
  - `BuildTaskDraftFromRow(row, fieldMap, options)`
  - `ValidateTaskDraft(draft)`
  - `CommitTaskDraft(draft, sessionId)`
- `ExecuteImportAsync` khi fail:
  - Add `ImportSessionRow`.
  - Store raw row and mapped values.
- `RetryRowsAsync`:
  - Verify session belongs to current user/project access.
  - Verify row status is `Failed`.
  - Merge edited values into mapped payload.
  - Validate and create task.
  - Update `ImportSessionRow.Status`.
  - Update session counters carefully:
    - `ImportedCount += retriedSuccess`
    - `SkippedCount -= retriedSuccess`
    - `FailedCount` should live in stats json or recompute from rows.

Important design decision:

- Do not mutate original file.
- Do not re-run full import.
- Retry rows should use stored session options, not current UI defaults.

### 3. Frontend Design

UI flow:

- Result step shows "Failed rows" table when `failedCount > 0`.
- Each row:
  - checkbox
  - row index
  - reason
  - editable fields: Title, Status, Priority, DueDate, EstimatedHours, Assignee, Labels
  - validation hints inline
- Actions:
  - Retry selected
  - Retry all fixed
  - Ignore selected
  - Export failed CSV as fallback

Components:

- `ImportFailedRowsEditor.vue`
- `ImportRetryResultBanner.vue`

State:

- `failedRows = ref<ImportFailedRow[]>([])`
- `selectedFailedRowIds = ref<Set<string>>()`
- `editedRows = reactive<Record<string, Record<string, string>>>()`
- `retryStatusByRowId`

UX constraints:

- Do not show massive inline editor for thousands of rows. Cap initial render, paginate at 50 rows.
- Save user edits locally until retry.
- Disable retry if selected row still missing title.
- Show retry result without closing modal.

### 4. Implementation Steps

1. Add data model `ImportSessionRow` and EF configuration.
2. Add migration.
3. Store mapping/options JSON in `ImportSession`.
4. During execute, persist failed row payload.
5. Add `GET failed-rows` endpoint.
6. Add retry DTOs and `RetryRowsAsync`.
7. Refactor row import logic so execute and retry share code.
8. Add result UI failed row editor.
9. Add retry selected/all fixed.
10. Add tests for retry success/failure.
11. Add optional failed CSV export if inline edit is too large.

### 5. Edge Cases

- User retries row that was already retried.
- User retries stale row after session was undone.
- Import session older than retention window.
- Original project was deleted/archived.
- Assignee edited to a user outside project.
- Status edited to invalid value.
- Due date format invalid.
- Retry row creates duplicate of a task already imported from another row.
- Retry selected rows partially succeed.
- User lacks permission after role changed.
- Session created before `ImportSessionRows` migration.

### 6. Tests

Unit tests:

- `ExecuteImportAsync_PersistsFailedRowPayload`.
- `GetFailedRowsAsync_ReturnsOnlyRetryableRows`.
- `RetryRowsAsync_WithFixedTitle_CreatesTask`.
- `RetryRowsAsync_AlreadyRetriedRow_IsRejected`.
- `RetryRowsAsync_InvalidEditedStatus_ReturnsFailedRow`.
- `RetryRowsAsync_UpdatesSessionCounts`.
- `RetryRowsAsync_DoesNotRequireOriginalFile`.

Integration tests:

- Import with missing title -> result contains failed row -> retry with title -> task created.
- Unauthorized user cannot fetch/retry another user's session row.
- Undo after retry deletes tasks created by retry if they are linked to same session.

## Priority 3: Import From URL / Google Sheet

### 1. Risks And Prerequisites

Rủi ro kỹ thuật:

- Network timeout làm request HTTP treo nếu xử lý đồng bộ.
- Google Sheet private cần OAuth hoặc service account, không thể chỉ paste link.
- Public Google Sheet có nhiều dạng URL và export endpoint khác nhau.
- File từ URL có thể quá lớn, sai content type, redirect nhiều lần, hoặc độc hại.
- Server-side request forgery (SSRF) nếu cho backend fetch URL tự do.
- Import từ URL có thể chậm, cần progress/background job.
- Format nguồn có thể thay đổi giữa parse và execute.

Điều kiện tiên quyết:

- Có allowlist URL/host hoặc SSRF guard.
- Có HTTP client timeout/retry policy.
- Có max file size giống upload.
- Có source snapshot lưu tạm hoặc hash để execute dùng đúng dữ liệu đã parse.
- Có background job nếu download/parse lâu.
- Có UI hiển thị source type và lỗi network rõ.

Khuyến nghị ít rủi ro:

- Phase 1 chỉ hỗ trợ public direct file URL `.csv`, `.tsv`, `.xlsx`, `.json`.
- Google Sheet phase 1 chỉ hỗ trợ public share link, convert sang CSV export.
- Không hỗ trợ private Google OAuth ở phase đầu.
- Download source vào temp storage/file storage, sau đó reuse flow parse hiện tại.
- Không execute trực tiếp từ URL live; execute từ snapshot đã parse.

### 2. Backend Design

Data model:

```csharp
public class ImportSourceSnapshot : BaseEntity
{
    public Guid UserId { get; set; }
    public string SourceType { get; set; } = "Url"; // Url, GoogleSheet
    public string SourceUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileHash { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}
```

DTO:

```csharp
public record ImportFromUrlParseRequest(
    string Url,
    string SourceType = "Auto",
    string? SheetName = null,
    bool FirstRowIsHeader = true
);

public record ParsedUrlImportResult(
    Guid SnapshotId,
    ParsedFileResult ParsedFile
);

public record ExecuteSnapshotImportRequest(
    Guid SnapshotId,
    ImportRequest ImportRequest
);
```

API:

- `POST /api/import/sources/parse-url`
  - Download source.
  - Store snapshot.
  - Parse snapshot with existing parser.
  - Return `SnapshotId + ParsedFileResult`.
- `POST /api/import/sources/execute`
  - Load snapshot by ID.
  - Execute import with existing `ExecuteImportAsync` stream.
- Optional:
  - `DELETE /api/import/sources/{snapshotId}` cleanup.

Service layer:

- New service `IImportSourceService`:
  - `ParseFromUrlAsync(request, ct)`
  - `ExecuteFromSnapshotAsync(request, ct)`
- Helper:
  - `NormalizeGoogleSheetUrl(url)`
  - `ValidateImportUrl(url)`
  - `DownloadWithLimitsAsync(url, ct)`
  - `StoreSnapshotAsync(stream, metadata, ct)`

SSRF/network guard:

- Only allow `http` and `https`.
- Block localhost/private IP ranges:
  - `127.0.0.0/8`
  - `10.0.0.0/8`
  - `172.16.0.0/12`
  - `192.168.0.0/16`
  - `169.254.0.0/16`
  - IPv6 loopback/link-local/private.
- Limit redirects to 3.
- Max file size 5 MB phase đầu.
- Timeout 10-15 seconds for parse request.
- Content type/extension allowlist.

Google Sheet public export:

- Input forms:
  - `https://docs.google.com/spreadsheets/d/{spreadsheetId}/edit#gid={gid}`
  - `https://docs.google.com/spreadsheets/d/{spreadsheetId}/view`
- Convert to:
  - `https://docs.google.com/spreadsheets/d/{spreadsheetId}/export?format=csv&gid={gid}`
- If multiple sheets are needed later, use Sheets API with OAuth, not export hack.

Background job direction:

- Phase 1 synchronous for <=5 MB direct/public URL.
- Phase 2:
  - `POST /api/import/jobs/from-url`
  - returns `jobId`
  - worker downloads/parses
  - UI polls or SignalR progress.

### 3. Frontend Design

UI flow:

- In `ImportUploadStep.vue`, add source tabs:
  - File upload
  - URL / Google Sheet
- URL tab:
  - input URL
  - source type auto/direct file/google sheet
  - first row is header
  - button "Fetch preview"
- After parse:
  - Reuse existing mapping step.
  - Store `snapshotId`.
- Execute:
  - If `snapshotId` exists, call `/api/import/sources/execute`.
  - Else use current multipart `/api/import/execute`.

Components/state:

- `ImportSourceUrlStep.vue`
- In `ImportModal.vue`:
  - `sourceMode = ref<'file' | 'url'>('file')`
  - `sourceSnapshotId = ref<string | null>(null)`
  - `sourceUrl = ref('')`
  - `file = null` for URL flow

UX:

- Show exact fetched file name/type/size/hash after parse.
- Warn: "QALY imports the snapshot just fetched; if the source changes, fetch preview again."
- Clear error states:
  - URL not public
  - file too large
  - unsupported format
  - timeout
  - blocked internal/private URL

### 4. Implementation Steps

1. Add `ImportSourceSnapshot` entity and migration.
2. Add `IImportSourceService` and implementation.
3. Add URL validation and SSRF guard tests first.
4. Add direct public URL download with size/timeout limits.
5. Store snapshot via existing file storage or local temp storage.
6. Reuse `ImportService.ParseFileAsync` on snapshot stream.
7. Add `parse-url` API.
8. Add `execute snapshot` API.
9. Add frontend URL tab and state.
10. Add Google Sheet public URL normalization.
11. Add integration tests with fake HTTP handler/test server.
12. Add cleanup job for expired snapshots.

### 5. Edge Cases

- URL redirects to private IP.
- URL has no extension but valid content type.
- URL extension says CSV but content is HTML login page.
- Google Sheet is private and returns login HTML.
- Google Sheet has Vietnamese headers and comma/newline in cells.
- Google Sheet gid missing.
- Source changes after preview.
- Snapshot expired before execute.
- User tries execute snapshot owned by another user.
- Network timeout.
- Partial download exceeds file size limit.
- XLSX has multiple sheets.
- CSV encoding has BOM or non-UTF8.

### 6. Tests

Unit tests:

- `ValidateImportUrl_BlocksLocalhost`.
- `ValidateImportUrl_BlocksPrivateIp`.
- `ValidateImportUrl_AllowsHttpsPublicHost`.
- `NormalizeGoogleSheetUrl_ConvertsEditLinkToCsvExport`.
- `DownloadWithLimits_StopsWhenFileTooLarge`.
- `ParseFromUrlAsync_PrivateGoogleSheet_ReturnsHelpfulError`.

Integration tests:

- Public CSV URL -> parse preview -> execute snapshot -> tasks created.
- Snapshot owned by user A cannot be executed by user B.
- Snapshot expired returns 410/400 with clear error.
- Redirect to private IP is blocked.

## Suggested Rollout Order

### Milestone A: Duplicate Modes Without Migration

Scope:

- Add `DuplicateMode`.
- Support `Skip`, `CreateNew`, `Overwrite`.
- Audit overwrite.
- Result counters.

Why first:

- Builds on current import flow.
- No new storage required.
- Highest business value after existing skip duplicate.

Rollback:

- Hide UI mode selector.
- Backend defaults to current `SkipDuplicates` behavior.

### Milestone B: Validate Endpoint

Scope:

- `POST /api/import/validate`.
- Full file parse with mapping/options.
- Return exact duplicate/failure preview.
- No task creation.

Why before partial retry:

- Provides reusable row validation logic.
- Reduces surprise before overwrite/retry.

Rollback:

- UI can stop calling validate; execute flow still works.

### Milestone C: Persist Failed Rows And Retry

Scope:

- Add `ImportSessionRow`.
- Store failed row payload.
- Retry selected rows.
- Inline editor in result screen.

Why after validate:

- Needs row-level validation/service refactor.
- More database/state complexity.

Rollback:

- Keep persisted rows but hide retry UI.
- Existing import result still works.

### Milestone D: URL Snapshot Import

Scope:

- Direct public file URL.
- Public Google Sheet CSV export.
- Snapshot parse/execute.
- SSRF guard and timeout.

Why last:

- Highest security/network complexity.
- Needs snapshot concept and cleanup.

Rollback:

- Disable URL tab by feature flag.
- Existing upload flow unaffected.

## Non-Goals For First Pass

- Private Google Sheet OAuth.
- Scheduled/recurring import.
- Per-row duplicate action matrix.
- Replacing labels/comments/attachments during overwrite.
- Import files larger than current 5 MB limit.
- Long-running background import progress for normal upload.

## Final Acceptance Checklist

Duplicate Handling:

- User can choose skip/create-new/overwrite.
- Overwrite updates only mapped fields.
- Audit log records every overwritten task.
- Result separates imported, skipped duplicate, created duplicate, overwritten, failed.

Partial Retry:

- Failed row payload persists after result screen.
- User can edit failed row values inline.
- Retry selected creates tasks without re-uploading file.
- Retried rows cannot be retried twice accidentally.

URL / Google Sheet:

- Public CSV/XLSX/JSON URL can be parsed into existing mapping flow.
- Public Google Sheet share link can be parsed as CSV.
- Private/internal/oversized/timeout URLs fail safely with clear messages.
- Execute uses stored snapshot, not a fresh network fetch.
