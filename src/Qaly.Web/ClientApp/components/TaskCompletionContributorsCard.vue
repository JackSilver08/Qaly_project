<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { AlertTriangle, CheckCircle2, LoaderCircle, RefreshCw, ShieldCheck, UserCheck, X } from 'lucide-vue-next'
import { ApiError, apiResult, errorMessage } from '../utils/api-client'
import { showError, showSuccess } from '../composables/use-toast'

type Contributor = { userId: string; fullName: string; email: string | null }
type Attribution = {
  id: string; contributorUserId: string; contributorName: string; status: 'Confirmed' | 'CorrectionRequested' | 'Revoked'
  completedAt: string; confirmedAt: string; confirmedByUserId: string; correctionReason: string | null
  correctionRequestedAt: string | null; rowVersion: string; canRequestCorrection: boolean
}
type AttributionState = {
  taskId: string; taskStatus: string; canManage: boolean; isEligibleForAttribution: boolean; taskRowVersion: string
  eligibleContributors: Contributor[]; attributions: Attribution[]; notice: string | null
}

const props = defineProps<{ taskId: string }>()
const state = ref<AttributionState | null>(null)
const selectedIds = ref<string[]>([])
const loading = ref(false)
const saving = ref(false)
const error = ref('')
const correctionFor = ref<Attribution | null>(null)
const correctionReason = ref('')
let requestVersion = 0

const selectedSet = computed(() => new Set(selectedIds.value))
const canSave = computed(() => Boolean(state.value?.canManage && state.value.isEligibleForAttribution && !saving.value))
const activeAttributions = computed(() => state.value?.attributions.filter(item => item.status !== 'Revoked') ?? [])

function statusLabel(value: Attribution['status']) {
  return value === 'Confirmed' ? 'Đã xác nhận' : value === 'CorrectionRequested' ? 'Chờ rà soát' : 'Đã thu hồi'
}

function syncSelection(next: AttributionState) {
  selectedIds.value = next.attributions
    .filter(item => item.status === 'Confirmed')
    .map(item => item.contributorUserId)
}

async function load() {
  const version = ++requestVersion
  loading.value = true
  error.value = ''
  correctionFor.value = null
  try {
    const next = await apiResult<AttributionState>(`/api/tasks/${props.taskId}/completion-contributors`)
    if (version !== requestVersion) return
    state.value = next
    syncSelection(next)
  } catch (cause) {
    if (version !== requestVersion) return
    state.value = null
    error.value = errorMessage(cause, 'Không thể tải bằng chứng đóng góp.')
  } finally {
    if (version === requestVersion) loading.value = false
  }
}

async function save() {
  const current = state.value
  if (!current || !canSave.value) return
  saving.value = true
  error.value = ''
  try {
    const next = await apiResult<AttributionState>(`/api/tasks/${props.taskId}/completion-contributors`, {
      method: 'PUT',
      body: JSON.stringify({
        taskRowVersion: current.taskRowVersion,
        contributorUserIds: selectedIds.value,
        confirmed: true,
        confirmationNote: 'Confirmed from Task Detail skill evidence card',
      }),
    })
    state.value = next
    syncSelection(next)
    showSuccess('Đã xác nhận đóng góp hoàn thành và ghi audit. Bằng chứng chỉ tính cho kỹ năng đã có trên task.')
  } catch (cause) {
    if (cause instanceof ApiError && cause.status === 409) await load()
    error.value = errorMessage(cause, 'Không thể cập nhật đóng góp hoàn thành.')
  } finally {
    saving.value = false
  }
}

function openCorrection(attribution: Attribution) {
  correctionFor.value = attribution
  correctionReason.value = ''
}

async function submitCorrection() {
  const item = correctionFor.value
  if (!item || correctionReason.value.trim().length < 3) return
  saving.value = true
  error.value = ''
  try {
    await apiResult(`/api/tasks/${props.taskId}/completion-contributors/${item.id}/correction`, {
      method: 'POST',
      body: JSON.stringify({ rowVersion: item.rowVersion, reason: correctionReason.value.trim() }),
    })
    showSuccess('Đã gửi yêu cầu rà soát. Bằng chứng này tạm thời không được dùng để suy luận kỹ năng.')
    await load()
  } catch (cause) {
    error.value = errorMessage(cause, 'Không thể gửi yêu cầu rà soát.')
  } finally {
    saving.value = false
  }
}

watch(() => props.taskId, load, { immediate: true })
onBeforeUnmount(() => { requestVersion += 1 })
</script>

<template>
  <section class="completion-card glass-card" data-testid="task-completion-contributors-card">
    <header>
      <div>
        <span class="eyebrow"><ShieldCheck :size="14" /> Bằng chứng kỹ năng</span>
        <h3>Ai đã hoàn thành phần việc?</h3>
        <p>Chỉ manager xác nhận người đã được giao sau khi task Done. Không tự suy luận từ assignee hoặc lịch sử chat.</p>
      </div>
      <button class="icon-button" type="button" :disabled="loading || saving" aria-label="Tải lại đóng góp" @click="load"><RefreshCw :size="16" :class="{ spin: loading }" /></button>
    </header>

    <div v-if="loading && !state" class="state-row"><LoaderCircle :size="20" class="spin" /> Đang tải attribution…</div>
    <div v-else-if="error && !state" class="state-row state-row--error"><AlertTriangle :size="20" /> {{ error }}</div>
    <template v-else-if="state">
      <div v-if="state.notice" class="notice"><AlertTriangle :size="16" /> {{ state.notice }}</div>
      <div v-else-if="state.canManage" class="contributor-options">
        <label v-for="member in state.eligibleContributors" :key="member.userId" class="contributor-option">
          <input v-model="selectedIds" type="checkbox" :value="member.userId" :disabled="saving" />
          <span class="avatar">{{ member.fullName.slice(0, 1).toUpperCase() }}</span>
          <span><strong>{{ member.fullName }}</strong><small>{{ member.email }}</small></span>
        </label>
        <button class="save-button" type="button" :disabled="!canSave" @click="save"><UserCheck :size="16" /> Xác nhận bằng chứng</button>
      </div>
      <p v-else class="read-only"><ShieldCheck :size="16" /> Chỉ người quản lý dự án mới có thể xác nhận hoặc thay đổi đóng góp.</p>

      <div v-if="activeAttributions.length" class="attribution-list">
        <article v-for="item in activeAttributions" :key="item.id" class="attribution" :class="{ pending: item.status === 'CorrectionRequested' }">
          <div><strong>{{ item.contributorName }}</strong><small>{{ statusLabel(item.status) }} · {{ new Date(item.completedAt).toLocaleDateString('vi-VN') }}</small></div>
          <button v-if="item.status === 'Confirmed' && item.canRequestCorrection" class="text-button" type="button" :disabled="saving" @click="openCorrection(item)">Yêu cầu rà soát</button>
          <p v-if="item.status === 'CorrectionRequested'">{{ item.correctionReason }}</p>
        </article>
      </div>
      <p v-else-if="state.isEligibleForAttribution" class="empty">Chưa có người được xác nhận. Không có bằng chứng không đồng nghĩa thành viên không có kỹ năng.</p>
      <p v-if="error && state" class="inline-error" role="alert">{{ error }}</p>
    </template>

    <div v-if="correctionFor" class="correction" role="dialog" aria-label="Yêu cầu rà soát attribution">
      <div class="correction__head"><strong>Rà soát đóng góp của {{ correctionFor.contributorName }}</strong><button class="icon-button" type="button" @click="correctionFor = null"><X :size="16" /></button></div>
      <textarea v-model="correctionReason" rows="3" maxlength="500" placeholder="Nêu lý do cần rà soát (không ghi dữ liệu nhạy cảm)…" />
      <div><button class="secondary-button" type="button" @click="correctionFor = null">Hủy</button><button class="save-button" type="button" :disabled="saving || correctionReason.trim().length < 3" @click="submitCorrection"><CheckCircle2 :size="16" /> Gửi yêu cầu</button></div>
    </div>
  </section>
</template>

<style scoped>
.completion-card{display:grid;gap:14px;padding:18px;margin-bottom:14px}.completion-card header,.correction__head{display:flex;justify-content:space-between;gap:12px}.eyebrow{display:inline-flex;align-items:center;gap:6px;color:#0f766e;font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.04em}.completion-card h3,.completion-card p{margin:5px 0}.completion-card header p,.read-only,.empty{color:var(--text-secondary,#64748b);font-size:13px;line-height:1.45}.icon-button,.text-button,.save-button,.secondary-button{border:0;cursor:pointer;font:inherit}.icon-button{width:34px;height:34px;border-radius:9px;background:var(--panel-soft,#f1f5f9);display:grid;place-items:center;color:inherit}.state-row,.notice,.read-only{display:flex;align-items:center;gap:8px;padding:11px;border-radius:9px;background:var(--panel-soft,#f8fafc);font-size:13px}.state-row--error,.inline-error{color:#b42318}.notice{color:#9a6700;background:#fffaeb}.contributor-options{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:8px}.contributor-option{display:flex;align-items:center;gap:9px;padding:9px;border:1px solid var(--border-color,#dbe2ea);border-radius:10px;cursor:pointer}.contributor-option>span:last-child{display:grid;gap:2px;min-width:0}.contributor-option small,.attribution small{color:var(--text-secondary,#64748b);font-size:12px;overflow:hidden;text-overflow:ellipsis}.avatar{display:grid;place-items:center;width:28px;height:28px;flex:0 0 28px;border-radius:50%;background:#dcfce7;color:#166534;font-size:12px;font-weight:800}.save-button{justify-self:start;display:inline-flex;align-items:center;gap:7px;padding:9px 12px;border-radius:9px;background:#0f766e;color:#fff;font-weight:750}.save-button:disabled{opacity:.55;cursor:not-allowed}.attribution-list{display:grid;gap:7px}.attribution{display:flex;justify-content:space-between;align-items:center;gap:10px;padding:9px 11px;border:1px solid var(--border-color,#dbe2ea);border-radius:9px}.attribution>div{display:grid;gap:3px}.attribution.pending{border-color:#f59e0b;background:#fffbeb}.attribution p{grid-column:1/-1;margin:0;color:#9a6700;font-size:12px}.text-button{color:#0f766e;background:transparent;font-size:12px;font-weight:700}.inline-error{font-size:13px}.correction{display:grid;gap:10px;padding:13px;border:1px solid #f59e0b;border-radius:10px;background:#fffbeb}.correction textarea{resize:vertical;border:1px solid #d0d5dd;border-radius:8px;padding:9px;background:#fff;color:inherit}.correction>div:last-child{display:flex;justify-content:flex-end;gap:8px}.secondary-button{padding:9px 12px;border-radius:9px;background:transparent;color:inherit}.spin{animation:spin 1s linear infinite}@keyframes spin{to{transform:rotate(360deg)}}@media (prefers-reduced-motion:reduce){.spin{animation:none}}
</style>
