# HƯỚNG DẪN CÀI ĐẶT VÀ KHỞI CHẠY QALY

Tài liệu này đi kèm bộ nộp `02_SourceCode_CSDL`. Dự án hỗ trợ hai cách chạy:

- Chạy nhanh bằng CSDL in-memory: không cần Docker hoặc SQL Server, phù hợp để chấm giao diện và nghiệp vụ.
- Chạy đầy đủ bằng SQL Server + Redis: phù hợp để kiểm tra CSDL, migration và dữ liệu demo.

## 1. Thành phần bộ nộp

- `Qaly_SourceCode.zip`: toàn bộ source cần thiết để build và chạy dự án.
- `Qaly_Database.sql`: script tạo mới CSDL `QalyDb`, gồm toàn bộ 47 EF Core migrations.
- `Qaly_Database_SQL.zip`: bản nén của script SQL.
- `HUONG_DAN_CAI_DAT.md`: tài liệu đang đọc.
- `SHA256SUMS.txt`: mã SHA-256 dùng để kiểm tra file nén không bị hỏng.

Các thư mục/file không cần thiết hoặc nhạy cảm đã được loại khỏi source nén: `.git`, `.vs`, `.env`, private key, `node_modules`, `bin`, `obj`, `dp-keys`, log, cache và kết quả test.

## 2. Yêu cầu môi trường

Bắt buộc:

- Windows 10/11 64-bit.
- .NET 10 SDK. Dự án dùng `global.json` phiên bản `10.0.103` và cho phép roll-forward lên feature band mới hơn.
- Kết nối Internet ở lần đầu để `dotnet restore` tải NuGet packages.

Chỉ cần khi build lại giao diện:

- Node.js 24 và npm 11 (hoặc Node.js 24 LTS tương thích).

Chỉ cần khi chạy chế độ đầy đủ:

- Docker Desktop có Docker Compose; hoặc SQL Server 2022/SQL Server Express và Redis cài riêng.
- SSMS hoặc `sqlcmd` nếu muốn chạy file SQL thủ công.

Kiểm tra nhanh:

```powershell
dotnet --version
node --version
npm --version
docker --version
docker compose version
```

## 3. Giải nén source

Mở PowerShell tại thư mục `02_SourceCode_CSDL` rồi chạy:

```powershell
Expand-Archive -Path .\Qaly_SourceCode.zip -DestinationPath . -Force
Set-Location .\Qaly_project
```

Sau khi giải nén, file solution phải nằm tại `Qaly_project\Qaly_project.slnx`.

## 4. Cách A - chạy nhanh, không cần Docker

Frontend production đã được build sẵn trong `src\Qaly.Web\wwwroot\dist`, vì vậy chỉ cần .NET SDK:

```powershell
dotnet restore .\Qaly_project.slnx
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:UseInMemoryDatabase = "true"
dotnet run --project .\src\Qaly.Web --urls "http://localhost:5000"
```

Mở `http://localhost:5000`. Dữ liệu demo được tạo khi ứng dụng khởi động và sẽ được tạo lại sau mỗi lần dừng/chạy vì đây là CSDL in-memory.

## 5. Cách B - chạy đầy đủ bằng Docker + SQL Server

### 5.1. Khởi động hạ tầng

Tại thư mục `Qaly_project`:

```powershell
Copy-Item .\.env.example .\.env
docker compose up -d qaly-sqlserver qaly-redis qaly-seq qaly-mailhog
docker compose ps
```

Chờ các container chuyển sang trạng thái chạy/healthy. Cấu hình mặc định dùng:

- SQL Server: `localhost,1434`, database `QalyDb`, user `sa`.
- Redis: `localhost:6380`.
- Seq: `http://localhost:8081`.
- MailHog: `http://localhost:8025`.

Mật khẩu local mặc định nằm trong `.env.example` và chỉ dành cho môi trường demo. Không dùng các giá trị này ở production.

### 5.2. Tạo CSDL

Cách khuyến nghị: để ứng dụng tự áp dụng EF Core migrations ở lần chạy đầu. Không cần chạy thêm lệnh SQL.

Nếu giảng viên muốn import file `.sql`, chạy `Qaly_Database.sql` bằng SSMS hoặc từ thư mục `02_SourceCode_CSDL` dùng:

```powershell
sqlcmd -S "localhost,1434" -U "sa" -P "Qaly@Dev2026!" -C -b -i ".\Qaly_Database.sql"
```

Trong source đã giải nén cũng có một bản sao tại `Qaly_project\database\Qaly_Database.sql`.

Script sẽ tự tạo `QalyDb`, tạo schema và ghi lịch sử 47 migrations. Script có tính idempotent nên có thể chạy lại mà không tạo trùng bảng.

### 5.3. Chạy ứng dụng với SQL Server

Quay lại thư mục `Qaly_project`:

```powershell
dotnet restore .\Qaly_project.slnx
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:UseInMemoryDatabase = "false"
dotnet run --project .\src\Qaly.Web --urls "http://localhost:5000"
```

Ứng dụng sẽ kiểm tra migration và seed dữ liệu demo ở lần chạy đầu. Nếu bạn đổi mật khẩu SQL trong `.env`, đồng thời phải đổi connection string:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost,1434;Database=QalyDb;User Id=sa;Password=<MAT_KHAU_MOI>;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True"
```

## 6. Build lại frontend

Chỉ thực hiện khi muốn sửa hoặc kiểm tra source Vue/TypeScript:

```powershell
npm ci --no-audit --no-fund
npm run typecheck
npm run build
```

Không chép `node_modules` vào bài nộp. `package.json` và `package-lock.json` đã đủ để cài lại đúng dependencies.

## 7. Tài khoản demo

- Admin: `admin@qaly.dev` / `Admin@123456`.
- Thành viên: `minh.anh@qaly.dev` / `Qaly@123456`.
- Thành viên: `bao.ngoc@qaly.dev` / `Qaly@123456`.

## 8. AI và các tích hợp ngoài

Không bắt buộc API key để mở và chấm các chức năng chính. LiveKit, GitHub App, OpenAI, Gemini và DeepSeek là tùy chọn; có thể điền vào `.env` khi cần kiểm tra tích hợp thật. Khi không cấu hình cloud AI, dự án dùng luồng fallback/degraded mock theo cấu hình Development.

## 9. Lệnh kiểm tra trước khi demo

```powershell
dotnet build .\Qaly_project.slnx --configuration Release
dotnet test .\tests\Qaly.UnitTests\Qaly.UnitTests.csproj --configuration Release
dotnet test .\tests\Qaly.WebFeatureTests\Qaly.WebFeatureTests.csproj --configuration Release
```

## 10. Xử lý lỗi thường gặp

- Cổng `5000` bị chiếm: đổi thành `--urls "http://localhost:5001"`.
- Không kết nối SQL: kiểm tra `docker compose ps` và `docker compose logs --tail 100 qaly-sqlserver`.
- Không đăng nhập được ở chế độ SQL: kiểm tra Redis đang chạy tại cổng `6380`.
- Build báo DLL đang bị khóa: dừng tiến trình `Qaly.Web` đang chạy rồi build lại.
- Lỗi certificate của `sqlcmd`: giữ tham số `-C` trong lệnh import.
- Muốn dừng hạ tầng nhưng giữ dữ liệu: chạy `docker compose down` và không thêm tùy chọn xóa volume.

## 11. Kết quả xác minh của bộ nộp

- `dotnet restore`: đạt.
- Backend Release build: đạt, 0 warning, 0 error.
- `npm ci`, TypeScript typecheck và Vite production build: đạt.
- Unit tests: 590/590 đạt.
- Web-feature tests: 38/38 đạt.
- Khởi chạy in-memory và tải trang đăng nhập: HTTP 200.
- Script SQL chạy hai lần liên tiếp trên CSDL sạch: đạt; 85 bảng, 47 migrations.
- Khởi chạy ứng dụng bằng SQL Server, seed dữ liệu và tải trang đăng nhập: đạt; 12 user, 6 project, 1 admin.
