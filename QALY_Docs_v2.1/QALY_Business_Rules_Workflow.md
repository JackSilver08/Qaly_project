# QALY Workspace — Business Rules & Workflow Specification v2.0

**Phiên bản:** v2.0 — Production/Japan-style specification  
**Ngày:** 16/05/2026  
**Phạm vi:** Dự án web quản lý dự án phần mềm cho doanh nghiệp outsource vừa và nhỏ.  
**Định hướng:** Jira + Notion + Mini Zalo + AI Assistant + Document Mining.  
**Ghi chú kiến trúc:** Tài liệu v2.0 mở rộng từ SRS/TKHT v1.0. Các phần dưới đây là target design để nhóm có thể triển khai 80–90% chức năng web trong 10–11 tuần, không bắt buộc implement toàn bộ bảng P2 nếu thiếu thời gian.

---

## PHẦN A — Business Rules


# BR-RBAC


## BR-RBAC-01: Cross-tenant access luôn bị chặn

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-RBAC-01 |
| Category | Authorization |
| Priority | Critical |
| Module | RBAC |

**Mô tả:** Cross-tenant access luôn bị chặn. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module RBAC.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module RBAC, DB constraints/index, frontend validation và test TC-RBAC.

---

## BR-RBAC-02: System admin có quyền xem audit toàn hệ thống

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-RBAC-02 |
| Category | Authorization |
| Priority | Critical |
| Module | RBAC |

**Mô tả:** System admin có quyền xem audit toàn hệ thống. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module RBAC.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module RBAC, DB constraints/index, frontend validation và test TC-RBAC.

---

## BR-RBAC-03: Org owner quản lý org admin/member

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-RBAC-03 |
| Category | Authorization |
| Priority | Critical |
| Module | RBAC |

**Mô tả:** Org owner quản lý org admin/member. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module RBAC.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module RBAC, DB constraints/index, frontend validation và test TC-RBAC.

---

## BR-RBAC-04: Project manager quản lý project member

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-RBAC-04 |
| Category | Authorization |
| Priority | Critical |
| Module | RBAC |

**Mô tả:** Project manager quản lý project member. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module RBAC.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module RBAC, DB constraints/index, frontend validation và test TC-RBAC.

---

## BR-RBAC-05: Sub-manager chỉ trong phase/module được ủy quyền

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-RBAC-05 |
| Category | Authorization |
| Priority | Critical |
| Module | RBAC |

**Mô tả:** Sub-manager chỉ trong phase/module được ủy quyền. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module RBAC.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module RBAC, DB constraints/index, frontend validation và test TC-RBAC.

---

## BR-RBAC-06: Customer chỉ thấy customer_safe/public data

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-RBAC-06 |
| Category | Authorization |
| Priority | Critical |
| Module | RBAC |

**Mô tả:** Customer chỉ thấy customer_safe/public data. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module RBAC.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module RBAC, DB constraints/index, frontend validation và test TC-RBAC.

---

# BR-TASK


## BR-TASK-01: Assignee phải là project member

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-TASK-01 |
| Category | State Machine |
| Priority | Critical |
| Module | TASK |

**Mô tả:** Assignee phải là project member. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module TASK.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module TASK, DB constraints/index, frontend validation và test TC-TASK.

---

## BR-TASK-02: Task Done cần subtask/checklist Done nếu policy bật

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-TASK-02 |
| Category | State Machine |
| Priority | Critical |
| Module | TASK |

**Mô tả:** Task Done cần subtask/checklist Done nếu policy bật. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module TASK.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module TASK, DB constraints/index, frontend validation và test TC-TASK.

---

## BR-TASK-03: Status transition phải theo workflow

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-TASK-03 |
| Category | State Machine |
| Priority | Critical |
| Module | TASK |

**Mô tả:** Status transition phải theo workflow. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module TASK.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module TASK, DB constraints/index, frontend validation và test TC-TASK.

---

## BR-TASK-04: Overdue task tự động flag

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-TASK-04 |
| Category | State Machine |
| Priority | Critical |
| Module | TASK |

**Mô tả:** Overdue task tự động flag. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module TASK.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module TASK, DB constraints/index, frontend validation và test TC-TASK.

---

## BR-TASK-05: Private task trả masked metadata

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-TASK-05 |
| Category | State Machine |
| Priority | Critical |
| Module | TASK |

**Mô tả:** Private task trả masked metadata. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module TASK.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module TASK, DB constraints/index, frontend validation và test TC-TASK.

---

## BR-TASK-06: Deadline quá khứ cần override reason

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-TASK-06 |
| Category | State Machine |
| Priority | Critical |
| Module | TASK |

**Mô tả:** Deadline quá khứ cần override reason. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module TASK.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module TASK, DB constraints/index, frontend validation và test TC-TASK.

---

# BR-DEP


## BR-DEP-01: Không self dependency

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-DEP-01 |
| Category | Validation/Constraint |
| Priority | High |
| Module | DEP |

**Mô tả:** Không self dependency. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module DEP.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module DEP, DB constraints/index, frontend validation và test TC-DEP.

---

## BR-DEP-02: Không circular dependency

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-DEP-02 |
| Category | Validation/Constraint |
| Priority | High |
| Module | DEP |

**Mô tả:** Không circular dependency. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module DEP.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module DEP, DB constraints/index, frontend validation và test TC-DEP.

---

## BR-DEP-03: Task blocked không được InProgress nếu block mode strict

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-DEP-03 |
| Category | Validation/Constraint |
| Priority | High |
| Module | DEP |

**Mô tả:** Task blocked không được InProgress nếu block mode strict. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module DEP.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module DEP, DB constraints/index, frontend validation và test TC-DEP.

---

## BR-DEP-04: Unblock task sẽ notify dependent assignee

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-DEP-04 |
| Category | Validation/Constraint |
| Priority | High |
| Module | DEP |

**Mô tả:** Unblock task sẽ notify dependent assignee. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module DEP.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module DEP, DB constraints/index, frontend validation và test TC-DEP.

---

# BR-KANBAN


## BR-KANBAN-01: Cột custom phải map status chuẩn

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-KANBAN-01 |
| Category | State Machine |
| Priority | High |
| Module | KANBAN |

**Mô tả:** Cột custom phải map status chuẩn. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module KANBAN.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module KANBAN, DB constraints/index, frontend validation và test TC-KANBAN.

---

## BR-KANBAN-02: Done column không xóa

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-KANBAN-02 |
| Category | State Machine |
| Priority | High |
| Module | KANBAN |

**Mô tả:** Done column không xóa. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module KANBAN.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module KANBAN, DB constraints/index, frontend validation và test TC-KANBAN.

---

## BR-KANBAN-03: WIP limit có mode warn/block

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-KANBAN-03 |
| Category | State Machine |
| Priority | High |
| Module | KANBAN |

**Mô tả:** WIP limit có mode warn/block. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module KANBAN.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module KANBAN, DB constraints/index, frontend validation và test TC-KANBAN.

---

## BR-KANBAN-04: Kéo thả phải ghi audit nếu đổi status

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-KANBAN-04 |
| Category | State Machine |
| Priority | High |
| Module | KANBAN |

**Mô tả:** Kéo thả phải ghi audit nếu đổi status. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module KANBAN.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module KANBAN, DB constraints/index, frontend validation và test TC-KANBAN.

---

# BR-SPRINT


## BR-SPRINT-01: Một project chỉ có một active sprint nếu setting strict

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-SPRINT-01 |
| Category | State Machine |
| Priority | High |
| Module | SPRINT |

**Mô tả:** Một project chỉ có một active sprint nếu setting strict. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module SPRINT.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module SPRINT, DB constraints/index, frontend validation và test TC-SPRINT.

---

## BR-SPRINT-02: Sprint start cần ít nhất một task

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-SPRINT-02 |
| Category | State Machine |
| Priority | High |
| Module | SPRINT |

**Mô tả:** Sprint start cần ít nhất một task. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module SPRINT.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module SPRINT, DB constraints/index, frontend validation và test TC-SPRINT.

---

## BR-SPRINT-03: Incomplete task khi close sprint phải move backlog/next sprint

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-SPRINT-03 |
| Category | State Machine |
| Priority | High |
| Module | SPRINT |

**Mô tả:** Incomplete task khi close sprint phải move backlog/next sprint. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module SPRINT.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module SPRINT, DB constraints/index, frontend validation và test TC-SPRINT.

---

## BR-SPRINT-04: Velocity snapshot khóa khi complete

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-SPRINT-04 |
| Category | State Machine |
| Priority | High |
| Module | SPRINT |

**Mô tả:** Velocity snapshot khóa khi complete. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module SPRINT.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module SPRINT, DB constraints/index, frontend validation và test TC-SPRINT.

---

# BR-EVIDENCE


## BR-EVIDENCE-01: Evidence required trước review

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-EVIDENCE-01 |
| Category | Validation/Constraint |
| Priority | High |
| Module | EVIDENCE |

**Mô tả:** Evidence required trước review. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module EVIDENCE.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module EVIDENCE, DB constraints/index, frontend validation và test TC-EVIDENCE.

---

## BR-EVIDENCE-02: File evidence theo allowlist

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-EVIDENCE-02 |
| Category | Validation/Constraint |
| Priority | High |
| Module | EVIDENCE |

**Mô tả:** File evidence theo allowlist. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module EVIDENCE.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module EVIDENCE, DB constraints/index, frontend validation và test TC-EVIDENCE.

---

## BR-EVIDENCE-03: Reviewer phải ghi reason khi reject

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-EVIDENCE-03 |
| Category | Validation/Constraint |
| Priority | High |
| Module | EVIDENCE |

**Mô tả:** Reviewer phải ghi reason khi reject. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module EVIDENCE.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module EVIDENCE, DB constraints/index, frontend validation và test TC-EVIDENCE.

---

## BR-EVIDENCE-04: Approval result immutable sau khi report locked

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-EVIDENCE-04 |
| Category | Validation/Constraint |
| Priority | High |
| Module | EVIDENCE |

**Mô tả:** Approval result immutable sau khi report locked. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module EVIDENCE.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module EVIDENCE, DB constraints/index, frontend validation và test TC-EVIDENCE.

---

# BR-CHAT


## BR-CHAT-01: Customer DM mặc định tắt

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-CHAT-01 |
| Category | Validation/Constraint |
| Priority | High |
| Module | CHAT |

**Mô tả:** Customer DM mặc định tắt. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module CHAT.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module CHAT, DB constraints/index, frontend validation và test TC-CHAT.

---

## BR-CHAT-02: Task from chat cần confirm

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-CHAT-02 |
| Category | Validation/Constraint |
| Priority | High |
| Module | CHAT |

**Mô tả:** Task from chat cần confirm. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module CHAT.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module CHAT, DB constraints/index, frontend validation và test TC-CHAT.

---

## BR-CHAT-03: Message delete là soft delete

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-CHAT-03 |
| Category | Validation/Constraint |
| Priority | High |
| Module | CHAT |

**Mô tả:** Message delete là soft delete. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module CHAT.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module CHAT, DB constraints/index, frontend validation và test TC-CHAT.

---

## BR-CHAT-04: Internal message không public cho customer

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-CHAT-04 |
| Category | Validation/Constraint |
| Priority | High |
| Module | CHAT |

**Mô tả:** Internal message không public cho customer. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module CHAT.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module CHAT, DB constraints/index, frontend validation và test TC-CHAT.

---

# BR-CUSTOMER


## BR-CUSTOMER-01: Customer portal filter dữ liệu theo policy

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-CUSTOMER-01 |
| Category | Authorization |
| Priority | High |
| Module | CUSTOMER |

**Mô tả:** Customer portal filter dữ liệu theo policy. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module CUSTOMER.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module CUSTOMER, DB constraints/index, frontend validation và test TC-CUSTOMER.

---

## BR-CUSTOMER-02: Customer không xem estimate nội bộ nếu bị tắt

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-CUSTOMER-02 |
| Category | Authorization |
| Priority | High |
| Module | CUSTOMER |

**Mô tả:** Customer không xem estimate nội bộ nếu bị tắt. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module CUSTOMER.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module CUSTOMER, DB constraints/index, frontend validation và test TC-CUSTOMER.

---

## BR-CUSTOMER-03: Customer comment cần PM review nếu project bật moderation

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-CUSTOMER-03 |
| Category | Authorization |
| Priority | High |
| Module | CUSTOMER |

**Mô tả:** Customer comment cần PM review nếu project bật moderation. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module CUSTOMER.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module CUSTOMER, DB constraints/index, frontend validation và test TC-CUSTOMER.

---

## BR-CUSTOMER-04: Customer report đã gửi phải snapshot

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-CUSTOMER-04 |
| Category | Authorization |
| Priority | High |
| Module | CUSTOMER |

**Mô tả:** Customer report đã gửi phải snapshot. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module CUSTOMER.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module CUSTOMER, DB constraints/index, frontend validation và test TC-CUSTOMER.

---

# BR-AI


## BR-AI-01: AI phải filter quyền trước retrieval

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-AI-01 |
| Category | Authorization |
| Priority | Critical |
| Module | AI |

**Mô tả:** AI phải filter quyền trước retrieval. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module AI.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module AI, DB constraints/index, frontend validation và test TC-AI.

---

## BR-AI-02: AI phải cite source nếu trả lời từ knowledge base

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-AI-02 |
| Category | Authorization |
| Priority | Critical |
| Module | AI |

**Mô tả:** AI phải cite source nếu trả lời từ knowledge base. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module AI.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module AI, DB constraints/index, frontend validation và test TC-AI.

---

## BR-AI-03: AI suggestion cần human approval

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-AI-03 |
| Category | Authorization |
| Priority | Critical |
| Module | AI |

**Mô tả:** AI suggestion cần human approval. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module AI.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module AI, DB constraints/index, frontend validation và test TC-AI.

---

## BR-AI-04: Fallback local/API/mock theo config và quota

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-AI-04 |
| Category | Authorization |
| Priority | Critical |
| Module | AI |

**Mô tả:** Fallback local/API/mock theo config và quota. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module AI.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module AI, DB constraints/index, frontend validation và test TC-AI.

---

# BR-IMPORT


## BR-IMPORT-01: Import phải preview trước commit

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-IMPORT-01 |
| Category | Validation/Constraint |
| Priority | High |
| Module | IMPORT |

**Mô tả:** Import phải preview trước commit. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module IMPORT.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module IMPORT, DB constraints/index, frontend validation và test TC-IMPORT.

---

## BR-IMPORT-02: Draft task cần approve

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-IMPORT-02 |
| Category | Validation/Constraint |
| Priority | High |
| Module | IMPORT |

**Mô tả:** Draft task cần approve. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module IMPORT.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module IMPORT, DB constraints/index, frontend validation và test TC-IMPORT.

---

## BR-IMPORT-03: Import lỗi vượt ngưỡng phải rollback

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-IMPORT-03 |
| Category | Validation/Constraint |
| Priority | High |
| Module | IMPORT |

**Mô tả:** Import lỗi vượt ngưỡng phải rollback. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module IMPORT.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module IMPORT, DB constraints/index, frontend validation và test TC-IMPORT.

---

## BR-IMPORT-04: Source document id phải lưu để truy vết

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-IMPORT-04 |
| Category | Validation/Constraint |
| Priority | High |
| Module | IMPORT |

**Mô tả:** Source document id phải lưu để truy vết. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module IMPORT.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module IMPORT, DB constraints/index, frontend validation và test TC-IMPORT.

---

# BR-WIKI


## BR-WIKI-01: Wiki version tạo mới khi cập nhật

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-WIKI-01 |
| Category | Validation/Constraint |
| Priority | High |
| Module | WIKI |

**Mô tả:** Wiki version tạo mới khi cập nhật. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module WIKI.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module WIKI, DB constraints/index, frontend validation và test TC-WIKI.

---

## BR-WIKI-02: Internal page không hiển thị customer

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-WIKI-02 |
| Category | Validation/Constraint |
| Priority | High |
| Module | WIKI |

**Mô tả:** Internal page không hiển thị customer. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module WIKI.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module WIKI, DB constraints/index, frontend validation và test TC-WIKI.

---

## BR-WIKI-03: Rollback phải ghi audit

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-WIKI-03 |
| Category | Validation/Constraint |
| Priority | High |
| Module | WIKI |

**Mô tả:** Rollback phải ghi audit. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module WIKI.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module WIKI, DB constraints/index, frontend validation và test TC-WIKI.

---

## BR-WIKI-04: Markdown render phải sanitize

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-WIKI-04 |
| Category | Validation/Constraint |
| Priority | High |
| Module | WIKI |

**Mô tả:** Markdown render phải sanitize. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module WIKI.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module WIKI, DB constraints/index, frontend validation và test TC-WIKI.

---

# BR-MEETING


## BR-MEETING-01: AI summary chỉ là draft

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-MEETING-01 |
| Category | Validation/Constraint |
| Priority | High |
| Module | MEETING |

**Mô tả:** AI summary chỉ là draft. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module MEETING.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module MEETING, DB constraints/index, frontend validation và test TC-MEETING.

---

## BR-MEETING-02: Action item tạo task cần host confirm

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-MEETING-02 |
| Category | Validation/Constraint |
| Priority | High |
| Module | MEETING |

**Mô tả:** Action item tạo task cần host confirm. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module MEETING.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module MEETING, DB constraints/index, frontend validation và test TC-MEETING.

---

## BR-MEETING-03: Meeting visibility quyết định customer access

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-MEETING-03 |
| Category | Validation/Constraint |
| Priority | High |
| Module | MEETING |

**Mô tả:** Meeting visibility quyết định customer access. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module MEETING.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module MEETING, DB constraints/index, frontend validation và test TC-MEETING.

---

## BR-MEETING-04: Decision log không xóa cứng

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-MEETING-04 |
| Category | Validation/Constraint |
| Priority | High |
| Module | MEETING |

**Mô tả:** Decision log không xóa cứng. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module MEETING.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module MEETING, DB constraints/index, frontend validation và test TC-MEETING.

---

# BR-WEBHOOK


## BR-WEBHOOK-01: Webhook phải ký secret

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-WEBHOOK-01 |
| Category | State Machine |
| Priority | High |
| Module | WEBHOOK |

**Mô tả:** Webhook phải ký secret. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module WEBHOOK.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module WEBHOOK, DB constraints/index, frontend validation và test TC-WEBHOOK.

---

## BR-WEBHOOK-02: Retry tối đa theo config

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-WEBHOOK-02 |
| Category | State Machine |
| Priority | High |
| Module | WEBHOOK |

**Mô tả:** Retry tối đa theo config. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module WEBHOOK.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module WEBHOOK, DB constraints/index, frontend validation và test TC-WEBHOOK.

---

## BR-WEBHOOK-03: Delivery log immutable

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-WEBHOOK-03 |
| Category | State Machine |
| Priority | High |
| Module | WEBHOOK |

**Mô tả:** Delivery log immutable. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module WEBHOOK.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module WEBHOOK, DB constraints/index, frontend validation và test TC-WEBHOOK.

---

## BR-WEBHOOK-04: Endpoint disabled sau nhiều lần fail nếu policy bật

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-WEBHOOK-04 |
| Category | State Machine |
| Priority | High |
| Module | WEBHOOK |

**Mô tả:** Endpoint disabled sau nhiều lần fail nếu policy bật. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module WEBHOOK.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module WEBHOOK, DB constraints/index, frontend validation và test TC-WEBHOOK.

---

# BR-AUDIT


## BR-AUDIT-01: Thay đổi role/status/deadline/visibility phải audit

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-AUDIT-01 |
| Category | Validation/Constraint |
| Priority | Critical |
| Module | AUDIT |

**Mô tả:** Thay đổi role/status/deadline/visibility phải audit. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module AUDIT.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module AUDIT, DB constraints/index, frontend validation và test TC-AUDIT.

---

## BR-AUDIT-02: Admin action audit bắt buộc

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-AUDIT-02 |
| Category | Validation/Constraint |
| Priority | Critical |
| Module | AUDIT |

**Mô tả:** Admin action audit bắt buộc. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module AUDIT.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module AUDIT, DB constraints/index, frontend validation và test TC-AUDIT.

---

## BR-AUDIT-03: Audit không soft delete

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-AUDIT-03 |
| Category | Validation/Constraint |
| Priority | Critical |
| Module | AUDIT |

**Mô tả:** Audit không soft delete. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module AUDIT.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module AUDIT, DB constraints/index, frontend validation và test TC-AUDIT.

---

## BR-AUDIT-04: Export audit cần ghi lại người export

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-AUDIT-04 |
| Category | Validation/Constraint |
| Priority | Critical |
| Module | AUDIT |

**Mô tả:** Export audit cần ghi lại người export. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module AUDIT.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module AUDIT, DB constraints/index, frontend validation và test TC-AUDIT.

---

# BR-SECURITY


## BR-SECURITY-01: File upload block extension nguy hiểm

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-SECURITY-01 |
| Category | Authorization |
| Priority | Critical |
| Module | SECURITY |

**Mô tả:** File upload block extension nguy hiểm. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module SECURITY.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module SECURITY, DB constraints/index, frontend validation và test TC-SECURITY.

---

## BR-SECURITY-02: CSRF cho form/cookie write action

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-SECURITY-02 |
| Category | Authorization |
| Priority | Critical |
| Module | SECURITY |

**Mô tả:** CSRF cho form/cookie write action. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module SECURITY.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module SECURITY, DB constraints/index, frontend validation và test TC-SECURITY.

---

## BR-SECURITY-03: Rate limit auth/AI/upload

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-SECURITY-03 |
| Category | Authorization |
| Priority | Critical |
| Module | SECURITY |

**Mô tả:** Rate limit auth/AI/upload. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module SECURITY.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module SECURITY, DB constraints/index, frontend validation và test TC-SECURITY.

---

## BR-SECURITY-04: IDOR test cho mọi resource ID

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-SECURITY-04 |
| Category | Authorization |
| Priority | Critical |
| Module | SECURITY |

**Mô tả:** IDOR test cho mọi resource ID. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module SECURITY.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module SECURITY, DB constraints/index, frontend validation và test TC-SECURITY.

---

# BR-REPORT


## BR-REPORT-01: Report lock sau khi gửi customer

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-REPORT-01 |
| Category | Validation/Constraint |
| Priority | High |
| Module | REPORT |

**Mô tả:** Report lock sau khi gửi customer. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module REPORT.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module REPORT, DB constraints/index, frontend validation và test TC-REPORT.

---

## BR-REPORT-02: Progress dùng task valid

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-REPORT-02 |
| Category | Validation/Constraint |
| Priority | High |
| Module | REPORT |

**Mô tả:** Progress dùng task valid. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module REPORT.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module REPORT, DB constraints/index, frontend validation và test TC-REPORT.

---

## BR-REPORT-03: Weighted progress dùng estimate/weight nếu bật

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-REPORT-03 |
| Category | Validation/Constraint |
| Priority | High |
| Module | REPORT |

**Mô tả:** Weighted progress dùng estimate/weight nếu bật. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module REPORT.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module REPORT, DB constraints/index, frontend validation và test TC-REPORT.

---

## BR-REPORT-04: Report export phải ghi snapshot filter

| Thuộc tính | Giá trị |
|---|---|
| Rule ID | BR-REPORT-04 |
| Category | Validation/Constraint |
| Priority | High |
| Module | REPORT |

**Mô tả:** Report export phải ghi snapshot filter. Quy tắc này phải được enforce ở backend service/policy và có test case tương ứng.

**Điều kiện:** WHEN actor thực hiện thao tác liên quan đến module REPORT.

**Hành động:** THEN system validate RBAC/business state trước khi ghi dữ liệu; ELSE trả 403/422 và ghi security/audit nếu cần.

**Ví dụ đúng:** User có quyền và dữ liệu hợp lệ nên thao tác thành công.

**Ví dụ sai:** User trái quyền hoặc dữ liệu vi phạm state/constraint nên thao tác bị từ chối.

**Áp dụng tại:** API module REPORT, DB constraints/index, frontend validation và test TC-REPORT.

---

# PHẦN B — Workflow State Machines

## 1. Task Status Flow
```text
[Todo] --[assignee/PM start]--> [InProgress]
[Todo] --[PM cancel]--> [Cancelled]
[InProgress] --[blocked reason]--> [OnHold]
[OnHold] --[unblocked]--> [InProgress]
[InProgress] --[submit evidence + request review]--> [InReview]
[InReview] --[reviewer/PM approve]--> [Done]
[InReview] --[reviewer/PM reject + reason]--> [InProgress]
[Done] --[PM reopen + reason]--> [InReview]
```

## 2. Project Status Flow
```text
[Planned] --[start]--> [Active]
[Active] --[pause]--> [OnHold]
[OnHold] --[resume]--> [Active]
[Active] --[all mandatory milestones accepted]--> [Completed]
[Any] --[archive]--> [Archived]
[Archived] --[admin/PM restore]--> [Active]
```

## 3. Sprint Status Flow
```text
[Draft] --[plan tasks]--> [Planned]
[Planned] --[start]--> [Active]
[Active] --[complete]--> [Completed]
[Active] --[cancel]--> [Cancelled]
```

## 4. Evidence/Approval Flow
```text
[DraftEvidence] --[submit]--> [Submitted]
[Submitted] --[reviewer approve]--> [Approved]
[Submitted] --[reviewer reject + reason]--> [Rejected]
[Rejected] --[assignee resubmit]--> [Submitted]
```

## 5. Document Import Flow
```text
[Uploaded] -> [Processing] -> [Extracted] -> [DraftGenerated] -> [HumanReviewed] -> [Imported]
[Processing|Extracted|DraftGenerated] -> [Failed]
```

## 6. Invitation Flow
```text
[Pending] --[accept before expiry]--> [Accepted]
[Pending] --[reject]--> [Rejected]
[Pending] --[time expired]--> [Expired]
[Pending] --[admin revoke]--> [Revoked]
```

## 7. Webhook Delivery Flow
```text
[Queued] -> [Sending] -> [Delivered]
[Sending] -> [Failed] -> [Retrying] -> [Sending]
[Retrying] --[max attempts]--> [DeadLetter]
```

# PHẦN C — Notification Rules

| Sự kiện | Người nhận | Kênh | Template | Priority |
|---|---|---|---|---|
| task.assigned | Assignee | in-app,email | task_assigned | High |
| approval.requested | Reviewer/PM | in-app,email | approval_requested | High |
| approval.rejected | Assignee | in-app,email | approval_rejected | High |
| deadline.warning | Assignee/PM | in-app,email,digest | deadline_warning | High |
| comment.mentioned | Mentioned user | in-app,push | mention | Medium |
| chat.message | Room members | SignalR/push | chat_message | Medium |
| webhook.failed | PM/Admin | in-app,email | webhook_failed | Medium |
| ai.job.completed | Requester | in-app | ai_job_done | Low |

# PHẦN D — Webhook Events Catalog

| Event | Trigger | Payload chính | Use case |
|---|---|---|---|
| project.created | Project tạo mới | project_id, actor_id | Đồng bộ ngoài |
| task.created | Task tạo mới | task_id, project_id | Tích hợp Git/Jira |
| task.updated | Task update | changed_fields | Audit ngoài |
| task.status_changed | Status đổi | old_status, new_status | CI/CD report |
| approval.requested | Gửi review | request_id | QA/reviewer tool |
| approval.completed | Approve/reject | decision | Report |
| comment.created | Comment mới | comment_id | Notification |
| wiki.updated | Wiki cập nhật | page_id, version | Knowledge sync |
