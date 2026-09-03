<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { AlertCircle, CheckCircle2, ExternalLink, GitPullRequest, Loader2, Package, RefreshCw, Workflow } from 'lucide-vue-next'
import { githubApi, type GitHubProjectManagement } from '../utils/github-api'
import { errorMessage } from '../utils/api-client'
import { showError, showSuccess } from '../composables/use-toast'

const props = defineProps<{ projectId: string; canManage: boolean }>()
const data = ref<GitHubProjectManagement | null>(null)
const loading = ref(true)
const syncing = ref(false)
const tab = ref<'pulls' | 'workflows' | 'releases'>('pulls')
const filter = ref('all')
const loadError = ref('')

const pulls = computed(() => data.value?.pullRequests.filter(item => filter.value === 'all' || item.state.toLowerCase() === filter.value) ?? [])
const workflows = computed(() => data.value?.workflows.filter(item => filter.value === 'all' || (item.conclusion ?? item.status) === filter.value) ?? [])
const hasActivity = computed(() => Boolean(data.value) && (data.value!.pullRequests.length > 0 || data.value!.workflows.length > 0 || data.value!.releases.length > 0))

onMounted(load)
watch(() => props.projectId, load)

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    data.value = await githubApi.management(props.projectId)
  } catch (e) {
    loadError.value = errorMessage(e, 'Không thể tải dữ liệu quản lý GitHub.')
  } finally {
    loading.value = false
  }
}

async function sync() {
  syncing.value = true
  try {
    data.value = await githubApi.syncManagement(props.projectId)
    showSuccess('Đã đồng bộ dữ liệu mới nhất từ GitHub')
  } catch (e) {
    showError(errorMessage(e, 'Không thể đồng bộ GitHub. Hãy kiểm tra quyền của GitHub App.'))
  } finally {
    syncing.value = false
  }
}

function switchTab(value: typeof tab.value) {
  tab.value = value
  filter.value = 'all'
}

function date(value: string | null) {
  return value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value)) : 'Chưa có'
}

function tone(value: string | null) {
  return value === 'success' ? 'success' : ['failure', 'timed_out', 'cancelled'].includes(value ?? '') ? 'danger' : 'pending'
}

function taskHref(taskId: string) {
  return `/projects/${props.projectId}/tasks/${taskId}`
}

function openTask(taskId: string) {
  window.location.assign(taskHref(taskId))
}

function openExternal(url: string) {
  window.open(url, '_blank', 'noopener,noreferrer')
}
</script>

<template>
  <section class="management">
    <header class="management__header">
      <div>
        <span>Trung tâm vận hành</span>
        <h3>Quản lý phát triển</h3>
        <p>PR, review, CI và release của toàn bộ repository trong dự án.</p>
      </div>
      <button v-if="canManage" :disabled="syncing" @click="sync">
        <Loader2 v-if="syncing" class="spin" :size="16" />
        <RefreshCw v-else :size="16" />
        {{ syncing ? 'Đang đồng bộ...' : 'Đồng bộ ngay' }}
      </button>
    </header>

    <div v-if="loading" class="state">
      <Loader2 class="spin" :size="20" />
      Đang tải dữ liệu...
    </div>

    <div v-else-if="loadError" class="state error">
      <AlertCircle :size="20" />
      <span>{{ loadError }}</span>
      <button @click="load">Thử lại</button>
    </div>

    <div v-else-if="!data || !hasActivity" class="state empty">
      <Package :size="24" />
      <strong>Chưa có hoạt động GitHub</strong>
      <p>Repository đã kết nối nhưng chưa có PR, workflow hoặc release nào để hiển thị.</p>
      <button v-if="canManage" class="retry-button" @click="sync">Đồng bộ ngay</button>
      <button v-else class="retry-button" @click="load">Làm mới</button>
    </div>

    <template v-else>
      <div class="metrics">
        <article>
          <GitPullRequest />
          <span>PR đang mở</span>
          <strong>{{ data.openPullRequests }}</strong>
        </article>
        <article>
          <AlertCircle />
          <span>Chờ review</span>
          <strong>{{ data.waitingForReview }}</strong>
        </article>
        <article :class="{ alert: data.failedWorkflows }">
          <Workflow />
          <span>CI cần xử lý</span>
          <strong>{{ data.failedWorkflows }}</strong>
        </article>
        <article>
          <Package />
          <span>Repository</span>
          <strong>{{ data.repositoryCount }}</strong>
        </article>
      </div>

      <div class="toolbar">
        <nav>
          <button :class="{ active: tab === 'pulls' }" @click="switchTab('pulls')">Pull requests <b>{{ data.pullRequests.length }}</b></button>
          <button :class="{ active: tab === 'workflows' }" @click="switchTab('workflows')">CI/CD <b>{{ data.workflows.length }}</b></button>
          <button :class="{ active: tab === 'releases' }" @click="switchTab('releases')">Releases <b>{{ data.releases.length }}</b></button>
        </nav>
        <select v-if="tab === 'pulls'" v-model="filter">
          <option value="all">Tất cả trạng thái</option>
          <option value="open">Đang mở</option>
          <option value="merged">Đã merge</option>
          <option value="closed">Đã đóng</option>
        </select>
        <select v-else-if="tab === 'workflows'" v-model="filter">
          <option value="all">Tất cả kết quả</option>
          <option value="success">Thành công</option>
          <option value="failure">Thất bại</option>
          <option value="in_progress">Đang chạy</option>
        </select>
      </div>

      <div v-if="tab === 'pulls'" class="items">
        <article v-for="item in pulls" :key="item.id" class="item-card" @click="openExternal(item.url)">
          <i><GitPullRequest :size="17" /></i>
          <div class="item-body">
            <strong>#{{ item.number }} {{ item.title }}</strong>
            <small>{{ item.repository }} · {{ item.headBranch }} → {{ item.baseBranch }} · {{ item.authorLogin || 'GitHub user' }}</small>
            <div class="task-tags">
              <button
                v-for="task in item.linkedTasks"
                :key="task.taskId"
                type="button"
                class="task-tag"
                @click.stop="openTask(task.taskId)"
              >
                {{ task.taskKey }}
              </button>
              <span v-if="item.linkedTasks.length === 0" class="task-tag task-tag--muted">Chưa có task key</span>
            </div>
          </div>
          <em :class="{ approved: item.approvalCount }">
            <CheckCircle2 :size="14" />
            {{ item.approvalCount ? `${item.approvalCount} duyệt` : item.isDraft ? 'Bản nháp' : 'Chờ duyệt' }}
          </em>
          <ExternalLink :size="14" class="external-icon" />
        </article>
        <p v-if="!pulls.length">Chưa có pull request phù hợp.</p>
      </div>

      <div v-else-if="tab === 'workflows'" class="items">
        <article v-for="item in workflows" :key="item.runId" class="item-card" @click="openExternal(item.url)">
          <i :class="tone(item.conclusion)">
            <Workflow :size="17" />
          </i>
          <div class="item-body">
            <strong>{{ item.title || item.name }}</strong>
            <small>{{ item.repository }} · {{ item.branch }} · {{ date(item.startedAt) }}</small>
            <div class="task-tags">
              <button
                v-for="task in item.linkedTasks"
                :key="task.taskId"
                type="button"
                class="task-tag"
                @click.stop="openTask(task.taskId)"
              >
                {{ task.taskKey }}
              </button>
              <span v-if="item.linkedTasks.length === 0" class="task-tag task-tag--muted">Chưa có task key</span>
            </div>
          </div>
          <em :class="tone(item.conclusion)">{{ item.conclusion || item.status }}</em>
          <ExternalLink :size="14" class="external-icon" />
        </article>
        <p v-if="!workflows.length">Chưa có workflow run phù hợp.</p>
      </div>

      <div v-else class="items">
        <article v-for="item in data.releases" :key="item.repository + item.tagName" class="item-card" @click="openExternal(item.url)">
          <i><Package :size="17" /></i>
          <div class="item-body">
            <strong>{{ item.name || item.tagName }}</strong>
            <small>{{ item.repository }} · {{ item.tagName }} · {{ date(item.publishedAt) }}</small>
          </div>
          <em v-if="item.isPrerelease" class="pending">Pre-release</em>
          <ExternalLink :size="14" class="external-icon" />
        </article>
        <p v-if="!data.releases.length">Chưa có release.</p>
      </div>

      <footer>Đồng bộ gần nhất: {{ date(data.lastSyncedAt) }}</footer>
    </template>
  </section>
</template>

<style scoped>
.management {
  margin: 2px 28px 24px;
  overflow: hidden;
  color: var(--text);
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 17px;
  box-shadow: var(--shadow-soft);
}

.management__header {
  min-height: 96px;
  padding: 20px 22px;
  display: flex;
  justify-content: space-between;
  gap: 18px;
  background: linear-gradient(120deg, var(--panel), var(--panel-soft));
  border-bottom: 1px solid var(--line);
}

.management__header span {
  color: var(--primary);
  font-size: 10px;
  font-weight: 800;
  letter-spacing: .13em;
  text-transform: uppercase;
}

.management__header h3 {
  margin: 3px 0 4px;
  color: var(--text-strong);
  font-size: 21px;
  letter-spacing: -.025em;
}

.management__header p {
  color: var(--muted);
  line-height: 1.45;
  margin: 0;
}

.management__header button {
  min-height: 42px;
  padding: 10px 15px;
  display: inline-flex;
  align-items: center;
  gap: 7px;
  background: linear-gradient(135deg, var(--primary), var(--primary-strong));
  border: 0;
  border-radius: 11px;
  color: var(--text-on-accent);
  font-weight: 700;
  box-shadow: 0 8px 18px color-mix(in srgb, var(--primary) 20%, transparent);
  cursor: pointer;
  transition: transform .25s ease, box-shadow .25s ease, opacity .25s ease;
}

.management__header button:hover:not(:disabled) {
  transform: translateY(-1px);
  box-shadow: 0 10px 22px color-mix(in srgb, var(--primary) 28%, transparent);
}

.management__header button:disabled {
  cursor: wait;
  opacity: .65;
}

.metrics {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 12px;
  padding: 18px 18px 14px;
}

.metrics article {
  min-height: 78px;
  padding: 14px;
  display: grid;
  grid-template-columns: auto 1fr auto;
  align-items: center;
  gap: 8px;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 13px;
  box-shadow: var(--shadow-soft);
}

.metrics svg {
  color: var(--primary);
}

.metrics span {
  color: var(--muted);
  font-size: 12px;
}

.metrics strong {
  color: var(--text-strong);
  font-size: 24px;
  letter-spacing: -.04em;
}

.metrics .alert svg,
.metrics .alert strong {
  color: var(--danger);
}

.toolbar {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: center;
  padding: 2px 18px 14px;
}

.toolbar nav {
  display: flex;
  gap: 4px;
  padding: 4px;
  background: var(--panel-soft);
  border-radius: 11px;
}

.toolbar button {
  border: 0;
  background: transparent;
  color: var(--muted);
  padding: 8px 12px;
  border-radius: 8px;
  cursor: pointer;
}

.toolbar button.active {
  background: var(--panel);
  color: var(--primary-strong);
  box-shadow: var(--shadow-soft);
}

.toolbar button b {
  margin-left: 3px;
  color: var(--primary);
}

.toolbar select {
  min-height: 40px;
  padding: 8px 34px 8px 11px;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 10px;
  color: inherit;
}

.toolbar select:focus {
  border-color: var(--primary);
  box-shadow: var(--qaly-focus-ring);
  outline: none;
}

.items {
  display: grid;
  gap: 10px;
  padding: 0 18px 18px;
}

.item-card {
  min-height: 66px;
  padding: 13px 18px;
  display: grid;
  grid-template-columns: auto 1fr auto auto;
  gap: 11px;
  align-items: center;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 14px;
  cursor: pointer;
  transition: background-color .22s ease, padding-left .22s ease, box-shadow .22s ease;
}

.item-card:hover {
  padding-left: 22px;
  background: var(--surface-hover);
  box-shadow: var(--shadow-card);
}

.item-card i {
  width: 36px;
  height: 36px;
  display: grid;
  place-items: center;
  border-radius: 10px;
  background: var(--primary-soft);
  color: var(--primary);
  font-style: normal;
}

.item-card i.success {
  color: var(--success);
  background: color-mix(in srgb, var(--success) 10%, transparent);
}

.item-card i.pending {
  color: var(--primary);
  background: var(--primary-soft);
}

.item-card i.danger {
  color: var(--danger);
  background: var(--danger-soft);
}

.item-body {
  display: flex;
  flex-direction: column;
  min-width: 0;
  gap: 5px;
}

.item-body strong {
  color: var(--text-strong);
  font-size: 13px;
}

.item-body small {
  color: var(--muted);
  line-height: 1.45;
}

.task-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.task-tag {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  min-height: 26px;
  padding: 4px 9px;
  border: 1px solid color-mix(in srgb, var(--primary) 30%, var(--line));
  border-radius: 999px;
  background: var(--primary-soft);
  color: var(--primary);
  font-size: 11px;
  font-weight: 700;
  cursor: pointer;
}

.task-tag:hover {
  background: color-mix(in srgb, var(--primary) 20%, var(--panel));
}

.task-tag--muted {
  cursor: default;
  color: var(--muted);
  border-color: var(--line);
  background: var(--panel-soft);
}

.item-card em {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 11px;
  font-style: normal;
  padding: 5px 8px;
  border-radius: 999px;
  background: var(--panel-soft);
  color: var(--muted);
  white-space: nowrap;
}

.item-card .approved,
.item-card .success {
  background: color-mix(in srgb, var(--success) 12%, transparent);
  color: var(--success);
}

.item-card .danger {
  background: var(--danger-soft);
  color: var(--danger);
}

.item-card .pending {
  background: var(--warning-soft);
  color: var(--warning-dark);
}

.external-icon {
  color: var(--text-muted);
}

.items p,
.state {
  padding: 28px;
  text-align: center;
  color: var(--muted);
}

.state {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 8px;
}

.state.empty {
  flex-direction: column;
  min-height: 160px;
  background: linear-gradient(180deg, var(--panel), var(--panel-soft));
}

.state.empty p {
  max-width: 520px;
  margin: 0;
}

.state.error {
  color: var(--danger);
}

.state.error button,
.retry-button {
  border: 0;
  background: transparent;
  color: inherit;
  text-decoration: underline;
  cursor: pointer;
}

footer {
  padding: 12px 18px;
  text-align: right;
  color: var(--muted);
  font-size: 11px;
  border-top: 1px solid var(--line);
  background: var(--panel);
}

.spin {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 800px) {
  .metrics {
    grid-template-columns: repeat(2, 1fr);
  }

  .management__header,
  .toolbar {
    align-items: stretch;
    flex-direction: column;
  }

  .toolbar nav {
    width: 100%;
    overflow: auto;
  }

  .toolbar nav button {
    flex: 1 0 auto;
  }

  .management {
    margin: 2px 16px 18px;
  }
}

@media (max-width: 560px) {
  .item-card {
    grid-template-columns: auto 1fr auto;
  }

  .external-icon {
    grid-column: 3;
  }

  .item-card em {
    grid-column: 2;
    width: fit-content;
  }
}

@media (prefers-reduced-motion: reduce) {
  .management * {
    transition-duration: .01ms !important;
    animation-duration: .01ms !important;
  }
}
</style>
