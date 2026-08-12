<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { CheckCircle2, CircleDot, ExternalLink, GitCommit, GitPullRequest, Github, Loader2, PackageCheck, RefreshCw, XCircle } from 'lucide-vue-next'
import { githubApi, type TaskDevelopment } from '../utils/github-api'
import { errorMessage } from '../utils/api-client'

const props = defineProps<{ taskId: string }>()
const loading = ref(false)
const loadError = ref('')
const development = ref<TaskDevelopment | null>(null)
const expanded = ref(false)

const activityCount = computed(() => {
  const item = development.value
  return item ? item.commits.length + item.pullRequests.length + item.workflowRuns.length + item.releases.length : 0
})
const latestPullRequest = computed(() => development.value?.pullRequests[0] ?? null)
const latestWorkflow = computed(() => development.value?.workflowRuns[0] ?? null)
const latestCommit = computed(() => development.value?.commits[0] ?? null)
const deliveryStatus = computed(() => {
  const pr = latestPullRequest.value
  const workflow = latestWorkflow.value
  if (workflow?.conclusion && workflowTone(workflow.conclusion) === 'danger') return { tone: 'danger', title: 'Cần xử lý CI', detail: 'Kiểm thử tự động đang thất bại. Chưa nên chuyển sang QA.' }
  if (pr?.state === 'Merged' && workflow?.conclusion === 'success') return { tone: 'success', title: 'Sẵn sàng cho QA', detail: 'Code đã merge và kiểm thử tự động đã qua.' }
  if (pr?.isDraft) return { tone: 'neutral', title: 'Đang phát triển', detail: 'Pull request vẫn ở trạng thái nháp.' }
  if (pr && workflow && workflow.status !== 'completed') return { tone: 'pending', title: 'Đang kiểm tra', detail: 'Pull request đã có, CI đang chạy hoặc đang chờ kết quả.' }
  if (pr) return { tone: 'pending', title: 'Đang chờ review', detail: 'Code đã sẵn sàng để người phụ trách xem xét.' }
  return { tone: 'neutral', title: 'Đang phát triển', detail: 'Đã có hoạt động code nhưng chưa tạo pull request.' }
})

watch(() => props.taskId, load, { immediate: true })

async function load() {
  loading.value = true; loadError.value = ''
  try { development.value = await githubApi.development(props.taskId) }
  catch (error) { loadError.value = errorMessage(error, 'Không thể tải trạng thái phát triển.') }
  finally { loading.value = false }
}
function date(value: string | null) {
  return value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value)) : 'Chưa có'
}
function workflowTone(value: string | null) {
  return value === 'success' ? 'success' : ['failure', 'cancelled', 'timed_out'].includes(value ?? '') ? 'danger' : 'pending'
}
function workflowLabel(value: string | null, status: string) {
  if (value === 'success') return 'CI đã qua'
  if (value === 'failure') return 'CI thất bại'
  if (value === 'cancelled') return 'CI đã hủy'
  return status === 'completed' ? 'CI hoàn tất' : 'CI đang chạy'
}
</script>

<template>
  <section class="development-card">
    <header class="development-head">
      <div class="development-title"><span class="development-icon"><Github :size="18" /></span><div><span>Development</span><strong>Bằng chứng kỹ thuật</strong></div></div>
      <button class="refresh-button" type="button" title="Làm mới" :disabled="loading" @click="load"><Loader2 v-if="loading" class="spin" :size="15" /><RefreshCw v-else :size="15" /></button>
    </header>
    <div v-if="loading && !development" class="development-state"><Loader2 class="spin" :size="18" /> Đang tải dữ liệu GitHub...</div>
    <div v-else-if="loadError" class="development-state is-error"><span>{{ loadError }}</span><button @click="load">Thử lại</button></div>
    <div v-else-if="!development || activityCount === 0" class="development-empty"><CircleDot :size="20" /><div><strong>Chưa có hoạt động GitHub</strong><p>Đặt mã task vào branch, commit hoặc PR, ví dụ <code>QALY-284</code>.</p></div></div>
    <template v-else>
      <div :class="['delivery-message', deliveryStatus.tone]" role="status">
        <component :is="deliveryStatus.tone === 'danger' ? XCircle : deliveryStatus.tone === 'success' ? CheckCircle2 : CircleDot" :size="18" />
        <div><strong>{{ deliveryStatus.title }}</strong><span>{{ deliveryStatus.detail }}</span></div>
      </div>
      <div class="delivery-strip">
        <div :class="['delivery-step', latestPullRequest ? 'active' : '']"><GitPullRequest :size="15" /><span>Pull request</span><strong>{{ latestPullRequest ? `#${latestPullRequest.number}` : 'Chưa có' }}</strong></div>
        <div :class="['delivery-step', workflowTone(latestWorkflow?.conclusion ?? null)]"><component :is="workflowTone(latestWorkflow?.conclusion ?? null) === 'danger' ? XCircle : CheckCircle2" :size="15" /><span>CI / kiểm thử</span><strong>{{ latestWorkflow ? workflowLabel(latestWorkflow.conclusion, latestWorkflow.status) : 'Chưa có' }}</strong></div>
        <div :class="['delivery-step', development.releases.length ? 'success' : '']"><PackageCheck :size="15" /><span>Release</span><strong>{{ development.releases[0]?.tagName ?? 'Chưa có' }}</strong></div>
      </div>

      <article v-if="latestPullRequest" class="primary-activity">
        <div class="activity-mark pr"><GitPullRequest :size="17" /></div><div class="activity-copy"><span>{{ latestPullRequest.repository }}</span><a :href="latestPullRequest.url" target="_blank" rel="noopener">{{ latestPullRequest.title }} <ExternalLink :size="12" /></a><small>{{ latestPullRequest.headBranch }} → {{ latestPullRequest.baseBranch }} · {{ latestPullRequest.approvalCount }}/{{ latestPullRequest.reviewCount }} approval</small></div><span :class="['state-pill', latestPullRequest.state.toLowerCase()]">{{ latestPullRequest.isDraft ? 'Draft' : latestPullRequest.state }}</span>
      </article>
      <article v-if="latestWorkflow" class="primary-activity">
        <div :class="['activity-mark', workflowTone(latestWorkflow.conclusion)]"><CheckCircle2 :size="17" /></div><div class="activity-copy"><span>{{ latestWorkflow.repository }}</span><a :href="latestWorkflow.url" target="_blank" rel="noopener">{{ latestWorkflow.workflowName }} <ExternalLink :size="12" /></a><small>{{ latestWorkflow.branch }} · {{ date(latestWorkflow.startedAt) }}</small></div><span :class="['state-pill', workflowTone(latestWorkflow.conclusion)]">{{ workflowLabel(latestWorkflow.conclusion, latestWorkflow.status) }}</span>
      </article>
      <article v-if="latestCommit" class="primary-activity">
        <div class="activity-mark"><GitCommit :size="17" /></div><div class="activity-copy"><span>{{ latestCommit.repository }}</span><a :href="latestCommit.url" target="_blank" rel="noopener">{{ latestCommit.message }} <ExternalLink :size="12" /></a><small>{{ latestCommit.sha.slice(0, 7) }} · {{ latestCommit.authorLogin ?? 'GitHub user' }} · {{ date(latestCommit.committedAt) }}</small></div>
      </article>

      <button v-if="activityCount > 3" class="show-more" type="button" @click="expanded = !expanded">{{ expanded ? 'Thu gọn' : `Xem toàn bộ ${activityCount} hoạt động` }}</button>
      <div v-if="expanded" class="activity-history">
        <a v-for="commit in development.commits.slice(1)" :key="commit.sha" :href="commit.url" target="_blank"><GitCommit :size="14" /><span>{{ commit.message }}</span><small>{{ commit.sha.slice(0, 7) }}</small></a>
        <a v-for="release in development.releases" :key="release.tagName" :href="release.url" target="_blank"><PackageCheck :size="14" /><span>{{ release.name || release.tagName }}</span><small>{{ date(release.publishedAt) }}</small></a>
      </div>
    </template>
  </section>
</template>

<style scoped>
.delivery-message{display:flex;align-items:center;gap:10px;padding:12px 15px;border-bottom:1px solid var(--line);background:var(--panel-soft)}.delivery-message>div{display:flex;flex-direction:column;gap:2px}.delivery-message span{font-size:11px;color:var(--muted)}.delivery-message.success{color:#067647;background:#ecfdf3}.delivery-message.danger{color:#b42318;background:#fef3f2}.delivery-message.pending{color:#175cd3;background:#eff8ff}.delivery-message.neutral{color:#475467}
.development-card{border:1px solid var(--line);border-radius:16px;background:var(--panel);overflow:hidden}.development-head{display:flex;justify-content:space-between;align-items:center;padding:14px 16px;border-bottom:1px solid var(--line)}.development-title{display:flex;align-items:center;gap:10px}.development-title>div{display:flex;flex-direction:column}.development-title>div span{font-size:11px;text-transform:uppercase;letter-spacing:.08em;color:var(--muted)}.development-icon{width:34px;height:34px;border-radius:10px;display:grid;place-items:center;background:#24292f;color:#fff}.refresh-button{width:32px;height:32px;border:1px solid var(--line);border-radius:9px;background:transparent;color:inherit;display:grid;place-items:center}.development-state,.development-empty{display:flex;align-items:center;justify-content:center;gap:10px;min-height:100px;padding:18px;color:var(--muted)}.development-empty{justify-content:flex-start}.development-empty p{margin:3px 0 0;font-size:12px}.development-empty code{padding:2px 5px;border-radius:5px;background:var(--panel-soft)}.is-error{color:#b42318}.is-error button{border:0;background:transparent;color:inherit;text-decoration:underline}.delivery-strip{display:grid;grid-template-columns:repeat(3,1fr);border-bottom:1px solid var(--line)}.delivery-step{display:grid;grid-template-columns:auto 1fr;gap:2px 7px;padding:12px;color:var(--muted);border-right:1px solid var(--line)}.delivery-step:last-child{border:0}.delivery-step span{font-size:11px}.delivery-step strong{grid-column:2;font-size:12px;color:var(--text)}.delivery-step.active{color:#7f56d9}.delivery-step.success{color:#12b76a}.delivery-step.danger{color:#d92d20}.primary-activity{display:grid;grid-template-columns:auto 1fr auto;gap:10px;align-items:center;padding:12px 15px;border-bottom:1px solid var(--line)}.activity-mark{width:32px;height:32px;border-radius:9px;display:grid;place-items:center;background:var(--panel-soft);color:#475467}.activity-mark.pr{color:#7f56d9}.activity-mark.success{color:#12b76a;background:rgba(18,183,106,.1)}.activity-mark.danger{color:#d92d20;background:rgba(217,45,32,.1)}.activity-copy{min-width:0;display:flex;flex-direction:column;gap:2px}.activity-copy>span,.activity-copy small{font-size:11px;color:var(--muted)}.activity-copy a{display:inline-flex;align-items:center;gap:4px;font-size:13px;font-weight:600;color:inherit;text-decoration:none;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.state-pill{font-size:10px;padding:4px 7px;border-radius:999px;background:var(--panel-soft);white-space:nowrap}.state-pill.merged,.state-pill.success{color:#067647;background:#ecfdf3}.state-pill.open,.state-pill.pending{color:#175cd3;background:#eff8ff}.state-pill.danger{color:#b42318;background:#fef3f2}.show-more{width:100%;padding:10px;border:0;background:transparent;color:#2563eb;font-size:12px}.activity-history a{display:grid;grid-template-columns:auto 1fr auto;gap:8px;padding:9px 15px;color:inherit;text-decoration:none;font-size:12px;border-top:1px solid var(--line)}.activity-history small{color:var(--muted)}.spin{animation:spin 1s linear infinite}@keyframes spin{to{transform:rotate(360deg)}}@media(max-width:560px){.delivery-strip{grid-template-columns:1fr}.delivery-step{border-right:0;border-bottom:1px solid var(--line)}.primary-activity{grid-template-columns:auto 1fr}.state-pill{grid-column:2;justify-self:start}}
</style>
