# QALY Workspace — API Specification v2.0

**Phiên bản:** v2.0 — Production/Japan-style specification  
**Ngày:** 16/05/2026  
**Phạm vi:** Dự án web quản lý dự án phần mềm cho doanh nghiệp outsource vừa và nhỏ.  
**Định hướng:** Jira + Notion + Mini Zalo + AI Assistant + Document Mining.  
**Ghi chú kiến trúc:** Tài liệu v2.0 mở rộng từ SRS/TKHT v1.0. Các phần dưới đây là target design để nhóm có thể triển khai 80–90% chức năng web trong 10–11 tuần, không bắt buộc implement toàn bộ bảng P2 nếu thiếu thời gian.

---


## 0. Quy ước API

- Base path: `/api/v1` cho target v2. Nếu giữ codebase hiện tại, có thể map dần từ `/api/[controller]` sang `/api/v1/...` bằng route versioning.
- Auth: Cookie session với ASP.NET Core cho web app; API Key cho webhook/integration; JWT chỉ dùng nếu sau này tách public API/mobile.
- Response chuẩn:
```json
{
  "success": true,
  "message": "OK",
  "data": {},
  "meta": { "page": 1, "limit": 20, "total": 100 },
  "correlationId": "..."
}
```
- Error chuẩn: 400 validation, 401 unauthenticated, 403 unauthorized, 404 not found/masked, 409 conflict, 422 business rule violation, 429 rate limit.
- Pagination: offset cho demo đơn giản; cursor cho chat/audit/log high-write.
- Side effects bắt buộc ghi trong service: audit, notification, webhook, cache invalidation, SignalR event, background job.

---

# GROUP — AUTH


## API-AUTH-001: `POST /api/v1/auth/register`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Auth Register |
| Method | POST |
| Path | `/api/v1/auth/register` |
| Auth | Public |
| Role | guest/user |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-AUTH-002: `POST /api/v1/auth/login`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Auth Login |
| Method | POST |
| Path | `/api/v1/auth/login` |
| Auth | Public |
| Role | guest/user |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-AUTH-003: `POST /api/v1/auth/logout`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Auth Logout |
| Method | POST |
| Path | `/api/v1/auth/logout` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-AUTH-004: `POST /api/v1/auth/refresh-token`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Auth Refresh Token |
| Method | POST |
| Path | `/api/v1/auth/refresh-token` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-AUTH-005: `GET /api/v1/auth/me`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Auth Me |
| Method | GET |
| Path | `/api/v1/auth/me` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-AUTH-006: `PATCH /api/v1/auth/me`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Auth Me |
| Method | PATCH |
| Path | `/api/v1/auth/me` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-AUTH-007: `POST /api/v1/auth/verify-email`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Auth Verify Email |
| Method | POST |
| Path | `/api/v1/auth/verify-email` |
| Auth | Public |
| Role | guest/user |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-AUTH-008: `POST /api/v1/auth/resend-verification`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Auth Resend Verification |
| Method | POST |
| Path | `/api/v1/auth/resend-verification` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-AUTH-009: `POST /api/v1/auth/forgot-password`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Auth Forgot Password |
| Method | POST |
| Path | `/api/v1/auth/forgot-password` |
| Auth | Public |
| Role | guest/user |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-AUTH-010: `POST /api/v1/auth/reset-password`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Auth Reset Password |
| Method | POST |
| Path | `/api/v1/auth/reset-password` |
| Auth | Public |
| Role | guest/user |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-AUTH-011: `POST /api/v1/auth/change-password`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Auth Change Password |
| Method | POST |
| Path | `/api/v1/auth/change-password` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-AUTH-012: `GET /api/v1/auth/sessions`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Auth Sessions |
| Method | GET |
| Path | `/api/v1/auth/sessions` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-AUTH-013: `DELETE /api/v1/auth/sessions/{sessionId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Auth Sessions Sessionid |
| Method | DELETE |
| Path | `/api/v1/auth/sessions/{sessionId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `sessionId` | UUID/String | ID của sessionId |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

# GROUP — ORGANIZATION


## API-ORGANIZATION-001: `POST /api/v1/organizations`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Organizations |
| Method | POST |
| Path | `/api/v1/organizations` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-ORGANIZATION-002: `GET /api/v1/organizations`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Organizations |
| Method | GET |
| Path | `/api/v1/organizations` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-ORGANIZATION-003: `GET /api/v1/organizations/{orgId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Organizations Orgid |
| Method | GET |
| Path | `/api/v1/organizations/{orgId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `orgId` | UUID/String | ID của orgId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-ORGANIZATION-004: `PATCH /api/v1/organizations/{orgId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Organizations Orgid |
| Method | PATCH |
| Path | `/api/v1/organizations/{orgId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `orgId` | UUID/String | ID của orgId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-ORGANIZATION-005: `DELETE /api/v1/organizations/{orgId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Organizations Orgid |
| Method | DELETE |
| Path | `/api/v1/organizations/{orgId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `orgId` | UUID/String | ID của orgId |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-ORGANIZATION-006: `GET /api/v1/organizations/{orgId}/members`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Organizations Orgid Members |
| Method | GET |
| Path | `/api/v1/organizations/{orgId}/members` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `orgId` | UUID/String | ID của orgId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-ORGANIZATION-007: `POST /api/v1/organizations/{orgId}/members/invite`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Organizations Orgid Members Invite |
| Method | POST |
| Path | `/api/v1/organizations/{orgId}/members/invite` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `orgId` | UUID/String | ID của orgId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-ORGANIZATION-008: `PATCH /api/v1/organizations/{orgId}/members/{userId}/role`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Organizations Orgid Members Userid Role |
| Method | PATCH |
| Path | `/api/v1/organizations/{orgId}/members/{userId}/role` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `orgId` | UUID/String | ID của orgId |
| `userId` | UUID/String | ID của userId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-ORGANIZATION-009: `DELETE /api/v1/organizations/{orgId}/members/{userId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Organizations Orgid Members Userid |
| Method | DELETE |
| Path | `/api/v1/organizations/{orgId}/members/{userId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `orgId` | UUID/String | ID của orgId |
| `userId` | UUID/String | ID của userId |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-ORGANIZATION-010: `GET /api/v1/organizations/{orgId}/stats`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Organizations Orgid Stats |
| Method | GET |
| Path | `/api/v1/organizations/{orgId}/stats` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `orgId` | UUID/String | ID của orgId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

# GROUP — PROJECT


## API-PROJECT-001: `POST /api/v1/orgs/{orgId}/projects`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Orgs Orgid Projects |
| Method | POST |
| Path | `/api/v1/orgs/{orgId}/projects` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `orgId` | UUID/String | ID của orgId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-PROJECT-002: `GET /api/v1/orgs/{orgId}/projects`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Orgs Orgid Projects |
| Method | GET |
| Path | `/api/v1/orgs/{orgId}/projects` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `orgId` | UUID/String | ID của orgId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-PROJECT-003: `GET /api/v1/projects/{projectId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid |
| Method | GET |
| Path | `/api/v1/projects/{projectId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-PROJECT-004: `PATCH /api/v1/projects/{projectId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid |
| Method | PATCH |
| Path | `/api/v1/projects/{projectId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-PROJECT-005: `DELETE /api/v1/projects/{projectId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid |
| Method | DELETE |
| Path | `/api/v1/projects/{projectId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-PROJECT-006: `POST /api/v1/projects/{projectId}/archive`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Archive |
| Method | POST |
| Path | `/api/v1/projects/{projectId}/archive` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-PROJECT-007: `POST /api/v1/projects/{projectId}/restore`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Restore |
| Method | POST |
| Path | `/api/v1/projects/{projectId}/restore` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-PROJECT-008: `GET /api/v1/projects/{projectId}/members`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Members |
| Method | GET |
| Path | `/api/v1/projects/{projectId}/members` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-PROJECT-009: `POST /api/v1/projects/{projectId}/members`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Members |
| Method | POST |
| Path | `/api/v1/projects/{projectId}/members` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-PROJECT-010: `PATCH /api/v1/projects/{projectId}/members/{userId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Members Userid |
| Method | PATCH |
| Path | `/api/v1/projects/{projectId}/members/{userId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |
| `userId` | UUID/String | ID của userId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-PROJECT-011: `DELETE /api/v1/projects/{projectId}/members/{userId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Members Userid |
| Method | DELETE |
| Path | `/api/v1/projects/{projectId}/members/{userId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |
| `userId` | UUID/String | ID của userId |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-PROJECT-012: `GET /api/v1/projects/{projectId}/stats`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Stats |
| Method | GET |
| Path | `/api/v1/projects/{projectId}/stats` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-PROJECT-013: `GET /api/v1/projects/{projectId}/health`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Health |
| Method | GET |
| Path | `/api/v1/projects/{projectId}/health` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-PROJECT-014: `POST /api/v1/projects/{projectId}/duplicate`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Duplicate |
| Method | POST |
| Path | `/api/v1/projects/{projectId}/duplicate` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

# GROUP — TASK


## API-TASK-001: `POST /api/v1/projects/{projectId}/tasks`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Tasks |
| Method | POST |
| Path | `/api/v1/projects/{projectId}/tasks` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-002: `GET /api/v1/projects/{projectId}/tasks`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Tasks |
| Method | GET |
| Path | `/api/v1/projects/{projectId}/tasks` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-003: `GET /api/v1/tasks/{taskId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Taskid |
| Method | GET |
| Path | `/api/v1/tasks/{taskId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-004: `PATCH /api/v1/tasks/{taskId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Taskid |
| Method | PATCH |
| Path | `/api/v1/tasks/{taskId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-005: `DELETE /api/v1/tasks/{taskId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Taskid |
| Method | DELETE |
| Path | `/api/v1/tasks/{taskId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-006: `POST /api/v1/tasks/{taskId}/assign`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Taskid Assign |
| Method | POST |
| Path | `/api/v1/tasks/{taskId}/assign` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-007: `DELETE /api/v1/tasks/{taskId}/assign/{userId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Taskid Assign Userid |
| Method | DELETE |
| Path | `/api/v1/tasks/{taskId}/assign/{userId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |
| `userId` | UUID/String | ID của userId |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-008: `POST /api/v1/tasks/{taskId}/subtasks`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Taskid Subtasks |
| Method | POST |
| Path | `/api/v1/tasks/{taskId}/subtasks` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-009: `GET /api/v1/tasks/{taskId}/subtasks`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Taskid Subtasks |
| Method | GET |
| Path | `/api/v1/tasks/{taskId}/subtasks` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-010: `POST /api/v1/tasks/{taskId}/dependencies`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Taskid Dependencies |
| Method | POST |
| Path | `/api/v1/tasks/{taskId}/dependencies` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-011: `DELETE /api/v1/tasks/{taskId}/dependencies/{depId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Taskid Dependencies Depid |
| Method | DELETE |
| Path | `/api/v1/tasks/{taskId}/dependencies/{depId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |
| `depId` | UUID/String | ID của depId |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-012: `POST /api/v1/tasks/{taskId}/watch`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Taskid Watch |
| Method | POST |
| Path | `/api/v1/tasks/{taskId}/watch` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-013: `DELETE /api/v1/tasks/{taskId}/watch`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Taskid Watch |
| Method | DELETE |
| Path | `/api/v1/tasks/{taskId}/watch` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-014: `POST /api/v1/tasks/{taskId}/status-change-request`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Taskid Status Change Request |
| Method | POST |
| Path | `/api/v1/tasks/{taskId}/status-change-request` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-015: `PATCH /api/v1/tasks/{taskId}/status-change-request/{reqId}/approve`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Taskid Status Change Request Reqid Approve |
| Method | PATCH |
| Path | `/api/v1/tasks/{taskId}/status-change-request/{reqId}/approve` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |
| `reqId` | UUID/String | ID của reqId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-016: `PATCH /api/v1/tasks/{taskId}/status-change-request/{reqId}/reject`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Taskid Status Change Request Reqid Reject |
| Method | PATCH |
| Path | `/api/v1/tasks/{taskId}/status-change-request/{reqId}/reject` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |
| `reqId` | UUID/String | ID của reqId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-017: `POST /api/v1/tasks/{taskId}/evidences`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Taskid Evidences |
| Method | POST |
| Path | `/api/v1/tasks/{taskId}/evidences` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-018: `GET /api/v1/tasks/{taskId}/evidences`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Taskid Evidences |
| Method | GET |
| Path | `/api/v1/tasks/{taskId}/evidences` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-019: `GET /api/v1/tasks/{taskId}/activity`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Taskid Activity |
| Method | GET |
| Path | `/api/v1/tasks/{taskId}/activity` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-020: `POST /api/v1/tasks/{taskId}/time-logs`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Taskid Time Logs |
| Method | POST |
| Path | `/api/v1/tasks/{taskId}/time-logs` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-021: `POST /api/v1/tasks/batch-status`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Batch Status |
| Method | POST |
| Path | `/api/v1/tasks/batch-status` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TASK-022: `POST /api/v1/tasks/batch-archive`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Batch Archive |
| Method | POST |
| Path | `/api/v1/tasks/batch-archive` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

# GROUP — KANBAN


## API-KANBAN-001: `GET /api/v1/projects/{projectId}/kanban`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Kanban |
| Method | GET |
| Path | `/api/v1/projects/{projectId}/kanban` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-KANBAN-002: `POST /api/v1/projects/{projectId}/kanban/columns`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Kanban Columns |
| Method | POST |
| Path | `/api/v1/projects/{projectId}/kanban/columns` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-KANBAN-003: `PATCH /api/v1/kanban/columns/{columnId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Kanban Columns Columnid |
| Method | PATCH |
| Path | `/api/v1/kanban/columns/{columnId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `columnId` | UUID/String | ID của columnId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-KANBAN-004: `DELETE /api/v1/kanban/columns/{columnId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Kanban Columns Columnid |
| Method | DELETE |
| Path | `/api/v1/kanban/columns/{columnId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `columnId` | UUID/String | ID của columnId |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-KANBAN-005: `PATCH /api/v1/kanban/columns/reorder`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Kanban Columns Reorder |
| Method | PATCH |
| Path | `/api/v1/kanban/columns/reorder` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-KANBAN-006: `POST /api/v1/kanban/columns/{columnId}/tasks/move`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Kanban Columns Columnid Tasks Move |
| Method | POST |
| Path | `/api/v1/kanban/columns/{columnId}/tasks/move` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `columnId` | UUID/String | ID của columnId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-KANBAN-007: `GET /api/v1/projects/{projectId}/kanban/filters`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Kanban Filters |
| Method | GET |
| Path | `/api/v1/projects/{projectId}/kanban/filters` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-KANBAN-008: `POST /api/v1/projects/{projectId}/kanban/filters`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Kanban Filters |
| Method | POST |
| Path | `/api/v1/projects/{projectId}/kanban/filters` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

# GROUP — SPRINT


## API-SPRINT-001: `POST /api/v1/projects/{projectId}/sprints`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Sprints |
| Method | POST |
| Path | `/api/v1/projects/{projectId}/sprints` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-SPRINT-002: `GET /api/v1/projects/{projectId}/sprints`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Sprints |
| Method | GET |
| Path | `/api/v1/projects/{projectId}/sprints` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-SPRINT-003: `GET /api/v1/sprints/{sprintId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Sprints Sprintid |
| Method | GET |
| Path | `/api/v1/sprints/{sprintId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `sprintId` | UUID/String | ID của sprintId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-SPRINT-004: `PATCH /api/v1/sprints/{sprintId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Sprints Sprintid |
| Method | PATCH |
| Path | `/api/v1/sprints/{sprintId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `sprintId` | UUID/String | ID của sprintId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-SPRINT-005: `POST /api/v1/sprints/{sprintId}/start`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Sprints Sprintid Start |
| Method | POST |
| Path | `/api/v1/sprints/{sprintId}/start` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `sprintId` | UUID/String | ID của sprintId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-SPRINT-006: `POST /api/v1/sprints/{sprintId}/complete`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Sprints Sprintid Complete |
| Method | POST |
| Path | `/api/v1/sprints/{sprintId}/complete` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `sprintId` | UUID/String | ID của sprintId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-SPRINT-007: `POST /api/v1/sprints/{sprintId}/tasks`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Sprints Sprintid Tasks |
| Method | POST |
| Path | `/api/v1/sprints/{sprintId}/tasks` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `sprintId` | UUID/String | ID của sprintId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-SPRINT-008: `DELETE /api/v1/sprints/{sprintId}/tasks/{taskId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Sprints Sprintid Tasks Taskid |
| Method | DELETE |
| Path | `/api/v1/sprints/{sprintId}/tasks/{taskId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `sprintId` | UUID/String | ID của sprintId |
| `taskId` | UUID/String | ID của taskId |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

# GROUP — TIMELINE


## API-TIMELINE-001: `GET /api/v1/projects/{projectId}/timeline`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Timeline |
| Method | GET |
| Path | `/api/v1/projects/{projectId}/timeline` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TIMELINE-002: `PATCH /api/v1/projects/{projectId}/timeline/tasks/{taskId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Timeline Tasks Taskid |
| Method | PATCH |
| Path | `/api/v1/projects/{projectId}/timeline/tasks/{taskId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |
| `taskId` | UUID/String | ID của taskId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TIMELINE-003: `GET /api/v1/projects/{projectId}/timeline/critical-path`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Timeline Critical Path |
| Method | GET |
| Path | `/api/v1/projects/{projectId}/timeline/critical-path` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-TIMELINE-004: `POST /api/v1/projects/{projectId}/timeline/baseline`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Timeline Baseline |
| Method | POST |
| Path | `/api/v1/projects/{projectId}/timeline/baseline` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

# GROUP — COMMENT


## API-COMMENT-001: `POST /api/v1/tasks/{taskId}/comments`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Taskid Comments |
| Method | POST |
| Path | `/api/v1/tasks/{taskId}/comments` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-COMMENT-002: `GET /api/v1/tasks/{taskId}/comments`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Tasks Taskid Comments |
| Method | GET |
| Path | `/api/v1/tasks/{taskId}/comments` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-COMMENT-003: `PATCH /api/v1/comments/{commentId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Comments Commentid |
| Method | PATCH |
| Path | `/api/v1/comments/{commentId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `commentId` | UUID/String | ID của commentId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-COMMENT-004: `DELETE /api/v1/comments/{commentId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Comments Commentid |
| Method | DELETE |
| Path | `/api/v1/comments/{commentId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `commentId` | UUID/String | ID của commentId |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-COMMENT-005: `POST /api/v1/comments/{commentId}/reactions`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Comments Commentid Reactions |
| Method | POST |
| Path | `/api/v1/comments/{commentId}/reactions` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `commentId` | UUID/String | ID của commentId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-COMMENT-006: `DELETE /api/v1/comments/{commentId}/reactions/{reactionId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Comments Commentid Reactions Reactionid |
| Method | DELETE |
| Path | `/api/v1/comments/{commentId}/reactions/{reactionId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `commentId` | UUID/String | ID của commentId |
| `reactionId` | UUID/String | ID của reactionId |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

# GROUP — CHAT


## API-CHAT-001: `POST /api/v1/chat/rooms`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Chat Rooms |
| Method | POST |
| Path | `/api/v1/chat/rooms` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-CHAT-002: `GET /api/v1/chat/rooms`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Chat Rooms |
| Method | GET |
| Path | `/api/v1/chat/rooms` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | No Cache |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-CHAT-003: `GET /api/v1/chat/rooms/{roomId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Chat Rooms Roomid |
| Method | GET |
| Path | `/api/v1/chat/rooms/{roomId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `roomId` | UUID/String | ID của roomId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-CHAT-004: `PATCH /api/v1/chat/rooms/{roomId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Chat Rooms Roomid |
| Method | PATCH |
| Path | `/api/v1/chat/rooms/{roomId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `roomId` | UUID/String | ID của roomId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-CHAT-005: `POST /api/v1/chat/rooms/{roomId}/members`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Chat Rooms Roomid Members |
| Method | POST |
| Path | `/api/v1/chat/rooms/{roomId}/members` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `roomId` | UUID/String | ID của roomId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-CHAT-006: `DELETE /api/v1/chat/rooms/{roomId}/members/{userId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Chat Rooms Roomid Members Userid |
| Method | DELETE |
| Path | `/api/v1/chat/rooms/{roomId}/members/{userId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `roomId` | UUID/String | ID của roomId |
| `userId` | UUID/String | ID của userId |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-CHAT-007: `POST /api/v1/chat/rooms/{roomId}/messages`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Chat Rooms Roomid Messages |
| Method | POST |
| Path | `/api/v1/chat/rooms/{roomId}/messages` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `roomId` | UUID/String | ID của roomId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-CHAT-008: `GET /api/v1/chat/rooms/{roomId}/messages`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Chat Rooms Roomid Messages |
| Method | GET |
| Path | `/api/v1/chat/rooms/{roomId}/messages` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `roomId` | UUID/String | ID của roomId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-CHAT-009: `PATCH /api/v1/chat/messages/{messageId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Chat Messages Messageid |
| Method | PATCH |
| Path | `/api/v1/chat/messages/{messageId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `messageId` | UUID/String | ID của messageId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-CHAT-010: `DELETE /api/v1/chat/messages/{messageId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Chat Messages Messageid |
| Method | DELETE |
| Path | `/api/v1/chat/messages/{messageId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `messageId` | UUID/String | ID của messageId |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-CHAT-011: `POST /api/v1/chat/messages/{messageId}/reactions`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Chat Messages Messageid Reactions |
| Method | POST |
| Path | `/api/v1/chat/messages/{messageId}/reactions` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `messageId` | UUID/String | ID của messageId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-CHAT-012: `POST /api/v1/chat/messages/{messageId}/pin`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Chat Messages Messageid Pin |
| Method | POST |
| Path | `/api/v1/chat/messages/{messageId}/pin` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `messageId` | UUID/String | ID của messageId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-CHAT-013: `POST /api/v1/chat/messages/{messageId}/link-task`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Chat Messages Messageid Link Task |
| Method | POST |
| Path | `/api/v1/chat/messages/{messageId}/link-task` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `messageId` | UUID/String | ID của messageId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-CHAT-014: `GET /api/v1/chat/rooms/{roomId}/pinned-messages`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Chat Rooms Roomid Pinned Messages |
| Method | GET |
| Path | `/api/v1/chat/rooms/{roomId}/pinned-messages` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `roomId` | UUID/String | ID của roomId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

# GROUP — MEETING


## API-MEETING-001: `POST /api/v1/projects/{projectId}/meetings`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Meetings |
| Method | POST |
| Path | `/api/v1/projects/{projectId}/meetings` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-MEETING-002: `GET /api/v1/projects/{projectId}/meetings`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Meetings |
| Method | GET |
| Path | `/api/v1/projects/{projectId}/meetings` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-MEETING-003: `GET /api/v1/meetings/{meetingId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Meetings Meetingid |
| Method | GET |
| Path | `/api/v1/meetings/{meetingId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `meetingId` | UUID/String | ID của meetingId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-MEETING-004: `PATCH /api/v1/meetings/{meetingId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Meetings Meetingid |
| Method | PATCH |
| Path | `/api/v1/meetings/{meetingId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `meetingId` | UUID/String | ID của meetingId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-MEETING-005: `POST /api/v1/meetings/{meetingId}/notes`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Meetings Meetingid Notes |
| Method | POST |
| Path | `/api/v1/meetings/{meetingId}/notes` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `meetingId` | UUID/String | ID của meetingId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-MEETING-006: `PATCH /api/v1/meetings/{meetingId}/notes`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Meetings Meetingid Notes |
| Method | PATCH |
| Path | `/api/v1/meetings/{meetingId}/notes` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `meetingId` | UUID/String | ID của meetingId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-MEETING-007: `POST /api/v1/meetings/{meetingId}/action-items`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Meetings Meetingid Action Items |
| Method | POST |
| Path | `/api/v1/meetings/{meetingId}/action-items` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `meetingId` | UUID/String | ID của meetingId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-MEETING-008: `POST /api/v1/meetings/{meetingId}/ai-summarize`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Meetings Meetingid Ai Summarize |
| Method | POST |
| Path | `/api/v1/meetings/{meetingId}/ai-summarize` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `meetingId` | UUID/String | ID của meetingId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

# GROUP — WIKI


## API-WIKI-001: `POST /api/v1/projects/{projectId}/wiki/pages`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Wiki Pages |
| Method | POST |
| Path | `/api/v1/projects/{projectId}/wiki/pages` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-WIKI-002: `GET /api/v1/projects/{projectId}/wiki/pages`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Wiki Pages |
| Method | GET |
| Path | `/api/v1/projects/{projectId}/wiki/pages` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-WIKI-003: `GET /api/v1/wiki/pages/{pageId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Wiki Pages Pageid |
| Method | GET |
| Path | `/api/v1/wiki/pages/{pageId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `pageId` | UUID/String | ID của pageId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-WIKI-004: `PATCH /api/v1/wiki/pages/{pageId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Wiki Pages Pageid |
| Method | PATCH |
| Path | `/api/v1/wiki/pages/{pageId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `pageId` | UUID/String | ID của pageId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-WIKI-005: `DELETE /api/v1/wiki/pages/{pageId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Wiki Pages Pageid |
| Method | DELETE |
| Path | `/api/v1/wiki/pages/{pageId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `pageId` | UUID/String | ID của pageId |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-WIKI-006: `GET /api/v1/wiki/pages/{pageId}/versions`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Wiki Pages Pageid Versions |
| Method | GET |
| Path | `/api/v1/wiki/pages/{pageId}/versions` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `pageId` | UUID/String | ID của pageId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-WIKI-007: `GET /api/v1/wiki/pages/{pageId}/versions/{versionId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Wiki Pages Pageid Versions Versionid |
| Method | GET |
| Path | `/api/v1/wiki/pages/{pageId}/versions/{versionId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `pageId` | UUID/String | ID của pageId |
| `versionId` | UUID/String | ID của versionId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-WIKI-008: `POST /api/v1/wiki/pages/{pageId}/restore/{versionId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Wiki Pages Pageid Restore Versionid |
| Method | POST |
| Path | `/api/v1/wiki/pages/{pageId}/restore/{versionId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `pageId` | UUID/String | ID của pageId |
| `versionId` | UUID/String | ID của versionId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-WIKI-009: `POST /api/v1/wiki/pages/{pageId}/watch`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Wiki Pages Pageid Watch |
| Method | POST |
| Path | `/api/v1/wiki/pages/{pageId}/watch` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `pageId` | UUID/String | ID của pageId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-WIKI-010: `GET /api/v1/projects/{projectId}/wiki/search`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Wiki Search |
| Method | GET |
| Path | `/api/v1/projects/{projectId}/wiki/search` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

# GROUP — FILE


## API-FILE-001: `POST /api/v1/files/upload`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Files Upload |
| Method | POST |
| Path | `/api/v1/files/upload` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-FILE-002: `GET /api/v1/files/{fileId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Files Fileid |
| Method | GET |
| Path | `/api/v1/files/{fileId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `fileId` | UUID/String | ID của fileId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-FILE-003: `DELETE /api/v1/files/{fileId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Files Fileid |
| Method | DELETE |
| Path | `/api/v1/files/{fileId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `fileId` | UUID/String | ID của fileId |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-FILE-004: `GET /api/v1/files/{fileId}/download`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Files Fileid Download |
| Method | GET |
| Path | `/api/v1/files/{fileId}/download` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `fileId` | UUID/String | ID của fileId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-FILE-005: `POST /api/v1/files/{fileId}/share`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Files Fileid Share |
| Method | POST |
| Path | `/api/v1/files/{fileId}/share` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `fileId` | UUID/String | ID của fileId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-FILE-006: `GET /api/v1/files/shared/{shareToken}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Files Shared Sharetoken |
| Method | GET |
| Path | `/api/v1/files/shared/{shareToken}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `shareToken` | UUID/String | ID của shareToken |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

# GROUP — NOTIFICATION


## API-NOTIFICATION-001: `GET /api/v1/notifications`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Notifications |
| Method | GET |
| Path | `/api/v1/notifications` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-NOTIFICATION-002: `PATCH /api/v1/notifications/{notificationId}/read`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Notifications Notificationid Read |
| Method | PATCH |
| Path | `/api/v1/notifications/{notificationId}/read` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `notificationId` | UUID/String | ID của notificationId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-NOTIFICATION-003: `POST /api/v1/notifications/read-all`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Notifications Read All |
| Method | POST |
| Path | `/api/v1/notifications/read-all` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-NOTIFICATION-004: `GET /api/v1/notifications/preferences`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Notifications Preferences |
| Method | GET |
| Path | `/api/v1/notifications/preferences` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-NOTIFICATION-005: `PATCH /api/v1/notifications/preferences`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Notifications Preferences |
| Method | PATCH |
| Path | `/api/v1/notifications/preferences` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-NOTIFICATION-006: `DELETE /api/v1/notifications/{notificationId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Notifications Notificationid |
| Method | DELETE |
| Path | `/api/v1/notifications/{notificationId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `notificationId` | UUID/String | ID của notificationId |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

# GROUP — WEBHOOK


## API-WEBHOOK-001: `POST /api/v1/projects/{projectId}/webhooks`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Webhooks |
| Method | POST |
| Path | `/api/v1/projects/{projectId}/webhooks` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-WEBHOOK-002: `GET /api/v1/projects/{projectId}/webhooks`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Webhooks |
| Method | GET |
| Path | `/api/v1/projects/{projectId}/webhooks` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-WEBHOOK-003: `GET /api/v1/webhooks/{webhookId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Webhooks Webhookid |
| Method | GET |
| Path | `/api/v1/webhooks/{webhookId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `webhookId` | UUID/String | ID của webhookId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-WEBHOOK-004: `PATCH /api/v1/webhooks/{webhookId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Webhooks Webhookid |
| Method | PATCH |
| Path | `/api/v1/webhooks/{webhookId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `webhookId` | UUID/String | ID của webhookId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-WEBHOOK-005: `DELETE /api/v1/webhooks/{webhookId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Webhooks Webhookid |
| Method | DELETE |
| Path | `/api/v1/webhooks/{webhookId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `webhookId` | UUID/String | ID của webhookId |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-WEBHOOK-006: `POST /api/v1/webhooks/{webhookId}/test`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Webhooks Webhookid Test |
| Method | POST |
| Path | `/api/v1/webhooks/{webhookId}/test` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `webhookId` | UUID/String | ID của webhookId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

# GROUP — AI


## API-AI-001: `POST /api/v1/ai/conversations`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Ai Conversations |
| Method | POST |
| Path | `/api/v1/ai/conversations` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-AI-002: `GET /api/v1/ai/conversations`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Ai Conversations |
| Method | GET |
| Path | `/api/v1/ai/conversations` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-AI-003: `GET /api/v1/ai/conversations/{conversationId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Ai Conversations Conversationid |
| Method | GET |
| Path | `/api/v1/ai/conversations/{conversationId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `conversationId` | UUID/String | ID của conversationId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-AI-004: `POST /api/v1/ai/conversations/{conversationId}/messages`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Ai Conversations Conversationid Messages |
| Method | POST |
| Path | `/api/v1/ai/conversations/{conversationId}/messages` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `conversationId` | UUID/String | ID của conversationId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-AI-005: `POST /api/v1/ai/projects/{projectId}/sync-knowledge`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Ai Projects Projectid Sync Knowledge |
| Method | POST |
| Path | `/api/v1/ai/projects/{projectId}/sync-knowledge` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-AI-006: `POST /api/v1/ai/projects/{projectId}/generate-report`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Ai Projects Projectid Generate Report |
| Method | POST |
| Path | `/api/v1/ai/projects/{projectId}/generate-report` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-AI-007: `POST /api/v1/ai/tasks/{taskId}/suggest-priority`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Ai Tasks Taskid Suggest Priority |
| Method | POST |
| Path | `/api/v1/ai/tasks/{taskId}/suggest-priority` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `taskId` | UUID/String | ID của taskId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-AI-008: `POST /api/v1/ai/meetings/{meetingId}/summarize`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Ai Meetings Meetingid Summarize |
| Method | POST |
| Path | `/api/v1/ai/meetings/{meetingId}/summarize` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `meetingId` | UUID/String | ID của meetingId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

# GROUP — IMPORT


## API-IMPORT-001: `POST /api/v1/import/documents`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Import Documents |
| Method | POST |
| Path | `/api/v1/import/documents` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-IMPORT-002: `GET /api/v1/import/jobs`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Import Jobs |
| Method | GET |
| Path | `/api/v1/import/jobs` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-IMPORT-003: `GET /api/v1/import/jobs/{jobId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Import Jobs Jobid |
| Method | GET |
| Path | `/api/v1/import/jobs/{jobId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `jobId` | UUID/String | ID của jobId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-IMPORT-004: `GET /api/v1/import/jobs/{jobId}/draft-tasks`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Import Jobs Jobid Draft Tasks |
| Method | GET |
| Path | `/api/v1/import/jobs/{jobId}/draft-tasks` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `jobId` | UUID/String | ID của jobId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-IMPORT-005: `PATCH /api/v1/import/jobs/{jobId}/draft-tasks/{draftId}/approve`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Import Jobs Jobid Draft Tasks Draftid Approve |
| Method | PATCH |
| Path | `/api/v1/import/jobs/{jobId}/draft-tasks/{draftId}/approve` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `jobId` | UUID/String | ID của jobId |
| `draftId` | UUID/String | ID của draftId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-IMPORT-006: `POST /api/v1/import/jobs/{jobId}/execute`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Import Jobs Jobid Execute |
| Method | POST |
| Path | `/api/v1/import/jobs/{jobId}/execute` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `jobId` | UUID/String | ID của jobId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-IMPORT-007: `POST /api/v1/import/csv/preview`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Import Csv Preview |
| Method | POST |
| Path | `/api/v1/import/csv/preview` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-IMPORT-008: `POST /api/v1/import/markdown/preview`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Import Markdown Preview |
| Method | POST |
| Path | `/api/v1/import/markdown/preview` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

# GROUP — ADMIN


## API-ADMIN-001: `GET /api/v1/admin/dashboard`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Admin Dashboard |
| Method | GET |
| Path | `/api/v1/admin/dashboard` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-ADMIN-002: `GET /api/v1/admin/users`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Admin Users |
| Method | GET |
| Path | `/api/v1/admin/users` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-ADMIN-003: `PATCH /api/v1/admin/users/{userId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Admin Users Userid |
| Method | PATCH |
| Path | `/api/v1/admin/users/{userId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `userId` | UUID/String | ID của userId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-ADMIN-004: `POST /api/v1/admin/users/{userId}/suspend`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Admin Users Userid Suspend |
| Method | POST |
| Path | `/api/v1/admin/users/{userId}/suspend` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `userId` | UUID/String | ID của userId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-ADMIN-005: `GET /api/v1/admin/tenants`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Admin Tenants |
| Method | GET |
| Path | `/api/v1/admin/tenants` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-ADMIN-006: `PATCH /api/v1/admin/tenants/{tenantId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Admin Tenants Tenantid |
| Method | PATCH |
| Path | `/api/v1/admin/tenants/{tenantId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `tenantId` | UUID/String | ID của tenantId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-ADMIN-007: `GET /api/v1/admin/feature-flags`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Admin Feature Flags |
| Method | GET |
| Path | `/api/v1/admin/feature-flags` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-ADMIN-008: `PATCH /api/v1/admin/feature-flags/{flagId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Admin Feature Flags Flagid |
| Method | PATCH |
| Path | `/api/v1/admin/feature-flags/{flagId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `flagId` | UUID/String | ID của flagId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-ADMIN-009: `GET /api/v1/admin/audit-logs`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Admin Audit Logs |
| Method | GET |
| Path | `/api/v1/admin/audit-logs` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-ADMIN-010: `GET /api/v1/admin/system-health`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Admin System Health |
| Method | GET |
| Path | `/api/v1/admin/system-health` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

# GROUP — REPORT


## API-REPORT-001: `GET /api/v1/projects/{projectId}/reports/weekly`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Reports Weekly |
| Method | GET |
| Path | `/api/v1/projects/{projectId}/reports/weekly` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-REPORT-002: `POST /api/v1/projects/{projectId}/reports/weekly`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Reports Weekly |
| Method | POST |
| Path | `/api/v1/projects/{projectId}/reports/weekly` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-REPORT-003: `GET /api/v1/reports/{reportId}`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Reports Reportid |
| Method | GET |
| Path | `/api/v1/reports/{reportId}` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `reportId` | UUID/String | ID của reportId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-REPORT-004: `POST /api/v1/reports/{reportId}/export`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Reports Reportid Export |
| Method | POST |
| Path | `/api/v1/reports/{reportId}/export` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 20 req/min |
| Cache | No Cache |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `reportId` | UUID/String | ID của reportId |

**Request Body mẫu**

```json
{
  "name": "string - required khi phù hợp",
  "description": "string - optional",
  "visibility": "public|customer_safe|internal|private",
  "metadata": {}
}
```

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-REPORT-005: `GET /api/v1/projects/{projectId}/analytics`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Analytics |
| Method | GET |
| Path | `/api/v1/projects/{projectId}/analytics` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

## API-REPORT-006: `GET /api/v1/projects/{projectId}/customer-dashboard`

| Thuộc tính | Giá trị |
|---|---|
| Tên | Projects Projectid Customer Dashboard |
| Method | GET |
| Path | `/api/v1/projects/{projectId}/customer-dashboard` |
| Auth | Required |
| Role | Theo RBAC 3 tầng: system/org/project policy tương ứng |
| Rate limit | 60 req/min |
| Cache | Redis 30-120s tùy scope; private/user-specific phải key theo user/tenant |

**Path Parameters**

| Param | Kiểu | Mô tả |
|---|---|---|
| `projectId` | UUID/String | ID của projectId |

**Query Parameters**

| Param | Kiểu | Required | Default | Mô tả |
|---|---|---|---|---|
| page | int | No | 1 | Trang dữ liệu |
| limit | int | No | 20 | Tối đa 100 |
| search | string | No |  | Tìm kiếm theo quyền |
| sort | string | No | newest | Sắp xếp |

**Validation Rules**
- Validate DTO bằng FluentValidation.
- Không nhận `tenantId` từ client cho dữ liệu nghiệp vụ nếu user không phải admin.
- Kiểm tra quyền backend bằng policy/service, không chỉ ẩn nút frontend.

**Response Success**
```json
{ "success": true, "data": {}, "correlationId": "req-..." }
```

**Response Errors**

| Code | Trường hợp |
|---|---|
| 400 | Validation failed |
| 401 | Chưa đăng nhập/session hết hạn |
| 403 | Không đủ quyền theo RBAC |
| 404 | Resource không tồn tại hoặc bị mask bởi quyền |
| 409 | Conflict unique/state |
| 422 | Vi phạm business rule |
| 429 | Rate limit exceeded |

**Side Effects**
- Audit log nếu thay đổi dữ liệu.
- Invalidate cache liên quan.
- Emit SignalR nếu task/project/chat/notification thay đổi.
- Trigger webhook event nếu endpoint thuộc project/task/comment/approval.

---

# API DESIGN DECISIONS

- Versioning: URL version `/api/v1`, không phá vỡ client khi nâng cấp.
- File upload: MVP dùng multipart upload; production khuyến nghị presigned URL MinIO/S3 để giảm tải web server.
- Cursor pagination: bắt buộc cho chat_messages, audit_logs, notification_delivery_logs.
- Idempotency-Key: dùng cho import/approval/webhook retry để tránh tạo trùng.
- Rate limit: per user + per tenant + per IP; AI endpoint có quota riêng theo ngày.
- WebSocket events: `project.updated`, `task.created`, `task.moved`, `approval.requested`, `message.created`, `notification.created`, `ai.stream.delta`, `ai.stream.done`.
