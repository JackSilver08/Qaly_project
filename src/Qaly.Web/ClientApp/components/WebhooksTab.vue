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
          <input v-model="newWebhook.payloadUrl" type="url" placeholder="https://your-app.com/webhook" required />
        </div>
        <div class="form-group">
          <label>Khóa bí mật (không bắt buộc)</label>
          <input v-model="newWebhook.secret" type="password" placeholder="Khóa bí mật để xác thực webhook" />
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
.webhooks-manager { padding: 24px; }
.panel-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
.title-group { display: flex; align-items: center; gap: 12px; }

.webhook-form { padding: 20px; margin-bottom: 24px; display: flex; flex-direction: column; gap: 16px; background: rgba(255, 255, 255, 0.05); border: 1px solid rgba(182, 194, 217, 0.24); border-radius: var(--qaly-radius-lg); }
.form-group { display: flex; flex-direction: column; gap: 8px; }
.form-group label { font-size: 13px; font-weight: 700; color: var(--muted); }
.form-group input { border: 1px solid rgba(182, 194, 217, 0.24); border-radius: var(--qaly-radius-lg); padding: 10px 16px; outline: none; color: var(--surface-milk); background: rgba(8, 21, 39, 0.74); }

.events-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 12px; }
.event-checkbox { display: flex; align-items: center; gap: 8px; cursor: pointer; font-size: 13px; color: var(--muted); }

.form-actions { display: flex; justify-content: flex-end; gap: 12px; margin-top: 8px; }

.webhooks-list { display: flex; flex-direction: column; gap: 12px; }
.webhook-item { display: flex; justify-content: space-between; align-items: center; padding: 16px; border: 1px solid rgba(182, 194, 217, 0.24); border-radius: var(--qaly-radius-lg); background: rgba(255, 255, 255, 0.05); }
.hook-main { flex: 1; display: flex; flex-direction: column; gap: 8px; }
.hook-url { display: flex; align-items: center; gap: 12px; font-family: monospace; color: var(--surface-milk); }
.secure-badge { display: flex; align-items: center; gap: 4px; font-size: 10px; color: var(--success); font-weight: 700; }
.hook-events { display: flex; flex-wrap: wrap; gap: 6px; }
.event-tag { font-size: 10px; background: rgba(31, 128, 255, 0.2); color: #d9edff; padding: 2px 8px; border-radius: 6px; border: 1px solid rgba(117, 182, 255, 0.34); }

.hook-actions { display: flex; gap: 8px; }
.empty-state { text-align: center; color: var(--muted); padding: 20px; font-style: italic; border: 1px dashed rgba(182, 194, 217, 0.3); border-radius: var(--qaly-radius-lg); background: rgba(255, 255, 255, 0.04); }

.ghost-button {
  border: 1px solid rgba(182, 194, 217, 0.26);
  border-radius: var(--qaly-radius-lg);
  color: #d2e1f8;
  background: rgba(255, 255, 255, 0.06);
  font-weight: 700;
}

.ghost-button:hover {
  border-color: rgba(117, 182, 255, 0.62);
  color: #f8fafc;
  background: rgba(31, 128, 255, 0.2);
}

.expand-enter-active, .expand-leave-active { transition: all 0.3s ease; }
.expand-enter-from, .expand-leave-to { opacity: 0; transform: translateY(-10px); }
</style>
