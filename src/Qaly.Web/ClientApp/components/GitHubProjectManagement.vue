<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { AlertCircle, CheckCircle2, ExternalLink, GitPullRequest, Loader2, Package, RefreshCw, Workflow } from 'lucide-vue-next'
import { githubApi, type GitHubProjectManagement } from '../utils/github-api'
import { errorMessage } from '../utils/api-client'
import { showError, showSuccess } from '../composables/use-toast'

const props = defineProps<{ projectId: string; canManage: boolean }>()
const data = ref<GitHubProjectManagement | null>(null)
const loading = ref(true), syncing = ref(false)
const tab = ref<'pulls' | 'workflows' | 'releases'>('pulls')
const filter = ref('all'), loadError = ref('')
const pulls = computed(() => data.value?.pullRequests.filter(x => filter.value === 'all' || x.state.toLowerCase() === filter.value) ?? [])
const workflows = computed(() => data.value?.workflows.filter(x => filter.value === 'all' || (x.conclusion ?? x.status) === filter.value) ?? [])

onMounted(load)
watch(() => props.projectId, load)
async function load() { loading.value = true; loadError.value = ''; try { data.value = await githubApi.management(props.projectId) } catch (e) { loadError.value = errorMessage(e, 'Không thể tải dữ liệu quản lý GitHub.') } finally { loading.value = false } }
async function sync() { syncing.value = true; try { data.value = await githubApi.syncManagement(props.projectId); showSuccess('Đã đồng bộ dữ liệu mới nhất từ GitHub') } catch (e) { showError(errorMessage(e, 'Không thể đồng bộ GitHub. Hãy kiểm tra quyền của GitHub App.')) } finally { syncing.value = false } }
function switchTab(value: typeof tab.value) { tab.value = value; filter.value = 'all' }
function date(value: string | null) { return value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value)) : 'Chưa có' }
function tone(value: string | null) { return value === 'success' ? 'success' : ['failure', 'timed_out', 'cancelled'].includes(value ?? '') ? 'danger' : 'pending' }
</script>

<template>
  <section class="management">
    <header><div><span>Trung tâm vận hành</span><h3>Quản lý phát triển</h3><p>PR, review, CI và release của toàn bộ repository trong dự án.</p></div><button v-if="canManage" :disabled="syncing" @click="sync"><Loader2 v-if="syncing" class="spin" :size="16" /><RefreshCw v-else :size="16" />{{ syncing ? 'Đang đồng bộ...' : 'Đồng bộ ngay' }}</button></header>
    <div v-if="loading" class="state"><Loader2 class="spin" :size="20" /> Đang tải dữ liệu...</div>
    <div v-else-if="loadError" class="state error"><AlertCircle :size="20" />{{ loadError }} <button @click="load">Thử lại</button></div>
    <template v-else-if="data">
      <div class="metrics"><article><GitPullRequest /><span>PR đang mở</span><strong>{{ data.openPullRequests }}</strong></article><article><AlertCircle /><span>Chờ review</span><strong>{{ data.waitingForReview }}</strong></article><article :class="{ alert: data.failedWorkflows }"><Workflow /><span>CI cần xử lý</span><strong>{{ data.failedWorkflows }}</strong></article><article><Package /><span>Repository</span><strong>{{ data.repositoryCount }}</strong></article></div>
      <div class="toolbar"><nav><button :class="{ active: tab === 'pulls' }" @click="switchTab('pulls')">Pull requests <b>{{ data.pullRequests.length }}</b></button><button :class="{ active: tab === 'workflows' }" @click="switchTab('workflows')">CI/CD <b>{{ data.workflows.length }}</b></button><button :class="{ active: tab === 'releases' }" @click="switchTab('releases')">Releases <b>{{ data.releases.length }}</b></button></nav><select v-if="tab === 'pulls'" v-model="filter"><option value="all">Tất cả trạng thái</option><option value="open">Đang mở</option><option value="merged">Đã merge</option><option value="closed">Đã đóng</option></select><select v-else-if="tab === 'workflows'" v-model="filter"><option value="all">Tất cả kết quả</option><option value="success">Thành công</option><option value="failure">Thất bại</option><option value="in_progress">Đang chạy</option></select></div>
      <div v-if="tab === 'pulls'" class="items"><a v-for="item in pulls" :key="item.id" :href="item.url" target="_blank" rel="noopener"><i><GitPullRequest :size="17" /></i><span><strong>#{{ item.number }} {{ item.title }}</strong><small>{{ item.repository }} · {{ item.headBranch }} → {{ item.baseBranch }} · {{ item.authorLogin || 'GitHub user' }}</small></span><em :class="{ approved: item.approvalCount }"><CheckCircle2 :size="14" />{{ item.approvalCount ? `${item.approvalCount} duyệt` : item.isDraft ? 'Bản nháp' : 'Chờ duyệt' }}</em><ExternalLink :size="14" /></a><p v-if="!pulls.length">Chưa có pull request phù hợp.</p></div>
      <div v-else-if="tab === 'workflows'" class="items"><a v-for="item in workflows" :key="item.runId" :href="item.url" target="_blank" rel="noopener"><i :class="tone(item.conclusion)"><Workflow :size="17" /></i><span><strong>{{ item.title || item.name }}</strong><small>{{ item.repository }} · {{ item.branch }} · {{ date(item.startedAt) }}</small></span><em :class="tone(item.conclusion)">{{ item.conclusion || item.status }}</em><ExternalLink :size="14" /></a><p v-if="!workflows.length">Chưa có workflow run.</p></div>
      <div v-else class="items"><a v-for="item in data.releases" :key="item.repository + item.tagName" :href="item.url" target="_blank" rel="noopener"><i><Package :size="17" /></i><span><strong>{{ item.name || item.tagName }}</strong><small>{{ item.repository }} · {{ item.tagName }} · {{ date(item.publishedAt) }}</small></span><em v-if="item.isPrerelease" class="pending">Pre-release</em><ExternalLink :size="14" /></a><p v-if="!data.releases.length">Chưa có release.</p></div>
      <footer>Đồng bộ gần nhất: {{ date(data.lastSyncedAt) }}</footer>
    </template>
  </section>
</template>

<style scoped>
.management{margin:0 24px 24px;border:1px solid var(--line);border-radius:16px;overflow:hidden;background:var(--panel-soft)}header{display:flex;align-items:center;justify-content:space-between;gap:18px;padding:20px;border-bottom:1px solid var(--line)}header span{font-size:11px;text-transform:uppercase;letter-spacing:.08em;color:#2563eb}h3{margin:3px 0;font-size:20px}header p{margin:0;color:var(--muted);font-size:13px}header button{display:flex;align-items:center;gap:7px;border:0;border-radius:10px;padding:10px 14px;background:#2563eb;color:white;font-weight:700}.metrics{display:grid;grid-template-columns:repeat(4,1fr);gap:10px;padding:16px}.metrics article{display:grid;grid-template-columns:auto 1fr auto;align-items:center;gap:8px;padding:13px;border:1px solid var(--line);border-radius:12px;background:var(--panel)}.metrics svg{width:17px;color:#2563eb}.metrics span{font-size:12px;color:var(--muted)}.metrics strong{font-size:21px}.metrics .alert svg,.metrics .alert strong{color:#d92d20}.toolbar{display:flex;justify-content:space-between;gap:12px;padding:0 16px 12px}.toolbar nav{display:flex;gap:5px}.toolbar button{border:0;background:transparent;color:var(--muted);padding:8px 10px;border-radius:8px}.toolbar button.active{background:var(--panel);color:var(--text);box-shadow:0 1px 4px #0002}.toolbar select{border:1px solid var(--line);border-radius:9px;background:var(--panel);color:inherit;padding:7px}.items{display:grid;gap:1px;border-top:1px solid var(--line);background:var(--line)}.items a{display:grid;grid-template-columns:auto 1fr auto auto;align-items:center;gap:11px;padding:12px 16px;background:var(--panel);color:inherit;text-decoration:none}.items a:hover{background:#2563eb0a}.items i{width:32px;height:32px;display:grid;place-items:center;border-radius:9px;background:#2563eb17;color:#2563eb;font-style:normal}.items a>span{display:flex;flex-direction:column;min-width:0}.items small{color:var(--muted);margin-top:3px}.items em{display:flex;align-items:center;gap:4px;font-size:11px;font-style:normal;padding:5px 8px;border-radius:999px;background:#f2f4f7;color:#667085}.items .approved,.items .success{background:#ecfdf3;color:#067647}.items .danger{background:#fef3f2;color:#b42318}.items .pending{background:#fffaeb;color:#b54708}.items p,.state{padding:28px;text-align:center;color:var(--muted)}.state{display:flex;justify-content:center;gap:8px}.error{color:#b42318}footer{padding:10px 16px;text-align:right;color:var(--muted);font-size:11px;border-top:1px solid var(--line)}.spin{animation:spin 1s linear infinite}@keyframes spin{to{transform:rotate(360deg)}}@media(max-width:800px){.metrics{grid-template-columns:repeat(2,1fr)}header,.toolbar{align-items:stretch;flex-direction:column}.toolbar nav{overflow:auto}.management{margin:0 12px 12px}}@media(max-width:480px){.metrics{grid-template-columns:1fr}}
/* Development operations workspace */
.management {
  margin: 2px 28px 24px;
  overflow: hidden;
  background: #f8fbff;
  border: 1px solid #d7e2ef;
  border-radius: 17px;
  box-shadow: 0 7px 20px rgba(35, 57, 84, .045);
}

.management > header {
  min-height: 96px;
  padding: 20px 22px;
  background: linear-gradient(120deg, #fffdf8, #f3f7ff);
  border-bottom-color: #dbe4f0;
}

.management > header span { color: #1358c8; font-size: 10px; font-weight: 800; letter-spacing: .13em; }
.management > header h3 { margin: 3px 0 4px; color: #14213a; font-size: 21px; letter-spacing: -.025em; }
.management > header p { color: #64748b; line-height: 1.45; }

.management > header button {
  min-height: 42px;
  padding: 10px 15px;
  background: linear-gradient(135deg, #1762d5, #0d4cad);
  border-radius: 11px;
  box-shadow: 0 8px 18px rgba(19, 88, 200, .2);
  cursor: pointer;
  transition: transform .25s ease, box-shadow .25s ease, opacity .25s ease;
}

.management > header button:hover:not(:disabled) { transform: translateY(-1px); box-shadow: 0 10px 22px rgba(19, 88, 200, .28); }
.management > header button:disabled { cursor: wait; opacity: .65; }

.metrics { gap: 12px; padding: 18px 18px 14px; }
.metrics article {
  min-height: 78px;
  padding: 14px;
  background: #fff;
  border-color: #dce5ef;
  border-radius: 13px;
  box-shadow: 0 4px 12px rgba(33, 55, 83, .035);
}
.metrics svg { color: #1358c8; }
.metrics span { color: #64748b; font-size: 12px; }
.metrics strong { color: #14213a; font-size: 24px; letter-spacing: -.04em; }

.toolbar {
  align-items: center;
  padding: 2px 18px 14px;
}
.toolbar nav { gap: 4px; padding: 4px; background: #edf2f8; border-radius: 11px; }
.toolbar button { min-height: 36px; padding: 8px 12px; border-radius: 8px; cursor: pointer; }
.toolbar button.active { background: #fff; color: #0b3f98; box-shadow: 0 3px 10px rgba(31, 54, 83, .1); }
.toolbar button b { margin-left: 3px; color: #1358c8; }
.toolbar select { min-height: 40px; padding: 8px 34px 8px 11px; background: #fff; border-color: #ccd9e8; border-radius: 10px; }
.toolbar select:focus { border-color: #6f9ddd; box-shadow: 0 0 0 3px rgba(19, 88, 200, .1); outline: none; }

.items { gap: 0; background: transparent; border-top-color: #dbe4f0; }
.items a {
  min-height: 66px;
  padding: 13px 18px;
  background: #fff;
  border-bottom: 1px solid #e2e8f0;
  transition: background-color .22s ease, padding-left .22s ease;
}
.items a:hover { padding-left: 22px; background: #f4f8ff; }
.items i { width: 36px; height: 36px; background: #eaf2ff; color: #1358c8; border-radius: 10px; }
.items strong { color: #17243a; font-size: 13px; }
.items small { color: #697990; line-height: 1.45; }
.items em { font-weight: 650; }

.items > p {
  min-height: 118px;
  display: grid;
  place-items: center;
  margin: 0;
  padding: 26px;
  background: linear-gradient(180deg, #f8fbff, #f2f6fb);
  color: #75859b;
}

.management > footer { padding: 12px 18px; background: #fff; border-top-color: #dbe4f0; color: #76869a; }
.state { min-height: 130px; align-items: center; background: linear-gradient(180deg, #fff, #f7faff); }

@media(max-width:800px) {
  .management { margin: 2px 16px 18px; }
  .management > header button { width: 100%; justify-content: center; }
  .toolbar nav { width: 100%; }
  .toolbar nav button { flex: 1 0 auto; }
}

@media(max-width:560px) {
  .items a { grid-template-columns: auto 1fr auto; }
  .items a > em { grid-column: 2; width: fit-content; }
}

@media(prefers-reduced-motion:reduce) {
  .management * { transition-duration: .01ms !important; animation-duration: .01ms !important; }
}
</style>
