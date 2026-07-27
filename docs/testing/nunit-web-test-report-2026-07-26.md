# Báo cáo kiểm thử NUnit toàn bề mặt web Qaly

- Ngày chạy: 26/07/2026
- Môi trường: local `Testing`, .NET 10, EF Core InMemory
- Phạm vi: Razor/SPA routes, frontend bundle contracts, API route contracts,
  authentication boundaries, unit tests, integration tests và các feature smoke
  tests hiện hữu

## Kết quả tổng quan

| Suite | Framework | Baseline |
|---|---|---:|
| Qaly.UnitTests | xUnit | 340 pass |
| Qaly.IntegrationTests | xUnit | 71 pass |
| Qaly.WebFeatureTests | NUnit | 9 pass |

Suite NUnit được mở rộng thêm các kiểm tra:

- 20 URL đại diện cho toàn bộ route SPA khai báo trong Vue Router.
- Tất cả page component lazy-loaded trong router phải tồn tại trên filesystem.
- Tất cả asset JS/CSS được application shell tham chiếu phải tải được.
- Tất cả controller action phải khai báo rõ `Authorize` hoặc `AllowAnonymous`.
- Không được có hai API endpoint trùng HTTP method và route template.

Kết quả verification cuối sau khi mở rộng: **441 pass, 3 fail, 0 skipped**
trên tổng **444 test cases**. Ba failure đều tương ứng với ba lỗi route được
liệt kê bên dưới; unit và integration suites không có failure.

## Lỗi phát hiện

### WEB-ROUTE-001 — Trang quản trị người điều phối không mở được bằng direct URL

- Mức độ: High
- URL: `GET /admin/moderators`
- Người dùng thử nghiệm: đã xác thực, role `Admin`
- Mong đợi: HTTP 200 và application shell để Vue Router hiển thị
  `ModeratorAssignmentsPage`.
- Thực tế: HTTP 404.
- Tái hiện:

  ```powershell
  dotnet test tests/Qaly.WebFeatureTests/Qaly.WebFeatureTests.csproj `
    --filter "FullyQualifiedName~Spa_route_returns_the_application_shell"
  ```

- Nguyên nhân có khả năng: Vue Router khai báo `/admin/moderators`, nhưng
  `AddQalyRazorPages` chưa ánh xạ URL này về Razor page `/Index`.

### WEB-ROUTE-002 — Trang người dùng tổ chức không mở được bằng direct URL

- Mức độ: High
- URL: `GET /organizations/users`
- Người dùng thử nghiệm: đã xác thực
- Mong đợi: HTTP 200 và application shell để Vue Router hiển thị
  `OrganizationUsersPage`.
- Thực tế: HTTP 404.
- Nguyên nhân có khả năng: route có trong Vue Router nhưng không có page route
  tương ứng ở server.

### WEB-ROUTE-003 — Vue route-not-found không thể xử lý URL lạ

- Mức độ: Medium
- URL: `GET /route-that-does-not-exist`
- Mong đợi: HTTP 200 và application shell; Vue Router hiển thị
  `RouteErrorPage`.
- Thực tế: server trả HTTP 404 trước khi Vue Router được tải.
- Nguyên nhân có khả năng: ứng dụng chưa cấu hình fallback route về `/Index`.

## Các kiểm tra đã đạt

- 17 route trang được ánh xạ hiện tại trả application shell hợp lệ.
- `/admin/users` trả application shell cho role `Admin`.
- Các JS/CSS entry asset của shell tồn tại và tải được.
- Mọi Vue page được router import đều tồn tại.
- Không phát hiện API route ambiguity.
- Không phát hiện controller action thiếu ranh giới xác thực rõ ràng.
- Các smoke flow hiện hữu qua HTTP/database vẫn đạt: project, task, wiki,
  dashboard, analytics, Erumi fallback, group, users và static demo assets.

## Giới hạn và rủi ro còn mở

- NUnit với `WebApplicationFactory` kiểm thử server, API và HTML shell; nó không
  thực thi Vue trong trình duyệt. Vì vậy lỗi render DOM, thao tác chuột, responsive,
  accessibility và JavaScript runtime cần Playwright hoặc browser automation.
- Các external dependency được mock hoặc tắt trong môi trường test, gồm Redis,
  AI provider và hosted services.
- Suite kiểm tra contract trên toàn bộ 262 controller action nhưng chưa gửi payload
  nghiệp vụ riêng cho từng action. Các luồng quan trọng hiện được kiểm tra bởi
  unit/integration/feature tests tương ứng.
- Unit và integration suites hiện hữu dùng xUnit. Chúng vẫn được chạy để bảo vệ
  regression; không chuyển framework chỉ để đổi cú pháp test.
