# 📥 Đặc tả & Kế hoạch triển khai — Import CSV/XLSX vào Kanban Board

> **Ngày tạo:** 13/05/2026 | **Phiên bản:** 1.0  
> **Tham chiếu:** Mô hình Notion Import CSV → Database  
> **Dự án:** Qaly – Task Management System

---

## I. PHÂN TÍCH KHẢ THI TRÊN QALY

### 1.1 Mapping mô hình Notion → Qaly

| Khái niệm Notion | Tương đương Qaly | Ghi chú |
|---|---|---|
| Database | `Project` | Một dự án chứa nhiều task |
| Database Item (Row) | `TaskItem` | Mỗi dòng CSV → 1 task |
| Property "Title" | `TaskItem.Title` | Bắt buộc, cột đầu tiên |
| Property "Status" | `TaskItem.Status` | Map vào cột Kanban: `Todo`, `InProgress`, `OnHold`, `InReview`, `Done` |
| Property "Select" (Priority) | `TaskItem.Priority` | `Low`, `Medium`, `High`, `Critical` |
| Property "Date" | `TaskItem.DueDate` | Hạn chót |
| Property "Text" (Description) | `TaskItem.Description` | Mô tả chi tiết |
| Property "Multi-select" (Tags) | `ProjectLabel` + `TaskLabel` | Nhãn gắn cho task |
| Property "Number" | `TaskItem.EstimatedHours` | Giờ ước tính |
| Kanban Column | Giá trị của `TaskItem.Status` | Quyết định card nằm cột nào |

### 1.2 Đánh giá khả thi

| Tiêu chí | Đánh giá | Chi tiết |
|---|---|---|
| Domain Model | ✅ Sẵn sàng | `TaskItem`, `ProjectLabel`, `TaskLabel` đã có đầy đủ |
| Kanban Status | ✅ Sẵn sàng | Đã có `statusColumns = ['Todo','InProgress','OnHold','InReview','Done']` |
| Label system | ✅ Sẵn sàng | `ProjectLabel` (M:N) với `TaskLabel` join table |
| File parsing | ⚠️ Cần thêm | Chưa có thư viện CSV parser – cần thêm `CsvHelper` |
| XLSX support | ✅ Có sẵn | Đã có `ClosedXML` trong `Qaly.Application.csproj` |
| Batch create | ⚠️ Cần viết | Hiện `TaskService.CreateAsync` tạo từng task đơn lẻ |
| Rollback | ⚠️ Cần viết | Cần entity `ImportSession` để hỗ trợ undo |

> **Kết luận:** Hoàn toàn khả thi. Hạ tầng domain (entities, labels, status) đã sẵn sàng. Chỉ cần bổ sung: parser, service layer mới, 1 entity tracking, và 1 component Vue.

---

## II. ĐẶC TẢ TÍNH NĂNG CHI TIẾT

### 2.1 Hai luồng Import (Entry Points)

```
┌───────────────────────────────────────────┐
│          Người dùng click "Import"        │
└─────────────────┬─────────────────────────┘
                  │
        ┌─────────▼─────────┐
        │ Tạo dự án mới từ  │    ┌──────────────────────┐
        │ file CSV/XLSX?     ├───►│ Flow 1: New Project  │
        │                   │    │ → Tạo Project mới    │
        │       hay         │    │ → Import tasks vào   │
        │                   │    └──────────────────────┘
        │ Thêm vào dự án    │    ┌──────────────────────┐
        │ hiện có?           ├───►│ Flow 2: Merge        │
        │                   │    │ → Chọn dự án đích    │
        │                   │    │ → Append tasks mới   │
        └───────────────────┘    └──────────────────────┘
```

**Flow 1 — Tạo Project mới từ file:**
- UI trên trang `/projects` → nút "Import từ CSV"
- Hệ thống tạo `Project` mới với tên lấy từ tên file (hoặc người dùng nhập)
- Tất cả dòng CSV trở thành `TaskItem` trong project mới
- `Project.OwnerId` = user hiện tại, tự thêm user làm `ProjectMember(Role=Owner)`

**Flow 2 — Merge vào Project đã có:**
- UI trên trang `/projects/:id` → tab Tasks → nút "Import thêm"
- Chỉ append thêm task mới, **không** update/xóa task cũ (giống Notion)
- Cảnh báo trước khi confirm: *"Import sẽ thêm X card mới. Card cũ không bị thay đổi."*

### 2.2 Upload File

| Thuộc tính | Giá trị |
|---|---|
| Định dạng | `.csv`, `.xlsx`, `.tsv` |
| Giới hạn dung lượng | **5 MB** |
| Giới hạn dòng | **2000 dòng** (tránh overload) |
| Encoding | Auto-detect, ưu tiên UTF-8. Cảnh báo nếu phát hiện encoding khác (quan trọng cho tiếng Việt) |
| Phương thức | Drag & drop + Click chọn file |
| XLSX multi-sheet | Cho phép chọn sheet (Notion chỉ lấy sheet đầu) |

### 2.3 Mapping cột — Bước trung tâm

Sau khi upload, hiển thị màn hình mapping với:

#### Preview Data
- Hiển thị **5 dòng đầu** của file để người dùng xác nhận dữ liệu
- Toggle **"Dòng đầu là header"** — mặc định BẬT

#### Bảng Mapping

| Cột CSV | Qaly Field (Dropdown) | Kiểu dữ liệu | Bắt buộc |
|---|---|---|---|
| *Tự detect header* | **Tiêu đề (Title)** | Text | ✅ Bắt buộc |
| *Tự detect* | Mô tả (Description) | Text | Không |
| *Tự detect* | **Cột Kanban (Status)** | Select | Không (mặc định `Todo`) |
| *Tự detect* | Độ ưu tiên (Priority) | Select | Không (mặc định `Medium`) |
| *Tự detect* | Hạn chót (DueDate) | Date | Không |
| *Tự detect* | Giờ ước tính (EstimatedHours) | Number | Không |
| *Tự detect* | Nhãn (Labels) | Multi-select | Không |
| *Tự detect* | *(Bỏ qua)* | — | — |

#### Auto-suggest logic
Hệ thống tự gợi ý mapping dựa trên tên header (case-insensitive):

```
"title", "tên", "name", "task", "tiêu đề"          → Title
"description", "mô tả", "desc", "chi tiết"          → Description
"status", "trạng thái", "column", "cột"              → Status
"priority", "ưu tiên", "độ ưu tiên", "mức độ"       → Priority
"due", "deadline", "hạn", "hạn chót", "due_date"    → DueDate
"hours", "estimate", "giờ", "ước tính"               → EstimatedHours
"label", "tag", "nhãn", "tags", "labels"             → Labels
```

### 2.4 Xử lý Status không khớp

Khi CSV chứa giá trị Status mà Qaly chưa có (ví dụ `"QA Testing"`):

```
Chiến lược: Tự động map vào cột gần nhất HOẶC fallback về "Todo"
```

| Giá trị CSV | Map thành | Lý do |
|---|---|---|
| `todo`, `backlog`, `new`, `mới` | `Todo` | Keyword match |
| `in progress`, `doing`, `đang làm`, `wip` | `InProgress` | Keyword match |
| `review`, `in review`, `đang review` | `InReview` | Keyword match |
| `done`, `completed`, `hoàn thành`, `xong` | `Done` | Keyword match |
| `hold`, `on hold`, `blocked`, `tạm dừng` | `OnHold` | Keyword match |
| `QA Testing` (không match) | `Todo` + cảnh báo | Fallback + thông báo cho user |

Hiển thị danh sách "Giá trị không nhận diện được" cho user review trước khi confirm.

### 2.5 Xử lý Labels (Multi-select)

- Mỗi ô Labels có thể chứa nhiều giá trị phân tách bằng `,` hoặc `;`
- Ví dụ: `"Bug, Frontend, Urgent"` → 3 labels
- Nếu label chưa tồn tại trong `ProjectLabel` → **tự tạo mới** với màu ngẫu nhiên
- Nếu đã có → reuse `ProjectLabel.Id` có sẵn

### 2.6 Xử lý Duplicate (Cải tiến so với Notion)

Notion không detect duplicate. Qaly sẽ làm tốt hơn:

- **Mặc định:** Append tất cả (giống Notion — an toàn)
- **Tùy chọn nâng cao:** Toggle "Bỏ qua task trùng tên trong cùng Status"
  - So sánh `Title` (case-insensitive, trim whitespace)
  - Nếu trùng → skip dòng đó và đếm vào "Đã bỏ qua X task trùng"

### 2.7 Preview trước khi Confirm

Trước khi import, hiển thị bảng tổng kết:

```
┌──────────────────────────────────────────┐
│         📋 Xác nhận Import               │
├──────────────────────────────────────────┤
│ Tổng số dòng đọc được:        47        │
│ Tasks sẽ được tạo:            45        │
│ Dòng bỏ qua (trùng/lỗi):      2        │
│ Labels mới sẽ tạo:             3        │
│ Status không nhận diện:        1        │
│                                          │
│ Phân bố theo cột Kanban:                 │
│   📌 Todo:         12                    │
│   🔄 InProgress:   15                    │
│   ⏸️ OnHold:        3                    │
│   👀 InReview:      8                    │
│   ✅ Done:          7                    │
│                                          │
│ ⚠️ Card cũ không bị thay đổi.           │
│                                          │
│    [ Hủy ]              [ Import Now ]   │
└──────────────────────────────────────────┘
```

### 2.8 Rollback (Undo Import)

Lưu `ImportSession` vào DB để hỗ trợ undo:
- Mỗi lần import tạo 1 record `ImportSession`
- Tất cả `TaskItem` tạo ra đều gắn `ImportSessionId`
- Trong vòng **30 phút** sau import, user có thể bấm "Undo Import" → xóa batch tất cả task thuộc session đó
- Sau 30 phút, nút undo ẩn đi (task đã trở thành data chính thức)

---

## III. THIẾT KẾ KỸ THUẬT

### 3.1 Entity mới: `ImportSession`

```csharp
// Qaly.Domain/Entities/ImportSession.cs
public class ImportSession : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    
    public string FileName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public bool IsUndone { get; set; } = false;
    
    // Navigation
    public Project Project { get; set; } = null!;
    public User User { get; set; } = null!;
}
```

**Thay đổi `TaskItem`**: Thêm FK optional:

```csharp
// Thêm vào TaskItem.cs
public Guid? ImportSessionId { get; set; }
public ImportSession? ImportSession { get; set; }
```

### 3.2 DTOs

```csharp
// Qaly.Application/DTOs/Import/ImportDtos.cs

/// Kết quả parse file lên — trả về cho frontend hiển thị mapping UI
public record ParsedFileResult(
    string FileName,
    List<string> Headers,              // Tên các cột
    List<List<string>> PreviewRows,    // 5 dòng đầu
    int TotalRowCount,
    List<ColumnMappingSuggestion> Suggestions,
    List<string>? SheetNames           // null nếu CSV
);

public record ColumnMappingSuggestion(
    int ColumnIndex,
    string HeaderName,
    string? SuggestedField             // "Title", "Status", "Priority", v.v.
);

/// Frontend gửi lên khi user confirm mapping
public record ImportRequest(
    Guid? ProjectId,                   // null = tạo project mới
    string? NewProjectName,            // Tên project mới (Flow 1)
    List<ColumnMapping> Mappings,
    bool FirstRowIsHeader,
    bool SkipDuplicates,
    string? SheetName                  // Chọn sheet (XLSX)
);

public record ColumnMapping(
    int ColumnIndex,
    string TargetField                 // "Title" | "Description" | "Status" | ...
);

/// Kết quả import trả về
public record ImportResult(
    Guid ImportSessionId,
    Guid ProjectId,
    int TotalRows,
    int ImportedCount,
    int SkippedCount,
    int NewLabelsCreated,
    List<string> UnmappedStatuses,     // Status không nhận diện
    Dictionary<string, int> StatusDistribution
);

public record ImportSessionDto(
    Guid Id,
    string FileName,
    int ImportedCount,
    int SkippedCount,
    bool IsUndone,
    bool CanUndo,                      // CreatedAt + 30 phút > Now
    DateTimeOffset CreatedAt
);
```

### 3.3 Service Interface

```csharp
// Qaly.Application/Common/Interfaces/IImportService.cs

public interface IImportService
{
    /// Bước 1: Parse file và trả về preview + gợi ý mapping
    Task<Result<ParsedFileResult>> ParseFileAsync(
        Stream fileStream, string fileName, CancellationToken ct = default);
    
    /// Bước 2: Thực hiện import với mapping đã confirm
    Task<Result<ImportResult>> ExecuteImportAsync(
        Stream fileStream, string fileName, ImportRequest request, 
        CancellationToken ct = default);
    
    /// Undo một import session
    Task<Result<int>> UndoImportAsync(Guid importSessionId, CancellationToken ct = default);
    
    /// Lấy danh sách import sessions của 1 project
    Task<Result<List<ImportSessionDto>>> GetSessionsAsync(
        Guid projectId, CancellationToken ct = default);
}
```

### 3.4 Controller API

```
POST   /api/import/parse                ← Upload file, nhận preview + suggestions
POST   /api/import/execute              ← Confirm mapping, thực hiện import
DELETE /api/import/sessions/{id}        ← Undo import session
GET    /api/import/sessions/{projectId} ← Lịch sử import của project
```

### 3.5 Luồng xử lý Backend (Sequence)

```
1. User upload file
   → POST /api/import/parse (multipart/form-data)
   → ImportService.ParseFileAsync()
   → Detect format (CSV/XLSX/TSV) bằng extension
   → CSV: CsvHelper.Read() / XLSX: ClosedXML workbook.Open()
   → Đọc headers + 5 preview rows + count total rows
   → Auto-suggest mappings dựa trên header names
   → Return ParsedFileResult

2. User review mapping, adjust dropdowns, confirm
   → POST /api/import/execute (file + ImportRequest JSON)
   → ImportService.ExecuteImportAsync()
   
   2a. Nếu ProjectId == null (Flow 1: New Project)
       → Tạo Project mới (name = NewProjectName ?? fileName)
       → Tạo ProjectMember (UserId = currentUser, Role = Owner)
   
   2b. Tạo ImportSession record
   
   2c. Loop qua từng dòng:
       → Parse row theo ColumnMapping
       → Map Status → valid Kanban column (StatusMapper)
       → Check duplicate nếu SkipDuplicates = true
       → Parse Labels (split by "," or ";")
       → Tạo ProjectLabel nếu chưa tồn tại
       → Tạo TaskItem (gắn ImportSessionId)
       → Tạo TaskLabel entries
   
   2d. SaveChanges trong 1 transaction
   → Return ImportResult

3. Undo (optional)
   → DELETE /api/import/sessions/{id}
   → Kiểm tra CreatedAt + 30 phút > now
   → Xóa tất cả TaskItem có ImportSessionId == id
   → Xóa TaskLabel liên quan
   → Đánh dấu ImportSession.IsUndone = true
```

### 3.6 Frontend Components

```
components/import/
├── ImportModal.vue            ← Modal chính, điều phối 3 bước
├── ImportUploadStep.vue       ← Bước 1: Drag & drop file + chọn flow
├── ImportMappingStep.vue      ← Bước 2: Preview data + mapping cột
├── ImportConfirmStep.vue      ← Bước 3: Tổng kết + nút confirm
└── ImportUndoBanner.vue       ← Banner "Undo import" sau khi xong
```

#### Component Flow:

```
┌─────────────────────────────────────────────────────────┐
│                    ImportModal.vue                       │
│                                                         │
│  Step 1 ─────────► Step 2 ─────────► Step 3             │
│  Upload File       Column Mapping    Confirm & Import   │
│                                                         │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │
│  │ Drag & Drop  │  │ Preview Table│  │ Summary      │   │
│  │ Choose File  │  │ Dropdowns    │  │ Stats        │   │
│  │ Flow Select  │  │ Auto-suggest │  │ Warnings     │   │
│  │ Sheet Select │  │ Skip toggle  │  │ Import btn   │   │
│  └──────────────┘  └──────────────┘  └──────────────┘   │
└─────────────────────────────────────────────────────────┘
```

---

## IV. KẾ HOẠCH TRIỂN KHAI

### 4.1 NuGet Package cần thêm

| Package | Layer | Mục đích |
|---|---|---|
| `CsvHelper` (31.x) | Application | Parse CSV/TSV, auto-detect encoding |

> `ClosedXML` đã có sẵn cho XLSX.

### 4.2 Sprint breakdown

#### Sprint 1 — Domain & Infrastructure (1 ngày)

| # | Task | File | Est. |
|---|---|---|---|
| 1 | Tạo entity `ImportSession` | `Domain/Entities/ImportSession.cs` | 15m |
| 2 | Thêm `ImportSessionId` vào `TaskItem` | `Domain/Entities/TaskItem.cs` | 5m |
| 3 | Thêm EF Configuration cho `ImportSession` | `Infrastructure/Configurations/ImportSessionConfiguration.cs` | 20m |
| 4 | Cập nhật `TaskItem` config (FK mới) | `Infrastructure/Configurations/TaskItemConfiguration.cs` | 10m |
| 5 | Tạo Migration | `dotnet ef migrations add AddImportSession` | 5m |
| 6 | Cài NuGet `CsvHelper` | `Application/Qaly.Application.csproj` | 5m |

#### Sprint 2 — Application Service (2–3 ngày)

| # | Task | File | Est. |
|---|---|---|---|
| 1 | Tạo DTOs import | `Application/DTOs/Import/ImportDtos.cs` | 30m |
| 2 | Tạo interface `IImportService` | `Application/Common/Interfaces/IImportService.cs` | 15m |
| 3 | Implement `ImportService` — ParseFile | `Application/Services/ImportService.cs` | 3h |
| 4 | Implement `ImportService` — ExecuteImport | `Application/Services/ImportService.cs` | 4h |
| 5 | Implement `ImportService` — UndoImport | `Application/Services/ImportService.cs` | 1h |
| 6 | File parser helper (CSV + XLSX + TSV) | `Application/Services/Import/FileParserHelper.cs` | 2h |
| 7 | Status mapping helper | `Application/Services/Import/StatusMapper.cs` | 1h |
| 8 | Đăng ký DI | `Application/DependencyInjection.cs` | 5m |
| 9 | Viết Unit Tests | `Tests/ImportServiceTests.cs` | 2h |

#### Sprint 3 — Web API (0.5 ngày)

| # | Task | File | Est. |
|---|---|---|---|
| 1 | Tạo `ImportController` | `Web/Controllers/ImportController.cs` | 1h |
| 2 | Test API bằng Scalar/Postman | — | 30m |

#### Sprint 4 — Frontend (2–3 ngày)

| # | Task | File | Est. |
|---|---|---|---|
| 1 | `ImportModal.vue` (shell + stepper) | `components/import/ImportModal.vue` | 2h |
| 2 | `ImportUploadStep.vue` (drag & drop) | `components/import/ImportUploadStep.vue` | 3h |
| 3 | `ImportMappingStep.vue` (preview + dropdowns) | `components/import/ImportMappingStep.vue` | 4h |
| 4 | `ImportConfirmStep.vue` (summary + confirm) | `components/import/ImportConfirmStep.vue` | 2h |
| 5 | `ImportUndoBanner.vue` (undo banner) | `components/import/ImportUndoBanner.vue` | 1h |
| 6 | CSS styling (glassmorphism, animations) | `assets/import.css` | 2h |
| 7 | Tích hợp vào `ProjectsPage.vue` (nút Import) | `pages/ProjectsPage.vue` | 30m |
| 8 | Tích hợp vào `ProjectDetailPage.vue` (nút Merge) | `pages/ProjectDetailPage.vue` | 30m |

#### Sprint 5 — Polish & Test (1 ngày)

| # | Task | Est. |
|---|---|---|
| 1 | Test end-to-end với file CSV tiếng Việt | 1h |
| 2 | Test XLSX multi-sheet | 30m |
| 3 | Test duplicate detection | 30m |
| 4 | Test undo import | 30m |
| 5 | Test file > 2000 dòng (reject gracefully) | 15m |
| 6 | Test encoding auto-detect | 30m |
| 7 | Refine UX animations | 1h |

### 4.3 Tổng ước lượng

| Phase | Effort |
|---|---|
| Domain & Infrastructure | **1 ngày** |
| Application Service | **2–3 ngày** |
| Web API | **0.5 ngày** |
| Frontend | **2–3 ngày** |
| Testing & Polish | **1 ngày** |
| **Tổng cộng** | **~7 ngày làm việc** |

---

## V. MẪU CSV ĐỂ TEST

```csv
Tiêu đề,Mô tả,Trạng thái,Ưu tiên,Hạn chót,Giờ ước tính,Nhãn
Thiết kế giao diện login,Tạo UI login page responsive,Todo,High,2026-06-01,8,"UI, Frontend"
Fix lỗi đăng nhập,Session timeout sau 5 phút,InProgress,Critical,2026-05-20,4,Bug
Viết API đăng ký,POST /api/auth/register,InReview,Medium,2026-05-25,6,"Backend, API"
Tối ưu query SQL,Thêm index cho bảng Tasks,OnHold,Low,,2,Backend
Deploy staging,Triển khai lên server staging,Todo,High,2026-06-10,3,DevOps
Viết unit test auth,Test AuthService đầy đủ,Done,Medium,2026-05-15,4,"Backend, Testing"
```

---

## VI. SO SÁNH VỚI NOTION — CẢI TIẾN CỦA QALY

| Tính năng | Notion | Qaly (Đề xuất) |
|---|---|---|
| Detect duplicate | ❌ Không | ✅ Tùy chọn skip trùng tên |
| Chọn sheet XLSX | ❌ Chỉ sheet đầu | ✅ Cho chọn sheet |
| Preview số lượng task | ❌ Không | ✅ Hiển thị summary trước confirm |
| Undo import | ❌ Chỉ có Page History | ✅ Nút Undo riêng trong 30 phút |
| Auto-detect encoding | ⚠️ Yêu cầu UTF-8 | ✅ Auto-detect, cảnh báo nếu sai |
| Cảnh báo status lạ | ❌ Tự tạo option | ✅ Hiển thị danh sách unmapped + cho xem lại |
| Multi-select labels | ✅ Có | ✅ Có, tự tạo label mới nếu chưa tồn tại |
| Hỗ trợ tiếng Việt header | ⚠️ Hạn chế | ✅ Auto-suggest cả header tiếng Việt |

---

*Cập nhật trạng thái khi bắt đầu triển khai từng Sprint.*
