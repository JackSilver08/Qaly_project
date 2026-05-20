# QALY Workspace — 10-Week Implementation Plan v3.0

**Mục tiêu:** hoàn thành MVP production-like trong 10 tuần, ưu tiên demo ổn định và giảm rủi ro AI.

---

## 1. Nguyên tắc cắt scope

- P0 trước, P1 sau, P2 chỉ ghi roadmap.
- Không tự viết realtime transcription từ đầu.
- Không bắt Ollama 14B làm điều kiện bắt buộc.
- AI API primary cho demo chất lượng cao.
- Meetily dùng cho meeting note.
- Qdrant optional P1, có feature flag.
- Mọi output AI là draft, user confirm.

---

## 2. Timeline 10 tuần

| Tuần | Mục tiêu | Deliverables | Definition of Done |
|---:|---|---|---|
| 1 | Freeze architecture v3.0 | AiGateway contract, Meetily approach, provider routing, env plan | chạy được skeleton backend/frontend, docs v3.0 chốt |
| 2 | Core auth/org/project/task | login, org/project/member, task CRUD | seed accounts demo, RBAC cơ bản |
| 3 | Task workflow + Kanban + audit | Todo/InProgress/InReview/Done, evidence, approval | demo status flow có audit |
| 4 | Mini Zalo chat MVP | rooms, messages, realtime, task-from-chat draft UI | gửi nhận realtime + tạo task draft |
| 5 | Meetily import MVP | import transcript/summary file, meeting note UI | upload/import sample Meetily export thành meeting note |
| 6 | AI extraction P0 | meeting extract, chat summary, action item, checklist | API/mock/local routing, output JSON valid |
| 7 | Task recommendation | skill/workload scoring + AI explanation | gợi ý 1-3 assignee, PM confirm |
| 8 | Report + optional Qdrant | sprint summary, semantic search/task duplicate nếu kịp | feature flag, fallback SQL nếu Qdrant tắt |
| 9 | Hardening/QA | security, tenant isolation, token budget, health checks | acceptance checklist pass, demo scripts |
| 10 | Defense prep | docs, diagrams, screenshots, rehearsal | full demo run 2 lần không lỗi nghiêm trọng |

---

## 3. Team workload ưu tiên

| Role | Trọng tâm |
|---|---|
| Backend lead | Auth/RBAC/SQL/API/AiGateway |
| Frontend lead | dashboard/task/chat/meeting UI |
| AI dev | Meetily connector, prompts, provider routing, mock dataset |
| QA/tester | acceptance, security, demo script, regression |
| PM/documenter | SRS/UML/traceability/report Nhật |

---

## 4. Demo script cuối kỳ

1. Đăng nhập PM.
2. Tạo project + members + skills.
3. Gửi chat realtime trong Mini Zalo.
4. Từ chat tạo task draft.
5. Import transcript/summary từ Meetily.
6. AI extract keywords/action items.
7. User xác nhận tạo task từ action item.
8. AI gợi ý assignee theo skill/workload.
9. PM xác nhận giao task.
10. Task chạy qua Kanban + evidence + approval.
11. AI sinh progress report.
12. Optional: semantic search tìm lại nội dung meeting/task.

---

## 5. Không nên làm nếu chưa xong P0

- Full PDF OCR/MinerU production.
- Custom Meetily core/audio engine.
- Ollama 14B làm bắt buộc.
- Multi-user AI realtime streaming.
- Sentiment/stress analytics.
- Auto-assign task không cần confirm.
