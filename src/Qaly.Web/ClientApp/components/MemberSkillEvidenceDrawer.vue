<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { AlertTriangle, BookOpenCheck, ChevronRight, Clock3, ExternalLink, LoaderCircle, ShieldCheck, X } from 'lucide-vue-next'
import { apiResult, errorMessage } from '../utils/api-client'

type Source = { attributionId:string; taskId:string; taskTitle:string | null; taskUrl:string | null; isRestricted:boolean; completedAt:string; requiredLevel:string }
type Skill = { skillId:string; skillName:string; evidenceBand:'emerging'|'practiced'|'experienced'; confidence:number; verifiedTaskCount:number; restrictedTaskCount:number; mostRecentCompletedAt:string|null; isStale:boolean; sources:Source[] }
type Profile = { organizationId:string; memberId:string; memberName:string; isSelf:boolean; canManageEvidence:boolean; evidenceMethodVersion:string; skills:Skill[]; pendingCorrectionCount:number; emptyState:string }

const props = defineProps<{ organizationId:string; memberId:string; memberName:string }>()
const emit = defineEmits<{ close: [] }>()
const profile = ref<Profile | null>(null)
const loading = ref(true)
const error = ref('')
let requestVersion = 0

const subtitle = computed(() => profile.value?.isSelf ? 'Bằng chứng kỹ năng của bạn' : `Bằng chứng kỹ năng của ${props.memberName}`)
function bandLabel(value: Skill['evidenceBand']) { return value === 'experienced' ? 'Đã có kinh nghiệm' : value === 'practiced' ? 'Đã thực hành' : 'Đang hình thành' }
async function load() { const version=++requestVersion; loading.value=true; error.value=''; try { const next=await apiResult<Profile>(`/api/organizations/${props.organizationId}/members/${props.memberId}/skill-evidence`); if(version===requestVersion)profile.value=next } catch(cause) { if(version===requestVersion)error.value=errorMessage(cause,'Không thể tải hồ sơ bằng chứng kỹ năng.') } finally { if(version===requestVersion)loading.value=false } }
function date(value:string|null) { return value ? new Date(value).toLocaleDateString('vi-VN') : '—' }
watch(() => [props.organizationId, props.memberId], load, { immediate:true })
onBeforeUnmount(() => { requestVersion += 1 })
</script>

<template>
  <div class="evidence-backdrop" @click.self="emit('close')" @keydown.esc="emit('close')">
    <aside class="evidence-drawer" role="dialog" aria-modal="true" :aria-label="subtitle">
      <header><div><span class="kicker"><ShieldCheck :size="15" /> Hồ sơ bằng chứng</span><h2>{{ props.memberName }}</h2><p>{{ subtitle }}</p></div><button class="icon" type="button" aria-label="Đóng" @click="emit('close')"><X :size="19" /></button></header>
      <div class="method"><BookOpenCheck :size="16" /><span>Band tính theo <strong>{{ profile?.evidenceMethodVersion ?? 'member-skill-evidence.v1' }}</strong>: chỉ task Done có contributor được manager xác nhận. Đây không phải điểm hiệu suất.</span></div>
      <div v-if="loading" class="state"><LoaderCircle :size="22" class="spin" /> Đang đối soát bằng chứng…</div>
      <div v-else-if="error" class="state error" role="alert"><AlertTriangle :size="22" /> {{ error }}</div>
      <template v-else-if="profile">
        <div v-if="profile.pendingCorrectionCount" class="pending"><Clock3 :size="16" /> {{ profile.pendingCorrectionCount }} yêu cầu rà soát đang chờ manager xử lý; các record đó không được tính vào band.</div>
        <div v-if="!profile.skills.length" class="state"><ShieldCheck :size="22" /> {{ profile.emptyState }}</div>
        <section v-else class="skill-list" data-testid="member-skill-evidence-drawer">
          <article v-for="skill in profile.skills" :key="skill.skillId" class="skill-card">
            <div class="skill-card__top"><div><h3>{{ skill.skillName }}</h3><span :class="['band', skill.evidenceBand]">{{ bandLabel(skill.evidenceBand) }}</span></div><strong>{{ Math.round(skill.confidence * 100) }}%</strong></div>
            <div class="skill-meta"><span>{{ skill.verifiedTaskCount }} task đã xác nhận</span><span v-if="skill.restrictedTaskCount">{{ skill.restrictedTaskCount }} nguồn hạn chế</span><span :class="{ stale: skill.isStale }">Mới nhất: {{ date(skill.mostRecentCompletedAt) }}</span></div>
            <div class="sources"><span>Nguồn bằng chứng</span><div v-for="source in skill.sources" :key="source.attributionId" class="source"><span v-if="source.isRestricted"><ShieldCheck :size="14" /> Nguồn hạn chế</span><a v-else :href="source.taskUrl ?? '#'" @click="!source.taskUrl && $event.preventDefault()">{{ source.taskTitle }} <ExternalLink :size="13" /></a><small>{{ source.requiredLevel }} · {{ date(source.completedAt) }}</small></div></div>
          </article>
        </section>
        <footer><ChevronRight :size="15" /> Nếu attribution không chính xác, mở task nguồn và chọn “Yêu cầu rà soát”. Bản thân AI không thể nâng band hoặc thêm bằng chứng.</footer>
      </template>
    </aside>
  </div>
</template>

<style scoped>
.evidence-backdrop{position:fixed;inset:0;z-index:1200;display:flex;justify-content:flex-end;background:rgba(15,23,42,.42)}.evidence-drawer{width:min(580px,100%);height:100%;overflow:auto;background:var(--surface,#fff);color:var(--text-primary,#172033);padding:24px;box-shadow:-20px 0 55px rgba(15,23,42,.2)}header{display:flex;justify-content:space-between;gap:16px;border-bottom:1px solid var(--border-color,#e2e8f0);padding-bottom:16px}h2,h3,p{margin:0}.kicker{display:inline-flex;align-items:center;gap:6px;color:#0f766e;font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.04em}header h2{margin-top:6px;font-size:24px}header p{margin-top:4px;color:var(--text-secondary,#64748b);font-size:14px}.icon{height:36px;width:36px;border:0;border-radius:9px;background:var(--panel-soft,#f1f5f9);color:inherit;display:grid;place-items:center;cursor:pointer}.method,.pending,.state,footer{display:flex;gap:8px;align-items:flex-start;padding:12px;margin-top:14px;border-radius:10px;font-size:13px;line-height:1.45}.method{background:#ecfdf5;color:#166534}.pending{background:#fffbeb;color:#9a6700}.state{justify-content:center;background:var(--panel-soft,#f8fafc);color:var(--text-secondary,#64748b);min-height:80px;align-items:center;text-align:center}.state.error{background:#fef3f2;color:#b42318}.skill-list{display:grid;gap:10px;margin-top:14px}.skill-card{padding:14px;border:1px solid var(--border-color,#dbe2ea);border-radius:12px}.skill-card__top{display:flex;justify-content:space-between;gap:12px}.skill-card h3{font-size:16px;margin-bottom:6px}.skill-card__top>strong{font-size:14px;color:#0f766e}.band{display:inline-flex;border-radius:999px;padding:3px 8px;font-size:11px;font-weight:800}.band.emerging{background:#e0f2fe;color:#075985}.band.practiced{background:#dcfce7;color:#166534}.band.experienced{background:#ede9fe;color:#5b21b6}.skill-meta{display:flex;gap:9px;flex-wrap:wrap;margin-top:11px;color:var(--text-secondary,#64748b);font-size:12px}.stale{color:#b45309}.sources{display:grid;gap:6px;margin-top:13px;font-size:12px}.sources>span{font-weight:800;color:var(--text-secondary,#64748b);text-transform:uppercase;letter-spacing:.03em}.source{display:flex;align-items:center;gap:7px;flex-wrap:wrap}.source a,.source span{display:inline-flex;align-items:center;gap:4px;color:#0f766e;font-weight:700;text-decoration:none}.source small{color:var(--text-secondary,#64748b)}footer{color:var(--text-secondary,#64748b);background:var(--panel-soft,#f8fafc);margin-bottom:12px}@keyframes spin{to{transform:rotate(360deg)}}.spin{animation:spin 1s linear infinite}@media(max-width:620px){.evidence-drawer{padding:18px}.skill-meta{display:grid;gap:4px}}@media(prefers-reduced-motion:reduce){.spin{animation:none}}
</style>
