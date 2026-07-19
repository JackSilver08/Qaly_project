<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import {
  AlertTriangle,
  Ban,
  CheckCircle2,
  Cloud,
  Download,
  FileClock,
  Loader2,
  LockKeyhole,
  Plus,
  RefreshCw,
  Scale,
  Server,
  ShieldCheck,
  Trash2,
} from 'lucide-vue-next'
import { apiResult, errorMessage } from '../../utils/api-client'
import { useDashboardContext } from '../../composables/dashboard-context'
import { showError, showSuccess } from '../../composables/use-toast'

type PrivacyMode = 'consent' | 'policies' | 'requests' | 'holds'

interface RetentionPolicy {
  id: string
  tenantId: string
  projectId: string | null
  name: string
  dataClassification: string
  purpose: string
  allowedRetentionDays: number[]
  defaultRetentionDays: number
  expiryAction: string
  allowCloudProcessing: boolean
  allowLocalProcessing: boolean
  requireExplicitConsent: boolean
  isActive: boolean
  policyVersion: string
  effectiveFrom: string
  effectiveUntil: string | null
  rowVersion: string
}

interface PrivacyConsent {
  id: string
  purpose: string
  providerClass: string
  policyVersion: string
  noticeVersion: string
  retentionPolicyId: string | null
  status: string
  grantedAt: string
  revokedAt: string | null
  expiresAt: string | null
  rowVersion: string
}

interface DataSubjectRequest {
  id: string
  requestType: string
  scope: string
  status: string
  requestedAt: string
  deadlineAt: string | null
  completedAt: string | null
  resultExpiresAt: string | null
  legalHoldDetected: boolean
  lastErrorCode: string | null
}

interface LegalHold {
  id: string
  subjectUserId: string | null
  entityType: string | null
  entityId: string | null
  status: string
  reason: string
  heldAt: string
  releasedAt: string | null
  rowVersion: string
}

interface PrivacyHealth {
  enabled: boolean
  enforcementEnabled: boolean
  workerEnabled: boolean
  status: string
  pendingRetentionActions: number
  failedRetentionActions: number
  pendingDataSubjectRequests: number
  failedDataSubjectRequests: number
}

const { currentUser, projects, selectedProject } = useDashboardContext()
const activeMode = ref<PrivacyMode>('consent')
const selectedProjectId = ref('')
const policies = ref<RetentionPolicy[]>([])
const consents = ref<PrivacyConsent[]>([])
const dataRequests = ref<DataSubjectRequest[]>([])
const legalHolds = ref<LegalHold[]>([])
const health = ref<PrivacyHealth | null>(null)
const isLoading = ref(false)
const isSubmitting = ref(false)
const loadError = ref('')

const policyForm = ref({
  name: 'Meeting data policy',
  allowedRetentionDays: [30, 90] as number[],
  defaultRetentionDays: 30,
  expiryAction: 'redact',
  allowCloudProcessing: false,
  allowLocalProcessing: true,
  requireExplicitConsent: true,
})
const consentForm = ref({
  retentionPolicyId: '',
  providerClass: 'local',
  expiresAt: '',
  accepted: false,
})
const requestForm = ref({ requestType: 'export', scope: 'all' })
const holdForm = ref({ subjectUserId: '', entityType: '', entityId: '', reason: '' })

const project = computed(() => projects.value.find((item: any) => item.id === selectedProjectId.value) ?? null)
const tenantId = computed(() => project.value?.organizationId || project.value?.id || '')
const activePolicies = computed(() => policies.value.filter(item => item.isActive))
const selectedPolicy = computed(() => activePolicies.value.find(item => item.id === consentForm.value.retentionPolicyId) ?? null)
const canManage = computed(() => {
  const role = String(currentUser.value?.role || '').toLowerCase()
  return role === 'admin' || role === 'owner' || role === 'manager' || project.value?.ownerId === currentUser.value?.id
})

function statusLabel(status: string) {
  return status.replaceAll('_', ' ')
}

function formatDate(value: string | null) {
  return value ? new Date(value).toLocaleString('vi-VN') : 'Chưa có'
}

function unwrapError(payload: unknown) {
  if (payload && typeof payload === 'object' && 'error' in payload) return String((payload as any).error || '')
  return ''
}

async function loadPrivacy() {
  if (!selectedProjectId.value || !tenantId.value) return false
  isLoading.value = true
  loadError.value = ''
  try {
    const policyResult = await apiResult<RetentionPolicy[]>(
      `/api/privacy/policies?tenantId=${encodeURIComponent(tenantId.value)}&projectId=${encodeURIComponent(selectedProjectId.value)}`,
    )
    policies.value = policyResult
    if (!activePolicies.value.some(item => item.id === consentForm.value.retentionPolicyId)) {
      consentForm.value.retentionPolicyId = activePolicies.value[0]?.id || ''
    }

    const [consentResult, requestResult, holdResult, healthResult] = await Promise.allSettled([
      apiResult<PrivacyConsent[]>(`/api/privacy/consents?projectId=${encodeURIComponent(selectedProjectId.value)}`),
      apiResult<DataSubjectRequest[]>(`/api/privacy/data-subject-requests?tenantId=${encodeURIComponent(tenantId.value)}`),
      apiResult<LegalHold[]>(`/api/privacy/legal-holds?tenantId=${encodeURIComponent(tenantId.value)}`),
      apiResult<PrivacyHealth>('/api/privacy/health'),
    ])
    consents.value = consentResult.status === 'fulfilled' ? consentResult.value : []
    dataRequests.value = requestResult.status === 'fulfilled' ? requestResult.value : []
    legalHolds.value = holdResult.status === 'fulfilled' ? holdResult.value : []
    health.value = healthResult.status === 'fulfilled' ? healthResult.value : null
    return true
  } catch (error) {
    loadError.value = errorMessage(error, 'Không thể tải cấu hình quyền riêng tư.')
    return false
  } finally {
    isLoading.value = false
  }
}

async function createPolicy() {
  if (!project.value || !tenantId.value) return
  isSubmitting.value = true
  try {
    await apiResult<RetentionPolicy>('/api/privacy/policies', {
      method: 'POST',
      body: JSON.stringify({
        tenantId: tenantId.value,
        projectId: selectedProjectId.value,
        name: policyForm.value.name,
        dataClassification: 'sensitive_collaboration',
        purpose: 'meeting_action_extraction',
        allowedRetentionDays: policyForm.value.allowedRetentionDays,
        defaultRetentionDays: policyForm.value.defaultRetentionDays,
        expiryAction: policyForm.value.expiryAction,
        allowCloudProcessing: policyForm.value.allowCloudProcessing,
        allowLocalProcessing: policyForm.value.allowLocalProcessing,
        requireExplicitConsent: policyForm.value.requireExplicitConsent,
        approvalOwnerUserId: currentUser.value?.id || null,
        effectiveFrom: null,
        effectiveUntil: null,
      }),
    })
    const refreshed = await loadPrivacy()
    if (refreshed) {
      showSuccess('Đã tạo retention policy mới.')
    } else {
      showError('Đã tạo policy nhưng không thể làm mới dữ liệu.')
    }
  } catch (error) {
    showError(errorMessage(error, 'Không thể tạo retention policy.'))
  } finally {
    isSubmitting.value = false
  }
}

async function disablePolicy(policy: RetentionPolicy) {
  try {
    await apiResult<RetentionPolicy>(`/api/privacy/policies/${policy.id}/disable`, {
      method: 'POST',
      body: JSON.stringify({ reason: 'Disabled from privacy settings', rowVersion: policy.rowVersion }),
    })
    const refreshed = await loadPrivacy()
    if (refreshed) {
      showSuccess('Policy đã được ngừng áp dụng.')
    } else {
      showError('Đã ngừng policy nhưng không thể làm mới dữ liệu.')
    }
  } catch (error) {
    showError(errorMessage(error, 'Không thể ngừng policy.'))
  }
}

async function grantConsent() {
  if (!selectedPolicy.value || !consentForm.value.accepted) {
    showError('Bạn cần chọn policy và xác nhận nội dung consent.')
    return
  }

  isSubmitting.value = true
  try {
    await apiResult<PrivacyConsent>('/api/privacy/consents', {
      method: 'POST',
      body: JSON.stringify({
        projectId: selectedProjectId.value,
        retentionPolicyId: selectedPolicy.value.id,
        purpose: 'meeting_action_extraction',
        providerClass: consentForm.value.providerClass,
        sourceType: 'meeting',
        sourceEntityId: null,
        noticeVersion: 'qaly-meeting-privacy-v4.0',
        expiresAt: consentForm.value.expiresAt || null,
      }),
    })
    consentForm.value.accepted = false
    const refreshed = await loadPrivacy()
    if (refreshed) {
      showSuccess('Consent đã được ghi nhận.')
    } else {
      showError('Đã ghi nhận consent nhưng không thể làm mới dữ liệu.')
    }
  } catch (error) {
    showError(errorMessage(error, 'Không thể ghi nhận consent.'))
  } finally {
    isSubmitting.value = false
  }
}

async function revokeConsent(consent: PrivacyConsent) {
  try {
    await apiResult<PrivacyConsent>(`/api/privacy/consents/${consent.id}/revoke`, {
      method: 'POST',
      body: JSON.stringify({ reason: 'Revoked by data subject', rowVersion: consent.rowVersion }),
    })
    const refreshed = await loadPrivacy()
    if (refreshed) {
      showSuccess('Consent đã được thu hồi.')
    } else {
      showError('Đã thu hồi consent nhưng không thể làm mới dữ liệu.')
    }
  } catch (error) {
    showError(errorMessage(error, 'Không thể thu hồi consent.'))
  }
}

async function submitDataRequest() {
  if (!tenantId.value) return
  isSubmitting.value = true
  try {
    await apiResult<DataSubjectRequest>('/api/privacy/data-subject-requests', {
      method: 'POST',
      headers: { 'Idempotency-Key': crypto.randomUUID() },
      body: JSON.stringify({
        tenantId: tenantId.value,
        projectId: requestForm.value.scope === 'project' ? selectedProjectId.value : null,
        subjectUserId: null,
        requestType: requestForm.value.requestType,
        scope: requestForm.value.scope,
        idempotencyKey: crypto.randomUUID(),
      }),
    })
    const refreshed = await loadPrivacy()
    if (refreshed) {
      showSuccess('Yêu cầu dữ liệu đã được gửi.')
    } else {
      showError('Đã gửi yêu cầu nhưng không thể làm mới dữ liệu.')
    }
  } catch (error) {
    showError(errorMessage(error, 'Không thể gửi yêu cầu dữ liệu.'))
  } finally {
    isSubmitting.value = false
  }
}

async function downloadExport(request: DataSubjectRequest) {
  try {
    const response = await fetch(`/api/privacy/data-subject-requests/${request.id}/download`, {
      credentials: 'same-origin',
      cache: 'no-store',
    })
    if (!response.ok) {
      const payload = await response.json().catch(() => null)
      throw new Error(unwrapError(payload) || `Không thể tải bản xuất (${response.status}).`)
    }
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = response.headers.get('Content-Disposition')?.match(/filename="?([^";]+)"?/)?.[1] || `qaly-dsar-${request.id}.json`
    anchor.click()
    URL.revokeObjectURL(url)
  } catch (error) {
    showError(errorMessage(error, 'Không thể tải bản xuất dữ liệu.'))
  }
}

async function createLegalHold() {
  if (!tenantId.value) return
  isSubmitting.value = true
  try {
    await apiResult<LegalHold>('/api/privacy/legal-holds', {
      method: 'POST',
      body: JSON.stringify({
        tenantId: tenantId.value,
        projectId: selectedProjectId.value,
        subjectUserId: holdForm.value.subjectUserId || null,
        entityType: holdForm.value.entityType || null,
        entityId: holdForm.value.entityId || null,
        reason: holdForm.value.reason,
      }),
    })
    holdForm.value = { subjectUserId: '', entityType: '', entityId: '', reason: '' }
    const refreshed = await loadPrivacy()
    if (refreshed) {
      showSuccess('Legal hold đã được tạo.')
    } else {
      showError('Đã tạo legal hold nhưng không thể làm mới dữ liệu.')
    }
  } catch (error) {
    showError(errorMessage(error, 'Không thể tạo legal hold.'))
  } finally {
    isSubmitting.value = false
  }
}

async function releaseLegalHold(hold: LegalHold) {
  try {
    await apiResult<LegalHold>(`/api/privacy/legal-holds/${hold.id}/release`, {
      method: 'POST',
      body: JSON.stringify({ reason: 'Released from privacy settings', rowVersion: hold.rowVersion }),
    })
    const refreshed = await loadPrivacy()
    if (refreshed) {
      showSuccess('Legal hold đã được giải phóng.')
    } else {
      showError('Đã giải phóng legal hold nhưng không thể làm mới dữ liệu.')
    }
  } catch (error) {
    showError(errorMessage(error, 'Không thể giải phóng legal hold.'))
  }
}

watch(selectedProjectId, loadPrivacy)

watch(
  [
    () => projects.value.map((item: any) => item.id).join('|'),
    () => selectedProject.value?.id || '',
  ],
  () => {
    if (projects.value.some((item: any) => item.id === selectedProjectId.value)) return

    const preferredId = selectedProject.value?.id || ''
    selectedProjectId.value = projects.value.some((item: any) => item.id === preferredId)
      ? preferredId
      : projects.value[0]?.id || ''
  },
  { immediate: true },
)
</script>

<template>
  <section class="privacy-surface">
    <header class="privacy-header">
      <div>
        <div class="privacy-title"><ShieldCheck :size="20" /><h3>Quyền riêng tư & dữ liệu</h3></div>
        <div v-if="health" class="health-line" :class="`health-${health.status}`">
          <span>{{ statusLabel(health.status) }}</span>
          <span>{{ health.pendingRetentionActions + health.pendingDataSubjectRequests }} đang chờ</span>
          <span>{{ health.failedRetentionActions + health.failedDataSubjectRequests }} lỗi</span>
        </div>
      </div>
      <div class="privacy-header-actions">
        <select v-model="selectedProjectId" aria-label="Dự án áp dụng">
          <option v-for="item in projects" :key="item.id" :value="item.id">{{ item.name }}</option>
        </select>
        <button class="icon-button" type="button" title="Tải lại" :disabled="isLoading" @click="loadPrivacy">
          <RefreshCw :size="17" :class="{ spinning: isLoading }" />
        </button>
      </div>
    </header>

    <div class="privacy-modes" role="tablist" aria-label="Privacy views">
      <button :class="{ active: activeMode === 'consent' }" @click="activeMode = 'consent'"><LockKeyhole :size="16" /> Consent</button>
      <button :class="{ active: activeMode === 'policies' }" @click="activeMode = 'policies'"><FileClock :size="16" /> Retention</button>
      <button :class="{ active: activeMode === 'requests' }" @click="activeMode = 'requests'"><Download :size="16" /> Data requests</button>
      <button v-if="canManage" :class="{ active: activeMode === 'holds' }" @click="activeMode = 'holds'"><Scale :size="16" /> Legal holds</button>
    </div>

    <div v-if="loadError" class="privacy-error"><AlertTriangle :size="17" />{{ loadError }}</div>
    <div v-if="isLoading" class="privacy-loading"><Loader2 :size="20" class="spinning" /> Đang tải...</div>

    <template v-else-if="activeMode === 'consent'">
      <div class="privacy-editor">
        <div class="field-grid">
          <label>Retention policy
            <select v-model="consentForm.retentionPolicyId">
              <option value="" disabled>Chọn policy</option>
              <option v-for="policy in activePolicies" :key="policy.id" :value="policy.id">
                {{ policy.name }} · {{ policy.defaultRetentionDays }} ngày
              </option>
            </select>
          </label>
          <label>Provider
            <select v-model="consentForm.providerClass">
              <option value="local">Chỉ xử lý cục bộ</option>
              <option value="any" :disabled="!selectedPolicy?.allowCloudProcessing">Cho phép cloud và local</option>
            </select>
          </label>
          <label>Ngày hết hạn consent
            <input v-model="consentForm.expiresAt" type="datetime-local" />
          </label>
        </div>
        <label class="consent-notice">
          <input v-model="consentForm.accepted" type="checkbox" />
          <span>Tôi đồng ý xử lý dữ liệu cuộc họp để trích xuất action item theo policy đã chọn, trong thời hạn lưu trữ hiển thị và với provider đã chọn.</span>
        </label>
        <button class="action-button" :disabled="isSubmitting || !activePolicies.length" @click="grantConsent">
          <CheckCircle2 :size="17" /> Ghi nhận consent
        </button>
      </div>

      <div class="record-list">
        <div v-for="consent in consents" :key="consent.id" class="record-row">
          <div class="record-icon"><Server v-if="consent.providerClass === 'local'" :size="17" /><Cloud v-else :size="17" /></div>
          <div class="record-main">
            <strong>{{ consent.purpose }}</strong>
            <span>{{ consent.providerClass }} · {{ consent.policyVersion }} · {{ formatDate(consent.grantedAt) }}</span>
          </div>
          <span class="state-tag" :class="`state-${consent.status}`">{{ statusLabel(consent.status) }}</span>
          <button v-if="consent.status === 'granted'" class="icon-button danger" title="Thu hồi consent" @click="revokeConsent(consent)">
            <Trash2 :size="16" />
          </button>
        </div>
        <div v-if="!consents.length" class="empty-row">Chưa có consent cho dự án này.</div>
      </div>
    </template>

    <template v-else-if="activeMode === 'policies'">
      <div v-if="canManage" class="privacy-editor">
        <div class="field-grid">
          <label>Tên policy<input v-model="policyForm.name" maxlength="120" /></label>
          <label>Thời hạn mặc định
            <select v-model="policyForm.defaultRetentionDays">
              <option v-for="day in policyForm.allowedRetentionDays" :key="day" :value="day">{{ day }} ngày</option>
            </select>
          </label>
          <label>Hành động khi hết hạn
            <select v-model="policyForm.expiryAction">
              <option value="redact">Redact nội dung</option>
              <option value="delete">Xóa nội dung</option>
              <option value="review">Yêu cầu review</option>
            </select>
          </label>
        </div>
        <div class="check-row">
          <label><input v-model="policyForm.allowLocalProcessing" type="checkbox" /> Local</label>
          <label><input v-model="policyForm.allowCloudProcessing" type="checkbox" /> Cloud</label>
          <label><input v-model="policyForm.requireExplicitConsent" type="checkbox" /> Bắt buộc consent</label>
        </div>
        <button class="action-button" :disabled="isSubmitting" @click="createPolicy"><Plus :size="17" /> Tạo policy</button>
      </div>

      <div class="record-list">
        <div v-for="policy in policies" :key="policy.id" class="record-row">
          <div class="record-icon"><FileClock :size="17" /></div>
          <div class="record-main">
            <strong>{{ policy.name }}</strong>
            <span>{{ policy.defaultRetentionDays }} ngày · {{ policy.expiryAction }} · {{ policy.allowCloudProcessing ? 'cloud + local' : 'local' }}</span>
          </div>
          <span class="state-tag" :class="policy.isActive ? 'state-granted' : 'state-revoked'">{{ policy.isActive ? 'active' : 'inactive' }}</span>
          <button v-if="canManage && policy.isActive" class="icon-button danger" title="Ngừng policy" @click="disablePolicy(policy)"><Ban :size="16" /></button>
        </div>
        <div v-if="!policies.length" class="empty-row">Chưa có retention policy.</div>
      </div>
    </template>

    <template v-else-if="activeMode === 'requests'">
      <div class="privacy-editor inline-editor">
        <label>Loại yêu cầu
          <select v-model="requestForm.requestType"><option value="export">Export</option><option value="delete">Delete</option></select>
        </label>
        <label>Phạm vi
          <select v-model="requestForm.scope"><option value="all">Toàn tenant</option><option value="project">Dự án hiện tại</option><option value="meetings">Meeting</option><option value="ai">AI data</option></select>
        </label>
        <button class="action-button" :disabled="isSubmitting" @click="submitDataRequest"><Plus :size="17" /> Gửi yêu cầu</button>
      </div>

      <div class="record-list">
        <div v-for="request in dataRequests" :key="request.id" class="record-row">
          <div class="record-icon"><Download v-if="request.requestType === 'export'" :size="17" /><Trash2 v-else :size="17" /></div>
          <div class="record-main">
            <strong>{{ request.requestType }} · {{ request.id.slice(0, 8) }}</strong>
            <span>{{ formatDate(request.requestedAt) }} · deadline {{ formatDate(request.deadlineAt) }}</span>
          </div>
          <span class="state-tag" :class="`state-${request.status}`">{{ statusLabel(request.status) }}</span>
          <button v-if="request.requestType === 'export' && ['completed', 'partially_completed'].includes(request.status)" class="icon-button" title="Tải bản xuất" @click="downloadExport(request)"><Download :size="16" /></button>
        </div>
        <div v-if="!dataRequests.length" class="empty-row">Chưa có yêu cầu dữ liệu.</div>
      </div>
    </template>

    <template v-else>
      <div class="privacy-editor">
        <div class="field-grid">
          <label>Subject user ID<input v-model="holdForm.subjectUserId" placeholder="UUID" /></label>
          <label>Entity type<input v-model="holdForm.entityType" placeholder="MeetingImport" /></label>
          <label>Entity ID<input v-model="holdForm.entityId" placeholder="UUID" /></label>
          <label>Lý do<input v-model="holdForm.reason" maxlength="1000" /></label>
        </div>
        <button class="action-button" :disabled="isSubmitting" @click="createLegalHold"><Scale :size="17" /> Tạo legal hold</button>
      </div>
      <div class="record-list">
        <div v-for="hold in legalHolds" :key="hold.id" class="record-row">
          <div class="record-icon"><Scale :size="17" /></div>
          <div class="record-main"><strong>{{ hold.entityType || 'Subject hold' }}</strong><span>{{ hold.reason }} · {{ formatDate(hold.heldAt) }}</span></div>
          <span class="state-tag" :class="hold.status === 'active' ? 'state-failed' : 'state-revoked'">{{ hold.status }}</span>
          <button v-if="hold.status === 'active'" class="icon-button" title="Giải phóng legal hold" @click="releaseLegalHold(hold)"><CheckCircle2 :size="16" /></button>
        </div>
        <div v-if="!legalHolds.length" class="empty-row">Không có legal hold.</div>
      </div>
    </template>
  </section>
</template>

<style scoped>
.privacy-surface { display: flex; flex-direction: column; gap: 18px; }
.privacy-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; }
.privacy-title { display: flex; align-items: center; gap: 10px; color: var(--primary); }
.privacy-title h3 { margin: 0; font-size: 16px; color: var(--text-strong); }
.privacy-header-actions { display: flex; gap: 8px; align-items: center; }
.privacy-header select, .privacy-editor select, .privacy-editor input { min-height: 38px; border: 1px solid var(--line); border-radius: 6px; background: var(--panel-soft); color: var(--text); padding: 8px 10px; }
.health-line { display: flex; gap: 12px; margin-top: 7px; font-size: 11px; color: var(--muted); }
.health-healthy span:first-child { color: var(--success); }
.health-degraded_worker_disabled span:first-child, .health-degraded_failures span:first-child { color: var(--warning); }
.privacy-modes { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); border-bottom: 1px solid var(--line); }
.privacy-modes button { display: flex; align-items: center; justify-content: center; gap: 7px; min-height: 42px; border: 0; border-bottom: 2px solid transparent; background: transparent; color: var(--muted); cursor: pointer; font-weight: 700; }
.privacy-modes button.active { color: var(--primary); border-bottom-color: var(--primary); }
.privacy-error, .privacy-loading { display: flex; align-items: center; gap: 8px; min-height: 44px; padding: 10px 12px; border: 1px solid var(--line); border-radius: 6px; color: var(--muted); }
.privacy-error { color: var(--danger); border-color: color-mix(in srgb, var(--danger) 35%, var(--line)); }
.privacy-editor { display: flex; flex-direction: column; gap: 14px; padding: 16px 0; border-bottom: 1px solid var(--line); }
.field-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
.privacy-editor label { display: flex; flex-direction: column; gap: 6px; color: var(--text-strong); font-size: 12px; font-weight: 700; }
.consent-notice { flex-direction: row !important; align-items: flex-start; line-height: 1.5; font-weight: 500 !important; color: var(--text) !important; }
.consent-notice input, .check-row input { width: 17px; height: 17px; accent-color: var(--primary); }
.check-row { display: flex; gap: 18px; flex-wrap: wrap; }
.check-row label { flex-direction: row; align-items: center; }
.inline-editor { display: grid; grid-template-columns: 1fr 1fr auto; align-items: end; }
.action-button { display: inline-flex; align-items: center; justify-content: center; gap: 8px; min-height: 38px; width: fit-content; border: 0; border-radius: 6px; padding: 8px 14px; background: var(--primary); color: white; font-weight: 750; cursor: pointer; }
.action-button:disabled { opacity: .5; cursor: not-allowed; }
.icon-button { display: inline-grid; place-items: center; width: 36px; height: 36px; flex: 0 0 36px; border: 1px solid var(--line); border-radius: 6px; background: var(--panel-soft); color: var(--text); cursor: pointer; }
.icon-button:hover { color: var(--primary); border-color: var(--primary); }
.icon-button.danger:hover { color: var(--danger); border-color: var(--danger); }
.record-list { display: flex; flex-direction: column; }
.record-row { display: grid; grid-template-columns: 36px minmax(0, 1fr) auto 36px; align-items: center; gap: 12px; min-height: 62px; border-bottom: 1px solid var(--line-light); }
.record-icon { display: grid; place-items: center; width: 32px; height: 32px; border-radius: 6px; background: var(--primary-soft); color: var(--primary); }
.record-main { display: flex; flex-direction: column; min-width: 0; gap: 3px; }
.record-main strong { color: var(--text-strong); font-size: 13px; overflow-wrap: anywhere; }
.record-main span { color: var(--muted); font-size: 11px; overflow-wrap: anywhere; }
.state-tag { padding: 3px 7px; border-radius: 5px; background: var(--panel-soft); color: var(--muted); font-size: 10px; font-weight: 800; text-transform: uppercase; white-space: nowrap; }
.state-granted, .state-completed { color: var(--success); background: color-mix(in srgb, var(--success) 12%, transparent); }
.state-revoked, .state-rejected { color: var(--muted); }
.state-failed, .state-review_required { color: var(--danger); background: color-mix(in srgb, var(--danger) 10%, transparent); }
.state-submitted, .state-accepted, .state-collecting, .state-partially_completed { color: var(--warning); background: color-mix(in srgb, var(--warning) 11%, transparent); }
.empty-row { padding: 28px 8px; text-align: center; color: var(--muted); font-size: 12px; }
.spinning { animation: spin 1s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }
@media (max-width: 760px) {
  .privacy-header { flex-direction: column; }
  .privacy-header-actions { width: 100%; }
  .privacy-header select { min-width: 0; flex: 1; }
  .privacy-modes { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .field-grid, .inline-editor { grid-template-columns: 1fr; }
  .record-row { grid-template-columns: 36px minmax(0, 1fr) 36px; }
  .record-row .state-tag { grid-column: 2; width: fit-content; }
}
</style>
