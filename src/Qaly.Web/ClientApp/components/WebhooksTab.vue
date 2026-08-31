<script setup lang="ts">
import { ref, watch } from 'vue'
import { Webhook, Trash2, Plus, Activity, ShieldCheck } from 'lucide-vue-next'
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

const webhooks = ref<WebhookDto[]>([])
const showCreateForm = ref(false)
const isLoading = ref(false)
const isFetching = ref(false)

const newWebhook = ref({
  payloadUrl: '',
  secret: '',
  events: ['task.created', 'task.updated']
})

const availableEvents = [
  { id: 'task.created', label: 'Đã tạo nhiệm vụ' },
  { id: 'task.updated', label: 'Đã cập nhật nhiệm vụ' },
  { id: 'task.deleted', label: 'Đã xóa nhiệm vụ' },
  { id: 'comment.added', label: 'Đã thêm bình luận' },
  { id: 'project.updated', label: 'Đã cập nhật dự án' }
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
    await fetchWebhooks()
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

    await fetchWebhooks()
    showSuccess('Đã xóa webhook.')
  } catch (e) {
    showError(errorMessage(e, 'Không thể xóa webhook.'))
  }
}

async function testWebhook(id: string) {
  try {
    await apiCommand(`/api/projects/${props.projectId}/webhooks/${id}/test`, { method: 'POST' })

    showSuccess('Đã gửi dữ liệu kiểm thử thành công!')
  } catch (e) {
    showError(errorMessage(e, 'Không thể gửi webhook test.'))
  }
}

watch(() => props.projectId, fetchWebhooks, { immediate: true })
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
        </div>
        <div class="hook-actions">
          <button type="button" @click="testWebhook(hook.id)" class="icon-button" title="Kiểm tra kết nối">
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
}
</style>
