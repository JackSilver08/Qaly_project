# QALY Workspace — AI Module Specification v2.0

**Phiên bản:** v2.0 — Production/Japan-style specification  
**Ngày:** 16/05/2026  
**Phạm vi:** Dự án web quản lý dự án phần mềm cho doanh nghiệp outsource vừa và nhỏ.  
**Định hướng:** Jira + Notion + Mini Zalo + AI Assistant + Document Mining.  
**Ghi chú kiến trúc:** Tài liệu v2.0 mở rộng từ SRS/TKHT v1.0. Các phần dưới đây là target design để nhóm có thể triển khai 80–90% chức năng web trong 10–11 tuần, không bắt buộc implement toàn bộ bảng P2 nếu thiếu thời gian.

---


# MODULE 1 — QALY Assistant

## 1.1 Architecture Overview

```text
QALY Web/API -> AiApplicationService -> IAiProvider
                                 ├-> LocalOllamaProvider
                                 ├-> OpenAICompatibleProvider (optional, quota)
                                 └-> MockProvider (test/demo fallback)

Domain Events/Outbox -> VectorSyncWorker -> Chunker -> EmbeddingProvider -> Qdrant
User Question -> PermissionFilter -> Retriever -> Reranker -> PromptBuilder -> LLM -> CitationFormatter
```

## 1.2 RAG Pipeline

### Data sources
- Project: projects, project_settings, project_status_reports, project_decision_logs.
- Task: tasks, task_comments, evidence, approval, dependency, time logs.
- Wiki: wiki_pages, wiki_page_versions, wiki_page_contents.
- Chat/Meeting: chat_messages public/internal theo quyền, meeting_notes, meeting_ai_summaries.
- Import: document_extracted_sections, document_draft_tasks đã approve.

### Chunking strategy
| Content type | Chunk size | Overlap | Metadata bắt buộc |
|---|---:|---:|---|
| Wiki page | 800–1200 tokens | 120 | tenant_id, project_id, page_id, visibility, version |
| Task detail | 300–600 tokens | 50 | task_id, status, assignee_ids, visibility |
| Comment/chat | 200–400 tokens | 50 | room_id/task_id, sender_id, visibility, created_at |
| Meeting transcript | 800 tokens | 100 | meeting_id, participant_ids, section |
| Imported document | theo heading/section | 100 | source_file_id, section_id, confidence |

### Qdrant metadata schema
```json
{
  "tenant_id": "uuid",
  "organization_id": "uuid",
  "project_id": "uuid",
  "source_entity": "task|wiki|chat|meeting|file",
  "source_id": "uuid",
  "visibility": "public|customer_safe|internal|private",
  "allowed_user_ids": ["uuid"],
  "allowed_role_codes": ["project_manager", "reviewer"],
  "content_hash": "sha256",
  "updated_at": "timestamp"
}
```

### Permission filtering
1. Resolve current user tenant/org/project roles.
2. Build allowed projects and visibility scope.
3. Add Qdrant filter: tenant_id, project_id IN allowed, visibility allowed.
4. For private items, require allowed_user_ids contains current user or elevated PM/admin role.
5. Re-check permission after retrieval before prompt injection.

### Generation rules
- Không trả lời nếu không có context đủ tin cậy; hỏi lại hoặc nói không tìm thấy.
- Mọi câu trả lời tiến độ phải cite project/task/wiki/meeting source.
- Không đưa nội dung internal/private cho customer.
- AI output quan trọng là draft, không tự ghi DB nếu user chưa confirm.

## 1.3 Prompt Templates

### Hỏi tiến độ project
```text
SYSTEM: Bạn là QALY Assistant. Chỉ dùng context được cung cấp. Không suy đoán dữ liệu ngoài quyền. Trả lời tiếng Việt, ngắn gọn, có nguồn.
USER: Người dùng {user_name} hỏi tiến độ dự án {project_name}. Context: {retrieved_context}. Hãy tóm tắt: % hoàn thành, task done/in progress/overdue, blocker, rủi ro, đề xuất bước tiếp theo.
EXPECTED OUTPUT: Markdown gồm Summary, Progress, Risks, Next Actions, Sources.
```

### Sinh báo cáo tuần
```text
SYSTEM: Bạn là PM assistant theo phong cách báo cáo Nhật: rõ số liệu, vấn đề, nguyên nhân, đối sách.
USER: Sinh báo cáo tuần từ {start_date} đến {end_date} cho project {project_name}. Context: {context}. 
EXPECTED OUTPUT: 1) Tổng quan 2) Hoàn thành 3) Đang làm 4) Quá hạn/blocker 5) Rủi ro 6) Kế hoạch tuần sau 7) Nguồn.
```

### Gợi ý assign task
```text
SYSTEM: Bạn chỉ gợi ý, không tự assign. Cân nhắc skill, workload, deadline, role và quyền.
USER: Task: {task}. Members: {member_workload}. Hãy gợi ý 1-3 assignee với lý do và rủi ro.
EXPECTED OUTPUT: JSON [{user_id, reason, risk, confidence}]
```

### Tóm tắt meeting
```text
SYSTEM: Trích xuất theo format biên bản: quyết định, action item, deadline, owner, câu hỏi mở. Không tạo task thật.
USER: Transcript: {transcript}. Participants: {participants}. Project: {project}.
EXPECTED OUTPUT: JSON {summary, decisions[], action_items[], risks[], questions[]}.
```

## 1.4 Queue jobs
| Job | Payload | Retry | Output |
|---|---|---:|---|
| vector.sync.project | project_id | 3 | ai_knowledge_sync_jobs |
| vector.sync.entity | entity_type, entity_id | 3 | chunks upsert |
| ai.generate_report | project_id, date_range | 1 | report draft |
| ai.summarize_meeting | meeting_id | 2 | meeting_ai_summaries |

## 1.5 Performance & Cost
- Local query mục tiêu: 3–15 giây tùy máy; dùng streaming để UX không đợi trắng.
- Cache: ai_response_cache theo hash(question+context_version+permission_scope) TTL 1–24h.
- Quota: per user/day, per project/day, per tenant/month.
- API ngoài: chỉ bật bằng feature flag và budget; nếu hết quota, fallback local/mock.

## 1.6 Evaluation
| Metric | Mục tiêu |
|---|---|
| Faithfulness | Không bịa ngoài context |
| Permission safety | 0 case leak internal/private |
| Citation coverage | >90% câu trả lời nghiệp vụ có source |
| Latency | P95 local dưới 20s cho câu dài; streaming dưới 2s có token đầu |

# MODULE 2 — Document Miner

## 2.1 Input formats
PDF text/scanned, DOCX, Markdown, CSV/Excel requirement matrix, plain text. Scanned PDF cần OCR/MinerU; nếu không có GPU, fallback upload Markdown/CSV để demo ổn định.

## 2.2 Processing pipeline
```text
Upload -> Validate -> Virus scan -> Extract text/layout -> Section detection -> AI classify -> Draft task/wiki -> Human review -> Commit import -> Audit/rollback
```

### Step 1 — Validation
- Allowlist: pdf, docx, md, txt, csv, xlsx, png/jpg nếu OCR.
- Max size: default 50MB per file, configurable theo tenant.
- Check MIME + extension + checksum.

### Step 2 — Extraction
- Primary: MinerU service adapter.
- Fallback: PyMuPDF/pdfplumber, python-docx, pandas/openpyxl, Markdown parser.
- Preserve heading, table, image placeholder, page number, section source.

### Step 3 — Classification prompt
```text
SYSTEM: Bạn là BA phân tích tài liệu yêu cầu. Hãy trích xuất Requirement, Task, Acceptance Criteria, Deadline, Assignee hint, Risk. Không tự thêm thông tin không có trong tài liệu.
USER: Section markdown: {section_md}
OUTPUT JSON: {requirements:[...], tasks:[...], risks:[...], questions:[...]}
```

### Step 4 — Draft schema
```json
{
  "draft_id": "uuid",
  "source_section_id": "uuid",
  "title": "string",
  "description_md": "string",
  "acceptance_criteria": ["string"],
  "priority": "low|medium|high|critical",
  "assignee_hint": "string|null",
  "due_date_hint": "date|null",
  "confidence": 0.0,
  "review_status": "pending|approved|rejected"
}
```

### Step 5 — Import execution
- Transaction theo batch đã approved.
- Tạo tasks/wiki_pages và link source_document_id.
- Nếu lỗi, rollback batch và ghi document_import_audit_logs.
- Sau import, enqueue vector sync.
