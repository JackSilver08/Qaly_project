# SPEC & Plan: File Import Module - Notion-Style Enhanced

Ngay 2026-05-28

## 1. Muc tieu

Nang cap tinh nang import hien tai cua QALY tu import file dang bang vao Kanban task thanh module import da dang file:

- Document import: Markdown, HTML, DOCX, TXT, PDF, EPUB tao Wiki Page/Block.
- Structured import: CSV, TSV/DSV, Excel, JSON tao task/database-like dataset, tiep tuc ho tro Kanban mapping hien tai.
- Bundle import: ZIP giai nen, route tung file, giu cau truc folder, import song song co bao cao loi.
- Import history/progress: theo doi session, trang thai, loi tung file/tung block, partial recovery.

Spec goc nguoi dung dua ra dinh huong theo Notion-style import, co cai tien quan trong nhat la PDF Parser tich hop MinerU pipeline de OCR, layout detection, table reconstruction, formula extraction va reading order.

## 2. Hien trang trong source

Module import hien tai dang tap trung vao task import:

- Backend:
  - `src/Qaly.Application/Services/ImportService.cs`
  - `src/Qaly.Application/DTOs/Import/ImportDtos.cs`
  - `src/Qaly.Web/Controllers/ImportController.cs`
  - `src/Qaly.Domain/Entities/ImportSession.cs`
- Frontend:
  - `src/Qaly.Web/ClientApp/components/import/ImportModal.vue`
  - `ImportUploadStep.vue`, `ImportMappingStep.vue`, `ImportConfirmStep.vue`
- Test:
  - `tests/Qaly.UnitTests/ImportEnhancementTests.cs`

Da co san:

- Parse/execute flow cho `.csv`, `.xlsx`, `.tsv`, `.txt`, `.psv`, `.json`.
- Preview 5 dong, sheet selection, first-row-is-header toggle.
- Auto suggest column mapping.
- Default assignee/default priority/assign-to-me-if-empty.
- AI categorization cho status/priority/label neu bat.
- Skipped row detail.
- Bulk insert task va task label.
- Undo import trong 30 phut.

Gioi han/chua co:

- Import hien tai gan chat voi `TaskItem`, chua co parser abstraction.
- Chua co PageAST/Block schema trung gian.
- Chua import document vao Wiki Page/block.
- Chua co ZIP batch import, per-file report, progress realtime.
- Chua co PDF OCR/MinerU pipeline.
- File size API dang gioi han 5 MB trong `ImportController`, spec moi muon toi da 5 GB cho ZIP/PDF lon. Can nang theo phase va theo loai file.

## 3. Kien truc muc tieu

### 3.1 Import facade

Them lop dieu phoi moi, khong thay the ngay flow CSV task hien tai:

```csharp
public interface IFileImportOrchestrator
{
    Task<Result<ImportPreviewDto>> PreviewAsync(FileImportInput input, CancellationToken ct);
    Task<Result<ImportExecutionResultDto>> ExecuteAsync(FileImportExecutionRequest request, CancellationToken ct);
}
```

Vai tro:

- Validate extension/size/content-type.
- Tao/cap nhat `ImportSession`.
- Chon parser theo file type.
- Goi renderer de luu vao Wiki Page, Task import hoac future Database.
- Tong hop progress/report.

### 3.2 Parser abstraction

```csharp
public interface IFileParser
{
    IReadOnlySet<string> SupportedExtensions { get; }
    ImportTargetKind TargetKind { get; }
    Task<ParseResult> ParseAsync(FileImportContext context, CancellationToken ct);
}
```

Target kind:

- `Page`: Markdown/HTML/DOCX/TXT/PDF/EPUB.
- `TaskTable`: CSV/Excel/TSV/JSON hien tai.
- `Bundle`: ZIP.

Voi structured import, giu lai `ImportService` hien tai lam `TaskTableImportService`, sau do boc vao parser/orchestrator. Cach nay it rui ro hon viec viet lai toan bo CSV flow.

### 3.3 Page AST / Block schema

Can them DTO/model trung gian:

- `PageAst`: title, description, metadata, blocks, assets.
- `Block`: heading, paragraph, bullet list, numbered list, code, quote, divider, table, image, math, diagram, callout, broken image.
- `Inline`: text, bold, italic, underline, strikethrough, inline code, link, math inline.

Renderer dau tien nen target `WikiPage.Content` bang JSON/Markdown tuong thich voi UI hien tai. Khi editor block mature hon thi chuyen sang block-native storage.

## 4. Pham vi file theo phase

### Phase 1 - Nen tang import da dang file nhe

Muc tieu: co skeleton parser/router va import Page cho file de xu ly local.

- Markdown `.md`, `.markdown`
  - Heading, paragraph, list, code fence, quote, divider, table co ban.
  - Cai tien: Mermaid code fence thanh `DiagramBlock`, `$...$`/`$$...$$` thanh math block/inline.
- Plain text `.txt`
  - Moi doan/dong thanh paragraph.
  - Neu file co dau hieu Markdown thi UI goi y parse nhu Markdown.
- HTML `.html`, `.htm`
  - Drop script/style/iframe/form/input.
  - Parse title/meta description.
  - Basic image handling: URL remote tam luu link, local/ZIP missing thanh BrokenImageBlock.
- Frontend:
  - Doi title modal tu "Nhap CSV / Excel" thanh "Nhap file".
  - Tach mode: "Task table" va "Document page".
  - Hien preview document block/page truoc khi import.

### Phase 2 - Structured import nang cao

Muc tieu: nang cap phan CSV/Excel hien co ma khong lam mat UX hien tai.

- CSV/TSV/DSV:
  - Auto detect delimiter comma/semicolon/tab/pipe.
  - Encoding fallback UTF-8 BOM/UTF-8/Windows-1252 neu kha thi.
  - Type inference cho column preview: number/date/select/text.
  - Multi CSV: moi file tao mot dataset/project/table tuy option.
- Excel:
  - Ho tro import all sheets.
  - Moi sheet co preview rieng.
  - Cong thuc lay cached value, drop formatting/chart/pivot/macro.
- Upsert mode:
  - User chon unique key column.
  - Existing key thi update, missing key thi insert.
  - Ban dau co the chi ap dung khi import vao project/task hien co.

### Phase 3 - DOCX va EPUB

Muc tieu: import tai lieu van phong/chapter vao Wiki.

- DOCX:
  - Dung OpenXML SDK hoac Mammoth pipeline.
  - Paragraph/heading/bold/italic/underline/link/list/table/image.
  - Khong ho tro `.doc`, tra loi ro can convert sang `.docx`.
  - Later: comment -> callout, header/footer -> metadata collapsed block.
- EPUB:
  - Doc OPF spine order.
  - Moi chapter HTML -> HTML parser -> Wiki Page con.
  - Cover image them vao page dau.
  - DRM encrypted -> loi ro rang.

### Phase 4 - ZIP bundle import

Muc tieu: import nhieu file co cau truc thu muc.

- Validate zip bomb: size, entry count, path traversal.
- Skip hidden/system files: `.DS_Store`, `__MACOSX/`, `Thumbs.db`.
- Route tung file theo parser.
- Folder trong ZIP -> page cha/workspace folder concept neu co.
- Concurrency default 4-10 cho parser nhe, PDF concurrency rieng = 2.
- Ket qua: `ImportReport` gom total/done/failed/skipped + loi tung file.

### Phase 5 - PDF + MinerU pipeline

Muc tieu: PDF thanh block editable/searchable thay vi chi upload attachment.

- Pre-check:
  - Text-based simple PDF -> lightweight extractor.
  - Scanned/complex/table/formula/multi-column -> MinerU.
- MinerU worker:
  - Khuyen nghi tach thanh Python worker/service rieng, vi du `scripts/import-workers/pdf_mineru_worker.py` hoac service container.
  - Main .NET app enqueue job, worker tra Markdown/JSON AST.
  - Limit concurrent MinerU jobs = 2.
- Mapping:
  - TextBlock -> Paragraph/Heading.
  - TableBlock -> TableBlock.
  - FigureBlock -> ImageBlock.
  - FormulaBlock -> MathBlock latex.
  - TitleBlock -> Heading1.
- Post-process:
  - Merge paragraph bi split.
  - Deduplicate header/footer lap theo trang.
  - Strip watermark neu lap pattern ro.

## 5. Data model can bo sung

Mo rong `ImportSession` de phu hop batch/page import:

- `Status`: pending, in_progress, completed, failed, partial.
- `FileType`, `FileSize`, `StartedAt`, `CompletedAt`.
- `TargetKind`: task_table, page, bundle.
- `ResultPageId`, `ResultProjectId`, future `ResultDatabaseId`.
- `StatsJson`: totalBlocks/successBlocks/skippedBlocks/errorBlocks/totalFiles/doneFiles.
- `ErrorsJson`: danh sach `ImportError`.

Them entity neu can:

- `ImportFileResult`: per file trong ZIP/batch.
- `ImportedAsset`: anh/pdf extracted asset lien ket WikiPage.

Can migration rieng, khong nen nhet JSON tuy tien vao nhieu bang neu da co nhu cau query history.

## 6. API de xuat

Giu API hien tai cho backward compatibility:

- `POST /api/import/parse`
- `POST /api/import/execute`

Them API moi:

- `POST /api/file-import/preview`
- `POST /api/file-import/execute`
- `GET /api/file-import/sessions/{id}`
- `GET /api/file-import/sessions/{id}/report`
- Later realtime: SignalR group `import:{sessionId}` emit progress.

Voi UI cu, co the migrate dan sang API moi khi structured parser da boc xong.

## 7. UI/UX de xuat

- Upload step:
  - Chap nhan nhieu file cho document/ZIP, single file cho flow task table ban dau.
  - Hien supported formats theo nhom: Document, Table, Bundle.
- Preview step:
  - Table file: giu mapping UI hien tai, them type inference/upsert controls.
  - Document file: hien outline 5-20 block dau, metadata title/description, target Wiki location.
  - ZIP: hien file tree, file nao support/skip, checkbox chon file.
- Progress:
  - Per file progress bar.
  - Status partial neu co file loi nhung batch van xong.
- Result:
  - Link den project/task import hoac Wiki page vua tao.
  - Report loi per file/per block.
  - Completed tab 30 ngay gan nhat.

## 8. Thu vien de can nhac

Vi project hien tai la .NET + Vue:

- CSV/TSV: tiep tuc `CsvHelper`.
- Excel: tiep tuc `ClosedXML`; can them `.xls` thi can lib khac hoac yeu cau convert `.xlsx`.
- Markdown: `Markdig` cho .NET, custom extension map AST.
- HTML: `HtmlAgilityPack` hoac `AngleSharp`.
- DOCX: `DocumentFormat.OpenXml`; option nhanh hon la Mammoth qua service Node/Python nhung them operational cost.
- EPUB: .NET co the tu doc ZIP/OPF/XML; neu muon nhanh, worker Python `ebooklib`.
- ZIP: `System.IO.Compression.ZipArchive` + validation path traversal.
- PDF: Python worker voi MinerU (`magic-pdf`) va PaddleOCR/MinerU dependencies.
- Frontend math/diagram: KaTeX + Mermaid.js.

## 9. Rui ro va quyet dinh can chot

- Storage block: hien WikiPage dang luu content kieu gi? Can xem editor/render flow truoc khi chot JSON block hay Markdown.
- PDF/MinerU nang ve CPU/GPU va dependency; nen chay worker rieng de khong block web app.
- File size 5 GB khong phu hop upload memory stream truc tiep; can streaming/temp storage/background job truoc khi nang gioi han.
- ZIP security bat buoc lam som: zip slip, zip bomb, hidden files.
- Multi-file import can progress realtime, neu khong UX se bi treo request HTTP.
- Upsert task can conflict policy: update field nao, co ghi de assignee/status/priority khong, co dry-run diff khong.

## 10. Ke hoach hanh dong uu tien

1. Refactor import hien tai thanh `TaskTableImportService` va them parser/orchestrator skeleton, giu test hien co xanh.
2. Them PageAST DTO + Markdown/TXT parser + renderer sang WikiPage.
3. Them UI upload mode document/table, document preview va import vao Wiki.
4. Them HTML parser + asset/broken image handling co ban.
5. Them ImportSession status/report/progress model.
6. Them ZIP parser voi validation va batch report.
7. Them CSV/Excel type inference + upsert preview.
8. Them DOCX/EPUB parser.
9. Tach PDF MinerU worker + queue/background execution.
10. Hoan thien SignalR progress va history 30 ngay.

## 11. Acceptance criteria ban dau

- Import CSV/Excel cu van hoat dong va tests hien tai pass.
- Import `.md` tao WikiPage dung title va block co ban.
- Import `.txt` tao WikiPage voi paragraph dung noi dung.
- Import `.html` tao WikiPage, drop script/style, lay title/meta.
- File khong support tra loi ro rang.
- ImportSession co trang thai completed/failed/partial va report loi.
- UI khong con gioi thieu chi CSV/Excel khi chon file document.

