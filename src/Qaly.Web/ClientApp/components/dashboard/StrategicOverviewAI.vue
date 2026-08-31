<script setup lang="ts">
import { computed, ref, onMounted, watch, onBeforeUnmount } from 'vue'
import { BrainCircuit, Activity, Target, AlertCircle, PlayCircle, BarChart3, RefreshCw, Server } from 'lucide-vue-next'
import { apiJson, apiResult, errorMessage } from '../../utils/api-client'
import type { StrategicOverviewDto } from '../../types'

type OrganizationScope = { id: string; name: string }
type AiJob = {
  jobId: string
  status: string
  progressPercent: number
  selectedProvider: string | null
  selectedModel: string | null
  lastErrorCode: string | null
  lastErrorMessage: string | null
  lastErrorRetryable: boolean
  isMock: boolean
}
type GroundedSummary = { text: string; metricRefs: string[]; sourceRefs: string[] }
type GroundedRisk = { severity: 'low' | 'medium' | 'high'; title: string; metricRefs: string[]; sourceRefs: string[] }
type GroundedPriority = { title: string; rationale: string; metricRefs: string[]; sourceRefs: string[] }
type StrategicSource = { key: string; type: 'project' | 'task'; entityId: string; projectId: string; label: string; url: string; version: string | null }
type StrategicBrief = {
  schemaId: 'dashboard_strategic_brief.v1'
  organizationId: string
  requestedById: string
  snapshotAt: string
  coverage: { visibleProjectCount: number; includedTaskCount: number; excludedPrivateTaskCount: number; visibility: string }
  metrics: Record<string, number>
  summaryPoints: GroundedSummary[]
  risks: GroundedRisk[]
  priorities: GroundedPriority[]
  sourceRefs: StrategicSource[]
  warnings: string[]
}
type JobResult = { schemaId: string; result: StrategicBrief; cacheHit: boolean; isMock: boolean; sourceStale: boolean }

const props = withDefaults(defineProps<{ organizationScopes?: OrganizationScope[] }>(), { organizationScopes: () => [] })

const isLoadingStats = ref(true)
const isLoadingAi = ref(false)

const statsData = ref<StrategicOverviewDto | null>(null)
const selectedOrganizationId = ref('')
const aiJob = ref<AiJob | null>(null)
const aiData = ref<JobResult | null>(null)
const aiError = ref('')
let pollToken = 0
const canGenerateAiInsight = computed(() => !isLoadingAi.value && !!selectedOrganizationId.value && !!statsData.value)
const jobRunning = computed(() => ['queued', 'running', 'retrying'].includes(aiJob.value?.status.toLowerCase() ?? ''))

function storageKey() { return `qaly:dashboard-strategic-brief:${selectedOrganizationId.value}` }

function statusLabel(status?: string) {
  const value = status?.toLowerCase()
  if (value === 'queued') return 'Đang xếp hàng'
  if (value === 'running') return 'AI đang phân tích snapshot máy chủ'
  if (value === 'retrying') return 'Đang chờ retry'
  if (value === 'succeeded') return 'Đã có strategic brief kiểm chứng được'
  if (value === 'failed') return 'Không thể tạo kết quả hợp lệ'
  if (value === 'canceled' || value === 'cancelled') return 'Đã hủy'
  return status || 'Chưa chạy'
}

function sourceFor(key: string) { return aiData.value?.result.sourceRefs.find(source => source.key === key) ?? null }

async function monitorJob(jobId: string) {
  const token = ++pollToken
  for (let attempt = 0; attempt < 80 && token === pollToken; attempt++) {
    const detail = await apiResult<AiJob>(`/api/ai/jobs/${jobId}`)
    aiJob.value = detail
    const status = detail.status.toLowerCase()
    if (status === 'succeeded') {
      const result = await apiResult<JobResult>(`/api/ai/jobs/${jobId}/result`)
      if (result.schemaId !== 'dashboard_strategic_brief.v1' || result.isMock) {
        throw new Error('Kết quả không đạt contract native hoặc là dữ liệu mock.')
      }
      aiData.value = result
      return
    }
    if (['failed', 'canceled', 'cancelled'].includes(status)) {
      aiError.value = detail.lastErrorMessage || 'AI không tạo được strategic brief hợp lệ.'
      return
    }
    await new Promise(resolve => window.setTimeout(resolve, 750))
  }
  if (token === pollToken) aiError.value = 'Job vẫn đang chạy; đóng/mở lại trang để tiếp tục đọc kết quả.'
}

async function restoreJob() {
  if (!selectedOrganizationId.value) return
  const jobId = localStorage.getItem(storageKey())
  if (!jobId) return
  try { await monitorJob(jobId) } catch { localStorage.removeItem(storageKey()) }
}

const loadStats = async () => {
  try {
    const res = await apiJson<StrategicOverviewDto>('/api/dashboard/strategic-overview')
    statsData.value = res
  } catch (error) {
    console.error(error)
  } finally {
    isLoadingStats.value = false
  }
}

const generateAiInsight = async () => {
  if (!canGenerateAiInsight.value) return

  isLoadingAi.value = true
  aiError.value = ''
  try {
    aiData.value = null
    const created = await apiResult<{ jobId: string }>('/api/ai/dashboard/strategic-brief', {
      method: 'POST',
      headers: { 'Idempotency-Key': crypto.randomUUID() },
      body: JSON.stringify({
        organizationId: selectedOrganizationId.value,
        language: 'vi',
        providerHint: 'deepseek-chat',
        cacheMode: 'use',
      })
    })
    localStorage.setItem(storageKey(), created.jobId)
    await monitorJob(created.jobId)
  } catch (error) {
    aiData.value = null
    aiError.value = errorMessage(error, 'Không thể tạo strategic brief từ snapshot máy chủ.')
  } finally {
    isLoadingAi.value = false
  }
}

async function cancelJob() {
  if (!aiJob.value) return
  try {
    aiJob.value = await apiResult<AiJob>(`/api/ai/jobs/${aiJob.value.jobId}/cancel`, {
      method: 'POST', body: JSON.stringify({ reason: 'Người dùng hủy Dashboard Strategic Brief.' })
    })
    pollToken++
    isLoadingAi.value = false
    aiError.value = 'Đã hủy job; không có kết quả giả được hiển thị.'
  } catch (error) { aiError.value = errorMessage(error, 'Không thể hủy job.') }
}

async function retryJob() {
  if (!aiJob.value) return
  isLoadingAi.value = true
  aiError.value = ''
  try {
    aiJob.value = await apiResult<AiJob>(`/api/ai/jobs/${aiJob.value.jobId}/retry`, {
      method: 'POST', body: JSON.stringify({ providerOverride: null })
    })
    await monitorJob(aiJob.value.jobId)
  } catch (error) { aiError.value = errorMessage(error, 'Không thể retry job.') }
  finally { isLoadingAi.value = false }
}

watch(() => props.organizationScopes, scopes => {
  if (!scopes.some(scope => scope.id === selectedOrganizationId.value)) selectedOrganizationId.value = scopes[0]?.id ?? ''
}, { immediate: true, deep: true })

watch(selectedOrganizationId, async () => {
  pollToken++
  aiJob.value = null
  aiData.value = null
  aiError.value = ''
  await restoreJob()
})

onMounted(async () => {
  await loadStats()
  await restoreJob()
})

onBeforeUnmount(() => { pollToken++ })
</script>

<template>
  <section class="strategy-section glass-panel" data-testid="dashboard-strategic-brief">
    <div class="strategy-header">
      <div class="header-titles">
        <h2>Tổng quan chiến lược</h2>
        <p>AI phân tích dữ liệu dự án, nhiệm vụ và hiệu suất đội nhóm để đề xuất hướng triển khai tiếp theo.</p>
      </div>
      <div class="header-actions">
        <select v-if="organizationScopes.length > 1" v-model="selectedOrganizationId" class="scope-select" aria-label="Chọn tổ chức cho Strategic Brief">
          <option v-for="scope in organizationScopes" :key="scope.id" :value="scope.id">{{ scope.name }}</option>
        </select>
        <button type="button"
          @click="generateAiInsight" 
          class="ai-button"
          :disabled="!canGenerateAiInsight"
        >
          <RefreshCw v-if="aiData" :size="18" />
          <BrainCircuit v-else :size="18" />
          {{ aiData ? 'Phân tích lại' : 'Tạo phân tích AI' }}
        </button>
      </div>
    </div>

    <div v-if="isLoadingStats" class="strategy-loading" role="status" aria-live="polite">
      Đang tải dữ liệu chiến lược...
    </div>
    
    <div v-else-if="statsData" class="strategy-body">
      
      <!-- Left side: Stats Data -->
      <div class="strategy-stats">
        
        <div class="stat-card">
          <div class="stat-icon"><Activity :size="20"/></div>
          <div class="stat-info">
            <span class="stat-label">Tiến trình chung</span>
            <strong class="stat-value">{{ statsData.averageProjectProgress }}%</strong>
          </div>
        </div>
        
        <div class="stat-card">
          <div class="stat-icon"><Target :size="20"/></div>
          <div class="stat-info">
            <span class="stat-label">Tỷ lệ hoàn thành task</span>
            <strong class="stat-value">{{ statsData.taskCompletionRate }}%</strong>
          </div>
        </div>

        <div class="stat-card">
          <div class="stat-icon alert-icon"><AlertCircle :size="20"/></div>
          <div class="stat-info">
            <span class="stat-label">Dự án rủi ro / Nhiệm vụ trễ</span>
            <strong class="stat-value">{{ statsData.riskProjectCount }} / {{ statsData.overdueTaskCount }}</strong>
          </div>
        </div>

        <div class="stat-card">
          <div class="stat-icon"><BarChart3 :size="20"/></div>
          <div class="stat-info">
            <span class="stat-label">Workload đội nhóm</span>
            <strong class="stat-value">{{ statsData.teamWorkloadLevel }}</strong>
          </div>
        </div>

      </div>

      <!-- Right side: AI Insights -->
      <div class="strategy-ai-insight">
        
        <div v-if="isLoadingAi" class="ai-loading" role="status" aria-live="polite">
          <BrainCircuit class="spin-icon" :size="32" />
          <strong>{{ statusLabel(aiJob?.status) }}</strong>
          <p>Kiểm tenant/quyền → dựng snapshot server → gọi model → kiểm schema/nguồn → lưu read-back</p>
          <button v-if="jobRunning" type="button" class="secondary-button" @click="cancelJob">Hủy job</button>
        </div>

        <div v-else-if="aiError" class="ai-error" role="alert">
          <AlertCircle :size="28" />
          <strong>Chưa có kết quả AI thật</strong>
          <p>{{ aiError }}</p>
          <button v-if="aiJob?.lastErrorRetryable" type="button" class="secondary-button" @click="retryJob">Retry</button>
        </div>

        <div v-else-if="!aiData" class="ai-empty">
          <div class="ai-empty-icon">
            <BrainCircuit :size="48" style="color: rgba(15, 82, 186, 0.3);" />
          </div>
          <p v-if="selectedOrganizationId">Nhấn <strong>Tạo phân tích AI</strong> để nhận nhận định có metric/source grounding và có thể đọc lại sau reload.</p>
          <p v-else>Chưa có phạm vi tổ chức/dự án hợp lệ để tạo strategic brief.</p>
        </div>

        <div v-else class="ai-results">
          <div class="ai-provider-badge">
            <Server :size="14" />
            {{ statusLabel(aiJob?.status) }} · {{ aiJob?.selectedProvider || 'provider chưa xác định' }} · {{ aiJob?.selectedModel || 'model chưa xác định' }}
          </div>
          <div v-if="aiData.sourceStale" class="ai-error compact-error" role="alert">Nguồn đã thay đổi sau lúc tạo. Hãy phân tích lại.</div>
          <div class="ai-summary" v-for="(item, index) in aiData.result.summaryPoints" :key="`${index}-${item.text}`">
            <strong>Nhận định:</strong> {{ item.text }}
            <div class="grounding-links">
              <span v-for="metric in item.metricRefs" :key="metric">{{ metric }}={{ aiData.result.metrics[metric] }}</span>
              <a v-for="sourceKey in item.sourceRefs" :key="sourceKey" :href="sourceFor(sourceKey)?.url">{{ sourceFor(sourceKey)?.label || sourceKey }}</a>
            </div>
          </div>
          
          <div class="ai-grid">
            <div class="ai-box warning-box">
              <h4>Rủi ro chính</h4>
              <ul v-if="aiData.result.risks.length">
                <li v-for="risk in aiData.result.risks" :key="`${risk.severity}-${risk.title}`">
                  <strong>[{{ risk.severity }}]</strong> {{ risk.title }}
                  <div class="grounding-links"><span v-for="metric in risk.metricRefs" :key="metric">{{ metric }}={{ aiData.result.metrics[metric] }}</span><a v-for="sourceKey in risk.sourceRefs" :key="sourceKey" :href="sourceFor(sourceKey)?.url">{{ sourceFor(sourceKey)?.label || sourceKey }}</a></div>
                </li>
              </ul>
              <p v-else>Không có rủi ro nào đủ căn cứ trong snapshot.</p>
            </div>
            
            <div class="ai-box success-box">
              <h4>Ưu tiên đề xuất</h4>
              <ul v-if="aiData.result.priorities.length">
                <li v-for="priority in aiData.result.priorities" :key="priority.title">
                  <strong>{{ priority.title }}</strong> — {{ priority.rationale }}
                  <div class="grounding-links"><span v-for="metric in priority.metricRefs" :key="metric">{{ metric }}={{ aiData.result.metrics[metric] }}</span><a v-for="sourceKey in priority.sourceRefs" :key="sourceKey" :href="sourceFor(sourceKey)?.url">{{ sourceFor(sourceKey)?.label || sourceKey }}</a></div>
                </li>
              </ul>
              <p v-else>Chưa có ưu tiên AI đủ căn cứ.</p>
            </div>
          </div>
          
          <div class="ai-priority">
            <h4><PlayCircle :size="16" style="margin-right: 6px;"/> Phạm vi đã kiểm</h4>
            <div class="priority-tags">
              <span class="priority-tag">{{ aiData.result.coverage.visibleProjectCount }} dự án</span>
              <span class="priority-tag">{{ aiData.result.coverage.includedTaskCount }} task non-private</span>
              <span class="priority-tag">Loại {{ aiData.result.coverage.excludedPrivateTaskCount }} task private khỏi model</span>
            </div>
          </div>

        </div>
        
      </div>
    </div>
  </section>
</template>

<style scoped>
.glass-panel {
  background: rgba(255, 255, 255, 0.85);
  backdrop-filter: none;
  border: 1px solid rgba(255, 255, 255, 0.6);
  border-radius: var(--qaly-radius-lg);
  padding: 24px;
  box-shadow: var(--qaly-shadow-md);
  margin-top: 24px;
}

.strategy-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 24px;
  padding-bottom: 20px;
  border-bottom: 1px solid rgba(15, 82, 186, 0.1);
}

.header-titles h2 {
  font-size: 20px;
  font-weight: 800;
  color: #0f172a;
  margin: 0 0 6px 0;
}

.header-titles p {
  font-size: 14px;
  color: #64748b;
  margin: 0;
  max-width: 500px;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 10px;
}

.scope-select {
  min-width: 180px;
  padding: 9px 12px;
  border: 1px solid rgba(15, 82, 186, 0.18);
  border-radius: var(--qaly-radius-lg);
  background: rgba(255, 255, 255, 0.9);
  color: #1e293b;
}

.ai-button {
  display: flex;
  align-items: center;
  gap: 8px;
  background: linear-gradient(135deg, #1f80ff, #0f52ba);
  color: white;
  border: none;
  padding: 10px 18px;
  border-radius: var(--qaly-radius-lg);
  font-weight: 600;
  font-size: 14px;
  cursor: pointer;
  transition: transform 0.2s, box-shadow 0.2s;
  box-shadow: var(--qaly-shadow-md);
}

.ai-button:hover {
  transform: translateY(-2px);
  box-shadow: var(--qaly-shadow-md);
}

.ai-button:disabled {
  opacity: 0.52;
  cursor: not-allowed;
  transform: none;
}

.secondary-button {
  background: rgba(15, 82, 186, 0.1);
  color: #1f80ff;
  border: none;
  padding: 10px 18px;
  border-radius: var(--qaly-radius-lg);
  font-weight: 600;
  font-size: 14px;
  cursor: pointer;
}

.strategy-body {
  display: flex;
  gap: 24px;
}

@media (max-width: 900px) {
  .strategy-body {
    flex-direction: column;
  }
}

.strategy-stats {
  flex: 0 0 35%;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.stat-card {
  display: flex;
  align-items: center;
  gap: 16px;
  background: rgba(255,255,255,0.6);
  padding: 16px;
  border-radius: var(--qaly-radius-lg);
  border: 1px solid rgba(15, 82, 186, 0.05);
}

.stat-icon {
  width: 48px;
  height: 48px;
  border-radius: var(--qaly-radius-lg);
  background: rgba(31, 128, 255, 0.1);
  color: #1f80ff;
  display: flex;
  align-items: center;
  justify-content: center;
}

.alert-icon {
  background: rgba(239, 68, 68, 0.1);
  color: #ef4444;
}

.stat-info {
  display: flex;
  flex-direction: column;
}

.stat-label {
  font-size: 12px;
  color: #64748b;
  font-weight: 600;
}

.stat-value {
  font-size: 20px;
  color: #0f172a;
  font-weight: 800;
}

.strategy-ai-insight {
  flex: 1;
  background: linear-gradient(145deg, rgba(31, 128, 255, 0.03), rgba(31, 128, 255, 0.08));
  border-radius: var(--qaly-radius-lg);
  padding: 24px;
  border: 1px solid rgba(31, 128, 255, 0.1);
  display: flex;
  flex-direction: column;
}

.ai-loading, .ai-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  min-height: 200px;
  color: #64748b;
  text-align: center;
}

.ai-error {
  min-height: 200px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  color: #b91c1c;
  text-align: center;
}

.ai-error p {
  max-width: 520px;
  margin: 0;
  color: #64748b;
}

.compact-error { min-height: 0; padding: 10px; }

.grounding-links {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 8px;
}

.grounding-links span,
.grounding-links a {
  padding: 3px 7px;
  border-radius: 999px;
  background: rgba(31, 128, 255, 0.1);
  color: #0f52ba;
  font-size: 11px;
  font-weight: 700;
  text-decoration: none;
}

.ai-provider-badge {
  width: fit-content;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  border: 1px solid rgba(5, 150, 105, 0.24);
  border-radius: 6px;
  padding: 6px 9px;
  color: #047857;
  background: rgba(16, 185, 129, 0.08);
  font-size: 12px;
  font-weight: 700;
}

.spin-icon {
  animation: spin 2s linear infinite;
  color: #1f80ff;
  margin-bottom: 16px;
}

@keyframes spin {
  100% { transform: rotate(360deg); }
}

.ai-results {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.ai-summary {
  font-size: 15px;
  line-height: 1.5;
  color: #1e293b;
  background: rgba(255,255,255,0.7);
  padding: 16px;
  border-radius: var(--qaly-radius-lg);
  border-left: 4px solid #1f80ff;
}

.ai-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

@media (max-width: 600px) {
  .ai-grid {
    grid-template-columns: 1fr;
  }
}

.ai-box {
  padding: 16px;
  border-radius: var(--qaly-radius-lg);
  background: rgba(255,255,255,0.7);
}

.ai-box h4 {
  margin: 0 0 12px 0;
  font-size: 14px;
  font-weight: 700;
}

.ai-box ul {
  margin: 0;
  padding-left: 20px;
  font-size: 13px;
  color: #475569;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.warning-box h4 { color: #ea580c; }
.success-box h4 { color: #059669; }

.ai-priority h4 {
  display: flex;
  align-items: center;
  margin: 0 0 12px 0;
  font-size: 14px;
  font-weight: 700;
  color: #0f172a;
}

.priority-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.priority-tag {
  background: #1f80ff;
  color: white;
  padding: 6px 12px;
  border-radius: var(--qaly-radius-lg);
  font-size: 12px;
  font-weight: 600;
  box-shadow: 0 2px 8px rgba(31, 128, 255, 0.2);
}
</style>
