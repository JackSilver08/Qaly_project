# Scope va quyet dinh ky thuat tuan 03/06/2026 - 09/06/2026

Nguoi chiu trach nhiem chot nen tang: Quang Tuan

## Muc tieu tuan nay

Trong cac giai doan truoc, ca nhom da cung code de hoan thien core Qaly. Tuan nay khong mo rong core tran lan nua. Quang Tuan giu vai tro code chinh va review kien truc; cac thanh vien con lai tap trung vao cac khoang trong da duoc quet tu bao cao va source hien tai.

## P0 phai xu ly

1. Meeting khong con dung o muc local preview.
   - Backend phai co active meeting endpoint, join/end co rule ro, SignalR co room meeting rieng.
   - Frontend sau do co the ghep WebRTC provider hoac Jitsi/LiveKit ma khong doi lai contract lon.

2. Import document khong duoc hua qua kha nang that.
   - UI phai tach dinh dang dang ho tro va dinh dang trong lo trinh.
   - Backend phase dau dang ho tro `.md`, `.markdown`, `.txt`, `.html`, `.htm`; DOCX/PDF/ZIP chi duoc ghi la dang lam neu chua co parser.

3. AI phai tien toi luong that.
   - Trang phan tich hien co nhieu phan rule-based/fallback.
   - Quoc Bao va Chi Khang phai dua vao AI Gateway, tool/RAG, schema validation va nguon du lieu that.

4. Deploy config phai sach hon.
   - Container web khong duoc tro ve SQL Server Windows host `CMI\\SQLEXPRESS`.
   - Cau hinh phai uu tien bien moi truong va service name trong Docker network.

5. QA va tai lieu phai co bang chung.
   - Duy Hoang va Doan Trung tap trung test plan, E2E, manual QA, tai lieu API/user guide/demo/report.

## Quyet dinh cho meeting MVP

Tuan nay backend chot theo huong provider-friendly:

- `GroupMeetingSession.Provider` mac dinh la `Jitsi`.
- `RoomId` la ma phong duy nhat do backend tao.
- `JoinUrl` duoc tao tu provider URL.
- SignalR co group rieng theo meeting: `workgroup:{groupId}:meeting:{meetingId}`.
- End meeting duoc thuc hien boi nguoi tao meeting hoac nguoi co quyen quan ly group.
- Join meeting da `Ended` bi tu choi bang HTTP 400.

Ly do: giu backend nhe, demo nhanh, nhung khong khoa vao UI local preview. Neu nhom chon WebRTC tu viet, SignalR room da san sang lam signaling channel. Neu chon Jitsi/LiveKit, `Provider`, `RoomId`, `JoinUrl` da du de embed.

## Quyet dinh cho import document

- P0: sua mismatch UI/backend de user khong hieu nham.
- P0 neu kip: parser DOCX phase 1 sang Markdown.
- P1: PDF/ZIP parser.
- Moi file import phai co warning ro neu mat formatting, anh, bang, hoac noi dung khong doc duoc.

## Quyet dinh cho AI nang cao

- Khong merge luong AI chi tra loi theo intent hardcode neu gan nhan la "AI that".
- Moi response quan trong can co it nhat mot trong cac bang chung:
  - Du lieu lay tu DB/tool.
  - Du lieu lay tu RAG co filter project/user.
  - Provider/model/cached/fallback duoc log ro.
- Output action items, summary, draft project phai qua validation toi thieu truoc khi UI render.

## Quyet dinh cho deploy

- `.env.example` la noi khai bao bien can thiet.
- `docker-compose.yml` chi nen dung gia tri dev an toan co the override.
- Web container ket noi SQL Server bang `Server=qaly-sqlserver,1433`.
- Production khong chay `ASPNETCORE_ENVIRONMENT=Development`.

## Gate cuoi tuan

- `npm run build` pass.
- `dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj` pass.
- `dotnet test tests/Qaly.IntegrationTests/Qaly.IntegrationTests.csproj` pass.
- E2E smoke hoac manual QA co bang chung cho meeting/import/AI.
- Tai lieu nghiem thu khong noi qua cac tinh nang chua hoan thien.
