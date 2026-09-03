<script setup lang="ts">
import { ref, watch } from 'vue'
import { Webhook, Trash2, Plus, Activity, ShieldCheck, RefreshCw, AlertTriangle } from 'lucide-vue-next'
import { apiCommand, apiResult, errorMessage } from '../utils/api-client'
import { showError, showSuccess } from '../composables/use-toast'
import { confirmDialog } from '../composables/use-confirm-dialog'

const props = defineProps<{
  projectId: string
}>()

interface WebhookDto {
  id: string
  projectId: string
  payloadUrl: string
  events: string[]
  hasSecret: boolean
  isActive: boolean
  createdAt: string
}

interface WebhookTestResult {
  deliveryStatus: string
  isDelivered: boolean
  attemptCount: number
  responseStatusCode: number | null
  deliveryLogId: string | null
}

interface WebhookOutboxItem {
  id: string
  projectId: string
  eventType: string
  status: 'pending' | 'retry_scheduled' | 'delivering' | 'dead_letter' | 'delivered'
  retryCount: number
  createdAt: string
  nextAttemptAt: string
  deadLetteredAt: string | null
  errorSummary: string | null
}

interface WebhookDeliveryLog {
  id: string
  webhookId: string
  eventType: string
  isSuccess: boolean
  attemptCount: number
  responseStatusCode: number | null
  durationMs: number
  createdAt: string
}

interface WebhookOperations {
  projectId: string
  pendingCount: number
  deadLetterCount: number
  recentOutbox: WebhookOutboxItem[]
  recentDeliveries: WebhookDeliveryLog[]
}

interface WebhookReplayResult {
  item: WebhookOutboxItem
  replayQueued: boolean
}

const webhooks = ref<WebhookDto[]>([])
const showCreateForm = ref(false)
const isLoading = ref(false)
const isFetching = ref(false)
const testingWebhookId = ref<string | null>(null)
const lastTest = ref<Record<string, WebhookTestResult>>({})
const operations = ref<WebhookOperations | null>(null)
const isFetchingOperations = ref(false)
const replayingOutboxId = ref<string | null>(null)

const newWebhook = ref({
  payloadUrl: '',
  secret: '',
  events: ['task.created', 'task.updated']
})

const availableEvents = [
  { id: 'task.created', label: 'Đã tạo nhiệm vụ' },
  { id: 'task.updated', label: 'Đã cập nhật nhiệm vụ' },
  { id: 'task.deleted', label: 'Đã xóa nhiệm vụ' },
  { id: '*', label: 'Tất cả event task hiện được hỗ trợ' }
]

async function fetchWebhooks() {
  isFetching.value = true
  try {
    webhooks.value = await apiResult<WebhookDto[]>(`/api/projects/${props.projectId}/webhooks`)
  } catch (e) {
    showError(errorMessage(e, 'Không thể tải danh sách webhook.'))
    webhooks.value = []
  } finally {
    isFetching.value = false
  }
}

async function fetchOperations() {
  isFetchingOperations.value = true
  try {
    operations.value = await apiResult<WebhookOperations>(`/api/projects/${props.projectId}/webhooks/operations`)
  } catch (e) {
    operations.value = null
    showError(errorMessage(e, 'Không thể tải trạng thái giao webhook.'))
  } finally {
    isFetchingOperations.value = false
  }
}

async function refreshAll() {
  await Promise.all([fetchWebhooks(), fetchOperations()])
}

async function createWebhook() {
  if (isLoading.value) return
  isLoading.value = true

  try {
    await apiResult(`/api/projects/${props.projectId}/webhooks`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(newWebhook.value)
    })

    showCreateForm.value = false
    newWebhook.value = { payloadUrl: '', secret: '', events: ['task.created', 'task.updated'] }
    await refreshAll()
    showSuccess('Đã tạo webhook thành công.')
  } catch (e) {
    showError(errorMessage(e, 'Không thể tạo webhook.'))
  } finally {
    isLoading.value = false
  }
}

async function deleteWebhook(id: string) {
  const webhook = webhooks.value.find(item => item.id === id)
  if (!await confirmDialog({ tone:'danger', title:'Xóa webhook?', subject:webhook?.payloadUrl, message:'Các sự kiện mới sẽ không còn được gửi đến endpoint này.', confirmLabel:'Xóa webhook' })) return

  try {
    await apiCommand(`/api/projects/${props.projectId}/webhooks/${id}`, { method: 'DELETE' })

    await refreshAll()
    showSuccess('Đã xóa webhook.')
  } catch (e) {
    showError(errorMessage(e, 'Không thể xóa webhook.'))
  }
}

async function testWebhook(id: string) {
  if (testingWebhookId.value) return
  testingWebhookId.value = id
  try {
    const receipt = await apiResult<WebhookTestResult>(`/api/projects/${props.projectId}/webhooks/${id}/test`, { method: 'POST' })
    lastTest.value[id] = receipt

    showSuccess(`Endpoint đã xác nhận HTTP ${receipt.responseStatusCode ?? 200} sau ${receipt.attemptCount} lần gửi.`)
  } catch (e) {
    showError(errorMessage(e, 'Không thể gửi webhook test.'))
  } finally {
    testingWebhookId.value = null
  }
}

async function replayDeadLetter(item: WebhookOutboxItem) {
  if (replayingOutboxId.value) return
  const confirmed = await confirmDialog({
    tone: 'warning',
    title: 'Gửi lại webhook lỗi?',
    subject: item.eventType,
    message: 'Qaly sẽ đưa đúng occurrence này về hàng đợi. Idempotency key cũ vẫn được giữ để endpoint không nhận trùng.',
    confirmLabel: 'Đưa vào hàng đợi'
  })
  if (!confirmed) return

  replayingOutboxId.value = item.id
  try {
    const result = await apiResult<WebhookReplayResult>(
      `/api/projects/${props.projectId}/webhooks/outbox/${item.id}/replay`,
      { method: 'POST' }
    )
    await fetchOperations()
    showSuccess(result.replayQueued
      ? 'Đã đưa occurrence lỗi vào hàng đợi và lưu audit.'
      : 'Occurrence này đã nằm trong hàng đợi; không tạo thêm bản sao.')
  } catch (e) {
    showError(errorMessage(e, 'Không thể đưa webhook vào hàng đợi.'))
  } finally {
    replayingOutboxId.value = null
  }
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'short',
    timeStyle: 'short'
  }).format(new Date(value))
}

function outboxStatusLabel(status: WebhookOutboxItem['status']) {
  return {
    pending: 'Đang chờ',
    retry_scheduled: 'Chờ thử lại',
    delivering: 'Đang gửi',
    dead_letter: 'Cần xử lý',
    delivered: 'Đã giao'
  }[status]
}

watch(() => props.projectId, refreshAll, { immediate: true })
</script>

<template>
  <div class="webhooks-manager glass-card reveal">
    <div class="panel-header">
      <div class="title-group">
        <Webhook :size="20" class="icon-primary" />
        <h3>Webhook</h3>
      </div>
      <button class="primary-button primary-button--compact" type="button" @click="showCreateForm = !showCreateForm">
        <Plus :size="16" /> Thêm webhook
      </button>
    </div>

    <transition name="expand">
      <form v-if="showCreateForm" class="webhook-form glass-card" @submit.prevent="createWebhook">
        <div class="form-group">
          <label>URL nhận dữ liệu</label>
          <input v-model="newWebhook.payloadUrl" type="url" autocomplete="url" aria-label="URL nhận dữ liệu webhook" placeholder="https://your-app.com/webhook" required />
        </div>
        <div class="form-group">
          <label>Khóa bí mật (không bắt buộc)</label>
          <input v-model="newWebhook.secret" type="password" autocomplete="new-password" aria-label="Khóa bí mật webhook" placeholder="Khóa bí mật để xác thực webhook" />
        </div>
        <div class="form-group">
          <label>Sự kiện kích hoạt</label>
          <div class="events-grid">
            <label v-for="event in availableEvents" :key="event.id" class="event-checkbox">
              <input type="checkbox" :value="event.id" v-model="newWebhook.events" />
              <span>{{ event.label }}</span>
            </label>
          </div>
        </div>
        <div class="form-actions">
          <button type="button" class="ghost-button" @click="showCreateForm = false">Hủy</button>
          <button type="submit" class="primary-button" :disabled="isLoading">
            {{ isLoading ? 'Đang tạo...' : 'Tạo webhook' }}
          </button>
        </div>
      </form>
    </transition>

    <div class="webhooks-list">
      <div v-for="hook in webhooks" :key="hook.id" class="webhook-item glass-card">
        <div class="hook-main">
          <div class="hook-url">
            <strong>{{ hook.payloadUrl }}</strong>
            <span v-if="hook.hasSecret" class="secure-badge"><ShieldCheck :size="12" /> Đã bảo mật</span>
          </div>
          <div class="hook-events">
            <span v-for="ev in hook.events" :key="ev" class="event-tag">{{ ev }}</span>
          </div>
          <div v-if="lastTest[hook.id]" class="test-receipt" role="status">
            Đã xác nhận · HTTP {{ lastTest[hook.id].responseStatusCode ?? '2xx' }} · {{ lastTest[hook.id].attemptCount }} lần gửi · log {{ lastTest[hook.id].deliveryLogId?.slice(0, 8) }}
          </div>
        </div>
        <div class="hook-actions">
          <button type="button" @click="testWebhook(hook.id)" class="icon-button" title="Kiểm tra kết nối" :disabled="testingWebhookId === hook.id">
            <Activity :size="16" />
          </button>
          <button type="button" @click="deleteWebhook(hook.id)" class="revoke-button" title="Xóa">
            <Trash2 :size="16" />
          </button>
        </div>
      </div>
      <div v-if="isFetching" class="empty-state">Đang tải danh sách webhook...</div>
      <div v-else-if="webhooks.length === 0" class="empty-state">Chưa có webhook nào được cấu hình cho dự án này.</div>
    </div>

    <section class="operations-panel" aria-labelledby="webhook-operations-title">
      <div class="operations-header">
        <div>
          <h4 id="webhook-operations-title">Vận hành giao sự kiện</h4>
          <p>Hiển thị dữ liệu canonical của outbox; nút gửi lại chỉ mở cho occurrence đã dead-letter.</p>
        </div>
        <button type="button" class="ghost-button operations-refresh" :disabled="isFetchingOperations" @click="fetchOperations">
          <RefreshCw :size="15" :class="{ spinning: isFetchingOperations }" /> Làm mới
        </button>
      </div>

      <div v-if="operations" class="operations-summary" aria-live="polite">
        <span><strong>{{ operations.pendingCount }}</strong> đang chờ</span>
        <span :class="{ 'summary-danger': operations.deadLetterCount > 0 }">
          <AlertTriangle :size="14" /> <strong>{{ operations.deadLetterCount }}</strong> cần xử lý
        </span>
      </div>

      <div v-if="isFetchingOperations && !operations" class="empty-state">Đang tải trạng thái giao sự kiện...</div>
      <div v-else-if="operations && operations.recentOutbox.length" class="outbox-list">
        <article v-for="item in operations.recentOutbox" :key="item.id" class="outbox-row" :class="`outbox-row--${item.status}`">
          <div class="outbox-main">
            <div class="outbox-title">
              <strong>{{ item.eventType }}</strong>
              <span class="outbox-status">{{ outboxStatusLabel(item.status) }}</span>
            </div>
            <p>{{ formatDate(item.createdAt) }} · thử lại {{ item.retryCount }} lần · occurrence {{ item.id.slice(0, 8) }}</p>
            <p v-if="item.errorSummary" class="outbox-error">{{ item.errorSummary }}</p>
          </div>
          <button
            v-if="item.status === 'dead_letter'"
            type="button"
            class="ghost-button"
            :disabled="replayingOutboxId === item.id"
            @click="replayDeadLetter(item)"
          >
            {{ replayingOutboxId === item.id ? 'Đang xếp lại...' : 'Gửi lại occurrence' }}
          </button>
        </article>
      </div>
      <div v-else-if="operations" class="empty-state">Chưa có occurrence webhook nào trong dự án này.</div>

      <details v-if="operations?.recentDeliveries.length" class="delivery-history">
        <summary>Lịch sử giao gần đây ({{ operations.recentDeliveries.length }})</summary>
        <div class="delivery-list">
          <div v-for="delivery in operations.recentDeliveries" :key="delivery.id" class="delivery-row">
            <span><strong>{{ delivery.eventType }}</strong> · {{ formatDate(delivery.createdAt) }}</span>
            <span :class="delivery.isSuccess ? 'delivery-ok' : 'delivery-failed'">
              {{ delivery.isSuccess ? 'Đã giao' : 'Thất bại' }} · HTTP {{ delivery.responseStatusCode ?? '—' }} · {{ delivery.durationMs }}ms
            </span>
          </div>
        </div>
      </details>
    </section>
  </div>
</template>

<style scoped>
.webhooks-manager {
  min-width: 0;
  padding: 24px;
  color: var(--text-strong);
  background: var(--panel);
  border: 1px solid var(--line);
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 20px;
  margin-bottom: 24px;
}

.title-group {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
}

.title-group h3 {
  margin: 0;
  color: var(--text-strong);
  font-size: 18px;
  font-weight: 800;
}

.icon-primary {
  flex: 0 0 auto;
  color: var(--primary);
}

.webhook-form {
  padding: 20px;
  margin-bottom: 24px;
  display: flex;
  flex-direction: column;
  gap: 16px;
  background: var(--bg-soft);
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
  box-shadow: none;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.form-group > label {
  color: var(--text-strong);
  font-size: 13px;
  font-weight: 700;
}

.form-group > input {
  width: 100%;
  min-height: 42px;
  padding: 10px 12px;
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
  outline: none;
  color: var(--text-strong);
  background: var(--panel);
}

.form-group > input::placeholder {
  color: var(--muted);
}

.form-group > input:focus {
  border-color: var(--primary);
  box-shadow: 0 0 0 3px var(--primary-soft);
}

.events-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 10px 16px;
  padding: 12px;
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
  background: var(--panel);
}

.event-checkbox {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--text-strong);
  cursor: pointer;
  font-size: 13px;
}

.event-checkbox input {
  width: 15px;
  height: 15px;
  margin: 0;
  accent-color: var(--primary);
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  margin-top: 8px;
}

.webhooks-list {
  display: grid;
  gap: 12px;
}

.webhook-item {
  min-width: 0;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 20px;
  padding: 16px;
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
  background: var(--bg-soft);
  box-shadow: none;
}

.hook-main {
  min-width: 0;
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.hook-url {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
  color: var(--text-strong);
}

.hook-url strong {
  min-width: 0;
  color: var(--text-strong);
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  font-size: 13px;
  overflow-wrap: anywhere;
}

.secure-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 3px 7px;
  border: 1px solid color-mix(in srgb, var(--success) 35%, var(--line));
  border-radius: 999px;
  color: var(--success);
  background: color-mix(in srgb, var(--success) 10%, var(--panel));
  font-size: 10px;
  font-weight: 700;
  white-space: nowrap;
}

.hook-events {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.test-receipt { color: var(--success); font-size: 12px; }

.event-tag {
  padding: 3px 8px;
  border: 1px solid color-mix(in srgb, var(--primary) 28%, var(--line));
  border-radius: 999px;
  color: var(--primary-strong);
  background: var(--primary-soft);
  font-size: 11px;
  font-weight: 700;
}

.hook-actions {
  flex: 0 0 auto;
  display: flex;
  align-items: center;
  gap: 8px;
}

.hook-actions .icon-button,
.hook-actions .revoke-button {
  width: 40px;
  height: 40px;
  display: inline-grid;
  place-items: center;
  padding: 0;
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
  color: var(--muted);
  background: var(--panel);
  box-shadow: none;
}

.hook-actions .revoke-button {
  color: var(--danger);
}

.hook-actions .revoke-button:hover,
.hook-actions .revoke-button:focus-visible {
  border-color: color-mix(in srgb, var(--danger) 45%, var(--line));
  color: var(--danger);
  background: var(--danger-soft);
  outline: 0;
}

.empty-state {
  padding: 20px;
  border: 1px dashed var(--line);
  border-radius: var(--qaly-radius-lg);
  color: var(--muted);
  background: var(--bg-soft);
  text-align: center;
}

.operations-panel {
  display: grid;
  gap: 14px;
  margin-top: 24px;
  padding-top: 20px;
  border-top: 1px solid var(--line);
}

.operations-header,
.operations-summary,
.outbox-row,
.delivery-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}

.operations-header h4,
.operations-header p,
.outbox-row p {
  margin: 0;
}

.operations-header p,
.outbox-row p {
  margin-top: 4px;
  color: var(--muted);
  font-size: 12px;
}

.operations-refresh {
  flex: 0 0 auto;
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.operations-summary {
  justify-content: flex-start;
  flex-wrap: wrap;
}

.operations-summary span,
.outbox-status {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 5px 9px;
  border-radius: 999px;
  color: var(--muted);
  background: var(--bg-soft);
  font-size: 12px;
}

.operations-summary .summary-danger,
.outbox-row--dead_letter .outbox-status,
.outbox-error {
  color: var(--danger);
}

.outbox-list,
.delivery-list {
  display: grid;
  gap: 8px;
}

.outbox-row,
.delivery-row {
  padding: 12px;
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
  background: var(--bg-soft);
}

.outbox-main {
  min-width: 0;
}

.outbox-title {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.outbox-error {
  overflow-wrap: anywhere;
}

.delivery-history summary {
  color: var(--text-strong);
  cursor: pointer;
  font-weight: 700;
}

.delivery-list {
  margin-top: 10px;
}

.delivery-ok { color: var(--success); }
.delivery-failed { color: var(--danger); }
.spinning { animation: spin 0.8s linear infinite; }

@keyframes spin { to { transform: rotate(360deg); } }

.ghost-button {
  min-height: 42px;
  padding: 0 14px;
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
  color: var(--text-strong);
  background: var(--panel);
  font-weight: 700;
}

.ghost-button:hover,
.ghost-button:focus-visible {
  border-color: color-mix(in srgb, var(--primary) 45%, var(--line));
  color: var(--primary-strong);
  background: var(--primary-soft);
  outline: 0;
}

.expand-enter-active, .expand-leave-active { transition: all 0.3s ease; }
.expand-enter-from, .expand-leave-to { opacity: 0; transform: translateY(-10px); }

@media (max-width: 640px) {
  .webhooks-manager {
    padding: 16px;
  }

  .panel-header {
    align-items: stretch;
    flex-direction: column;
  }

  .panel-header .primary-button {
    align-self: flex-start;
  }

  .webhook-item {
    align-items: stretch;
    flex-direction: column;
  }

  .hook-actions {
    justify-content: flex-end;
  }

  .form-actions {
    align-items: stretch;
    flex-direction: column-reverse;
  }

  .operations-header,
  .outbox-row,
  .delivery-row {
    align-items: stretch;
    flex-direction: column;
  }
}
</style>
