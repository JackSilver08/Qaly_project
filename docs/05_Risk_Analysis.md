# ⚠️ QALY PROJECT – PHÂN TÍCH RỦI RO

> **Ngày tạo:** 01/05/2026 | **Phiên bản:** 1.0

---

## I. RỦI RO CAO (🔴)

### 1. AuditLog JSONB → NVARCHAR(MAX)
- **Mô tả:** PostgreSQL JSONB hỗ trợ query trực tiếp JSON path. SQL Server không có kiểu JSONB native.
- **Impact:** Mất khả năng query mạnh trên audit data
- **Giải pháp:**
  - Phase 1: Serialize/Deserialize bằng `System.Text.Json` trong C#
  - Phase 2: Dùng `OPENJSON()` trong SQL Server cho query phức tạp
- **Người phụ trách:** Backend Dev 3

### 2. Full-text Search
- **Mô tả:** PostgreSQL GIN index mạnh hơn nhiều so với SQL Server LIKE
- **Impact:** Performance search kém nếu data lớn
- **Giải pháp:**
  - Phase 1: `LIKE '%keyword%'` + pagination
  - Phase 2: Setup SQL Server Full-Text Catalog & Index
- **Người phụ trách:** Backend Dev 2

### 3. Solution format .slnx
- **Mô tả:** .NET 10 dùng `.slnx` mới, nhiều tool CI/CD chưa hỗ trợ
- **Impact:** Build pipeline có thể fail
- **Giải pháp:** Chuyển sang `.sln` classic
- **Người phụ trách:** Tech Lead

### 4. Code Spaghetti khi scale
- **Mô tả:** Nếu không tuân thủ Clean Architecture, service sẽ phình
- **Impact:** Khó maintain, khó test
- **Giải pháp:** Code review nghiêm ngặt, enforce layer boundaries
- **Người phụ trách:** Tech Lead

---

## II. RỦI RO TRUNG BÌNH (🟡)

### 5. .NET 10 Preview
- **Mô tả:** Có thể có breaking changes giữa các preview
- **Impact:** Phải update code khi có release mới
- **Giải pháp:** Pin version, theo dõi release notes
- **Người phụ trách:** Tech Lead

### 6. Vue Islands + Razor complexity
- **Mô tả:** Kết hợp Vue components trong Razor Pages phức tạp khi debug
- **Impact:** Chậm development frontend
- **Giải pháp:** Tách rõ API endpoints cho Vue, dùng Vue DevTools
- **Người phụ trách:** Frontend Dev 2

### 7. SignalR Scaling
- **Mô tả:** In-process SignalR không scale được multi-server
- **Impact:** Bottleneck khi user nhiều
- **Giải pháp:** Phase 1: In-process. Phase 2: Redis backplane
- **Người phụ trách:** Backend Dev 3

---

## III. RỦI RO THẤP (🟢)

### 8. Team coordination (7 người)
- **Mô tả:** Nếu không có lead cứng, dễ vỡ tiến độ
- **Giải pháp:** Daily standup, PR review bắt buộc, milestone checkpoint
- **Người phụ trách:** Tech Lead

### 9. SQL Server licensing
- **Mô tả:** SQL Server Enterprise đắt
- **Giải pháp:** Dùng Developer Edition (free cho dev), Express cho MVP
- **Người phụ trách:** Tech Lead

---

## IV. RISK MATRIX

```
Impact ↑
  High   │  3,4    │  1,2    │
  Medium │  5,6,7  │         │
  Low    │  8,9    │         │
         └─────────┴─────────┘
           Low       High    → Probability
```

---

*Cập nhật khi phát hiện rủi ro mới.*
