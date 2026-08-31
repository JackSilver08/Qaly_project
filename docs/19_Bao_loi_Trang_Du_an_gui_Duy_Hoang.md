# Báo lỗi trang Dự án & Chi tiết dự án — gửi Duy Hoàng

**Từ:** nhóm kiểm thử (Chí Khang · Quốc Bảo · Viết Minh · Gia Long)
**Ngày:** 29/08/2026 · **Hạn bàn giao:** 01/09/2026
**Phạm vi:** `pages/ProjectsPage.vue`, `pages/ProjectDetailPage.vue`, `components/ProjectRoadmapTab.vue`

> Nhóm **không sửa logic** ba file này. Tài liệu này ghi lại những gì đã kiểm thử được, một lỗi đã
> phải vá gấp vì làm đỏ toàn bộ E2E, và phần nợ giao diện để bạn quyết định xử lý.

---

## 1. Đã sửa gấp — cần bạn review

### QALY-UI-03 · `ProjectDetailPage.vue` · Đã vá

**Triệu chứng:** Mở trang chi tiết dự án ném `TypeError: Cannot read properties of null (reading 'id')`
ở **mọi lần mở**. Lỗi bị Vue nuốt nên giao diện vẫn render, nhưng cổng "không có lỗi trình duyệt"
trong `demo-script-30-minutes.spec.ts` bắt được và làm đỏ kịch bản demo 30 phút.

**Nguyên nhân:** Nhánh `v-else` của template cũng render khi dự án **đang tải**
(`isLoading === true`, `selectedProject === null`). Ba tab `stats`, `roadmap`, `members` chỉ gác theo
id tab mà không gác `selectedProject`, trong khi các tab còn lại đã dùng `canShow*`:

```vue
<!-- Trước — thiếu guard -->
<div v-if="activeProjectTab === 'stats'" class="tab-pane reveal">
  <ProjectStatsTab :project-id="selectedProject.id" ... />
```

Tab mặc định là `stats`, nên lỗi phát sinh mỗi lần vào trang.

**Cách đã sửa:** Bổ sung ba computed cho khớp với pattern sẵn có của bạn
(`canShowCapacityTab`, `canShowActivityTab`, `canShowWebhooksTab`, `canShowGitHubTab`):

```js
const canShowStatsTab   = computed(() => activeProjectTab.value === "stats"   && !!selectedProject.value);
const canShowRoadmapTab = computed(() => activeProjectTab.value === "roadmap" && !!selectedProject.value);
const canShowMembersTab = computed(() => activeProjectTab.value === "members" && !!selectedProject.value);
```

rồi thay `v-if` của ba tab tương ứng.

**Vì sao nhóm tự sửa thay vì chờ bạn:** lỗi này chặn cổng kiểm thử của cả team. Đây là thay đổi
tối thiểu và theo đúng quy ước đã có trong file — nhưng vẫn cần bạn xác nhận là không đụng vào ý đồ
thiết kế nào của bạn.

---

## 2. Đã kiểm thử — không phát hiện lỗi

| Hạng mục | Kết quả | Nguồn |
| --- | --- | --- |
| Không tràn ngang ở 1440 / 768 / 390px | ✅ Đạt | `responsive-audit.spec.ts` |
| Dark theme — không có mảng nền sáng lọt | ✅ Đạt | `dark-theme.spec.ts` (có ca riêng cho chi tiết dự án) |
| Console trình duyệt sạch ở `/projects` | ✅ Đạt | `browser-console.spec.ts` |
| Tab Lộ trình: milestone, panel chi tiết, modal mẫu | ✅ Đạt | `roadmap-ui.spec.ts` |
| Tab Lộ trình: không tải font ngoài, không emoji trong nút | ✅ Đạt | `roadmap-ui.spec.ts` |
| Gantt hiển thị ngày lấy từ database | ✅ Đạt | `qaly.smoke.spec.ts` |

---

## 3. Nợ giao diện — bạn quyết định, không gấp

### 3.1 Màu nền sáng hardcode

| File | Số chỗ |
| --- | ---: |
| `components/ProjectRoadmapTab.vue` | 39 |
| `pages/ProjectsPage.vue` | 15 |
| `pages/ProjectDetailPage.vue` | 8 |

**Không phải lỗi đang hiện hữu.** `style.css` có sẵn khối "dark-mode surface corrections" với 338
rule `:root[data-theme='dark']` vá lại các màu này, và `dark-theme.spec.ts` đang xanh.

Vấn đề là **độ bền**: mỗi màu hardcode mới thêm vào component đều cần một override tương ứng trong
`style.css`, nếu quên thì vỡ dark theme mà không có gì cảnh báo tại chỗ. Hướng xử lý sạch là dùng
biến (`var(--panel)`, `var(--bg-soft)`, `var(--line)`) ngay trong component — nhưng đây là việc dọn
dẹp, **không nên làm trước ngày 01/09**.

Riêng trong `ProjectRoadmapTab.vue`, cần phân biệt hai nhóm:

- **Màu ngữ nghĩa — giữ nguyên là đúng:** `.timeline-bar.is-normal/.is-due/.is-stale/.is-overdue`
  và legend tương ứng. Đây là mã màu trạng thái, cố ý không đổi theo theme.
- **Màu bề mặt — nên chuyển sang biến:** `#fff`, `#f8fafc`, `#f8fbff`, `#fffdf8`,
  `rgba(255,255,255,.55/.78/.9/.94)`.

### 3.2 Nút icon cần nhãn cho trình đọc màn hình

| File | Số nút icon |
| --- | ---: |
| `pages/ProjectsPage.vue` | 8 |
| `pages/ProjectDetailPage.vue` | 8 |
| `components/ProjectRoadmapTab.vue` | 6 |

Cần rà lại từng nút xem đã có `aria-label` hoặc `title` chưa. Đây là hạng mục nằm trong tiêu chí
nghiệm thu của cả sản phẩm.

---

## 4. Đề nghị

1. **Review giúp bản vá QALY-UI-03** — nếu bạn muốn sửa theo cách khác, cứ thay, miễn là
   `demo-script-30-minutes.spec.ts` vẫn xanh.
2. **Nút icon (mục 3.2)** — nếu bạn không kịp, báo lại để nhóm làm hộ phần thuần giao diện này.
3. **Màu hardcode (mục 3.1)** — đề nghị **để lại sau bàn giao**. Đang chạy đúng, sửa lúc này rủi ro
   cao hơn lợi ích.

Chạy lại các kiểm thử liên quan tới hai trang của bạn:

```bash
npx playwright test tests/e2e/roadmap-ui.spec.ts tests/e2e/dark-theme.spec.ts \
  tests/e2e/responsive-audit.spec.ts tests/e2e/demo-script-30-minutes.spec.ts --workers=1
```
