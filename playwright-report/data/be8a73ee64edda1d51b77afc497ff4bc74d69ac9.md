# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: ai-action-composer.spec.ts >> TEST-ACTION-E2E unified AI assistant shows real progress, editable review and execution receipt
- Location: tests\e2e\ai-action-composer.spec.ts:18:1

# Error details

```
Error: expect(locator).toBeVisible() failed

Locator: getByText(/Đã chạy \d+ giây/)
Expected: visible
Timeout: 10000ms
Error: element(s) not found

Call log:
  - Expect "toBeVisible" with timeout 10000ms
  - waiting for getByText(/Đã chạy \d+ giây/)

```

```yaml
- banner:
  - button "Mo tim kiem":
    - img
    - text: Tìm kiếm dự án, nhiệm vụ, hoặc thành viên...
  - button "Mở Trợ lý AI": Trợ lý AI
  - button "Chuyen sang giao dien toi"
  - button "Thong bao": "2"
  - button "QT Quản trị viên Qaly QUẢN LÝ DỰ ÁN":
    - text: QT
    - strong: Quản trị viên Qaly
    - text: QUẢN LÝ DỰ ÁN
- complementary:
  - text: Q
  - strong: QALY
  - text: Vận hành Toàn cầu
  - region "Hồ sơ người dùng":
    - img "Quản trị viên Qaly"
    - text: Admin
    - strong: Quản trị viên Qaly
  - navigation "Main navigation":
    - link "Thành viên tổ chức":
      - /url: /organizations/users
    - link "Tổng quan":
      - /url: /dashboard
    - link "Dự án":
      - /url: /projects
    - link "Nhiệm vụ":
      - /url: /tasks
    - link "Nhóm":
      - /url: /teams
    - link "Phân tích":
      - /url: /analytics
    - link "Quản lý tổ chức":
      - /url: /organizations
    - link "Ủy quyền Moderator":
      - /url: /admin/moderators
    - link "Quản lý người dùng":
      - /url: /admin/users
  - link "Dự án đã lưu trữ":
    - /url: /projects/archived
  - link "Cài đặt":
    - /url: /settings
- main:
  - heading "Chào mừng bạn quay trở lại!" [level=1]
  - paragraph: Dưới đây là tóm tắt hoạt động của không gian làm việc hôm nay.
  - button "Tạo Dự Án Mới"
  - region "Tổng quan dự án":
    - article:
      - text: DỰ ÁN ĐANG HOẠT ĐỘNG
      - strong: "5"
      - paragraph: 1 dự án mới tháng này
    - article:
      - text: NHIỆM VỤ CẦN LÀM
      - strong: "20"
      - paragraph: "Gần nhất: 23:32 12-08"
    - article:
      - text: CÔNG SUẤT ĐỘI NGŨ
      - strong: 37%
  - heading "Thống kê tiến độ Dự án" [level=3]
  - button "Biểu đồ 2D"
  - button "Biểu đồ 3D"
  - img: 100% 75% 50% 25% 0% 33% Ops & Co... 33% Field Op... 25% Erumi Lo... 20% Nova Ret... 25% Qaly Rel...
  - text: Đúng tiến độ
  - strong: 4 Dự án
  - text: Có rủi ro
  - strong: 1 Dự án
  - text: Chậm trễ
  - strong: 0 Dự án
  - heading "Tổng quan chiến lược" [level=2]
  - paragraph: AI phân tích dữ liệu dự án, nhiệm vụ và hiệu suất đội nhóm để đề xuất hướng triển khai tiếp theo.
  - button "Tạo phân tích AI"
  - text: Tiến trình chung
  - strong: 39%
  - text: Tỷ lệ hoàn thành task
  - strong: 29%
  - text: Dự án rủi ro / Nhiệm vụ trễ
  - strong: 5 / 1
  - text: Workload đội nhóm
  - strong: Medium
  - paragraph:
    - text: Nhấn
    - strong: Tạo phân tích AI
    - text: để nhận nhận định có metric/source grounding và có thể đọc lại sau reload.
  - complementary:
    - heading "Cần chú ý" [level=3]
    - img
    - text: 10 Vấn đề
    - list:
      - listitem:
        - text: Trễ hạn
        - strong: 1 nhiệm vụ
      - listitem:
        - text: Sắp đến hạn
        - strong: 4 nhiệm vụ
      - listitem:
        - text: Dự án rủi ro
        - strong: 5 dự án
    - button "Xem Tất cả đầu việc"
    - heading "Hoạt động gần đây" [level=3]
    - text: 7 ngày trước Hôm nay (4)
    - article:
      - text: HỆ
      - strong: Hệ thống
      - text: Login User 7 giờ trước
    - article:
      - text: HỆ
      - strong: Hệ thống
      - text: Login User 7 giờ trước
    - article:
      - text: HỆ
      - strong: Hệ thống
      - text: Login User 7 giờ trước
- button "Mở Trợ lý AI":
  - img "Erumi chatbot"
- dialog "Trợ lý AI":
  - separator "Thay đổi chiều rộng Trợ lý AI"
  - separator "Thay đổi chiều cao Trợ lý AI"
  - separator "Thay đổi kích thước Trợ lý AI"
  - banner:
    - img "Erumi chatbot"
    - text: Trợ lý AI Nói điều bạn cần · AI sẽ hỏi rõ hoặc chuẩn bị bản nháp
    - button "Trò chuyện"
    - button "Hoạt động AI"
    - button "Lịch sử phiên Trợ lý AI"
    - button "Đặt lại kích thước Trợ lý AI"
    - button "Đóng"
  - heading "Phân tích dự án" [level=1]
  - text: Phiên được lưu tự động
  - button "Phiên"
  - button "Tùy chọn cuộc trò chuyện"
  - img "Erumi chatbot"
  - paragraph: Xin chào! Mình là Erumi, trợ lý phân tích AI của Qaly. Hãy hỏi mình bất kỳ câu hỏi nào về các chỉ số hoặc dự án trong Workspace của bạn nhé.
  - text: Dữ liệu hệ thống
  - button "Tùy chọn phản hồi"
  - text: Tạo task frontend có tiêu chí nghiệm thu.
  - img "Erumi chatbot"
  - paragraph: Mình đã hiểu yêu cầu và sẽ soạn bản nháp để bạn duyệt.
  - group: Các bước Trợ lý AI đã thực hiện (2)
  - paragraph: Yêu cầu đã được định tuyến sang Task Action Composer. AI chỉ soạn option; bạn vẫn kiểm tra và xác nhận trước khi tạo task.
  - button "Mở phương án task"
  - text: Dữ liệu hệ thống Độ tin cậy 94%
  - button "Tùy chọn phản hồi"
  - textbox "Nhập yêu cầu tiếp theo cho Trợ lý AI":
    - /placeholder: Mô tả điều bạn muốn phân tích hoặc thực hiện... (gõ / để xem lệnh nhanh)
  - button "Mở chức năng"
  - text: Tất cả dự án
  - button "DeepSeek V4 Pro Cloud"
  - button "Gửi câu hỏi" [disabled]
```

# Test source

```ts
  138 |           activity(5, 'persist_receipt', 'succeeded', 'Đã lưu biên nhận'),
  139 |           activity(6, 'read_back', 'succeeded', 'Đã đọc lại kết quả'),
  140 |         ]
  141 |       : [
  142 |           activity(1, 'understand_intent', 'succeeded', 'Đã hiểu yêu cầu'),
  143 |           activity(2, 'route_model', 'running', 'Đang định tuyến DeepSeek V4 Pro'),
  144 |           activity(3, 'await_confirmation', 'waiting_user', 'Đang chờ bạn xác nhận'),
  145 |         ]
  146 |     await route.fulfill({
  147 |       contentType: 'application/json',
  148 |       body: JSON.stringify(envelope({
  149 |         jobId,
  150 |         jobStatus: confirmed ? 'succeeded' : (jobPolls > 1 ? 'succeeded' : 'running'),
  151 |         startedAt: new Date(Date.now() - 2_000).toISOString(),
  152 |         finishedAt: jobPolls > 1 ? new Date().toISOString() : null,
  153 |         lastSequence: confirmed ? 6 : 3,
  154 |         cancellable: jobPolls <= 1,
  155 |         events,
  156 |       })),
  157 |     })
  158 |   })
  159 | 
  160 |   const plan = () => ({
  161 |     schemaId: 'ai_action_intent_envelope.v1',
  162 |     schemaVersion: '1.0',
  163 |     projectId,
  164 |     sourceVersion: 'source-v1',
  165 |     userIntent: 'Tạo task frontend có tiêu chí nghiệm thu.',
  166 |     intentType: 'task.create',
  167 |     confidence: 0.92,
  168 |     targetEntities: [{ type: 'project', id: projectId, label: 'Demo project' }],
  169 |     assumptions: [],
  170 |     missingFields: [],
  171 |     warnings: [],
  172 |     options: [{
  173 |       optionId: 'balanced',
  174 |       label: 'Cân bằng',
  175 |       summary: 'Một task frontend có thể duyệt.',
  176 |       tradeOffs: ['Ưu tiên tốc độ triển khai.'],
  177 |       commands: [{
  178 |         commandId: 'task-1',
  179 |         toolName: 'task.create.v1',
  180 |         toolVersion: '1.0',
  181 |         title: 'AI draft task',
  182 |         description: 'Draft only.',
  183 |         acceptanceCriteria: ['UI hoạt động', 'Có test'],
  184 |         priority: 'High',
  185 |         dueDate: new Date(Date.now() + 86_400_000 * 7).toISOString(),
  186 |         estimatedHours: 8,
  187 |         assigneeId: null,
  188 |         assigneeMode: 'unassigned',
  189 |         requiredSkills: [],
  190 |         sourceRefs: [`/projects/${projectId}`],
  191 |       }],
  192 |     }],
  193 |     review: { selectedOptionId: 'balanced', selectedCommandIds: ['task-1'] },
  194 |     generatedAt: new Date().toISOString(),
  195 |   })
  196 | 
  197 |   await page.route(`**/api/ai/jobs/${jobId}/result`, async route => {
  198 |     await route.fulfill({
  199 |       contentType: 'application/json',
  200 |       body: JSON.stringify(envelope({ result: plan(), draftIds: [draftId], sourceStale: false })),
  201 |     })
  202 |   })
  203 |   await page.route(`**/api/ai/drafts/${draftId}`, async route => {
  204 |     const actionReceipt = confirmed ? receipt(projectId, taskId) : null
  205 |     await route.fulfill({
  206 |       contentType: 'application/json',
  207 |       body: JSON.stringify(envelope({
  208 |         draftId,
  209 |         status: confirmed ? 'confirmed' : 'pending_review',
  210 |         workingPayload: plan(),
  211 |         rowVersion: 'AQID',
  212 |         confirmationResult: confirmed
  213 |           ? { status: 'confirmed', createdTaskCount: 1, actionReceipt }
  214 |           : null,
  215 |       })),
  216 |     })
  217 |   })
  218 |   await page.route(`**/api/ai/drafts/${draftId}/confirm`, async route => {
  219 |     confirmed = true
  220 |     const edited = JSON.parse(route.request().postDataJSON().editedPayloadJson)
  221 |     expect(edited.options[0].commands[0].title).toBe('Reviewed task from browser')
  222 |     await route.fulfill({
  223 |       contentType: 'application/json',
  224 |       body: JSON.stringify(envelope({
  225 |         status: 'confirmed',
  226 |         createdTaskCount: 1,
  227 |         actionReceipt: receipt(projectId, taskId),
  228 |       })),
  229 |     })
  230 |   })
  231 | 
  232 |   await page.getByRole('button', { name: /Mở Trợ lý AI/i }).first().click()
  233 |   await expect(page.getByText('Bạn muốn Qaly giúp gì?')).toBeVisible()
  234 |   await expect(page.getByText('Bạn muốn Qaly chuẩn bị việc gì?')).toHaveCount(0)
  235 |   await page.getByLabel('Nhập yêu cầu cho Trợ lý AI').fill('Tạo task frontend có tiêu chí nghiệm thu.')
  236 |   await page.getByRole('button', { name: 'Gửi câu hỏi' }).click()
  237 | 
> 238 |   await expect(page.getByText(/Đã chạy \d+ giây/)).toBeVisible()
      |                                                    ^ Error: expect(locator).toBeVisible() failed
  239 |   await expect(page.getByText('Đang định tuyến DeepSeek V4 Pro')).toBeVisible()
  240 |   await expect(page.getByText('Đã soạn xong — chưa thay đổi dữ liệu')).toBeVisible({ timeout: 10_000 })
  241 | 
  242 |   const artifactSplitter = page.getByRole('separator', { name: 'Thay đổi độ rộng hội thoại và bản nháp' })
  243 |   await expect(artifactSplitter).toBeVisible()
  244 |   const initialConversationRatio = Number(await artifactSplitter.getAttribute('aria-valuenow'))
  245 |   await artifactSplitter.press('ArrowRight')
  246 |   await expect(artifactSplitter).toHaveAttribute('aria-valuenow', String(initialConversationRatio + 3))
  247 |   await page.getByRole('button', { name: 'Thu gọn bản nháp AI' }).click()
  248 |   await expect(page.locator('.assistant-artifact-pane')).toHaveCount(0)
  249 |   await page.getByRole('button', { name: 'Mở bản nháp AI' }).click()
  250 |   await expect(page.locator('.assistant-artifact-pane')).toBeVisible()
  251 | 
  252 |   await page.getByLabel('Tiêu đề').fill('Reviewed task from browser')
  253 |   await page.getByRole('button', { name: 'Xác nhận và tạo task' }).click()
  254 | 
  255 |   await expect(page.getByText('Đã thực hiện sau khi bạn xác nhận')).toBeVisible()
  256 |   await expect(page.getByText('Reviewed task from browser')).toBeVisible()
  257 |   await expect(page.getByText('Đã đọc lại kết quả')).toBeVisible()
  258 | 
  259 |   await page.reload({ waitUntil: 'domcontentloaded' })
  260 |   await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
  261 |   await page.getByRole('button', { name: /Mở Trợ lý AI/i }).first().click()
  262 |   await page.getByRole('button', { name: 'Mở bản nháp AI' }).click()
  263 |   await expect(page.getByText('Đã thực hiện sau khi bạn xác nhận')).toBeVisible({ timeout: 10_000 })
  264 |   await expect(page.locator('.receipt-meta').getByText('DeepSeek · deepseek-v4-pro')).toBeVisible()
  265 |   await expect(page.getByText('Reviewed task from browser')).toBeVisible()
  266 | })
  267 | 
  268 | test('TEST-ACTION-LIVE-WORKER-01 live worker claims Action Composer job and persists a reviewable draft', async ({ page }) => {
  269 |   test.setTimeout(70_000)
  270 |   await login(page)
  271 |   const projectsResponse = await page.request.get('/api/projects?page=1&pageSize=20')
  272 |   expect(projectsResponse.ok()).toBeTruthy()
  273 |   const projectsPayload = await projectsResponse.json()
  274 |   const project = (projectsPayload.data?.items ?? projectsPayload.items ?? [])
  275 |     .find((item: { status?: string }) => item.status !== 'Archived')
  276 |   expect(project?.id).toBeTruthy()
  277 | 
  278 |   const csrfResponse = await page.request.get('/api/security/csrf')
  279 |   expect(csrfResponse.ok()).toBeTruthy()
  280 |   const csrf = await csrfResponse.json()
  281 |   const composeResponse = await page.request.post('/api/ai/actions/compose', {
  282 |     headers: {
  283 |       'X-CSRF-TOKEN': csrf.token,
  284 |       'Idempotency-Key': `live-action-compose-${Date.now()}`,
  285 |     },
  286 |     data: {
  287 |       message: 'Soạn hai task chi tiết cho Sprint 1: hoàn thiện API dịch vụ và kiểm thử luồng thanh toán. Chỉ tạo bản nháp để duyệt.',
  288 |       context: {
  289 |         route: `/projects/${project.id}`,
  290 |         module: 'project_tasks',
  291 |         projectId: project.id,
  292 |         entityType: 'project',
  293 |         entityId: project.id,
  294 |       },
  295 |       language: 'vi',
  296 |       modelProfile: 'action_composer_strong',
  297 |       maximumOptions: 2,
  298 |       maximumEstimatedCostUsd: 0.08,
  299 |       cacheMode: 'bypass',
  300 |     },
  301 |   })
  302 |   expect(composeResponse.status()).toBe(202)
  303 |   const createdPayload = await composeResponse.json()
  304 |   const created = createdPayload.data ?? createdPayload
  305 |   expect(created.jobId).toBeTruthy()
  306 | 
  307 |   async function jobDetail() {
  308 |     const response = await page.request.get(`/api/ai/jobs/${created.jobId}`)
  309 |     expect(response.ok()).toBeTruthy()
  310 |     const payload = await response.json()
  311 |     return payload.data ?? payload
  312 |   }
  313 | 
  314 |   await expect.poll(async () => (await jobDetail()).status, {
  315 |     message: 'Worker phải claim job tương tác thay vì để queued vô hạn',
  316 |     timeout: 15_000,
  317 |     intervals: [250, 500, 1_000],
  318 |   }).not.toBe('queued')
  319 | 
  320 |   await expect.poll(async () => (await jobDetail()).status, {
  321 |     message: 'Action Composer phải tạo xong bản nháp hoặc trả trạng thái terminal rõ ràng',
  322 |     timeout: 50_000,
  323 |     intervals: [1_000, 2_000, 3_000],
  324 |   }).toBe('succeeded')
  325 | 
  326 |   const completed = await jobDetail()
  327 |   expect(completed.draftIds?.length).toBeGreaterThan(0)
  328 |   expect(completed.selectedProvider).toBeTruthy()
  329 |   expect(completed.selectedModel).toBeTruthy()
  330 |   const resultResponse = await page.request.get(`/api/ai/jobs/${created.jobId}/result`)
  331 |   expect(resultResponse.ok()).toBeTruthy()
  332 |   const resultPayload = await resultResponse.json()
  333 |   const result = resultPayload.data ?? resultPayload
  334 |   expect(result.draftIds?.length).toBeGreaterThan(0)
  335 |   expect(result.result?.options?.length).toBeGreaterThan(0)
  336 | })
  337 | 
  338 | test('TEST-UA-E2E chat function call opens the canonical task composer without mutating data', async ({ page }) => {
```