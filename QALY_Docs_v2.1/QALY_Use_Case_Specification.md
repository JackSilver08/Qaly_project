# QALY Workspace — Use Case Specification v2.0

**Phiên bản:** v2.0 — Production/Japan-style specification  
**Ngày:** 16/05/2026  
**Phạm vi:** Dự án web quản lý dự án phần mềm cho doanh nghiệp outsource vừa và nhỏ.  
**Định hướng:** Jira + Notion + Mini Zalo + AI Assistant + Document Mining.  
**Ghi chú kiến trúc:** Tài liệu v2.0 mở rộng từ SRS/TKHT v1.0. Các phần dưới đây là target design để nhóm có thể triển khai 80–90% chức năng web trong 10–11 tuần, không bắt buộc implement toàn bộ bảng P2 nếu thiếu thời gian.

---

## 0. Quy ước Use Case

Mỗi use case được viết theo chuẩn đủ để dev/tester/documenter mapping sang API, DB, UI và test case.

---

## UC-AUTH-01: Đăng ký tài khoản

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-AUTH-01 |
| Use Case Name | Đăng ký tài khoản |
| Module | AUTH |
| Priority | Must Have |
| Actor chính | Guest |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Đăng ký tài khoản` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-AUTH.  
**API liên quan:** `/api/v1/auth/register`  
**DB Tables liên quan:** `users, user_sessions, user_login_history`  
**UI Screen liên quan:** AUTH screen / project detail tab  
**Test Case liên quan:** TC-AUTH-xx

---

## UC-AUTH-02: Xác thực email

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-AUTH-02 |
| Use Case Name | Xác thực email |
| Module | AUTH |
| Priority | Should Have |
| Actor chính | Guest/User |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Xác thực email` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-AUTH.  
**API liên quan:** `/api/v1/auth/register`  
**DB Tables liên quan:** `users, user_sessions, user_login_history`  
**UI Screen liên quan:** AUTH screen / project detail tab  
**Test Case liên quan:** TC-AUTH-xx

---

## UC-AUTH-03: Đăng nhập

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-AUTH-03 |
| Use Case Name | Đăng nhập |
| Module | AUTH |
| Priority | Must Have |
| Actor chính | Guest/User |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Đăng nhập` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-AUTH.  
**API liên quan:** `/api/v1/auth/register`  
**DB Tables liên quan:** `users, user_sessions, user_login_history`  
**UI Screen liên quan:** AUTH screen / project detail tab  
**Test Case liên quan:** TC-AUTH-xx

---

## UC-AUTH-04: Đăng xuất

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-AUTH-04 |
| Use Case Name | Đăng xuất |
| Module | AUTH |
| Priority | Must Have |
| Actor chính | User |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Đăng xuất` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-AUTH.  
**API liên quan:** `/api/v1/auth/register`  
**DB Tables liên quan:** `users, user_sessions, user_login_history`  
**UI Screen liên quan:** AUTH screen / project detail tab  
**Test Case liên quan:** TC-AUTH-xx

---

## UC-AUTH-05: Làm mới phiên/token

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-AUTH-05 |
| Use Case Name | Làm mới phiên/token |
| Module | AUTH |
| Priority | Should Have |
| Actor chính | User |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Làm mới phiên/token` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-AUTH.  
**API liên quan:** `/api/v1/auth/register`  
**DB Tables liên quan:** `users, user_sessions, user_login_history`  
**UI Screen liên quan:** AUTH screen / project detail tab  
**Test Case liên quan:** TC-AUTH-xx

---

## UC-AUTH-06: Quên mật khẩu

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-AUTH-06 |
| Use Case Name | Quên mật khẩu |
| Module | AUTH |
| Priority | Should Have |
| Actor chính | Guest |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Quên mật khẩu` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-AUTH.  
**API liên quan:** `/api/v1/auth/register`  
**DB Tables liên quan:** `users, user_sessions, user_login_history`  
**UI Screen liên quan:** AUTH screen / project detail tab  
**Test Case liên quan:** TC-AUTH-xx

---

## UC-AUTH-07: Đặt lại mật khẩu

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-AUTH-07 |
| Use Case Name | Đặt lại mật khẩu |
| Module | AUTH |
| Priority | Should Have |
| Actor chính | Guest |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Đặt lại mật khẩu` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-AUTH.  
**API liên quan:** `/api/v1/auth/register`  
**DB Tables liên quan:** `users, user_sessions, user_login_history`  
**UI Screen liên quan:** AUTH screen / project detail tab  
**Test Case liên quan:** TC-AUTH-xx

---

## UC-AUTH-08: Đổi mật khẩu

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-AUTH-08 |
| Use Case Name | Đổi mật khẩu |
| Module | AUTH |
| Priority | Must Have |
| Actor chính | User |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Đổi mật khẩu` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-AUTH.  
**API liên quan:** `/api/v1/auth/register`  
**DB Tables liên quan:** `users, user_sessions, user_login_history`  
**UI Screen liên quan:** AUTH screen / project detail tab  
**Test Case liên quan:** TC-AUTH-xx

---

## UC-ORG-01: Tạo organization

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-ORG-01 |
| Use Case Name | Tạo organization |
| Module | ORG |
| Priority | Must Have |
| Actor chính | System customer/admin |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Tạo organization` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-ORG.  
**API liên quan:** `/api/v1/organizations`  
**DB Tables liên quan:** `organizations, organization_members, organization_member_invitations`  
**UI Screen liên quan:** ORG screen / project detail tab  
**Test Case liên quan:** TC-ORG-xx

---

## UC-ORG-02: Mời thành viên vào organization

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-ORG-02 |
| Use Case Name | Mời thành viên vào organization |
| Module | ORG |
| Priority | Must Have |
| Actor chính | org_owner/org_admin |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Mời thành viên vào organization` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-ORG.  
**API liên quan:** `/api/v1/organizations`  
**DB Tables liên quan:** `organizations, organization_members, organization_member_invitations`  
**UI Screen liên quan:** ORG screen / project detail tab  
**Test Case liên quan:** TC-ORG-xx

---

## UC-ORG-03: Chấp nhận lời mời

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-ORG-03 |
| Use Case Name | Chấp nhận lời mời |
| Module | ORG |
| Priority | Should Have |
| Actor chính | Invited user |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Chấp nhận lời mời` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-ORG.  
**API liên quan:** `/api/v1/organizations`  
**DB Tables liên quan:** `organizations, organization_members, organization_member_invitations`  
**UI Screen liên quan:** ORG screen / project detail tab  
**Test Case liên quan:** TC-ORG-xx

---

## UC-ORG-04: Phân quyền thành viên organization

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-ORG-04 |
| Use Case Name | Phân quyền thành viên organization |
| Module | ORG |
| Priority | Must Have |
| Actor chính | org_owner/org_admin |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Phân quyền thành viên organization` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-ORG.  
**API liên quan:** `/api/v1/organizations`  
**DB Tables liên quan:** `organizations, organization_members, organization_member_invitations`  
**UI Screen liên quan:** ORG screen / project detail tab  
**Test Case liên quan:** TC-ORG-xx

---

## UC-ORG-05: Xóa thành viên khỏi organization

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-ORG-05 |
| Use Case Name | Xóa thành viên khỏi organization |
| Module | ORG |
| Priority | Must Have |
| Actor chính | org_owner/org_admin |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Xóa thành viên khỏi organization` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-ORG.  
**API liên quan:** `/api/v1/organizations`  
**DB Tables liên quan:** `organizations, organization_members, organization_member_invitations`  
**UI Screen liên quan:** ORG screen / project detail tab  
**Test Case liên quan:** TC-ORG-xx

---

## UC-PROJ-01: Tạo project

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-PROJ-01 |
| Use Case Name | Tạo project |
| Module | PROJ |
| Priority | Must Have |
| Actor chính | org_admin/project_manager |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Tạo project` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-PROJ.  
**API liên quan:** `/api/v1/orgs/{orgId}/projects`  
**DB Tables liên quan:** `projects, project_members, project_settings`  
**UI Screen liên quan:** PROJ screen / project detail tab  
**Test Case liên quan:** TC-PROJ-xx

---

## UC-PROJ-02: Cập nhật thông tin project

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-PROJ-02 |
| Use Case Name | Cập nhật thông tin project |
| Module | PROJ |
| Priority | Must Have |
| Actor chính | project_manager |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Cập nhật thông tin project` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-PROJ.  
**API liên quan:** `/api/v1/orgs/{orgId}/projects`  
**DB Tables liên quan:** `projects, project_members, project_settings`  
**UI Screen liên quan:** PROJ screen / project detail tab  
**Test Case liên quan:** TC-PROJ-xx

---

## UC-PROJ-03: Thêm thành viên vào project

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-PROJ-03 |
| Use Case Name | Thêm thành viên vào project |
| Module | PROJ |
| Priority | Must Have |
| Actor chính | project_manager |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Thêm thành viên vào project` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-PROJ.  
**API liên quan:** `/api/v1/orgs/{orgId}/projects`  
**DB Tables liên quan:** `projects, project_members, project_settings`  
**UI Screen liên quan:** PROJ screen / project detail tab  
**Test Case liên quan:** TC-PROJ-xx

---

## UC-PROJ-04: Phân quyền thành viên project

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-PROJ-04 |
| Use Case Name | Phân quyền thành viên project |
| Module | PROJ |
| Priority | Must Have |
| Actor chính | project_manager |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Phân quyền thành viên project` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-PROJ.  
**API liên quan:** `/api/v1/orgs/{orgId}/projects`  
**DB Tables liên quan:** `projects, project_members, project_settings`  
**UI Screen liên quan:** PROJ screen / project detail tab  
**Test Case liên quan:** TC-PROJ-xx

---

## UC-PROJ-05: Lưu trữ project

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-PROJ-05 |
| Use Case Name | Lưu trữ project |
| Module | PROJ |
| Priority | Should Have |
| Actor chính | project_manager |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Lưu trữ project` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-PROJ.  
**API liên quan:** `/api/v1/orgs/{orgId}/projects`  
**DB Tables liên quan:** `projects, project_members, project_settings`  
**UI Screen liên quan:** PROJ screen / project detail tab  
**Test Case liên quan:** TC-PROJ-xx

---

## UC-PROJ-06: Xem dashboard tiến độ project

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-PROJ-06 |
| Use Case Name | Xem dashboard tiến độ project |
| Module | PROJ |
| Priority | Must Have |
| Actor chính | project_member/customer |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Xem dashboard tiến độ project` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-PROJ.  
**API liên quan:** `/api/v1/orgs/{orgId}/projects`  
**DB Tables liên quan:** `projects, project_members, project_settings`  
**UI Screen liên quan:** PROJ screen / project detail tab  
**Test Case liên quan:** TC-PROJ-xx

---

## UC-TASK-01: Tạo task

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-TASK-01 |
| Use Case Name | Tạo task |
| Module | TASK |
| Priority | Must Have |
| Actor chính | PM/Member |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Tạo task` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-TASK.  
**API liên quan:** `/api/v1/projects/{projectId}/tasks`  
**DB Tables liên quan:** `tasks, task_assignments, task_status_change_requests, task_evidences`  
**UI Screen liên quan:** TASK screen / project detail tab  
**Test Case liên quan:** TC-TASK-xx

---

## UC-TASK-02: Giao task cho thành viên

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-TASK-02 |
| Use Case Name | Giao task cho thành viên |
| Module | TASK |
| Priority | Must Have |
| Actor chính | PM/Sub-manager |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Giao task cho thành viên` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-TASK.  
**API liên quan:** `/api/v1/projects/{projectId}/tasks`  
**DB Tables liên quan:** `tasks, task_assignments, task_status_change_requests, task_evidences`  
**UI Screen liên quan:** TASK screen / project detail tab  
**Test Case liên quan:** TC-TASK-xx

---

## UC-TASK-03: Tạo subtask phụ thuộc task cha

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-TASK-03 |
| Use Case Name | Tạo subtask phụ thuộc task cha |
| Module | TASK |
| Priority | Should Have |
| Actor chính | PM/Member |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Tạo subtask phụ thuộc task cha` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-TASK.  
**API liên quan:** `/api/v1/projects/{projectId}/tasks`  
**DB Tables liên quan:** `tasks, task_assignments, task_status_change_requests, task_evidences`  
**UI Screen liên quan:** TASK screen / project detail tab  
**Test Case liên quan:** TC-TASK-xx

---

## UC-TASK-04: Tạo task dependency

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-TASK-04 |
| Use Case Name | Tạo task dependency |
| Module | TASK |
| Priority | Must Have |
| Actor chính | PM/Member |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Tạo task dependency` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-TASK.  
**API liên quan:** `/api/v1/projects/{projectId}/tasks`  
**DB Tables liên quan:** `tasks, task_assignments, task_status_change_requests, task_evidences`  
**UI Screen liên quan:** TASK screen / project detail tab  
**Test Case liên quan:** TC-TASK-xx

---

## UC-TASK-05: Cập nhật trạng thái task

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-TASK-05 |
| Use Case Name | Cập nhật trạng thái task |
| Module | TASK |
| Priority | Must Have |
| Actor chính | Assignee/PM |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Cập nhật trạng thái task` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-TASK.  
**API liên quan:** `/api/v1/projects/{projectId}/tasks`  
**DB Tables liên quan:** `tasks, task_assignments, task_status_change_requests, task_evidences`  
**UI Screen liên quan:** TASK screen / project detail tab  
**Test Case liên quan:** TC-TASK-xx

---

## UC-TASK-06: Gửi yêu cầu chuyển trạng thái

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-TASK-06 |
| Use Case Name | Gửi yêu cầu chuyển trạng thái |
| Module | TASK |
| Priority | Must Have |
| Actor chính | Assignee |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Gửi yêu cầu chuyển trạng thái` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-TASK.  
**API liên quan:** `/api/v1/projects/{projectId}/tasks`  
**DB Tables liên quan:** `tasks, task_assignments, task_status_change_requests, task_evidences`  
**UI Screen liên quan:** TASK screen / project detail tab  
**Test Case liên quan:** TC-TASK-xx

---

## UC-TASK-07: PM duyệt yêu cầu chuyển trạng thái

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-TASK-07 |
| Use Case Name | PM duyệt yêu cầu chuyển trạng thái |
| Module | TASK |
| Priority | Must Have |
| Actor chính | PM/Reviewer |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `PM duyệt yêu cầu chuyển trạng thái` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-TASK.  
**API liên quan:** `/api/v1/projects/{projectId}/tasks`  
**DB Tables liên quan:** `tasks, task_assignments, task_status_change_requests, task_evidences`  
**UI Screen liên quan:** TASK screen / project detail tab  
**Test Case liên quan:** TC-TASK-xx

---

## UC-TASK-08: PM từ chối và yêu cầu làm lại

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-TASK-08 |
| Use Case Name | PM từ chối và yêu cầu làm lại |
| Module | TASK |
| Priority | Must Have |
| Actor chính | PM/Reviewer |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `PM từ chối và yêu cầu làm lại` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-TASK.  
**API liên quan:** `/api/v1/projects/{projectId}/tasks`  
**DB Tables liên quan:** `tasks, task_assignments, task_status_change_requests, task_evidences`  
**UI Screen liên quan:** TASK screen / project detail tab  
**Test Case liên quan:** TC-TASK-xx

---

## UC-TASK-09: Upload bằng chứng hoàn thành

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-TASK-09 |
| Use Case Name | Upload bằng chứng hoàn thành |
| Module | TASK |
| Priority | Must Have |
| Actor chính | Assignee |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Upload bằng chứng hoàn thành` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-TASK.  
**API liên quan:** `/api/v1/projects/{projectId}/tasks`  
**DB Tables liên quan:** `tasks, task_assignments, task_status_change_requests, task_evidences`  
**UI Screen liên quan:** TASK screen / project detail tab  
**Test Case liên quan:** TC-TASK-xx

---

## UC-TASK-10: Log thời gian làm việc

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-TASK-10 |
| Use Case Name | Log thời gian làm việc |
| Module | TASK |
| Priority | Should Have |
| Actor chính | Assignee |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Log thời gian làm việc` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-TASK.  
**API liên quan:** `/api/v1/projects/{projectId}/tasks`  
**DB Tables liên quan:** `tasks, task_assignments, task_status_change_requests, task_evidences`  
**UI Screen liên quan:** TASK screen / project detail tab  
**Test Case liên quan:** TC-TASK-xx

---

## UC-KANBAN-01: Kéo thả task giữa các cột

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-KANBAN-01 |
| Use Case Name | Kéo thả task giữa các cột |
| Module | KANBAN |
| Priority | Must Have |
| Actor chính | Member/PM |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Kéo thả task giữa các cột` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-KANBAN.  
**API liên quan:** `/api/v1/projects/{projectId}/kanban`  
**DB Tables liên quan:** `kanban_boards, kanban_columns, kanban_cards`  
**UI Screen liên quan:** KANBAN screen / project detail tab  
**Test Case liên quan:** TC-KANBAN-xx

---

## UC-KANBAN-02: Thêm cột tùy chỉnh

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-KANBAN-02 |
| Use Case Name | Thêm cột tùy chỉnh |
| Module | KANBAN |
| Priority | Should Have |
| Actor chính | PM |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Thêm cột tùy chỉnh` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-KANBAN.  
**API liên quan:** `/api/v1/projects/{projectId}/kanban`  
**DB Tables liên quan:** `kanban_boards, kanban_columns, kanban_cards`  
**UI Screen liên quan:** KANBAN screen / project detail tab  
**Test Case liên quan:** TC-KANBAN-xx

---

## UC-KANBAN-03: Xóa cột với điều kiện

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-KANBAN-03 |
| Use Case Name | Xóa cột với điều kiện |
| Module | KANBAN |
| Priority | Should Have |
| Actor chính | PM |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Xóa cột với điều kiện` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-KANBAN.  
**API liên quan:** `/api/v1/projects/{projectId}/kanban`  
**DB Tables liên quan:** `kanban_boards, kanban_columns, kanban_cards`  
**UI Screen liên quan:** KANBAN screen / project detail tab  
**Test Case liên quan:** TC-KANBAN-xx

---

## UC-KANBAN-04: Đặt WIP limit cho cột

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-KANBAN-04 |
| Use Case Name | Đặt WIP limit cho cột |
| Module | KANBAN |
| Priority | Should Have |
| Actor chính | PM |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Đặt WIP limit cho cột` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-KANBAN.  
**API liên quan:** `/api/v1/projects/{projectId}/kanban`  
**DB Tables liên quan:** `kanban_boards, kanban_columns, kanban_cards`  
**UI Screen liên quan:** KANBAN screen / project detail tab  
**Test Case liên quan:** TC-KANBAN-xx

---

## UC-KANBAN-05: Lọc task trên kanban

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-KANBAN-05 |
| Use Case Name | Lọc task trên kanban |
| Module | KANBAN |
| Priority | Must Have |
| Actor chính | Member/PM/Customer |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Lọc task trên kanban` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-KANBAN.  
**API liên quan:** `/api/v1/projects/{projectId}/kanban`  
**DB Tables liên quan:** `kanban_boards, kanban_columns, kanban_cards`  
**UI Screen liên quan:** KANBAN screen / project detail tab  
**Test Case liên quan:** TC-KANBAN-xx

---

## UC-SPRINT-01: Tạo sprint

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-SPRINT-01 |
| Use Case Name | Tạo sprint |
| Module | SPRINT |
| Priority | Should Have |
| Actor chính | PM |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Tạo sprint` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-SPRINT.  
**API liên quan:** `/api/v1/projects/{projectId}/sprints`  
**DB Tables liên quan:** `sprints, sprint_tasks, sprint_analytics`  
**UI Screen liên quan:** SPRINT screen / project detail tab  
**Test Case liên quan:** TC-SPRINT-xx

---

## UC-SPRINT-02: Lên kế hoạch sprint

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-SPRINT-02 |
| Use Case Name | Lên kế hoạch sprint |
| Module | SPRINT |
| Priority | Should Have |
| Actor chính | PM/Sub-manager |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Lên kế hoạch sprint` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-SPRINT.  
**API liên quan:** `/api/v1/projects/{projectId}/sprints`  
**DB Tables liên quan:** `sprints, sprint_tasks, sprint_analytics`  
**UI Screen liên quan:** SPRINT screen / project detail tab  
**Test Case liên quan:** TC-SPRINT-xx

---

## UC-SPRINT-03: Bắt đầu sprint

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-SPRINT-03 |
| Use Case Name | Bắt đầu sprint |
| Module | SPRINT |
| Priority | Should Have |
| Actor chính | PM |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Bắt đầu sprint` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-SPRINT.  
**API liên quan:** `/api/v1/projects/{projectId}/sprints`  
**DB Tables liên quan:** `sprints, sprint_tasks, sprint_analytics`  
**UI Screen liên quan:** SPRINT screen / project detail tab  
**Test Case liên quan:** TC-SPRINT-xx

---

## UC-SPRINT-04: Hoàn thành sprint

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-SPRINT-04 |
| Use Case Name | Hoàn thành sprint |
| Module | SPRINT |
| Priority | Should Have |
| Actor chính | PM |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Hoàn thành sprint` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-SPRINT.  
**API liên quan:** `/api/v1/projects/{projectId}/sprints`  
**DB Tables liên quan:** `sprints, sprint_tasks, sprint_analytics`  
**UI Screen liên quan:** SPRINT screen / project detail tab  
**Test Case liên quan:** TC-SPRINT-xx

---

## UC-SPRINT-05: Xem burndown chart

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-SPRINT-05 |
| Use Case Name | Xem burndown chart |
| Module | SPRINT |
| Priority | Nice to Have |
| Actor chính | PM/Member |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Xem burndown chart` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-SPRINT.  
**API liên quan:** `/api/v1/projects/{projectId}/sprints`  
**DB Tables liên quan:** `sprints, sprint_tasks, sprint_analytics`  
**UI Screen liên quan:** SPRINT screen / project detail tab  
**Test Case liên quan:** TC-SPRINT-xx

---

## UC-TIMELINE-01: Xem timeline Gantt

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-TIMELINE-01 |
| Use Case Name | Xem timeline Gantt |
| Module | TIMELINE |
| Priority | Must Have |
| Actor chính | Project member |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Xem timeline Gantt` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-TIMELINE.  
**API liên quan:** `/api/v1/projects/{projectId}/timeline`  
**DB Tables liên quan:** `timeline_views, timeline_milestones, task_dependencies`  
**UI Screen liên quan:** TIMELINE screen / project detail tab  
**Test Case liên quan:** TC-TIMELINE-xx

---

## UC-TIMELINE-02: Kéo thả điều chỉnh timeline

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-TIMELINE-02 |
| Use Case Name | Kéo thả điều chỉnh timeline |
| Module | TIMELINE |
| Priority | Should Have |
| Actor chính | PM/Sub-manager |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Kéo thả điều chỉnh timeline` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-TIMELINE.  
**API liên quan:** `/api/v1/projects/{projectId}/timeline`  
**DB Tables liên quan:** `timeline_views, timeline_milestones, task_dependencies`  
**UI Screen liên quan:** TIMELINE screen / project detail tab  
**Test Case liên quan:** TC-TIMELINE-xx

---

## UC-TIMELINE-03: Xem critical path

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-TIMELINE-03 |
| Use Case Name | Xem critical path |
| Module | TIMELINE |
| Priority | Nice to Have |
| Actor chính | PM |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Xem critical path` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-TIMELINE.  
**API liên quan:** `/api/v1/projects/{projectId}/timeline`  
**DB Tables liên quan:** `timeline_views, timeline_milestones, task_dependencies`  
**UI Screen liên quan:** TIMELINE screen / project detail tab  
**Test Case liên quan:** TC-TIMELINE-xx

---

## UC-CHAT-01: Tạo nhóm chat project

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-CHAT-01 |
| Use Case Name | Tạo nhóm chat project |
| Module | CHAT |
| Priority | Must Have |
| Actor chính | PM/System |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Tạo nhóm chat project` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-CHAT.  
**API liên quan:** `/api/v1/chat/rooms`  
**DB Tables liên quan:** `chat_rooms, chat_messages, chat_message_task_links`  
**UI Screen liên quan:** CHAT screen / project detail tab  
**Test Case liên quan:** TC-CHAT-xx

---

## UC-CHAT-02: Gửi tin nhắn văn bản

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-CHAT-02 |
| Use Case Name | Gửi tin nhắn văn bản |
| Module | CHAT |
| Priority | Must Have |
| Actor chính | Room member |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Gửi tin nhắn văn bản` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-CHAT.  
**API liên quan:** `/api/v1/chat/rooms`  
**DB Tables liên quan:** `chat_rooms, chat_messages, chat_message_task_links`  
**UI Screen liên quan:** CHAT screen / project detail tab  
**Test Case liên quan:** TC-CHAT-xx

---

## UC-CHAT-03: Giao task trực tiếp từ chat

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-CHAT-03 |
| Use Case Name | Giao task trực tiếp từ chat |
| Module | CHAT |
| Priority | Must Have |
| Actor chính | PM/Sub-manager |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Giao task trực tiếp từ chat` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-CHAT.  
**API liên quan:** `/api/v1/chat/rooms`  
**DB Tables liên quan:** `chat_rooms, chat_messages, chat_message_task_links`  
**UI Screen liên quan:** CHAT screen / project detail tab  
**Test Case liên quan:** TC-CHAT-xx

---

## UC-CHAT-04: Link task vào tin nhắn

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-CHAT-04 |
| Use Case Name | Link task vào tin nhắn |
| Module | CHAT |
| Priority | Should Have |
| Actor chính | Room member |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Link task vào tin nhắn` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-CHAT.  
**API liên quan:** `/api/v1/chat/rooms`  
**DB Tables liên quan:** `chat_rooms, chat_messages, chat_message_task_links`  
**UI Screen liên quan:** CHAT screen / project detail tab  
**Test Case liên quan:** TC-CHAT-xx

---

## UC-CHAT-05: Pin tin nhắn quan trọng

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-CHAT-05 |
| Use Case Name | Pin tin nhắn quan trọng |
| Module | CHAT |
| Priority | Should Have |
| Actor chính | PM/Room admin |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Pin tin nhắn quan trọng` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-CHAT.  
**API liên quan:** `/api/v1/chat/rooms`  
**DB Tables liên quan:** `chat_rooms, chat_messages, chat_message_task_links`  
**UI Screen liên quan:** CHAT screen / project detail tab  
**Test Case liên quan:** TC-CHAT-xx

---

## UC-CHAT-06: Xem task được giao trong chat

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-CHAT-06 |
| Use Case Name | Xem task được giao trong chat |
| Module | CHAT |
| Priority | Should Have |
| Actor chính | Room member |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Xem task được giao trong chat` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-CHAT.  
**API liên quan:** `/api/v1/chat/rooms`  
**DB Tables liên quan:** `chat_rooms, chat_messages, chat_message_task_links`  
**UI Screen liên quan:** CHAT screen / project detail tab  
**Test Case liên quan:** TC-CHAT-xx

---

## UC-MEETING-01: Tạo meeting và ghi note

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-MEETING-01 |
| Use Case Name | Tạo meeting và ghi note |
| Module | MEETING |
| Priority | Should Have |
| Actor chính | PM/Member |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Tạo meeting và ghi note` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-MEETING.  
**API liên quan:** `/api/v1/projects/{projectId}/meetings`  
**DB Tables liên quan:** `meetings, meeting_notes, meeting_action_items`  
**UI Screen liên quan:** MEETING screen / project detail tab  
**Test Case liên quan:** TC-MEETING-xx

---

## UC-MEETING-02: AI tóm tắt meeting

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-MEETING-02 |
| Use Case Name | AI tóm tắt meeting |
| Module | MEETING |
| Priority | Should Have |
| Actor chính | PM/AI |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `AI tóm tắt meeting` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-MEETING.  
**API liên quan:** `/api/v1/projects/{projectId}/meetings`  
**DB Tables liên quan:** `meetings, meeting_notes, meeting_action_items`  
**UI Screen liên quan:** MEETING screen / project detail tab  
**Test Case liên quan:** TC-MEETING-xx

---

## UC-MEETING-03: Tạo action item từ meeting

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-MEETING-03 |
| Use Case Name | Tạo action item từ meeting |
| Module | MEETING |
| Priority | Should Have |
| Actor chính | PM |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Tạo action item từ meeting` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-MEETING.  
**API liên quan:** `/api/v1/projects/{projectId}/meetings`  
**DB Tables liên quan:** `meetings, meeting_notes, meeting_action_items`  
**UI Screen liên quan:** MEETING screen / project detail tab  
**Test Case liên quan:** TC-MEETING-xx

---

## UC-WIKI-01: Tạo wiki page

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-WIKI-01 |
| Use Case Name | Tạo wiki page |
| Module | WIKI |
| Priority | Must Have |
| Actor chính | PM/Member |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Tạo wiki page` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-WIKI.  
**API liên quan:** `/api/v1/projects/{projectId}/wiki/pages`  
**DB Tables liên quan:** `wiki_pages, wiki_page_versions`  
**UI Screen liên quan:** WIKI screen / project detail tab  
**Test Case liên quan:** TC-WIKI-xx

---

## UC-WIKI-02: Chỉnh sửa wiki page versioning

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-WIKI-02 |
| Use Case Name | Chỉnh sửa wiki page versioning |
| Module | WIKI |
| Priority | Must Have |
| Actor chính | PM/Member |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Chỉnh sửa wiki page versioning` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-WIKI.  
**API liên quan:** `/api/v1/projects/{projectId}/wiki/pages`  
**DB Tables liên quan:** `wiki_pages, wiki_page_versions`  
**UI Screen liên quan:** WIKI screen / project detail tab  
**Test Case liên quan:** TC-WIKI-xx

---

## UC-WIKI-03: Rollback phiên bản wiki cũ

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-WIKI-03 |
| Use Case Name | Rollback phiên bản wiki cũ |
| Module | WIKI |
| Priority | Should Have |
| Actor chính | PM |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Rollback phiên bản wiki cũ` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-WIKI.  
**API liên quan:** `/api/v1/projects/{projectId}/wiki/pages`  
**DB Tables liên quan:** `wiki_pages, wiki_page_versions`  
**UI Screen liên quan:** WIKI screen / project detail tab  
**Test Case liên quan:** TC-WIKI-xx

---

## UC-AI-01: Hỏi AI về tiến độ project

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-AI-01 |
| Use Case Name | Hỏi AI về tiến độ project |
| Module | AI |
| Priority | Should Have |
| Actor chính | Project member |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Hỏi AI về tiến độ project` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-AI.  
**API liên quan:** `/api/v1/ai/conversations`  
**DB Tables liên quan:** `ai_conversations, ai_knowledge_chunks, ai_usage_logs`  
**UI Screen liên quan:** AI screen / project detail tab  
**Test Case liên quan:** TC-AI-xx

---

## UC-AI-02: AI sinh báo cáo tiến độ tuần

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-AI-02 |
| Use Case Name | AI sinh báo cáo tiến độ tuần |
| Module | AI |
| Priority | Should Have |
| Actor chính | PM |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `AI sinh báo cáo tiến độ tuần` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-AI.  
**API liên quan:** `/api/v1/ai/conversations`  
**DB Tables liên quan:** `ai_conversations, ai_knowledge_chunks, ai_usage_logs`  
**UI Screen liên quan:** AI screen / project detail tab  
**Test Case liên quan:** TC-AI-xx

---

## UC-AI-03: AI gợi ý assign task

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-AI-03 |
| Use Case Name | AI gợi ý assign task |
| Module | AI |
| Priority | Nice to Have |
| Actor chính | PM |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `AI gợi ý assign task` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-AI.  
**API liên quan:** `/api/v1/ai/conversations`  
**DB Tables liên quan:** `ai_conversations, ai_knowledge_chunks, ai_usage_logs`  
**UI Screen liên quan:** AI screen / project detail tab  
**Test Case liên quan:** TC-AI-xx

---

## UC-AI-04: Sync dữ liệu project vào AI knowledge base

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-AI-04 |
| Use Case Name | Sync dữ liệu project vào AI knowledge base |
| Module | AI |
| Priority | Should Have |
| Actor chính | System/PM |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Sync dữ liệu project vào AI knowledge base` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-AI.  
**API liên quan:** `/api/v1/ai/conversations`  
**DB Tables liên quan:** `ai_conversations, ai_knowledge_chunks, ai_usage_logs`  
**UI Screen liên quan:** AI screen / project detail tab  
**Test Case liên quan:** TC-AI-xx

---

## UC-IMPORT-01: Upload tài liệu PDF/DOCX

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-IMPORT-01 |
| Use Case Name | Upload tài liệu PDF/DOCX |
| Module | IMPORT |
| Priority | Should Have |
| Actor chính | PM/BA |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Upload tài liệu PDF/DOCX` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-IMPORT.  
**API liên quan:** `/api/v1/import/documents`  
**DB Tables liên quan:** `document_import_jobs, document_draft_tasks`  
**UI Screen liên quan:** IMPORT screen / project detail tab  
**Test Case liên quan:** TC-IMPORT-xx

---

## UC-IMPORT-02: AI phân tích và trích xuất task

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-IMPORT-02 |
| Use Case Name | AI phân tích và trích xuất task |
| Module | IMPORT |
| Priority | Should Have |
| Actor chính | PM/AI |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `AI phân tích và trích xuất task` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-IMPORT.  
**API liên quan:** `/api/v1/import/documents`  
**DB Tables liên quan:** `document_import_jobs, document_draft_tasks`  
**UI Screen liên quan:** IMPORT screen / project detail tab  
**Test Case liên quan:** TC-IMPORT-xx

---

## UC-IMPORT-03: Review và approve draft task

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-IMPORT-03 |
| Use Case Name | Review và approve draft task |
| Module | IMPORT |
| Priority | Should Have |
| Actor chính | PM/BA |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Review và approve draft task` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-IMPORT.  
**API liên quan:** `/api/v1/import/documents`  
**DB Tables liên quan:** `document_import_jobs, document_draft_tasks`  
**UI Screen liên quan:** IMPORT screen / project detail tab  
**Test Case liên quan:** TC-IMPORT-xx

---

## UC-IMPORT-04: Import task vào project

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-IMPORT-04 |
| Use Case Name | Import task vào project |
| Module | IMPORT |
| Priority | Should Have |
| Actor chính | PM |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Import task vào project` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-IMPORT.  
**API liên quan:** `/api/v1/import/documents`  
**DB Tables liên quan:** `document_import_jobs, document_draft_tasks`  
**UI Screen liên quan:** IMPORT screen / project detail tab  
**Test Case liên quan:** TC-IMPORT-xx

---

## UC-CUST-01: Xem tiến độ project customer view

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-CUST-01 |
| Use Case Name | Xem tiến độ project customer view |
| Module | CUST |
| Priority | Must Have |
| Actor chính | Customer |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Xem tiến độ project customer view` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-CUST.  
**API liên quan:** `/api/v1/orgs/{orgId}/projects`  
**DB Tables liên quan:** `project_customer_access_policies, project_status_reports`  
**UI Screen liên quan:** CUST screen / project detail tab  
**Test Case liên quan:** TC-CUST-xx

---

## UC-CUST-02: Gửi phản hồi về task

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-CUST-02 |
| Use Case Name | Gửi phản hồi về task |
| Module | CUST |
| Priority | Should Have |
| Actor chính | Customer |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Gửi phản hồi về task` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-CUST.  
**API liên quan:** `/api/v1/orgs/{orgId}/projects`  
**DB Tables liên quan:** `project_customer_access_policies, project_status_reports`  
**UI Screen liên quan:** CUST screen / project detail tab  
**Test Case liên quan:** TC-CUST-xx

---

## UC-CUST-03: Xem thông tin public

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-CUST-03 |
| Use Case Name | Xem thông tin public |
| Module | CUST |
| Priority | Must Have |
| Actor chính | Customer |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Xem thông tin public` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-CUST.  
**API liên quan:** `/api/v1/orgs/{orgId}/projects`  
**DB Tables liên quan:** `project_customer_access_policies, project_status_reports`  
**UI Screen liên quan:** CUST screen / project detail tab  
**Test Case liên quan:** TC-CUST-xx

---

## UC-ADMIN-01: Bật/tắt feature flag

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-ADMIN-01 |
| Use Case Name | Bật/tắt feature flag |
| Module | ADMIN |
| Priority | Should Have |
| Actor chính | System admin |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Bật/tắt feature flag` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-ADMIN.  
**API liên quan:** `/api/v1/admin/dashboard`  
**DB Tables liên quan:** `feature_flags, audit_logs, tenants`  
**UI Screen liên quan:** ADMIN screen / project detail tab  
**Test Case liên quan:** TC-ADMIN-xx

---

## UC-ADMIN-02: Xem audit log hệ thống

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-ADMIN-02 |
| Use Case Name | Xem audit log hệ thống |
| Module | ADMIN |
| Priority | Must Have |
| Actor chính | System admin/org_admin |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Xem audit log hệ thống` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-ADMIN.  
**API liên quan:** `/api/v1/admin/dashboard`  
**DB Tables liên quan:** `feature_flags, audit_logs, tenants`  
**UI Screen liên quan:** ADMIN screen / project detail tab  
**Test Case liên quan:** TC-ADMIN-xx

---

## UC-ADMIN-03: Quản lý tenant

| Thuộc tính | Giá trị |
|---|---|
| Use Case ID | UC-ADMIN-03 |
| Use Case Name | Quản lý tenant |
| Module | ADMIN |
| Priority | Should Have |
| Actor chính | System admin |
| Actor phụ | System, Notification Service, Audit Service |
| Trigger | Actor chọn chức năng `Quản lý tenant` trên UI/API |
| Pre-conditions | Actor đã có quyền phù hợp; dữ liệu project/org tồn tại nếu cần |
| Post-condition Success | Dữ liệu nghiệp vụ được cập nhật, audit/notification phát sinh nếu cần |
| Post-condition Failure | Không thay đổi dữ liệu; lỗi được trả về có correlationId |

**Main Flow**
1. Actor mở màn hình hoặc gọi API tương ứng.
2. System xác thực session và xác định tenant/org/project scope.
3. System kiểm tra RBAC 3 tầng và business rules liên quan.
4. Actor nhập dữ liệu và xác nhận.
5. System validate DTO, ghi dữ liệu trong transaction.
6. System ghi audit log, cập nhật cache, phát notification/webhook nếu có.
7. System trả kết quả và UI refresh realtime nếu cần.

**Alternative Flow A — dữ liệu cần review**
A1. Nếu thao tác tạo ra draft/AI suggestion/import preview, System lưu trạng thái `pending_review`.
A2. Actor có quyền kiểm tra, sửa và approve trước khi commit.

**Exception Flow E — lỗi quyền hoặc business rule**
E1. Nếu chưa đăng nhập, trả 401.
E2. Nếu không đủ quyền, trả 403 hoặc 404 masked với customer/private data.
E3. Nếu vi phạm rule, trả 422 và không commit transaction.

**Business Rules áp dụng:** BR-RBAC, BR-AUDIT, BR-ADMIN.  
**API liên quan:** `/api/v1/admin/dashboard`  
**DB Tables liên quan:** `feature_flags, audit_logs, tenants`  
**UI Screen liên quan:** ADMIN screen / project detail tab  
**Test Case liên quan:** TC-ADMIN-xx

---

# Use Case Relationship Diagram (text)

```text
UC-PROJ-01 includes UC-PROJ-03, UC-PROJ-04
UC-TASK-06 includes UC-TASK-09
UC-TASK-07 extends UC-TASK-06
UC-TASK-08 extends UC-TASK-06
UC-CHAT-03 includes UC-TASK-01
UC-MEETING-03 includes UC-TASK-01
UC-IMPORT-04 includes UC-TASK-01 and UC-WIKI-01
UC-AI-02 includes UC-AI-04
UC-CUST-01 includes UC-PROJ-06
```

# Actor-Use Case Matrix

| Actor | Primary UC | Secondary/View UC |
|---|---|---|
| System Admin | UC-ADMIN-01..03 | Toàn bộ audit/config |
| Org Owner/Admin | UC-ORG, UC-PROJ, UC-ADMIN-02 | UC-REPORT/analytics |
| Project Manager | UC-PROJ, UC-TASK, UC-KANBAN, UC-SPRINT, UC-CHAT, UC-MEETING | UC-AI/IMPORT |
| Developer/Tester | UC-TASK-05/06/09/10, UC-CHAT-02, UC-WIKI | UC-PROJ-06 |
| Reviewer | UC-TASK-07/08, UC-COMMENT | UC-TASK detail/evidence |
| Customer | UC-CUST-01..03 | Public task/wiki/report |
