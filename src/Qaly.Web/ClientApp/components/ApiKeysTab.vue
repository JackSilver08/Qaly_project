<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Check, Copy, Key, RefreshCw, ShieldAlert, Trash2 } from 'lucide-vue-next'
import { confirmDialog } from '../composables/use-confirm-dialog'
import { apiFetch } from '../utils/api-client'

interface ApiKeyItem {
  id: string
  name: string
  prefix: string
  scopes: string[]
  expiresAt: string | null
  lastUsedAt: string | null
  isRevoked: boolean
  createdAt: string
}

interface ApiResult<T> {
  data?: T
  error?: string
}

const scopeOptions = [
  { value: 'projects:read', label: 'Đọc dự án', description: 'Xem danh sách, chi tiết, nhãn và capacity dự án.' },
  { value: 'projects:write', label: 'Sửa dự án', description: 'Tạo, cập nhật, lưu trữ và quản lý thành viên dự án.' },
  { value: 'tasks:read', label: 'Đọc task', description: 'Xem task, Sprint, dependency, timeline và workload.' },
  { value: 'tasks:write', label: 'Sửa task', description: 'Tạo, cập nhật, giao task và quản lý Sprint.' },
  { value: 'webhooks:read', label: 'Đọc webhook', description: 'Xem cấu hình webhook trong dự án được phép.' },
  { value: 'webhooks:write', label: 'Sửa webhook', description: 'Tạo, cập nhật, kiểm thử và thu hồi webhook.' }
] as const

const apiKeys = ref<ApiKeyItem[]>([])
const newKeyName = ref('')
const selectedScopes = ref<string[]>(['projects:read', 'tasks:read'])
const expiryPreset = ref<'30' | '90' | '365' | 'never'>('90')
const generatedKey = ref<string | null>(null)
const generatedScopes = ref<string[]>([])
const isLoading = ref(false)
const isFetching = ref(false)
const copied = ref(false)
const errorMessage = ref('')

const isExpired = (key: ApiKeyItem) => Boolean(key.expiresAt && new Date(key.expiresAt).getTime() <= Date.now())
const activeKeys = computed(() => apiKeys.value.filter(key => !key.isRevoked && !isExpired(key)))
const inactiveKeys = computed(() => apiKeys.value.filter(key => key.isRevoked || isExpired(key)))
const canCreate = computed(() =>
  newKeyName.value.trim().length > 0 &&
  newKeyName.value.trim().length <= 100 &&
  selectedScopes.value.length > 0 &&
  !isLoading.value)

function expiryDate(): string | null {
  if (expiryPreset.value === 'never') return null
  const expiresAt = new Date()
  expiresAt.setUTCDate(expiresAt.getUTCDate() + Number(expiryPreset.value))
  return expiresAt.toISOString()
}

async function readError(response: Response, fallback: string): Promise<string> {
  const payload = await response.json().catch(() => null) as ApiResult<unknown> | null
  return payload?.error || fallback
}

async function fetchKeys() {
  isFetching.value = true
  errorMessage.value = ''
  try {
    const response = await apiFetch('/api/auth/api-keys')
    if (!response.ok) {
      errorMessage.value = await readError(response, 'Không thể tải danh sách API Key.')
      return
    }

    const payload = await response.json() as ApiResult<ApiKeyItem[]>
    apiKeys.value = Array.isArray(payload.data) ? payload.data : []
  } catch {
    errorMessage.value = 'Mất kết nối khi tải API Key. Vui lòng thử lại.'
  } finally {
    isFetching.value = false
  }
}

async function createKey() {
  if (!canCreate.value) return
  isLoading.value = true
  errorMessage.value = ''
  try {
    const response = await apiFetch('/api/auth/api-keys', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name: newKeyName.value.trim(),
        scopes: selectedScopes.value,
        expiresAt: expiryDate()
      })
    })
    if (!response.ok) {
      errorMessage.value = await readError(response, 'Không thể tạo API Key.')
      return
    }

    const payload = await response.json() as ApiResult<{ key: string; scopes: string[] }>
    if (!payload.data?.key) {
      errorMessage.value = 'Máy chủ không trả về key vừa tạo. Không có dữ liệu nào được ghi nhận là thành công.'
      return
    }

    generatedKey.value = payload.data.key
    generatedScopes.value = payload.data.scopes
    newKeyName.value = ''
    await fetchKeys()
  } catch {
    errorMessage.value = 'Mất kết nối khi tạo API Key. Hãy kiểm tra danh sách trước khi thử lại.'
  } finally {
    isLoading.value = false
  }
}

async function revokeKey(id: string) {
  const key = apiKeys.value.find(item => item.id === id)
  if (!key || key.isRevoked) return
  if (!await confirmDialog({
    tone: 'critical',
    title: 'Thu hồi API key?',
    subject: key.name,
    message: 'Ứng dụng đang dùng key này sẽ mất quyền truy cập ngay lập tức.',
    confirmLabel: 'Thu hồi key'
  })) return

  errorMessage.value = ''
  try {
    const response = await apiFetch(`/api/auth/api-keys/${id}`, { method: 'DELETE' })
    if (!response.ok) {
      errorMessage.value = await readError(response, 'Không thể thu hồi API Key.')
      return
    }
    await fetchKeys()
  } catch {
    errorMessage.value = 'Mất kết nối khi thu hồi API Key. Hãy tải lại để kiểm tra trạng thái thật.'
  }
}

async function copyToClipboard() {
  if (!generatedKey.value) return
  try {
    await navigator.clipboard.writeText(generatedKey.value)
    copied.value = true
    window.setTimeout(() => { copied.value = false }, 2000)
  } catch {
    errorMessage.value = 'Trình duyệt không cho phép sao chép. Hãy chọn và sao chép key thủ công.'
  }
}

function formatDate(value: string | null): string {
  if (!value) return 'Chưa có'
  return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

function scopeLabel(scope: string): string {
  return scopeOptions.find(option => option.value === scope)?.label || scope
}

onMounted(fetchKeys)
</script>

<template>
  <section class="api-keys-manager glass-card reveal" aria-labelledby="api-key-heading">
    <div class="panel-header">
      <div>
        <div class="title-row">
          <Key :size="20" class="icon-primary" aria-hidden="true" />
          <h3 id="api-key-heading">API Key cá nhân</h3>
        </div>
        <p class="panel-desc">Key kế thừa quyền của bạn nhưng chỉ được phép dùng đúng scope đã chọn. Endpoint ngoài danh mục sẽ bị từ chối.</p>
      </div>
      <button type="button" class="icon-button refresh-button" :disabled="isFetching" aria-label="Tải lại danh sách API Key" @click="fetchKeys">
        <RefreshCw :size="17" :class="{ spinning: isFetching }" />
      </button>
    </div>

    <div v-if="errorMessage" class="error-banner" role="alert">{{ errorMessage }}</div>

    <div v-if="generatedKey" class="key-display-banner">
      <ShieldAlert :size="24" aria-hidden="true" />
      <div class="banner-content">
        <strong>Lưu key ngay — đây là lần hiển thị duy nhất</strong>
        <p>Scope: {{ generatedScopes.map(scopeLabel).join(', ') }}. Đóng khung này sẽ không thể xem lại plaintext.</p>
        <div class="key-box">
          <code>{{ generatedKey }}</code>
          <button type="button" :aria-label="copied ? 'Đã sao chép API key' : 'Sao chép API key'" class="icon-button" @click="copyToClipboard">
            <Check v-if="copied" :size="16" class="success-icon" />
            <Copy v-else :size="16" />
          </button>
        </div>
        <button type="button" class="secondary-button" @click="generatedKey = null">Tôi đã lưu an toàn</button>
      </div>
    </div>

    <form class="create-key-form" @submit.prevent="createKey">
      <div class="form-field">
        <label for="api-key-name">Tên gợi nhớ</label>
        <input id="api-key-name" v-model="newKeyName" type="text" maxlength="100" placeholder="Ví dụ: CI/CD production" required />
        <small>Dùng tên mô tả hệ thống đang giữ key; tối đa 100 ký tự.</small>
      </div>

      <fieldset class="scope-fieldset">
        <legend>Phạm vi truy cập <span aria-hidden="true">*</span></legend>
        <p>Chỉ chọn quyền tích hợp thực sự cần. Scope không thay thế RBAC dự án của tài khoản.</p>
        <div class="scope-grid">
          <label v-for="option in scopeOptions" :key="option.value" class="scope-option">
            <input v-model="selectedScopes" type="checkbox" :value="option.value" />
            <span>
              <strong>{{ option.label }}</strong>
              <small>{{ option.description }}</small>
            </span>
          </label>
        </div>
        <small v-if="selectedScopes.length === 0" class="field-error">Chọn ít nhất một scope để tạo key.</small>
      </fieldset>

      <div class="form-field expiry-field">
        <label for="api-key-expiry">Thời hạn</label>
        <select id="api-key-expiry" v-model="expiryPreset">
          <option value="30">30 ngày — tích hợp tạm thời</option>
          <option value="90">90 ngày — khuyến nghị</option>
          <option value="365">1 năm</option>
          <option value="never">Không hết hạn — cần tự thu hồi</option>
        </select>
        <small>Key hết hạn bị từ chối ngay cả khi chưa thu hồi.</small>
      </div>

      <div class="form-actions">
        <span>{{ activeKeys.length }}/20 key đang hoạt động</span>
        <button type="submit" class="primary-button" :disabled="!canCreate">
          {{ isLoading ? 'Đang tạo và kiểm tra...' : 'Tạo API Key' }}
        </button>
      </div>
    </form>

    <div class="keys-section">
      <div class="list-heading">
        <h4>Key đang hoạt động</h4>
        <span>{{ activeKeys.length }}</span>
      </div>
      <div v-if="isFetching && apiKeys.length === 0" class="empty-state">Đang tải danh sách…</div>
      <div v-else-if="activeKeys.length === 0" class="empty-state">Chưa có API Key đang hoạt động.</div>
      <article v-for="key in activeKeys" v-else :key="key.id" class="key-item">
        <div class="key-info">
          <div class="key-name-row"><strong>{{ key.name }}</strong><code>{{ key.prefix }}…</code></div>
          <div class="scope-tags"><span v-for="scope in key.scopes" :key="scope">{{ scopeLabel(scope) }}</span></div>
          <dl>
            <div><dt>Tạo lúc</dt><dd>{{ formatDate(key.createdAt) }}</dd></div>
            <div><dt>Hết hạn</dt><dd>{{ key.expiresAt ? formatDate(key.expiresAt) : 'Không hết hạn' }}</dd></div>
            <div><dt>Dùng gần nhất</dt><dd>{{ formatDate(key.lastUsedAt) }}</dd></div>
          </dl>
        </div>
        <button type="button" class="revoke-button" :aria-label="`Thu hồi ${key.name}`" @click="revokeKey(key.id)">
          <Trash2 :size="16" /> Thu hồi
        </button>
      </article>
    </div>

    <details v-if="inactiveKeys.length" class="revoked-section">
      <summary>Không còn hiệu lực ({{ inactiveKeys.length }})</summary>
      <div v-for="key in inactiveKeys" :key="key.id" class="revoked-item">
        <span><strong>{{ key.name }}</strong> · <code>{{ key.prefix }}…</code></span>
        <span>{{ key.isRevoked ? 'Đã thu hồi' : 'Đã hết hạn' }} · tạo {{ formatDate(key.createdAt) }}</span>
      </div>
    </details>
  </section>
</template>

<style scoped>
.api-keys-manager { padding: 24px; }
.panel-header, .title-row, .form-actions, .list-heading, .key-name-row { display: flex; align-items: center; }
.panel-header { justify-content: space-between; gap: 16px; margin-bottom: 20px; }
.title-row { gap: 10px; }
.title-row h3, .list-heading h4 { margin: 0; }
.panel-desc { color: var(--muted); font-size: 14px; margin: 6px 0 0; max-width: 780px; }
.refresh-button:disabled { opacity: .55; cursor: wait; }
.spinning { animation: spin .8s linear infinite; }

.error-banner { padding: 12px 14px; margin-bottom: 16px; border: 1px solid rgba(239, 68, 68, .42); border-radius: var(--qaly-radius-lg); background: rgba(239, 68, 68, .12); color: #fecaca; }
.key-display-banner { display: flex; gap: 14px; padding: 18px; margin-bottom: 20px; border: 1px solid rgba(245, 158, 11, .38); border-radius: var(--qaly-radius-lg); background: var(--warning-soft); }
.banner-content { min-width: 0; flex: 1; }
.banner-content p { margin: 5px 0 0; color: var(--muted); }
.key-box { display: flex; align-items: center; gap: 8px; margin: 12px 0; padding: 9px 12px; border: 1px solid rgba(245, 158, 11, .34); border-radius: var(--qaly-radius-lg); background: rgba(8, 21, 39, .78); }
.key-box code { flex: 1; min-width: 0; overflow-wrap: anywhere; color: var(--warning-dark); font-weight: 700; }
.success-icon { color: var(--success); }

.create-key-form { display: grid; grid-template-columns: minmax(220px, .8fr) minmax(480px, 2fr); gap: 18px; padding: 18px; border: 1px solid rgba(182, 194, 217, .24); border-radius: var(--qaly-radius-lg); background: rgba(255, 255, 255, .035); }
.form-field { display: flex; flex-direction: column; gap: 7px; }
.form-field label, .scope-fieldset legend { font-weight: 700; }
.form-field input, .form-field select { width: 100%; border: 1px solid rgba(182, 194, 217, .28); border-radius: var(--qaly-radius-lg); padding: 10px 12px; outline: none; color: var(--surface-milk); background: rgba(8, 21, 39, .74); }
.form-field small, .scope-fieldset > p, .scope-option small { color: var(--muted); font-size: 12px; }
.scope-fieldset { grid-column: 2; grid-row: 1 / span 2; min-width: 0; padding: 0; border: 0; }
.scope-fieldset > p { margin: 3px 0 10px; }
.scope-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px; }
.scope-option { display: flex; align-items: flex-start; gap: 9px; padding: 10px; border: 1px solid rgba(182, 194, 217, .24); border-radius: var(--qaly-radius-lg); cursor: pointer; }
.scope-option:has(input:checked) { border-color: rgba(37, 99, 235, .68); background: rgba(37, 99, 235, .12); }
.scope-option input { margin-top: 3px; }
.scope-option span, .scope-option small { display: block; }
.field-error { display: block; margin-top: 8px; color: #fca5a5 !important; }
.form-actions { grid-column: 1 / -1; justify-content: space-between; gap: 12px; padding-top: 4px; color: var(--muted); font-size: 13px; }

.keys-section { margin-top: 24px; }
.list-heading { justify-content: space-between; margin-bottom: 10px; }
.list-heading span { padding: 2px 8px; border-radius: 999px; background: rgba(37, 99, 235, .14); color: #93c5fd; }
.key-item { display: flex; justify-content: space-between; gap: 16px; padding: 16px; margin-bottom: 10px; border: 1px solid rgba(182, 194, 217, .24); border-radius: var(--qaly-radius-lg); background: rgba(255, 255, 255, .04); }
.key-info { min-width: 0; flex: 1; }
.key-name-row { gap: 10px; flex-wrap: wrap; }
.key-name-row code { color: var(--muted); }
.scope-tags { display: flex; flex-wrap: wrap; gap: 6px; margin: 9px 0; }
.scope-tags span { padding: 3px 8px; border-radius: 999px; background: rgba(37, 99, 235, .13); color: #bfdbfe; font-size: 11px; }
dl { display: flex; flex-wrap: wrap; gap: 8px 20px; margin: 0; color: var(--muted); font-size: 12px; }
dl div { display: flex; gap: 5px; }
dt { font-weight: 600; }
dd { margin: 0; }
.revoke-button { align-self: center; display: inline-flex; align-items: center; gap: 6px; padding: 8px 10px; border: 1px solid rgba(239, 68, 68, .34); border-radius: var(--qaly-radius-lg); background: rgba(239, 68, 68, .12); color: #fda4af; cursor: pointer; }
.revoke-button:hover { border-color: rgba(239, 68, 68, .58); background: rgba(239, 68, 68, .2); color: #fee2e2; }
.empty-state { padding: 20px; text-align: center; color: var(--muted); border: 1px dashed rgba(182, 194, 217, .3); border-radius: var(--qaly-radius-lg); }
.revoked-section { margin-top: 18px; color: var(--muted); }
.revoked-section summary { cursor: pointer; font-weight: 700; }
.revoked-item { display: flex; justify-content: space-between; gap: 12px; padding: 9px 4px; border-bottom: 1px solid rgba(182, 194, 217, .14); font-size: 12px; }

@keyframes spin { to { transform: rotate(360deg); } }
@media (max-width: 900px) {
  .create-key-form { grid-template-columns: 1fr; }
  .scope-fieldset { grid-column: 1; grid-row: auto; }
  .scope-grid { grid-template-columns: 1fr; }
  .key-item { flex-direction: column; }
  .revoke-button { align-self: flex-start; }
}
</style>
