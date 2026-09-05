# Qaly V4 — Hướng dẫn cài đặt và chạy dự án

Tài liệu này dành cho máy phát triển Windows dùng PowerShell. Cách khuyên dùng là chạy SQL Server/Redis bằng Docker, còn ASP.NET Core và Vite chạy trực tiếp trên máy để có hot reload.

## Chạy toàn diện dự án — lệnh dùng hằng ngày

Từ thư mục gốc `Qaly_project`, chạy:

```powershell
npm run dev:preview:ai
```

Đây là lệnh đầy đủ nhất cho môi trường phát triển local: nó khởi động SQL Server, Redis, Seq, MailHog, Qdrant, Ollama, frontend Vite có hot reload, backend ASP.NET Core, AI job worker, migrations và dữ liệu demo. Lệnh **không tự mở trình duyệt**; sau khi thấy `QALY ĐÃ SẴN SÀNG`, mở `https://localhost:5005` trong Browser của Codex hoặc trình duyệt thường. Nhấn `Ctrl+C` để dừng frontend/backend.

Lưu ý: “toàn diện” ở đây là toàn bộ thành phần có thể chạy local. Chức năng dùng nhà cung cấp cloud, GitHub hoặc dịch vụ ngoài vẫn cần API key/quyền tích hợp thật trong `.env`; hệ thống không giả lập các tích hợp đó thành công. Lần đầu lấy source, vẫn thực hiện phần **Cần cài trước**, sao chép `.env` và tin cậy HTTPS certificate theo hướng dẫn bên dưới.

## 1. Cần cài trước

- Git.
- Docker Desktop và bật Docker trước khi chạy Qaly.
- .NET SDK 10. Dự án ghim SDK `10.0.103` trong `global.json` và cho phép dùng feature band mới hơn tương thích.
- Node.js 22 và npm.

Kiểm tra nhanh:

```powershell
git --version
docker version
dotnet --version
node --version
npm --version
```

## 2. Lấy source lần đầu

```powershell
git clone https://github.com/JackSilver08/Qaly_project.git
Set-Location Qaly_project
Copy-Item .env.example .env
```

`.env` chỉ dùng trên máy local và đã được Git ignore. Không commit API key hoặc mật khẩu thật.

Nếu đã có source:

```powershell
Set-Location C:\duong-dan-den\Qaly_project
git pull --ff-only origin main
```

Nếu `git pull --ff-only` báo có thay đổi local, hãy xem `git status` và giữ/commit/stash phần đang làm trước; không dùng `git reset --hard` để xử lý nhanh.

## 3. Cấu hình local

Mặc định trong `.env.example` đã phù hợp với Docker local:

| Thành phần | Địa chỉ mặc định |
|---|---|
| Qaly preview | `https://localhost:5005` |
| Vite HMR | `https://localhost:5173` |
| SQL Server | `localhost:1434` |
| Redis | `localhost:6380` |
| Seq | `http://localhost:8081` |
| MailHog | `http://localhost:8025` |

AI cloud là tùy chọn. Nếu cần phản hồi từ provider thật, điền một trong các key sau vào `.env`:

```dotenv
DEEPSEEK_API_KEY=...
OPENAI_API_KEY=...
GEMINI_API_KEY=...
```

Không có key thì những capability có fallback nội bộ vẫn có thể chạy, nhưng không được hiểu là bằng chứng provider cloud đã hoạt động.

Tin cậy HTTPS certificate local một lần:

```powershell
dotnet dev-certs https --trust
```

## 4. Cách chạy khuyên dùng — giống “preview” trong Codex

### Lần đầu hoặc sau khi vừa clone

```powershell
npm ci
dotnet restore
npm run dev:preview
```

`npm run dev:preview` sẽ:

1. Kiểm tra và khởi động Docker Desktop nếu cần.
2. Chạy SQL Server, Redis, Seq và MailHog.
3. Chạy Vite HMR tại `https://localhost:5173`.
4. Chạy ASP.NET Core bằng `dotnet watch` tại `https://localhost:5005`.
5. Tự áp dụng EF migrations và seed dữ liệu demo khi backend khởi động.
6. Không tự mở Chrome hoặc Edge.

Khi thấy dòng `QALY ĐÃ SẴN SÀNG`, mở:

```text
https://localhost:5005
```

Trong Codex, mở tab Browser ở panel bên phải rồi nhập URL trên. Đây chính là cách tự mở cùng preview mà Codex thường mở giúp bạn; không cần một server hay bản build riêng.

Muốn lệnh tự mở trình duyệt hệ thống:

```powershell
npm run dev:all
```

Muốn chạy thêm Qdrant và Ollama local:

```powershell
npm run dev:preview:ai
```

Lần đầu dùng model local, tải model sau khi container Ollama đã chạy:

```powershell
docker exec qaly-ollama ollama pull qwen2.5:3b
```

### Những lần chạy sau

Thông thường chỉ cần:

```powershell
npm run dev:preview
```

Chỉ chạy lại `npm ci` khi `package-lock.json` thay đổi. Chỉ cần `dotnet restore` lại khi dependency .NET thay đổi hoặc thư mục build đã bị xóa.

## 5. Chạy hoàn toàn thủ công để dễ dò lỗi

Dùng cách này khi muốn nhìn riêng log frontend và backend.

### Terminal 1 — hạ tầng

```powershell
docker compose up -d --wait qaly-sqlserver qaly-redis qaly-seq qaly-mailhog
docker compose ps
```

### Chuẩn bị certificate Vite một lần

```powershell
New-Item -ItemType Directory -Force .tmp | Out-Null
dotnet dev-certs https -ep .tmp/qaly-vite-dev.pfx -p qaly-local-dev
```

### Terminal 2 — frontend HMR

```powershell
$env:QALY_VITE_DEV = "1"
$env:QALY_VITE_CERT_PASSWORD = "qaly-local-dev"
npm run dev
```

### Terminal 3 — backend

Các giá trị dưới đây khớp `.env.example`. Nếu đã đổi port/mật khẩu SQL trong `.env`, hãy đổi connection string tương ứng.

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "https://localhost:5005"
$env:Vite__DevServerUrl = "https://localhost:5173"
$env:ConnectionStrings__DefaultConnection = "Server=localhost,1434;Database=QalyDb;User Id=sa;Password=Qaly@Dev2026!;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True"
$env:Redis__ConnectionString = "localhost:6380"
$env:AI_JOB_V4_WORKER_ENABLED = "true"
$env:PRIVACY_V4_WORKER_ENABLED = "false"
$env:GitHub__WorkerEnabled = "false"
dotnet watch --project src/Qaly.Web --no-restore
```

Sau đó mở `https://localhost:5005`. Không mở trực tiếp cổng `5173`; đó chỉ là dev server cung cấp asset và HMR cho ứng dụng tại cổng `5005`.

## 6. Chạy bản ổn định từ bundle đã build

Cách này không có frontend HMR, phù hợp để tập dượt demo sau khi đã chốt code:

```powershell
docker compose up -d --wait qaly-sqlserver qaly-redis qaly-seq qaly-mailhog
npm ci
npm run typecheck
npm run build
dotnet restore
$env:ConnectionStrings__DefaultConnection = "Server=localhost,1434;Database=QalyDb;User Id=sa;Password=Qaly@Dev2026!;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True"
$env:Redis__ConnectionString = "localhost:6380"
dotnet run --project src/Qaly.Web --launch-profile Qaly.LocalPreview --no-restore
```

Mỗi lần sửa Vue/TypeScript trong chế độ này phải chạy lại `npm run build` rồi reload trang. Nếu cần sửa UI liên tục, dùng `npm run dev:preview`.

## 7. Chạy toàn bộ bằng Docker

```powershell
Copy-Item .env.example .env -ErrorAction SilentlyContinue
docker compose --profile docker-web up -d --build --wait
docker compose ps
```

Mở `http://localhost:5000`. Theo dõi log web:

```powershell
docker compose logs -f qaly-web
```

Chế độ này tiện để kiểm tra container, nhưng chậm hơn cho vòng lặp sửa UI.

## 8. Tài khoản demo trên database mới

| Quyền | Email | Mật khẩu |
|---|---|---|
| Admin | `admin@qaly.dev` | `Admin@123456` |
| Manager | `minh.anh@qaly.dev` | `Qaly@123456` |
| Manager | `bao.ngoc@qaly.dev` | `Qaly@123456` |
| Member | `linh.chi@qaly.dev` | `Qaly@123456` |

Database cũ giữ password đã seed trước đó. Nếu thông tin trên không đăng nhập được, kiểm tra bạn có đang dùng volume/database cũ hay không trước khi reset.

## 9. Dừng dự án

- Nhấn `Ctrl+C` tại terminal chạy preview để dừng backend và frontend.
- Script cố ý giữ container hạ tầng để lần sau khởi động nhanh hơn.

Dừng container nhưng giữ dữ liệu:

```powershell
docker compose stop
```

Xóa container/network nhưng vẫn giữ named volumes:

```powershell
docker compose down
```

Reset sạch database và toàn bộ dữ liệu Docker local — **lệnh này xóa dữ liệu, chỉ dùng khi chắc chắn không cần giữ dữ liệu local**:

```powershell
docker compose down -v
```

Sau đó chạy lại `npm run dev:preview`; migrations và rich demo seed sẽ được tạo lại.

## 10. Kiểm tra trước khi demo hoặc trước khi đẩy code

Kiểm tra nhanh:

```powershell
npm run typecheck
npm run build
dotnet build Qaly_project.slnx --configuration Release
```

Kiểm tra đầy đủ hơn:

```powershell
npm run test:unit
dotnet test Qaly_project.slnx --configuration Release
npm run test:e2e:basic
```

Health endpoints khi app đang chạy:

```text
https://localhost:5005/health/live
https://localhost:5005/health/ready
```

## 11. Xử lý lỗi thường gặp

### Cổng 5005 hoặc 5173 đang được sử dụng

```powershell
Get-NetTCPConnection -State Listen -LocalPort 5005,5173 |
    Select-Object LocalPort, OwningProcess
```

Xem đúng process trước khi dừng:

```powershell
Get-Process -Id <PID>
Stop-Process -Id <PID>
```

### Docker chưa sẵn sàng

Mở Docker Desktop, chờ trạng thái engine running rồi kiểm tra:

```powershell
docker info
docker compose ps
```

### SQL Server chưa healthy

```powershell
docker compose logs --tail 100 qaly-sqlserver
```

Nếu vừa tạo container, SQL Server có thể cần khoảng 30–60 giây để healthy.

### Trang vẫn hiện UI cũ

- Với `npm run dev:preview`: xác nhận terminal Vite vẫn chạy, sau đó hard reload trang.
- Với `dotnet run` dùng bundle: chạy `npm run build`, restart backend rồi reload.
- Không chạy đồng thời preview cũ và preview mới trên cùng cổng.

### HTTPS báo certificate không tin cậy

```powershell
dotnet dev-certs https --trust
```

Sau đó đóng tab cũ và mở lại `https://localhost:5005`.

### AI bị treo ở trạng thái queued

- Xác nhận backend được chạy với `AI_JOB_V4_WORKER_ENABLED=true`; các lệnh `dev:preview` đã tự đặt giá trị này.
- Kiểm tra API key trong `.env` nếu capability cần provider cloud thật.
- Xem log backend và Seq; không coi phản hồi fallback là bằng chứng provider thật đã chạy.

### Cần xem log tập trung

- Backend/frontend: terminal đang chạy preview.
- Seq: `http://localhost:8081`.
- Email local: `http://localhost:8025`.
- Container: `docker compose logs --tail 100 <ten-service>`.

## Tóm tắt ngắn nhất

Máy mới:

```powershell
git clone https://github.com/JackSilver08/Qaly_project.git
Set-Location Qaly_project
Copy-Item .env.example .env
npm ci
dotnet restore
dotnet dev-certs https --trust
npm run dev:preview
```

Máy đã setup:

```powershell
git pull --ff-only origin main
npm run dev:preview
```

Mở `https://localhost:5005` trong browser thường hoặc tab Browser của Codex. Nhấn `Ctrl+C` khi muốn dừng preview.
