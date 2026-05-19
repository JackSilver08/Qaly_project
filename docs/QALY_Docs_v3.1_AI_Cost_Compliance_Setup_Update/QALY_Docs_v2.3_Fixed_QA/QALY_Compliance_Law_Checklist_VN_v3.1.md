# QALY Compliance and Law Checklist v3.1 - Vietnam + AI

> Tài liệu này là checklist kỹ thuật/đặc tả cho đồ án, không thay thế tư vấn pháp lý chính thức.

## 1. Nguồn luật/chính sách cần theo dõi

| Nhóm | Văn bản/chính sách | Ảnh hưởng tới QALY |
|---|---|---|
| Personal Data | Nghị định 13/2023/NĐ-CP về bảo vệ dữ liệu cá nhân | consent, mục đích xử lý, bảo vệ dữ liệu, xử lý dữ liệu cá nhân |
| Personal Data | Luật Bảo vệ dữ liệu cá nhân số 91/2025/QH15 | từ 2026 là khung luật chính cần theo dõi khi sản phẩm public |
| Cybersecurity | Luật An ninh mạng 2018 | an toàn hệ thống, sự cố an ninh mạng, yêu cầu lưu trữ/xử lý dữ liệu trong một số trường hợp |
| Internet/Social network | Nghị định 147/2024/NĐ-CP | nếu QALY mở rộng thành mạng xã hội/dịch vụ internet công khai |
| AI Provider Policy | OpenAI/Gemini/Anthropic terms/data policy | kiểm soát dữ liệu gửi lên API, retention, training opt-in/out |
| Open-source License | MIT/Apache-2.0/Microsoft EULA | đảm bảo dùng Meetily/Qdrant/Ollama/SQL Server đúng license |

## 2. Dữ liệu cá nhân trong QALY

| Loại dữ liệu | Ví dụ | Mức nhạy cảm | Xử lý |
|---|---|---|---|
| Tài khoản | họ tên, email, avatar | personal data | lưu SQL, hash password, RBAC |
| Tin nhắn | nội dung chat, file | có thể chứa personal/sensitive data | permission theo workspace/project/channel |
| Meeting transcript | lời nói, quyết định, người được giao việc | nhạy cảm cao | user consent, label AI processing, retention policy |
| Task/workload | performance, task history | nhạy cảm nội bộ | chỉ manager/member có quyền xem |
| AI logs | prompt hash, provider, token, result | audit data | không log raw sensitive text trừ khi debug có quyền |

## 3. Quy tắc xử lý AI bắt buộc

1. Thông báo rõ: meeting/chat có thể được AI xử lý để tóm tắt/gợi ý task.
2. Không gửi raw meeting transcript lên cloud API nếu meeting được đánh dấu `sensitive=true`.
3. Với sensitive meeting, ưu tiên Meetily/Ollama local hoặc manual review.
4. AI output là draft/proposal, không phải quyết định cuối.
5. Người dùng có quyền xem/chỉnh/sửa/xóa draft trước khi tạo task.
6. Ghi audit log cho mọi AI job.
7. Không lưu API key trong source code.
8. Không gửi password/token/secret vào prompt.
9. Có cơ chế xóa meeting transcript hoặc anonymize theo chính sách retention.
10. Có role-based access control cho meeting note và AI result.

## 4. License checklist

| Thành phần | License/Policy | Hành động |
|---|---|---|
| Meetily Community | MIT theo GitHub repo | giữ LICENSE, ghi attribution nếu fork/phân phối |
| Qdrant OSS | Apache-2.0 | có thể self-host; giữ license notice nếu redistribute |
| Ollama | kiểm tra license binary/model riêng | mỗi model có license riêng, không assume dùng thương mại được |
| SQL Server Developer | dev/test only | production thật cần license phù hợp hoặc Express/paid |
| AI API | provider terms | không expose key, tuân thủ usage/data policy |

## 5. Data retention policy đề xuất

| Data | Retention demo | Retention production future |
|---|---:|---:|
| Raw meeting transcript | 30-90 ngày | theo tenant policy |
| Meeting summary/action item | theo vòng đời project | theo tenant policy |
| AI prompt raw | không lưu mặc định | chỉ lưu khi debug có mask |
| AI prompt hash/result hash | 180 ngày | 1 năm |
| AI usage ledger | 1 năm | 2 năm hoặc theo kế toán |
| Deleted user data | soft delete 30 ngày | theo yêu cầu pháp lý/hợp đồng |

## 6. Quy tắc “chuẩn Nhật” áp dụng

1. **Horen-so:** AI-generated action item phải rõ nguồn, người phụ trách, deadline, trạng thái xác nhận.
2. **Genchi Genbutsu:** luôn hiển thị source message/meeting để kiểm chứng.
3. **Kaizen:** lưu feedback đúng/sai của AI để cải thiện prompt/rule.
4. **No silent automation:** AI không âm thầm giao task hoặc đổi deadline.
5. **Traceability:** mọi AI decision phải truy được nguồn input và người confirm.
6. **Check-before-action:** PM/leader confirm trước khi task chính thức được tạo/gán.

## 7. Compliance acceptance criteria

- Có privacy notice trong màn hình meeting import/AI assist.
- Có checkbox/user action xác nhận xử lý transcript bằng AI.
- Có cấu hình `sensitive` để ép local-only/no-cloud.
- Có audit log xem được bởi admin.
- Có quyền xóa transcript/meeting note theo owner/admin.
- Có budget/quota để tránh lạm dụng API.
- Có license register trong tài liệu bàn giao.
