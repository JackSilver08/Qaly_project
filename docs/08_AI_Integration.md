# 🤖 QALY PROJECT – KẾ HOẠCH TÍCH HỢP AI

> **Ngày tạo:** 01/05/2026  
> **Phiên bản:** 1.0  
> **Trạng thái:** Planning

---

## I. TẦM NHÌN

Qaly không chỉ là tool quản lý dự án thông thường. 
**Mục tiêu**: Biến Qaly thành **AI-powered Project Management System** – nơi AI là "thành viên thứ 8" của team.

### Khác biệt so với Jira/Trello/Notion
| Feature | Jira/Trello | Qaly + AI |
|---|---|---|
| Task Assignment | Manual | AI đề xuất dựa trên workload & skill |
| Priority | Người dùng set | AI phân tích context và suggest |
| Risk Detection | Không có | AI cảnh báo sớm khi project có vấn đề |
| Search | Text match | Semantic search (tìm theo ý nghĩa) |
| Summary | Không có | AI tự tạo báo cáo tiến độ |
| Subtask Creation | Manual | AI tự phân rã task lớn |
| Chat | Không có | Hỏi AI về project: "task nào cần làm gấp?" |

---

## II. KIẾN TRÚC AI

```
┌──────────────────────────────────────────────────┐
│                  Qaly Web (UI)                    │
│  ┌─────────┐ ┌──────────┐ ┌──────────────────┐   │
│  │Dashboard│ │Task Board│ │ AI Chat Widget   │   │
│  │+Insight │ │+AI Badge │ │ (Floating)       │   │
│  └────┬────┘ └────┬─────┘ └────────┬─────────┘   │
├───────┴──────────┴──────────────┴─────────────┤
│              Application Layer                    │
│  ┌──────────────────────────────────────────┐     │
│  │             IAiService                    │     │
│  │  - SuggestTaskPriority()                  │     │
│  │  - GenerateProjectSummary()               │     │
│  │  - AnalyzeProjectRisks()                  │     │
│  │  - SuggestTaskAssignment()                │     │
│  │  - SmartSearch()                          │     │
│  │  - GenerateSubtasks()                     │     │
│  │  - Chat()                                 │     │
│  └──────────────┬───────────────────────────┘     │
├─────────────────┴─────────────────────────────┤
│           Infrastructure Layer                    │
│  ┌────────────────────────────────────────┐       │
│  │        AiService Implementation        │       │
│  │                                        │       │
│  │  ┌─────────┐ ┌──────┐ ┌───────────┐   │       │
│  │  │ OpenAI  │ │Gemini│ │Local Model│   │       │
│  │  │ GPT-4o  │ │  2.0 │ │ Ollama    │   │       │
│  │  └────┬────┘ └──┬───┘ └─────┬─────┘   │       │
│  │       └─────────┴───────────┘          │       │
│  │         AI Provider Factory            │       │
│  └────────────────────────────────────────┘       │
└───────────────────────────────────────────────────┘
```

---

## III. 7 ĐIỂM TÍCH HỢP AI

### 3.1 🎯 Smart Priority Suggestion
**Khi nào:** User tạo task mới  
**AI làm gì:** Phân tích title + description + context project → đề xuất Priority (Low/Medium/High/Critical)  
**UI:** Badge gợi ý bên cạnh dropdown Priority  
**Prompt mẫu:**
```
Dựa trên context project "{projectName}" với các tasks hiện tại: {taskList}.
Task mới: "{taskTitle}" - "{taskDescription}".
Đề xuất priority (Low/Medium/High/Critical) và giải thích ngắn gọn.
```

### 3.2 📊 Project Summary Generator
**Khi nào:** Dashboard load, hoặc user click "AI Summary"  
**AI làm gì:** Tổng hợp tasks → tạo báo cáo tiến độ bằng ngôn ngữ tự nhiên  
**UI:** Card trên Dashboard  
**Output mẫu:**
> "Dự án Qaly MVP đang ở tuần 3/12. Hoàn thành 40% tasks (4/10). 2 task overdue cần chú ý. Workload phân bổ không đều: Nguyễn Văn A đang quá tải (8 tasks), Trần Thị B chỉ có 2 tasks."

### 3.3 ⚠️ Risk Analysis Engine
**Khi nào:** Mỗi ngày (background job) hoặc on-demand  
**AI làm gì:** Phân tích dữ liệu → phát hiện rủi ro:
- Tasks overdue
- Workload imbalance
- Bottleneck (nhiều task InReview)
- Velocity giảm  
**UI:** Alert banner trên Dashboard + Notification  

### 3.4 👤 Smart Task Assignment
**Khi nào:** Task chưa có assignee  
**AI làm gì:** Phân tích skill (từ lịch sử tasks), workload hiện tại → đề xuất người phù hợp  
**UI:** Dropdown với icon ⭐ bên cạnh tên người được AI suggest  

### 3.5 🔍 Semantic Search
**Khi nào:** User tìm kiếm  
**AI làm gì:** Tìm theo ý nghĩa, không chỉ text match  
**Ví dụ:** Search "authentication bug" → tìm được task "Lỗi đăng nhập khi dùng Google OAuth"  
**Tech:** Embeddings + Vector similarity (có thể dùng pgvector hoặc Azure AI Search)

### 3.6 📝 Auto Subtask Generation
**Khi nào:** User tạo task có description dài  
**AI làm gì:** Phân tách thành các subtask nhỏ hơn  
**Ví dụ:**  
- Input: "Implement user authentication module"  
- Output: ["Setup ASP.NET Identity", "Create Login page", "Create Register page", "Add JWT token", "Setup middleware", "Write unit tests"]  

### 3.7 💬 AI Chat Assistant (Qaly Bot)
**Khi nào:** User mở chat widget (góc phải dưới)  
**AI làm gì:** Trả lời câu hỏi về project:
- "Có bao nhiêu task overdue?"
- "Ai đang rảnh nhất?"
- "Tổng hợp công việc tuần này"
- "Tạo task mới: Fix login bug"

---

## IV. LỘ TRÌNH TRIỂN KHAI

### Phase 1: Foundation (Tuần 1-2) ← **ĐANG Ở ĐÂY**
- [x] `IAiService` interface đã tạo
- [ ] Cài đặt NuGet: `Azure.AI.OpenAI` hoặc `Google.Cloud.AIPlatform`
- [ ] Tạo `AiService` implementation (OpenAI GPT-4o)
- [ ] Tạo `AiSettings` trong appsettings.json
- [ ] Rate limiting + caching cho AI calls

### Phase 2: Smart Features (Tuần 3-4)
- [ ] Priority Suggestion (khi tạo task)
- [ ] Project Summary (Dashboard card)
- [ ] Subtask Generation

### Phase 3: Intelligence (Tuần 5-6)
- [ ] Risk Analysis (background job)
- [ ] Smart Assignment
- [ ] Semantic Search (embeddings)

### Phase 4: Chat & Polish (Tuần 7-8)
- [ ] AI Chat Widget
- [ ] Prompt tuning & optimization
- [ ] Usage analytics & feedback loop

---

## V. CONFIGURATION

### appsettings.json structure
```json
{
  "AiSettings": {
    "Provider": "OpenAI",
    "ApiKey": "",
    "Model": "gpt-4o",
    "MaxTokens": 2000,
    "Temperature": 0.7,
    "EnableSmartPriority": true,
    "EnableRiskAnalysis": true,
    "EnableSmartAssignment": true,
    "EnableSemanticSearch": false,
    "EnableChat": true,
    "CacheDurationMinutes": 30,
    "MaxRequestsPerMinute": 30
  }
}
```

### Fallback Strategy
```
Primary:   OpenAI GPT-4o (cloud)
Fallback:  Gemini 2.0 (cloud)
Offline:   Ollama + llama3 (local, optional)
No AI:     Graceful degradation - tất cả features vẫn hoạt động, chỉ thiếu suggestions
```

---

## VI. CHI PHÍ DỰ KIẾN

| Provider | Model | Cost / 1K tokens | Monthly estimate (team 7) |
|---|---|---|---|
| OpenAI | GPT-4o | $5 / 1M input | ~$15-30/tháng |
| Google | Gemini 2.0 | Free tier / $3.50 / 1M | ~$10-20/tháng |
| Local | Ollama | $0 (chỉ tốn GPU) | $0 |

---

## VII. BẢO MẬT AI

1. **KHÔNG gửi sensitive data** (password, email thật) lên AI provider
2. **Sanitize** prompt inputs trước khi gửi
3. **API key** lưu trong secrets, KHÔNG hardcode
4. **Rate limiting** chống abuse
5. **Audit log** mọi AI interaction
6. **Opt-in**: User phải đồng ý trước khi AI truy cập project data
