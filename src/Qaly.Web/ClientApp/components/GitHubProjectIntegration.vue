<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { Check, ChevronDown, ExternalLink, Github, Loader2, Lock, Plus, RefreshCw, Search, ShieldCheck, Trash2 } from 'lucide-vue-next'
import { githubApi, type GitHubInstallation, type GitHubIntegrationStatus, type GitHubRepository, type GitHubRepositoryConnection } from '../utils/github-api'
import { errorMessage } from '../utils/api-client'
import { showError, showSuccess } from '../composables/use-toast'
import { confirmDialog } from '../composables/use-confirm-dialog'
import GitHubProjectManagement from './GitHubProjectManagement.vue'

const props = defineProps<{ projectId: string; canManage: boolean }>()
const loading = ref(true)
const connecting = ref(false)
const saving = ref(false)
const installations = ref<GitHubInstallation[]>([])
const connections = ref<GitHubRepositoryConnection[]>([])
const repositories = ref<GitHubRepository[]>([])
const selectedInstallationId = ref('')
const selectedRepositoryIds = ref<number[]>([])
const query = ref('')
const loadError = ref('')
const pickerOpen = ref(false)
const integrationStatus = ref<GitHubIntegrationStatus | null>(null)

const activeConnections = computed(() => connections.value.filter(item => item.isActive))
const connectedIds = computed(() => new Set(activeConnections.value.map(item => item.repositoryExternalId)))
const visibleRepositories = computed(() => {
  const term = query.value.trim().toLowerCase()
  return repositories.value.filter(item => !connectedIds.value.has(item.id) && (!term || item.fullName.toLowerCase().includes(term)))
})
const hasInstallation = computed(() => installations.value.some(item => item.status === 'Active'))
const hasVerifiedConnection = computed(() =>
  (integrationStatus.value?.state === 'connected' && integrationStatus.value.liveVerified) ||
  (integrationStatus.value?.state === 'cached' && activeConnections.value.length > 0),
)
const isCachedSnapshot = computed(() => integrationStatus.value?.state === 'cached')
const hasConnectionProblem = computed(() => ['invalid_credentials', 'insufficient_permissions', 'rate_limited', 'unavailable'].includes(integrationStatus.value?.state ?? ''))
const serverUnavailableForSetup = computed(() => ['disabled', 'unconfigured'].includes(integrationStatus.value?.state ?? ''))
const syncedConnections = computed(() => activeConnections.value.filter(item => item.lastSyncedAt).length)
const privateConnections = computed(() => activeConnections.value.filter(item => item.isPrivate).length)
const onboardingStep = computed(() => !hasInstallation.value ? 1 : activeConnections.value.length === 0 ? 2 : 3)

onMounted(async () => {
  const setupStatus = new URLSearchParams(window.location.search).get('github')
  if (setupStatus === 'error') showError('Không thể hoàn tất kết nối GitHub App.')
  if (setupStatus) {
    const params = new URLSearchParams(window.location.search); params.delete('github')
    history.replaceState({}, '', `${window.location.pathname}${params.size ? `?${params}` : ''}${window.location.hash}`)
  }
  await finishGitHubSetupFromUrl()
  await loadAll()
})
watch(() => props.projectId, loadAll)
watch(selectedInstallationId, () => loadAvailableRepositories())

async function loadAll() {
  loading.value = true
  loadError.value = ''
  try {
    const [status, installationItems, connectionItems] = await Promise.all([
      githubApi.status(props.projectId),
      githubApi.installations(props.projectId),
      githubApi.connections(props.projectId),
    ])
    integrationStatus.value = status
    installations.value = installationItems
    connections.value = connectionItems
    if (connections.value.filter(item => item.isActive).length === 0 && installationItems.some(item => item.status === 'Active') && props.canManage) pickerOpen.value = true
    const active = installations.value.find(item => item.status === 'Active')
    selectedInstallationId.value = active?.id ?? ''
  } catch (error) {
    loadError.value = errorMessage(error, 'Không thể tải trạng thái kết nối GitHub.')
  } finally {
    loading.value = false
  }
}

async function finishGitHubSetupFromUrl() {
  const params = new URLSearchParams(window.location.search)
  const installationId = Number(params.get('installation_id'))
  const state = params.get('state')
  if (!installationId || (state && state !== props.projectId.replaceAll('-', ''))) return
  connecting.value = true
  try {
    await githubApi.completeInstallation(props.projectId, installationId)
    params.delete('installation_id'); params.delete('setup_action'); params.delete('state')
    history.replaceState({}, '', `${window.location.pathname}${params.size ? `?${params}` : ''}${window.location.hash}`)
    showSuccess('Đã kết nối GitHub App')
  } catch (error) {
    showError(errorMessage(error, 'Không thể hoàn tất cài đặt GitHub App.'))
  } finally { connecting.value = false }
}

async function connectGitHub() {
  connecting.value = true
  try { window.location.assign(await githubApi.installUrl(props.projectId)) }
  catch (error) { connecting.value = false; showError(errorMessage(error, 'Không thể mở trang cài đặt GitHub.')) }
}

async function loadAvailableRepositories() {
  repositories.value = []
  selectedRepositoryIds.value = []
  if (!selectedInstallationId.value || !props.canManage || isCachedSnapshot.value) return
  try { repositories.value = await githubApi.availableRepositories(props.projectId, selectedInstallationId.value) }
  catch (error) { showError(errorMessage(error, 'Không thể tải repository được cấp quyền.')) }
}

function toggleRepository(id: number) {
  selectedRepositoryIds.value = selectedRepositoryIds.value.includes(id)
    ? selectedRepositoryIds.value.filter(item => item !== id)
    : [...selectedRepositoryIds.value, id]
}

async function saveRepositories() {
  if (!selectedInstallationId.value || selectedRepositoryIds.value.length === 0) return
  saving.value = true
  try {
    const selected = repositories.value.filter(item => selectedRepositoryIds.value.includes(item.id))
    for (const repository of selected) await githubApi.connectRepository(props.projectId, selectedInstallationId.value, repository)
    showSuccess(`Đã kết nối ${selected.length} repository`)
    await loadAll()
  } catch (error) { showError(errorMessage(error, 'Không thể kết nối repository.')) }
  finally { saving.value = false }
}

async function disconnect(connection: GitHubRepositoryConnection) {
  if (!await confirmDialog({ tone: 'warning', title: `Ngắt ${connection.fullName}?`, message: 'Qaly sẽ ngừng nhận dữ liệu mới. Lịch sử đã đồng bộ vẫn được giữ lại.', confirmLabel: 'Ngắt kết nối' })) return
  try { await githubApi.disconnectRepository(props.projectId, connection.id); showSuccess('Đã ngắt repository'); await loadAll() }
  catch (error) { showError(errorMessage(error, 'Không thể ngắt repository.')) }
}

function formatSync(value: string | null) {
  if (!value) return 'Chưa nhận dữ liệu'
  return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value))
}
</script>

<template>
  <section class="github-panel glass-card">
    <header class="github-hero">
      <div class="github-mark"><Github :size="26" /></div>
      <div><span>Tích hợp mã nguồn</span><h2>GitHub</h2><p>Theo dõi PR, review, CI và release ngay trong Qaly — chỉ yêu cầu quyền đọc.</p></div>
      <button v-if="canManage && !serverUnavailableForSetup && !isCachedSnapshot" class="primary-button" type="button" :disabled="connecting" @click="connectGitHub">
        <Loader2 v-if="connecting" :size="16" class="spin" /><Github v-else :size="16" />
        {{ hasInstallation ? 'Quản lý quyền GitHub' : 'Kết nối GitHub' }}
      </button>
    </header>

    <div v-if="loading" class="github-state"><Loader2 class="spin" :size="22" /><span>Đang kiểm tra kết nối...</span></div>
    <div v-else-if="loadError" class="github-state is-error" role="alert"><p>{{ loadError }}</p><button type="button" class="ghost-button" @click="loadAll"><RefreshCw :size="15" /> Thử lại</button></div>
    <div v-else-if="serverUnavailableForSetup" class="github-empty">
      <Github :size="38" /><h3>{{ integrationStatus?.state === 'disabled' ? 'GitHub đang bị tắt' : 'GitHub App chưa được cấu hình' }}</h3>
      <p>{{ integrationStatus?.message }}</p>
    </div>
    <div v-else-if="hasConnectionProblem" class="github-state is-error" role="alert">
      <Github :size="38" /><h3>Không thể xác minh kết nối GitHub</h3><p>{{ integrationStatus?.message }}</p>
      <button type="button" class="ghost-button" @click="loadAll"><RefreshCw :size="15" /> Kiểm tra lại</button>
    </div>
    <div v-else-if="!hasVerifiedConnection" class="github-empty">
      <Github :size="38" /><h3>Chưa kết nối GitHub</h3><p>Quản trị viên cài Qaly GitHub App, chọn đúng repository và Qaly sẽ tự nhận hoạt động kỹ thuật.</p>
      <button type="button" v-if="canManage" class="primary-button" @click="connectGitHub">Bắt đầu kết nối</button>
      <small v-else>Liên hệ quản trị viên project để kết nối.</small>
    </div>
    <template v-else>
      <div class="onboarding" aria-label="Tiến trình thiết lập GitHub">
        <div v-for="step in [{ n: 1, label: 'Cài GitHub App' }, { n: 2, label: 'Chọn repository' }, { n: 3, label: 'Theo dõi tiến độ' }]" :key="step.n" :class="['onboarding-step', { done: onboardingStep > step.n, current: onboardingStep === step.n }]">
          <span><Check v-if="onboardingStep > step.n" :size="13" />{{ onboardingStep > step.n ? '' : step.n }}</span><strong>{{ step.label }}</strong>
        </div>
      </div>
      <div class="connection-summary">
        <span class="status-dot" :class="{ cached: isCachedSnapshot }"></span>
        <strong>GitHub đang hoạt động</strong>
        <span v-if="isCachedSnapshot">Cached · snapshot đã đồng bộ, live adapter đang tắt</span>
        <span v-else>Live · Qaly chỉ đọc metadata được cấp quyền</span>
      </div>
      <div class="health-grid">
        <article><span>Repository</span><strong>{{ activeConnections.length }}</strong><small>đang kết nối</small></article>
        <article><span>Đã nhận dữ liệu</span><strong>{{ syncedConnections }}</strong><small>repository</small></article>
        <article><span>Private</span><strong>{{ privateConnections }}</strong><small>vẫn giữ quyền riêng tư</small></article>
      </div>
      <div class="repo-list">
        <article v-for="connection in activeConnections" :key="connection.id" class="repo-card">
          <div class="repo-icon"><Lock v-if="connection.isPrivate" :size="15" /><Github v-else :size="16" /></div>
          <div class="repo-copy"><strong>{{ connection.fullName }}</strong><span>Nhánh mặc định: {{ connection.defaultBranch }} · {{ formatSync(connection.lastSyncedAt) }}</span></div>
          <button type="button" v-if="canManage" class="icon-button danger" title="Ngắt kết nối" @click="disconnect(connection)"><Trash2 :size="15" /></button>
        </article>
        <div v-if="activeConnections.length === 0" class="repo-empty">GitHub App đã cài đặt. Hãy chọn repository bên dưới.</div>
      </div>

      <GitHubProjectManagement v-if="activeConnections.length > 0" :project-id="projectId" :can-manage="canManage" />

      <button v-if="canManage && !isCachedSnapshot && !pickerOpen" class="add-repository" type="button" @click="pickerOpen = true"><Plus :size="16" /><span><strong>Thêm repository</strong><small>Chỉ chọn những repository thuộc project này</small></span><ChevronDown :size="16" /></button>
      <section v-if="canManage && !isCachedSnapshot && pickerOpen" class="repo-picker">
        <div class="picker-head"><div><span>Thêm repository</span><strong>Chọn nơi Qaly được phép đọc</strong></div><button type="button" class="ghost-button" @click="loadAvailableRepositories"><RefreshCw :size="14" /> Làm mới</button></div>
        <div class="privacy-note"><ShieldCheck :size="17" /><span><strong>An toàn theo mặc định</strong><small>Qaly không sao chép source code và không yêu cầu quyền ghi.</small></span></div>
        <label v-if="installations.length > 1" class="field"><span>Tài khoản GitHub</span><select v-model="selectedInstallationId"><option v-for="item in installations" :key="item.id" :value="item.id">{{ item.accountLogin }}</option></select></label>
        <label class="repo-search"><Search :size="16" /><input v-model="query" placeholder="Tìm repository..." /></label>
        <div class="repo-options">
          <button v-for="repository in visibleRepositories" :key="repository.id" type="button" class="repo-option" :class="{ selected: selectedRepositoryIds.includes(repository.id) }" :aria-pressed="selectedRepositoryIds.includes(repository.id)" @click="toggleRepository(repository.id)">
            <span class="check-box"><Check v-if="selectedRepositoryIds.includes(repository.id)" :size="14" /></span>
            <span><strong>{{ repository.fullName }}</strong><small>{{ repository.isPrivate ? 'Private' : 'Public' }} · {{ repository.defaultBranch }}</small></span>
            <ExternalLink :size="14" />
          </button>
          <p v-if="visibleRepositories.length === 0" class="repo-empty">Không còn repository phù hợp hoặc GitHub App chưa được cấp quyền.</p>
        </div>
        <div class="picker-footer"><button class="ghost-button" type="button" @click="pickerOpen = false">Đóng</button><span>{{ selectedRepositoryIds.length }} repository đã chọn</span><button type="button" class="primary-button" :disabled="saving || selectedRepositoryIds.length === 0" @click="saveRepositories"><Loader2 v-if="saving" class="spin" :size="15" /> Kết nối repository</button></div>
      </section>
    </template>
  </section>
</template>

<style scoped>
.onboarding{display:grid;grid-template-columns:repeat(3,1fr);padding:18px 24px;border-bottom:1px solid var(--line);background:var(--panel-soft)}
.onboarding-step{position:relative;display:flex;align-items:center;gap:8px;color:var(--muted);font-size:12px}.onboarding-step:not(:last-child)::after{content:"";position:absolute;left:32px;right:12px;top:13px;height:1px;background:var(--line)}.onboarding-step>span{position:relative;z-index:1;width:26px;height:26px;border:1px solid var(--line);border-radius:50%;display:grid;place-items:center;background:var(--panel)}.onboarding-step.done>span{background:#12b76a;border-color:#12b76a;color:white}.onboarding-step.current{color:var(--text)}.onboarding-step.current>span{border-color:#2563eb;color:#2563eb;box-shadow:0 0 0 3px rgba(37,99,235,.1)}
.health-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:10px;padding:16px 24px}.health-grid article{display:grid;grid-template-columns:1fr auto;padding:13px;border:1px solid var(--line);border-radius:12px;background:var(--panel-soft)}.health-grid span,.health-grid small{font-size:11px;color:var(--muted)}.health-grid strong{grid-row:1/3;grid-column:2;font-size:23px}
.add-repository{width:calc(100% - 48px);margin:0 24px 24px;display:grid;grid-template-columns:auto 1fr auto;align-items:center;gap:10px;text-align:left;padding:14px;border:1px dashed #84adff;border-radius:13px;background:rgba(59,130,246,.04);color:inherit}.add-repository>span{display:flex;flex-direction:column;gap:2px}.add-repository small{color:var(--muted)}
.privacy-note{display:flex;align-items:center;gap:9px;margin:14px 0;padding:10px 12px;border-radius:10px;background:rgba(18,183,106,.08);color:#067647}.privacy-note>span{display:flex;flex-direction:column}.privacy-note small{opacity:.82}
@media(max-width:620px){.onboarding{grid-template-columns:1fr;gap:9px}.onboarding-step::after{display:none}.health-grid{grid-template-columns:1fr}.add-repository{width:calc(100% - 24px);margin:0 12px 12px}}
.github-panel{padding:0;overflow:hidden;border:1px solid var(--line);background:var(--panel)}.github-hero{display:grid;grid-template-columns:auto 1fr auto;gap:16px;align-items:center;padding:24px;background:linear-gradient(135deg,rgba(36,41,47,.08),rgba(59,130,246,.06));border-bottom:1px solid var(--line)}.github-mark{width:52px;height:52px;border-radius:16px;display:grid;place-items:center;background:#24292f;color:#fff}.github-hero span,.picker-head span{font-size:12px;text-transform:uppercase;letter-spacing:.08em;color:var(--muted)}.github-hero h2{margin:2px 0;font-size:24px}.github-hero p{margin:0;color:var(--muted);max-width:650px}.github-state,.github-empty{min-height:230px;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:10px;text-align:center;padding:30px}.github-empty p{max-width:520px;color:var(--muted);margin:0}.github-state.is-error{color:#b42318}.connection-summary{display:flex;align-items:center;gap:9px;padding:16px 24px;border-bottom:1px solid var(--line);font-size:14px}.connection-summary span:last-child{color:var(--muted)}.status-dot{width:9px;height:9px;border-radius:50%;background:#12b76a;box-shadow:0 0 0 4px rgba(18,183,106,.12)}.repo-list{display:grid;gap:10px;padding:20px 24px}.repo-card{display:grid;grid-template-columns:auto 1fr auto;align-items:center;gap:12px;padding:14px;border:1px solid var(--line);border-radius:14px;background:var(--panel-soft)}.repo-icon{width:34px;height:34px;display:grid;place-items:center;border-radius:10px;background:var(--panel)}.repo-copy{display:flex;flex-direction:column;gap:4px}.repo-copy span{font-size:12px;color:var(--muted)}.danger{color:#d92d20}.repo-picker{margin:0 24px 24px;padding:18px;border:1px solid var(--line);border-radius:16px;background:var(--panel-soft)}.picker-head,.picker-footer{display:flex;align-items:center;justify-content:space-between;gap:12px}.picker-head>div{display:flex;flex-direction:column;gap:3px}.repo-search{display:flex;align-items:center;gap:8px;margin:16px 0 10px;padding:10px 12px;border:1px solid var(--line);border-radius:10px;background:var(--panel)}.repo-search input{border:0;outline:0;background:transparent;width:100%;color:inherit}.repo-options{display:grid;gap:7px;max-height:300px;overflow:auto}.repo-option{display:grid;grid-template-columns:auto 1fr auto;gap:10px;align-items:center;text-align:left;padding:11px;border:1px solid transparent;border-radius:10px;background:var(--panel);color:inherit}.repo-option:hover,.repo-option.selected{border-color:#84adff;background:rgba(59,130,246,.07)}.repo-option>span:nth-child(2){display:flex;flex-direction:column}.repo-option small{color:var(--muted);margin-top:3px}.check-box{width:20px;height:20px;border:1px solid var(--line);border-radius:6px;display:grid;place-items:center}.selected .check-box{background:#2563eb;color:white;border-color:#2563eb}.picker-footer{padding-top:14px;margin-top:12px;border-top:1px solid var(--line);font-size:13px;color:var(--muted)}.repo-empty{text-align:center;color:var(--muted);padding:18px}.ghost-button{display:inline-flex;align-items:center;gap:6px;border:1px solid var(--line);background:var(--panel);color:inherit;border-radius:9px;padding:8px 11px}.spin{animation:spin 1s linear infinite}@keyframes spin{to{transform:rotate(360deg)}}@media(max-width:720px){.github-hero{grid-template-columns:auto 1fr}.github-hero>.primary-button{grid-column:1/-1;width:100%;justify-content:center}.repo-picker{margin:0 12px 12px}.picker-head,.picker-footer{align-items:stretch;flex-direction:column}.picker-footer .primary-button{width:100%;justify-content:center}}
/* GitHub workspace visual refresh */
.github-panel{--gh-blue:#1358c8;--gh-dark:#0b3f98;--gh-soft:#eaf2ff;--gh-ink:#14213a;--gh-muted:#64748b;--gh-line:#dbe4f0;--gh-milk:#fffdf8;overflow:hidden;background:var(--gh-milk);border:1px solid #d5e0ed;border-radius:20px;box-shadow:0 18px 48px rgba(30,55,88,.08);color:var(--gh-ink)}
.github-hero{position:relative;isolation:isolate;min-height:132px;gap:18px;padding:26px 28px;overflow:hidden;background:linear-gradient(120deg,#fffdf8 0%,#f3f7ff 72%,#e9f1ff 100%);border-bottom-color:var(--gh-line)}
.github-hero::after{content:'';position:absolute;z-index:-1;right:12%;bottom:-105px;width:310px;height:220px;border-radius:50%;background:radial-gradient(circle,rgba(19,88,200,.14),transparent 68%)}
.github-mark{width:58px;height:58px;border-radius:17px;background:linear-gradient(145deg,#202a38,#111827);box-shadow:0 10px 24px rgba(17,24,39,.2)}
.github-hero span,.picker-head span{color:var(--gh-blue);font-size:10px;font-weight:800;letter-spacing:.13em}.github-hero h2{margin:3px 0 4px;color:var(--gh-ink);font-size:27px;letter-spacing:-.03em}.github-hero p{color:var(--gh-muted);font-size:13px;line-height:1.55}
.github-hero .primary-button,.picker-footer .primary-button{min-height:42px;padding:10px 15px;background:linear-gradient(135deg,#1762d5,#0d4cad);border:0;border-radius:11px;color:#fff;box-shadow:0 8px 18px rgba(19,88,200,.22)}
.github-hero .primary-button:hover,.picker-footer .primary-button:hover{background:linear-gradient(135deg,#0f55c4,#093b8f);transform:translateY(-1px)}
.onboarding{padding:18px 28px;background:#f8fbff;border-bottom-color:var(--gh-line)}.onboarding-step{color:#708097;font-size:12px}.onboarding-step:not(:last-child)::after{height:2px;background:#dce5f0}.onboarding-step>span{width:28px;height:28px;background:#fff;border-color:#cbd8e8}.onboarding-step.done>span{background:#17a673;border-color:#17a673;box-shadow:0 4px 10px rgba(23,166,115,.18)}.onboarding-step.current{color:var(--gh-ink)}.onboarding-step.current>span{border-color:var(--gh-blue);color:var(--gh-blue);box-shadow:0 0 0 4px rgba(19,88,200,.1)}
.connection-summary{min-height:56px;padding:14px 28px;background:#fff;border-bottom-color:var(--gh-line)}.connection-summary strong{color:#165c46}.connection-summary span:last-child{color:var(--gh-muted);font-size:12px}
.connection-summary .status-dot.cached{background:#f59e0b;box-shadow:0 0 0 4px rgba(245,158,11,.14)}
.health-grid{gap:12px;padding:20px 28px 12px}.health-grid article{min-height:82px;padding:16px;background:linear-gradient(145deg,#fff,#f8fbff);border-color:var(--gh-line);border-radius:14px;box-shadow:0 5px 14px rgba(34,57,86,.04)}.health-grid span{color:#4c5f79;font-size:12px;font-weight:650}.health-grid small{color:var(--gh-muted)}.health-grid strong{color:var(--gh-dark);font-size:28px;letter-spacing:-.04em}
.repo-list{gap:10px;padding:10px 28px 22px}.repo-card{min-height:70px;padding:14px 16px;background:#fff;border-color:var(--gh-line);border-radius:14px;box-shadow:0 4px 12px rgba(35,57,84,.035);transition:transform .25s ease,border-color .25s ease,box-shadow .25s ease}.repo-card:hover{border-color:#9ab9e2;box-shadow:0 8px 20px rgba(19,88,200,.08);transform:translateY(-1px)}.repo-icon{width:38px;height:38px;background:var(--gh-soft);color:var(--gh-dark);border-radius:11px}.repo-copy strong{color:var(--gh-ink);font-size:13px}.repo-copy span{color:var(--gh-muted);line-height:1.45}
.repo-card .icon-button{width:38px;height:38px;display:grid;place-items:center;background:#fff;border:1px solid #d6e0eb;border-radius:10px}.repo-card .icon-button:hover{background:#fff0ef;border-color:#f0b9b5}
.add-repository{width:calc(100% - 56px);min-height:72px;margin:4px 28px 26px;padding:15px 17px;background:#f6f9ff;border:1px dashed #7ca7df;border-radius:14px;color:var(--gh-ink);cursor:pointer;transition:background-color .25s ease,border-color .25s ease,transform .25s ease}.add-repository:hover{background:#edf4ff;border-color:var(--gh-blue);transform:translateY(-1px)}.add-repository>svg:first-child{color:var(--gh-blue)}.add-repository small{margin-top:3px;color:var(--gh-muted)}
.repo-picker{margin:4px 28px 26px;padding:20px;background:#f8fbff;border-color:#cddceb;border-radius:16px}.picker-head strong{color:var(--gh-ink);font-size:16px}.privacy-note{background:#eaf8f2;border:1px solid #b9dfcf}.repo-search{min-height:44px;background:#fff;border-color:#ccd9e8}.repo-search:focus-within{border-color:#6f9ddd;box-shadow:0 0 0 3px rgba(19,88,200,.1)}.repo-option{min-height:58px;background:#fff;border-color:#e0e7f0}.repo-option:hover,.repo-option.selected{border-color:#84a9df;background:#edf4ff}.ghost-button{min-height:38px;background:#fff;border-color:#ccd8e6;color:#3b4e68;cursor:pointer}.ghost-button:hover{background:#edf4ff;border-color:#9bb8de}.github-state,.github-empty{background:linear-gradient(180deg,#fffdf8,#f8fbff)}
@media(max-width:720px){.github-panel{border-radius:14px}.github-hero{padding:20px}.health-grid{padding:16px 16px 10px}.repo-list{padding:8px 16px 18px}.add-repository{width:calc(100% - 32px);margin:4px 16px 18px}}
@media(prefers-reduced-motion:reduce){.github-panel *{transition-duration:.01ms!important;animation-duration:.01ms!important}}
</style>
