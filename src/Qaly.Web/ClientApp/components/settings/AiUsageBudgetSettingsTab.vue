<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import {
  AlertTriangle,
  BarChart3,
  Bot,
  Check,
  CircleDollarSign,
  Database,
  ExternalLink,
  LockKeyhole,
  RefreshCw,
  Save,
  ServerCog,
  ShieldCheck,
  X,
} from 'lucide-vue-next'
import { ApiError, apiResult, errorMessage } from '../../utils/api-client'
import { showError, showSuccess } from '../../composables/use-toast'

const props = defineProps<{ organizationId?: string }>()

type BudgetScope = {
  scopeType: 'organization' | 'project'
  scopeId: string
  organizationId: string | null
  projectId: string | null
  name: string
}

type UsageBreakdown = {
  key: string
  attemptCount: number
  succeededCount: number
  failedCount: number
  inputTokens: number
  outputTokens: number
  estimatedCostUsd: number
  effectiveCostUsd: number
  cacheHitCount: number
}

type UsageSnapshot = {
  scopeType: string
  scopeId: string
  organizationId: string | null
  projectId: string | null
  scopeName: string
  from: string
  to: string
  attemptCount: number
  succeededCount: number
  failedCount: number
  inputTokens: number
  outputTokens: number
  estimatedCostUsd: number
  effectiveCostUsd: number
  actualCostCount: number
  estimatedOnlyCount: number
  cacheHitCount: number
  daily: UsageBreakdown[]
  byProvider: UsageBreakdown[]
  byFunction: UsageBreakdown[]
  byStatus: UsageBreakdown[]
  byCache: UsageBreakdown[]
  calculatedAt: string
}

type BudgetSnapshot = {
  scopeType: string
  scopeId: string
  organizationId: string | null
  projectId: string | null
  scopeName: string
  policyId: string | null
  effectivePolicyId: string | null
  policySource: 'organization' | 'project' | 'none'
  isInherited: boolean
  hasEffectivePolicy: boolean
  canEdit: boolean
  editingEnabled: boolean
  dailyBudgetUsd: number
  monthlyBudgetUsd: number
  warningAtPercent: number
  hardStopEnabled: boolean
  dailyUsageUsd: number
  monthlyUsageUsd: number
  dailyRemainingUsd: number
  monthlyRemainingUsd: number
  warningActive: boolean
  hardStopActive: boolean
  allowCloudForSensitive: boolean
  version: string | null
  effectiveVersion: string | null
  calculatedAt: string
}

type PlatformHealth = {
  status: string
  platformEnabled: boolean
  workerEnabled: boolean
  degradedReason: string | null
  checkedAt: string
}

const scopes = ref<BudgetScope[]>([])
const selectedScopeKey = ref('')
const rangeDays = ref(30)
const usage = ref<UsageSnapshot | null>(null)
const budget = ref<BudgetSnapshot | null>(null)
const health = ref<PlatformHealth | null>(null)
const isLoadingScopes = ref(true)
const isLoadingSnapshot = ref(false)
const isSaving = ref(false)
const permissionDenied = ref(false)
const loadError = ref('')
const healthError = ref('')
const conflictMessage = ref('')
const reviewOpen = ref(false)
const confirmationChecked = ref(false)
const requestSequence = ref(0)

const policyForm = ref({
  dailyBudgetUsd: 2,
  monthlyBudgetUsd: 30,
  warningAtPercent: 80,
  hardStopEnabled: true,
  allowCloudForSensitive: false,
})

const selectedScope = computed(() =>
  scopes.value.find(scope => scopeKey(scope) === selectedScopeKey.value) ?? null,
)
const canSubmit = computed(() =>
  Boolean(
    budget.value?.canEdit &&
    budget.value?.editingEnabled &&
    confirmationChecked.value &&
    !isSaving.value,
  ),
)
const cacheRate = computed(() =>
  usage.value?.attemptCount
    ? Math.round((usage.value.cacheHitCount / usage.value.attemptCount) * 100)
    : 0,
)
const totalTokens = computed(() => (usage.value?.inputTokens ?? 0) + (usage.value?.outputTokens ?? 0))
const dailyPercent = computed(() => budgetPercent(budget.value?.dailyUsageUsd, budget.value?.dailyBudgetUsd))
const monthlyPercent = computed(() => budgetPercent(budget.value?.monthlyUsageUsd, budget.value?.monthlyBudgetUsd))
const policySourceLabel = computed(() => {
  if (!budget.value?.hasEffectivePolicy) return 'Chưa có policy'
  if (budget.value.isInherited) return 'Kế thừa từ tổ chức'
  return budget.value.policySource === 'organization' ? 'Policy tổ chức' : 'Policy dự án'
})

function scopeKey(scope: BudgetScope) {
  return `${scope.scopeType}:${scope.scopeId}`
}

function scopeQuery(scope: BudgetScope) {
  const params = new URLSearchParams()
  if (scope.organizationId) params.set('organizationId', scope.organizationId)
  if (scope.projectId) params.set('projectId', scope.projectId)
  return params
}

function formatUsd(value: number | null | undefined) {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 2,
    maximumFractionDigits: 6,
  }).format(Number(value ?? 0))
}

function formatNumber(value: number | null | undefined) {
  return new Intl.NumberFormat('vi-VN').format(Number(value ?? 0))
}

function formatDate(value: string | null | undefined) {
  if (!value) return '—'
  return new Date(value).toLocaleString('vi-VN')
}

function budgetPercent(used?: number, limit?: number) {
  if (!limit || limit <= 0) return 0
  return Math.min(100, Math.max(0, Math.round(((used ?? 0) / limit) * 100)))
}

function resetReview() {
  reviewOpen.value = false
  confirmationChecked.value = false
  conflictMessage.value = ''
}

function syncPolicyForm(snapshot: BudgetSnapshot) {
  policyForm.value = {
    dailyBudgetUsd: snapshot.hasEffectivePolicy ? snapshot.dailyBudgetUsd : 2,
    monthlyBudgetUsd: snapshot.hasEffectivePolicy ? snapshot.monthlyBudgetUsd : 30,
    warningAtPercent: snapshot.warningAtPercent || 80,
    hardStopEnabled: snapshot.hasEffectivePolicy ? snapshot.hardStopEnabled : true,
    allowCloudForSensitive: snapshot.hasEffectivePolicy ? snapshot.allowCloudForSensitive : false,
  }
}

async function loadScopes() {
  isLoadingScopes.value = true
  permissionDenied.value = false
  loadError.value = ''
  try {
    const loadedScopes = await apiResult<BudgetScope[]>('/api/ai/budget/scopes')
    scopes.value = props.organizationId
      ? loadedScopes.filter(scope => scope.scopeType === 'organization' && scope.organizationId === props.organizationId)
      : loadedScopes
    if (!scopes.value.length) {
      permissionDenied.value = true
      return
    }

    const currentStillExists = scopes.value.some(scope => scopeKey(scope) === selectedScopeKey.value)
    if (!currentStillExists) {
      selectedScopeKey.value = scopeKey(
        scopes.value.find(scope => scope.scopeType === 'organization') ?? scopes.value[0],
      )
    }
    await loadSnapshot()
  } catch (error) {
    permissionDenied.value = error instanceof ApiError && [403, 404].includes(error.status)
    if (!permissionDenied.value) {
      loadError.value = errorMessage(error, 'Không thể tải phạm vi quản trị AI.')
    }
  } finally {
    isLoadingScopes.value = false
  }
}

async function loadSnapshot() {
  const scope = selectedScope.value
  if (!scope) return

  const sequence = ++requestSequence.value
  isLoadingSnapshot.value = true
  loadError.value = ''
  healthError.value = ''
  resetReview()

  const params = scopeQuery(scope)
  const now = new Date()
  const from = new Date(now.getTime() - rangeDays.value * 24 * 60 * 60 * 1000)
  params.set('from', from.toISOString())
  params.set('to', now.toISOString())
  const budgetParams = scopeQuery(scope)

  try {
    const [nextUsage, nextBudget, nextHealth] = await Promise.all([
      apiResult<UsageSnapshot>(`/api/ai/usage?${params}`),
      apiResult<BudgetSnapshot>(`/api/ai/budget?${budgetParams}`),
      apiResult<PlatformHealth>('/api/ai/health').catch(error => {
        healthError.value = errorMessage(error, 'Không đọc được trạng thái AI platform.')
        return null
      }),
    ])
    if (sequence !== requestSequence.value) return
    usage.value = nextUsage
    budget.value = nextBudget
    health.value = nextHealth
    syncPolicyForm(nextBudget)
  } catch (error) {
    if (sequence !== requestSequence.value) return
    usage.value = null
    budget.value = null
    loadError.value = errorMessage(error, 'Không thể tải usage và budget AI.')
  } finally {
    if (sequence === requestSequence.value) isLoadingSnapshot.value = false
  }
}

function openReview() {
  conflictMessage.value = ''
  const form = policyForm.value
  if (
    form.dailyBudgetUsd <= 0 ||
    form.monthlyBudgetUsd <= 0 ||
    form.dailyBudgetUsd > form.monthlyBudgetUsd ||
    form.monthlyBudgetUsd > 1_000_000 ||
    form.warningAtPercent < 1 ||
    form.warningAtPercent > 100
  ) {
    showError('Daily budget phải dương và không vượt monthly budget; ngưỡng cảnh báo phải từ 1–100%.')
    return
  }

  reviewOpen.value = true
  confirmationChecked.value = false
}

async function savePolicy() {
  const scope = selectedScope.value
  const currentBudget = budget.value
  if (!scope || !currentBudget || !canSubmit.value) return

  isSaving.value = true
  conflictMessage.value = ''
  try {
    await apiResult<BudgetSnapshot>(`/api/ai/budget?${scopeQuery(scope)}`, {
      method: 'PUT',
      body: JSON.stringify({
        ...policyForm.value,
        version: currentBudget.version,
        confirmed: true,
      }),
    })
    showSuccess('Đã cập nhật AI budget policy và ghi audit.')
    await loadSnapshot()
  } catch (error) {
    if (error instanceof ApiError && error.status === 409) {
      conflictMessage.value = 'Policy đã thay đổi ở phiên khác. Dữ liệu mới nhất đã được tải lại; hãy kiểm tra và xác nhận lại.'
      await loadSnapshot()
      conflictMessage.value = 'Policy đã thay đổi ở phiên khác. Dữ liệu mới nhất đã được tải lại; hãy kiểm tra và xác nhận lại.'
    } else {
      showError(errorMessage(error, 'Không thể cập nhật AI budget policy.'))
    }
  } finally {
    isSaving.value = false
  }
}

watch(selectedScopeKey, (next, previous) => {
  if (next && previous && next !== previous) loadSnapshot()
})

watch(rangeDays, () => {
  if (selectedScope.value) loadSnapshot()
})

watch(() => props.organizationId, () => {
  selectedScopeKey.value = ''
  void loadScopes()
})

onMounted(loadScopes)
</script>

<template>
  <section class="ai-budget-shell" data-testid="ai-usage-budget-settings">
    <div class="ai-budget-heading">
      <div>
        <span class="eyebrow"><Bot :size="14" /> AI platform control</span>
        <h3>AI Usage &amp; Budget</h3>
        <p>
          Chi phí được tính deterministic từ usage ledger; hệ thống không dùng LLM để tự suy đoán số liệu
          và không tự thay đổi policy.
        </p>
      </div>
      <button type="button"
        class="secondary-button"
        :disabled="isLoadingScopes || isLoadingSnapshot"
        data-testid="refresh-ai-budget"
        @click="loadScopes"
      >
        <RefreshCw :size="16" :class="{ spin: isLoadingScopes || isLoadingSnapshot }" />
        Làm mới
      </button>
    </div>

    <div v-if="isLoadingScopes" class="state-panel" data-testid="ai-budget-loading">
      <RefreshCw :size="24" class="spin" />
      <strong>Đang tải quyền và phạm vi budget…</strong>
    </div>

    <div v-else-if="permissionDenied" class="state-panel state-panel--locked" data-testid="ai-budget-permission">
      <LockKeyhole :size="28" />
      <strong>Bạn không có phạm vi AI budget được phép quản trị.</strong>
      <p>Chỉ System Admin, Organization Owner/Admin/Billing Admin hoặc Project Owner/Manager được xem dữ liệu này.</p>
    </div>

    <div v-else-if="loadError && !budget" class="state-panel state-panel--error" role="alert" data-testid="ai-budget-error">
      <AlertTriangle :size="28" />
      <strong>Không thể tải dữ liệu AI platform.</strong>
      <p>{{ loadError }}</p>
      <button type="button" class="secondary-button" @click="loadSnapshot">Thử lại</button>
    </div>

    <template v-else>
      <div class="scope-toolbar">
        <label>
          Phạm vi quản trị
          <select v-model="selectedScopeKey" data-testid="ai-budget-scope-select">
            <optgroup label="Tổ chức">
              <option
                v-for="scope in scopes.filter(item => item.scopeType === 'organization')"
                :key="scopeKey(scope)"
                :value="scopeKey(scope)"
              >
                {{ scope.name }}
              </option>
            </optgroup>
            <optgroup label="Dự án">
              <option
                v-for="scope in scopes.filter(item => item.scopeType === 'project')"
                :key="scopeKey(scope)"
                :value="scopeKey(scope)"
              >
                {{ scope.name }}
              </option>
            </optgroup>
          </select>
        </label>
        <label>
          Khoảng usage
          <select v-model.number="rangeDays" data-testid="ai-usage-range">
            <option :value="7">7 ngày</option>
            <option :value="30">30 ngày</option>
            <option :value="90">90 ngày</option>
          </select>
        </label>
        <a
          v-if="selectedScope?.projectId"
          class="source-link"
          :href="`/projects/${selectedScope.projectId}`"
        >
          Mở dự án <ExternalLink :size="14" />
        </a>
      </div>

      <div v-if="isLoadingSnapshot" class="state-panel" data-testid="ai-budget-snapshot-loading">
        <RefreshCw :size="24" class="spin" />
        <strong>Đang đối soát ledger và policy…</strong>
      </div>

      <template v-else-if="usage && budget">
        <div
          class="platform-state"
          :class="{ 'platform-state--degraded': health?.status !== 'healthy' || healthError }"
          data-testid="ai-platform-health"
        >
          <ServerCog :size="18" />
          <div>
            <strong>
              {{ health?.status === 'healthy' ? 'AI platform healthy' : 'AI platform degraded hoặc chưa xác minh' }}
            </strong>
            <span v-if="health">
              Platform {{ health.platformEnabled ? 'on' : 'off' }} · Worker {{ health.workerEnabled ? 'on' : 'off' }}
              <template v-if="health.degradedReason"> · {{ health.degradedReason }}</template>
            </span>
            <span v-else>{{ healthError }}</span>
          </div>
        </div>

        <div class="metric-grid">
          <article class="metric-card">
            <CircleDollarSign :size="19" />
            <span>Chi phí ghi nhận</span>
            <strong data-testid="ai-usage-cost">{{ formatUsd(usage.effectiveCostUsd) }}</strong>
            <small>
              {{ usage.actualCostCount }} actual · {{ usage.estimatedOnlyCount }} dùng estimated fallback
            </small>
          </article>
          <article class="metric-card">
            <BarChart3 :size="19" />
            <span>Provider attempts</span>
            <strong>{{ formatNumber(usage.attemptCount) }}</strong>
            <small>{{ usage.succeededCount }} thành công · {{ usage.failedCount }} thất bại</small>
          </article>
          <article class="metric-card">
            <Database :size="19" />
            <span>Tokens</span>
            <strong>{{ formatNumber(totalTokens) }}</strong>
            <small>{{ formatNumber(usage.inputTokens) }} input · {{ formatNumber(usage.outputTokens) }} output</small>
          </article>
          <article class="metric-card">
            <ShieldCheck :size="19" />
            <span>Cache hit</span>
            <strong>{{ cacheRate }}%</strong>
            <small>{{ usage.cacheHitCount }}/{{ usage.attemptCount }} attempts</small>
          </article>
        </div>

        <div v-if="usage.attemptCount === 0" class="state-panel state-panel--empty" data-testid="ai-usage-empty">
          <Database :size="26" />
          <strong>Chưa có usage trong khoảng đã chọn.</strong>
          <p>Không có cost hoặc token nào được bịa thêm; hãy đổi khoảng thời gian nếu cần.</p>
        </div>

        <div class="budget-grid">
          <article
            class="budget-card"
            :class="{ 'budget-card--warning': budget.warningActive, 'budget-card--blocked': budget.hardStopActive }"
            data-testid="ai-budget-status"
          >
            <div class="budget-card__header">
              <div>
                <span>Effective budget</span>
                <h4>{{ policySourceLabel }}</h4>
              </div>
              <span v-if="budget.hardStopActive" class="state-badge state-badge--danger">HARD STOP</span>
              <span v-else-if="budget.warningActive" class="state-badge state-badge--warning">WARNING</span>
              <span v-else class="state-badge state-badge--ok">WITHIN LIMIT</span>
            </div>

            <div v-if="!budget.hasEffectivePolicy" class="policy-empty">
              Chưa có policy: usage vẫn được ghi nhận nhưng chưa có giới hạn tự động. Lưu form bên cạnh để tạo policy.
            </div>
            <template v-else>
              <div class="budget-row">
                <div>
                  <strong>Hôm nay</strong>
                  <span>{{ formatUsd(budget.dailyUsageUsd) }} / {{ formatUsd(budget.dailyBudgetUsd) }}</span>
                </div>
                <div class="progress-track"><span :style="{ width: `${dailyPercent}%` }"></span></div>
                <small>Còn {{ formatUsd(budget.dailyRemainingUsd) }}</small>
              </div>
              <div class="budget-row">
                <div>
                  <strong>Tháng này</strong>
                  <span>{{ formatUsd(budget.monthlyUsageUsd) }} / {{ formatUsd(budget.monthlyBudgetUsd) }}</span>
                </div>
                <div class="progress-track"><span :style="{ width: `${monthlyPercent}%` }"></span></div>
                <small>Còn {{ formatUsd(budget.monthlyRemainingUsd) }}</small>
              </div>
              <p class="budget-note">
                Cảnh báo tại {{ budget.warningAtPercent }}%.
                {{ budget.hardStopEnabled ? 'Hard stop đang bật.' : 'Hard stop đang tắt; warning không chặn request.' }}
              </p>
            </template>
          </article>

          <article class="policy-card">
            <div class="policy-card__header">
              <div>
                <span>Policy editor</span>
                <h4>Giới hạn AI</h4>
              </div>
              <span v-if="!budget.editingEnabled" class="state-badge">READ ONLY</span>
            </div>
            <p v-if="budget.isInherited" class="inherit-note">
              Dự án đang kế thừa policy tổ chức. Lưu thay đổi sẽ tạo một project override riêng.
            </p>
            <p v-if="!budget.editingEnabled" class="inherit-note">
              Editing bị tắt bởi feature flag; ledger và effective policy vẫn ở chế độ đọc.
            </p>

            <div class="policy-form">
              <label>
                Daily budget (USD)
                <input v-model.number="policyForm.dailyBudgetUsd" type="number" min="0.01" step="0.01" :disabled="!budget.editingEnabled" />
              </label>
              <label>
                Monthly budget (USD)
                <input v-model.number="policyForm.monthlyBudgetUsd" type="number" min="0.01" max="1000000" step="0.01" :disabled="!budget.editingEnabled" />
              </label>
              <label>
                Warning threshold (%)
                <input v-model.number="policyForm.warningAtPercent" type="number" min="1" max="100" :disabled="!budget.editingEnabled" />
              </label>
              <label class="policy-check">
                <input v-model="policyForm.hardStopEnabled" type="checkbox" :disabled="!budget.editingEnabled" />
                <span><strong>Bật hard stop</strong><small>Chặn AI request mới khi daily hoặc monthly limit đã đạt.</small></span>
              </label>
              <label class="policy-check policy-check--sensitive">
                <input v-model="policyForm.allowCloudForSensitive" type="checkbox" :disabled="!budget.editingEnabled" />
                <span><strong>Cho phép cloud với dữ liệu sensitive</strong><small>Chỉ bật khi privacy/consent/provider policy tương ứng đã được phê duyệt.</small></span>
              </label>
            </div>
            <button type="button"
              class="primary-button"
              :disabled="!budget.canEdit || !budget.editingEnabled"
              data-testid="review-ai-budget-policy"
              @click="openReview"
            >
              <Save :size="16" /> Xem lại thay đổi
            </button>
          </article>
        </div>

        <div v-if="reviewOpen" class="review-panel" data-testid="ai-budget-review">
          <div class="review-panel__header">
            <div>
              <span>Human confirmation</span>
              <h4>Xác nhận AI budget policy</h4>
            </div>
            <button type="button" class="icon-button" aria-label="Đóng xác nhận" @click="resetReview"><X :size="18" /></button>
          </div>
          <dl>
            <div><dt>Daily</dt><dd>{{ formatUsd(budget.dailyBudgetUsd) }} → {{ formatUsd(policyForm.dailyBudgetUsd) }}</dd></div>
            <div><dt>Monthly</dt><dd>{{ formatUsd(budget.monthlyBudgetUsd) }} → {{ formatUsd(policyForm.monthlyBudgetUsd) }}</dd></div>
            <div><dt>Warning</dt><dd>{{ budget.warningAtPercent }}% → {{ policyForm.warningAtPercent }}%</dd></div>
            <div><dt>Hard stop</dt><dd>{{ budget.hardStopEnabled ? 'Bật' : 'Tắt' }} → {{ policyForm.hardStopEnabled ? 'Bật' : 'Tắt' }}</dd></div>
            <div><dt>Sensitive cloud</dt><dd>{{ budget.allowCloudForSensitive ? 'Cho phép' : 'Chặn' }} → {{ policyForm.allowCloudForSensitive ? 'Cho phép' : 'Chặn' }}</dd></div>
          </dl>
          <label class="confirm-row">
            <input v-model="confirmationChecked" type="checkbox" />
            Tôi đã kiểm tra phạm vi, giới hạn, hard-stop và tác động privacy; xác nhận ghi policy này.
          </label>
          <p v-if="conflictMessage" class="conflict-message" data-testid="ai-budget-conflict">{{ conflictMessage }}</p>
          <button type="button" class="primary-button" :disabled="!canSubmit" data-testid="confirm-ai-budget-policy" @click="savePolicy">
            <Check :size="16" /> {{ isSaving ? 'Đang lưu…' : 'Xác nhận và lưu' }}
          </button>
        </div>

        <div v-if="conflictMessage && !reviewOpen" class="conflict-banner" data-testid="ai-budget-conflict">
          <AlertTriangle :size="17" /> {{ conflictMessage }}
        </div>

        <div v-if="usage.attemptCount > 0" class="breakdown-grid">
          <article class="breakdown-card">
            <h4>Usage theo provider</h4>
            <div class="breakdown-table">
              <div class="breakdown-row breakdown-row--head"><span>Provider</span><span>Attempts</span><span>Cache</span><span>Cost</span></div>
              <div v-for="item in usage.byProvider" :key="item.key" class="breakdown-row">
                <strong>{{ item.key }}</strong>
                <span>{{ item.attemptCount }}</span>
                <span>{{ item.cacheHitCount }}</span>
                <span>{{ formatUsd(item.effectiveCostUsd) }}</span>
              </div>
            </div>
          </article>
          <article class="breakdown-card">
            <h4>Usage theo chức năng</h4>
            <div class="breakdown-table">
              <div class="breakdown-row breakdown-row--head"><span>Function</span><span>Success</span><span>Failed</span><span>Cost</span></div>
              <div v-for="item in usage.byFunction" :key="item.key" class="breakdown-row">
                <strong>{{ item.key }}</strong>
                <span>{{ item.succeededCount }}</span>
                <span>{{ item.failedCount }}</span>
                <span>{{ formatUsd(item.effectiveCostUsd) }}</span>
              </div>
            </div>
          </article>
        </div>

        <footer class="grounding-footer">
          <ShieldCheck :size="16" />
          <span>
            Nguồn: <strong>AiUsageLedger + effective AiBudgetPolicy</strong> · UTC
            {{ formatDate(usage.from) }} – {{ formatDate(usage.to) }} · đối soát {{ formatDate(usage.calculatedAt) }}
          </span>
        </footer>
      </template>
    </template>
  </section>
</template>

<style scoped>
.ai-budget-shell {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.ai-budget-heading,
.scope-toolbar,
.budget-card__header,
.policy-card__header,
.review-panel__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}

.ai-budget-heading h3,
.budget-card h4,
.policy-card h4,
.review-panel h4,
.breakdown-card h4 {
  margin: 0;
  color: var(--text-strong);
}

.ai-budget-heading p,
.state-panel p {
  margin: 6px 0 0;
  color: var(--muted);
  line-height: 1.55;
}

.eyebrow,
.budget-card__header span,
.policy-card__header span,
.review-panel__header span {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: var(--primary);
  font-size: 11px;
  font-weight: 800;
  letter-spacing: .08em;
  text-transform: uppercase;
}

.secondary-button,
.primary-button,
.icon-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  border-radius: 10px;
  border: 1px solid var(--line);
  padding: 10px 14px;
  font-weight: 750;
  cursor: pointer;
}

.secondary-button,
.icon-button {
  background: var(--panel-soft);
  color: var(--text);
}

.primary-button {
  background: var(--primary);
  color: white;
  border-color: var(--primary);
}

button:disabled {
  opacity: .55;
  cursor: not-allowed;
}

.icon-button {
  padding: 8px;
}

.state-panel {
  min-height: 150px;
  border: 1px dashed var(--line);
  border-radius: 16px;
  background: var(--panel-soft);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  gap: 8px;
  padding: 24px;
  color: var(--muted);
}

.state-panel--locked {
  color: #b45309;
  background: rgba(245, 158, 11, .06);
}

.state-panel--error {
  color: #dc2626;
  background: rgba(239, 68, 68, .06);
}

.scope-toolbar {
  align-items: flex-end;
  padding: 14px;
  background: var(--panel-soft);
  border: 1px solid var(--line);
  border-radius: 14px;
  flex-wrap: wrap;
}

.scope-toolbar label,
.policy-form label {
  display: flex;
  flex-direction: column;
  gap: 6px;
  color: var(--text-strong);
  font-size: 12px;
  font-weight: 700;
}

.scope-toolbar select,
.policy-form input[type="number"] {
  min-width: 190px;
  padding: 10px 12px;
  border: 1px solid var(--line);
  border-radius: 9px;
  background: var(--panel);
  color: var(--text);
}

.source-link {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: var(--primary);
  font-size: 12px;
  font-weight: 750;
  text-decoration: none;
  padding: 10px 0;
}

.platform-state {
  display: flex;
  align-items: center;
  gap: 12px;
  border: 1px solid rgba(16, 185, 129, .25);
  background: rgba(16, 185, 129, .07);
  border-radius: 12px;
  padding: 12px 14px;
  color: #047857;
}

.platform-state--degraded {
  border-color: rgba(245, 158, 11, .3);
  background: rgba(245, 158, 11, .08);
  color: #b45309;
}

.platform-state div {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.platform-state span {
  font-size: 12px;
}

.metric-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
}

.metric-card,
.budget-card,
.policy-card,
.breakdown-card,
.review-panel {
  border: 1px solid var(--line);
  border-radius: 16px;
  background: var(--panel);
  box-shadow: var(--qaly-shadow-sm);
}

.metric-card {
  padding: 16px;
  display: grid;
  grid-template-columns: auto 1fr;
  gap: 5px 9px;
}

.metric-card svg {
  grid-row: span 3;
  color: var(--primary);
}

.metric-card span,
.metric-card small,
.budget-row small,
.budget-note,
.inherit-note {
  color: var(--muted);
}

.metric-card strong {
  font-size: 20px;
  color: var(--text-strong);
}

.budget-grid,
.breakdown-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
}

.budget-card,
.policy-card,
.breakdown-card,
.review-panel {
  padding: 20px;
}

.budget-card--warning {
  border-color: rgba(245, 158, 11, .45);
}

.budget-card--blocked {
  border-color: rgba(239, 68, 68, .5);
}

.state-badge {
  border-radius: 999px;
  background: var(--panel-soft);
  color: var(--muted) !important;
  padding: 5px 9px;
  font-size: 10px !important;
}

.state-badge--ok {
  background: rgba(16, 185, 129, .12);
  color: #047857 !important;
}

.state-badge--warning {
  background: rgba(245, 158, 11, .14);
  color: #b45309 !important;
}

.state-badge--danger {
  background: rgba(239, 68, 68, .13);
  color: #dc2626 !important;
}

.budget-row {
  margin-top: 22px;
}

.budget-row > div:first-child {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  font-size: 13px;
}

.progress-track {
  height: 9px;
  border-radius: 999px;
  background: var(--panel-soft);
  overflow: hidden;
  margin: 8px 0 5px;
}

.progress-track span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, var(--primary), #8b5cf6);
}

.policy-empty,
.inherit-note,
.conflict-banner,
.conflict-message {
  padding: 11px 12px;
  border-radius: 10px;
  background: rgba(245, 158, 11, .08);
  color: #92400e;
  font-size: 12px;
  line-height: 1.5;
}

.policy-form {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 14px;
  margin: 18px 0;
}

.policy-form input[type="number"] {
  min-width: 0;
  width: 100%;
  box-sizing: border-box;
}

.policy-check {
  grid-column: 1 / -1;
  flex-direction: row !important;
  align-items: flex-start;
  padding: 12px;
  border-radius: 10px;
  background: var(--panel-soft);
}

.policy-check input,
.confirm-row input {
  width: 18px;
  height: 18px;
  accent-color: var(--primary);
}

.policy-check span {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.policy-check small {
  color: var(--muted);
  font-weight: 500;
}

.policy-check--sensitive {
  border: 1px solid rgba(245, 158, 11, .28);
}

.review-panel {
  border-color: rgba(15, 82, 186, .35);
  background: linear-gradient(180deg, rgba(15, 82, 186, .04), var(--panel));
}

.review-panel dl {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
}

.review-panel dl div {
  background: var(--panel-soft);
  border-radius: 10px;
  padding: 10px;
}

.review-panel dt {
  color: var(--muted);
  font-size: 11px;
}

.review-panel dd {
  margin: 4px 0 0;
  color: var(--text-strong);
  font-weight: 700;
}

.confirm-row {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  margin: 16px 0;
  color: var(--text);
  font-size: 13px;
  line-height: 1.45;
}

.conflict-banner {
  display: flex;
  align-items: center;
  gap: 8px;
}

.breakdown-card h4 {
  margin-bottom: 14px;
}

.breakdown-table {
  display: flex;
  flex-direction: column;
}

.breakdown-row {
  display: grid;
  grid-template-columns: minmax(0, 1.5fr) repeat(3, minmax(60px, .7fr));
  gap: 8px;
  padding: 10px 0;
  border-bottom: 1px solid var(--line-light);
  font-size: 12px;
  color: var(--text);
}

.breakdown-row--head {
  color: var(--muted);
  font-size: 10px;
  font-weight: 800;
  text-transform: uppercase;
}

.breakdown-row span:not(:first-child) {
  text-align: right;
}

.grounding-footer {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  color: var(--muted);
  font-size: 11px;
  line-height: 1.5;
}

.grounding-footer svg {
  flex: 0 0 auto;
  color: var(--primary);
}

.spin {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

@media (max-width: 900px) {
  .metric-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .budget-grid,
  .breakdown-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 600px) {
  .ai-budget-heading,
  .scope-toolbar {
    align-items: stretch;
    flex-direction: column;
  }

  .metric-grid,
  .policy-form,
  .review-panel dl {
    grid-template-columns: 1fr;
  }

  .scope-toolbar select {
    width: 100%;
  }

  .breakdown-row {
    grid-template-columns: minmax(0, 1.2fr) repeat(3, minmax(52px, .7fr));
  }
}
</style>
