<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowUpRight, Building2, FolderKanban, MessageSquare, Plus, RefreshCw, Settings2, ShieldCheck, Users, X } from 'lucide-vue-next'
import OrganizationActivityTab from '../components/organization/OrganizationActivityTab.vue'
import OrganizationCapacityTab from '../components/organization/OrganizationCapacityTab.vue'
import OrganizationProfessionalProfilesTab from '../components/organization/OrganizationProfessionalProfilesTab.vue'
import OrganizationRulebookTab from '../components/organization/OrganizationRulebookTab.vue'
import OrganizationSkillsTab from '../components/organization/OrganizationSkillsTab.vue'
import AiUsageBudgetSettingsTab from '../components/settings/AiUsageBudgetSettingsTab.vue'
import PageStatePanel from '../components/PageStatePanel.vue'
import { useDashboardContext } from '../composables/dashboard-context'
import { showError, showSuccess } from '../composables/use-toast'
import type { PagedResult, ProjectDto, UserDto } from '../types'
import { apiResult, errorMessage } from '../utils/api-client'
import { hasNativeOrganizationManagement, type OrganizationActorContext } from '../utils/organization-access'

type Organization = { id:string; name:string; code:string; description?:string|null; ownerId:string; ownerName:string; memberCount:number; projectCount:number; isActive:boolean }
type Group = { id:string; name:string; memberCount:number; organizationId?:string|null; currentUserRole:string; status:string }
type ProjectMembership = { projectId:string; projectName:string; projectCode:string; role:string }
type OrganizationMember = { userId:string; fullName:string; email:string; role:string; projects:ProjectMembership[]|null; weeklyCapacityHours:number|null; capacityState:string|null; skills:string[]|null }

const route = useRoute()
const router = useRouter()
const { loadDashboard } = useDashboardContext()
const organization = ref<Organization|null>(null)
const me = ref<UserDto|null>(null)
const projects = ref<ProjectDto[]>([])
const groups = ref<Group[]>([])
const members = ref<OrganizationMember[]>([])
const moderatorCapabilities = ref<string[]>([])
const isLoading = ref(true)
const isSaving = ref(false)
const error = ref('')
const activeTab = ref('overview')
const projectModalOpen = ref(false)
const groupModalOpen = ref(false)
const projectForm = reactive({ name:'', code:'', description:'', sourceGroupId:'' })
const groupForm = reactive({ name:'', color:'#287a55' })

const organizationId = computed(() => String(route.params.organizationId ?? ''))
const activeProjects = computed(() => projects.value.filter(project => project.status !== 'Archived'))
const linkedGroups = computed(() => groups.value.filter(group => group.organizationId === organizationId.value && group.status !== 'Archived'))
const membership = computed(() => members.value.find(item => item.userId === me.value?.id) ?? null)
const accessContext = computed<OrganizationActorContext>(() => ({ systemRole:me.value?.role, actorId:me.value?.id, ownerId:organization.value?.ownerId, membershipRole:membership.value?.role, hasMembership:!!membership.value, moderatorCapabilities:moderatorCapabilities.value }))
const canManage = computed(() => hasNativeOrganizationManagement(accessContext.value))
const tabs = [
  { id:'overview', label:'Tổng quan' }, { id:'members', label:'Thành viên' },
  { id:'skills', label:'Skills' }, { id:'profiles', label:'Professional Profiles' },
  { id:'capacity', label:'Capacity' }, { id:'ai-budget', label:'AI Budget' },
  { id:'rulebook', label:'Work Rulebook' }, { id:'activity', label:'Activity' },
]

async function load() {
  if (!organizationId.value) return
  isLoading.value = true
  error.value = ''
  try {
    const [loadedMe, loadedOrganization, projectPage, groupPage, loadedMembers, capabilities] = await Promise.all([
      apiResult<UserDto>('/api/auth/me'),
      apiResult<Organization>(`/api/organizations/${organizationId.value}`),
      apiResult<PagedResult<ProjectDto>>(`/api/projects?pageSize=100&organizationId=${organizationId.value}`),
      apiResult<PagedResult<Group>>(`/api/groups?pageSize=100&organizationId=${organizationId.value}`),
      apiResult<OrganizationMember[]>(`/api/organizations/${organizationId.value}/users`),
      apiResult<string[]>(`/api/organizations/${organizationId.value}/moderator-capabilities`),
    ])
    me.value=loadedMe; organization.value=loadedOrganization; projects.value=projectPage.items
    groups.value=groupPage.items; members.value=loadedMembers; moderatorCapabilities.value=capabilities
  } catch (cause) {
    error.value = errorMessage(cause, 'Không thể tải tổng quan tổ chức.')
  } finally { isLoading.value = false }
}

function openProject(projectId:string) { void router.push({ name:'project-detail', params:{ projectId } }) }
function openGroup(groupId:string) { void router.push({ name:'group-detail', params:{ groupId } }) }
function openMembers() { void router.push({ path:'/organizations/users', query:{ organization:organizationId.value } }) }
function openProjectModal(sourceGroupId='') { Object.assign(projectForm,{ name:'',code:'',description:'',sourceGroupId }); projectModalOpen.value=true }
function groupProject(groupId:string) { return projects.value.find(project => project.sourceGroupId === groupId) ?? null }
function roleLabel(role:string) { return ({Owner:'Chủ sở hữu',OrganizationAdmin:'Quản trị tổ chức',Manager:'Quản lý',Member:'Thành viên'} as Record<string,string>)[role] ?? role }

async function createProject() {
  if (!projectForm.name.trim() || isSaving.value) return
  isSaving.value=true
  try {
    let created:ProjectDto
    if (projectForm.sourceGroupId) {
      const result=await apiResult<{project:ProjectDto}>(`/api/groups/${projectForm.sourceGroupId}/create-project`,{method:'POST',body:JSON.stringify({name:projectForm.name.trim(),code:projectForm.code.trim()||null,description:projectForm.description.trim()||null,startDate:null,endDate:null})})
      created=result.project
    } else {
      created=await apiResult<ProjectDto>('/api/projects',{method:'POST',body:JSON.stringify({name:projectForm.name.trim(),code:projectForm.code.trim()||null,description:projectForm.description.trim()||null,logoUrl:null,startDate:null,endDate:null,organizationId:organizationId.value,sourceGroupId:null})})
    }
    projectModalOpen.value=false
    showSuccess(projectForm.sourceGroupId ? 'Đã tạo project từ group và đồng bộ thành viên.' : 'Đã tạo project trong organization.')
    await Promise.all([load(), loadDashboard()]); openProject(created.id)
  } catch (cause) { showError(errorMessage(cause,'Không thể tạo project.')) }
  finally { isSaving.value=false }
}

async function createGroup() {
  if (!groupForm.name.trim() || isSaving.value) return
  isSaving.value=true
  try {
    await apiResult<Group>('/api/groups',{method:'POST',body:JSON.stringify({name:groupForm.name.trim(),organizationId:organizationId.value,color:groupForm.color,avatarUrl:null})})
    groupModalOpen.value=false; Object.assign(groupForm,{name:'',color:'#287a55'})
    showSuccess('Đã thêm group vào organization.'); await load()
  } catch (cause) { showError(errorMessage(cause,'Không thể tạo group.')) }
  finally { isSaving.value=false }
}

onMounted(load)
</script>

<template>
  <main class="organization-overview">
    <PageStatePanel v-if="isLoading" variant="loading" title="Đang tải tổng quan tổ chức" message="Đang đồng bộ project, group, thành viên và cấu hình workspace." />
    <PageStatePanel v-else-if="error || !organization" variant="error" title="Không thể tải tổ chức" :message="error || 'Tổ chức không tồn tại, đã inactive hoặc bạn không có quyền truy cập.'">
      <template #icon><RefreshCw :size="22" /></template><template #actions><button class="primary" type="button" @click="load">Thử lại</button></template>
    </PageStatePanel>

    <template v-else>
      <header class="overview-head">
        <div><button type="button" class="back-link" @click="router.push({name:'organizations'})">← Danh sách tổ chức</button><span class="kicker"><Building2 :size="16" /> Workspace doanh nghiệp</span><h1>{{ organization.name }}</h1><p>{{ organization.description || 'Trung tâm điều hành project, nhóm cộng tác, thành viên và policy.' }}</p></div>
        <div class="head-actions"><button v-if="canManage" type="button" class="primary" data-testid="create-organization-project" @click="openProjectModal()"><Plus :size="17" /> Tạo project</button><button type="button" class="secondary" @click="openMembers"><Users :size="17" /> Quản lý thành viên</button><button type="button" class="icon-button" title="Tải lại" aria-label="Tải lại tổng quan tổ chức" @click="load"><RefreshCw :size="18" /></button></div>
      </header>

      <nav class="org-tabs" aria-label="Khu vực organization"><button v-for="tab in tabs" :key="tab.id" type="button" :class="{active:activeTab===tab.id}" @click="activeTab=tab.id">{{ tab.label }}</button></nav>

      <template v-if="activeTab==='overview'">
        <section class="metric-grid" aria-label="Chỉ số tổ chức">
          <article><span><FolderKanban :size="18" /> Project</span><strong>{{ activeProjects.length }}</strong><small>{{ organization.projectCount }} tổng số</small></article>
          <article><span><MessageSquare :size="18" /> Group</span><strong>{{ linkedGroups.length }}</strong><small>{{ linkedGroups.filter(group=>!groupProject(group.id)).length }} chưa liên kết project</small></article>
          <article><span><Users :size="18" /> Thành viên</span><strong>{{ members.length }}</strong><small>Trong organization</small></article>
          <article><span><ShieldCheck :size="18" /> Trạng thái</span><strong>{{ organization.isActive?'Active':'Inactive' }}</strong><small>{{ organization.code }}</small></article>
        </section>

        <section class="overview-grid">
          <div class="panel"><div class="panel-head"><div><span class="eyebrow">Delivery</span><h2>Project trong organization</h2></div><button v-if="canManage" type="button" class="text-link" @click="openProjectModal()"><Plus :size="16" /> Tạo project</button></div><div v-if="!activeProjects.length" class="empty">Chưa có project nào thuộc organization này.</div><button v-for="project in activeProjects" :key="project.id" type="button" class="list-row" @click="openProject(project.id)"><span class="row-icon"><FolderKanban :size="17" /></span><span class="row-copy"><strong>{{ project.name }}</strong><small>{{ project.code }} · {{ project.memberCount }} thành viên · {{ project.taskCount }} task</small></span><span class="progress"><span :style="{width:`${Math.min(100,Math.max(0,project.progressPercentage))}%`}" /><small>{{ Math.round(project.progressPercentage) }}%</small></span><ArrowUpRight :size="17" /></button></div>
          <div class="panel"><div class="panel-head"><div><span class="eyebrow">Collaboration</span><h2>Group thuộc organization</h2></div><button v-if="canManage" type="button" class="text-link" data-testid="create-organization-group" @click="groupModalOpen=true"><Plus :size="16" /> Thêm group</button></div><div v-if="!linkedGroups.length" class="empty">Chưa có group nào được gắn vào organization.</div><article v-for="group in linkedGroups" :key="group.id" class="group-row"><button type="button" class="group-main" @click="openGroup(group.id)"><span class="row-icon group-icon"><MessageSquare :size="17" /></span><span class="row-copy"><strong>{{ group.name }}</strong><small>{{ group.memberCount }} thành viên</small></span><ArrowUpRight :size="17" /></button><div class="group-link"><span :class="{linked:!!groupProject(group.id)}">{{ groupProject(group.id)?`Đã liên kết ${groupProject(group.id)?.code}`:'Chưa liên kết project' }}</span><button v-if="canManage" type="button" @click="openProjectModal(group.id)">{{ groupProject(group.id)?'Tạo project khác':'Tạo project từ group' }}</button></div></article></div>
        </section>
      </template>

      <section v-else-if="activeTab==='members'" class="panel tab-panel" data-testid="organization-members-tab">
        <div class="panel-head"><div><span class="eyebrow">People directory</span><h2>Thành viên, project role, skill và capacity</h2></div><button type="button" class="primary" @click="openMembers">Quản lý role</button></div>
        <div class="member-table"><table><thead><tr><th>Thành viên</th><th>Vai trò tổ chức</th><th>Project / role</th><th>Skills</th><th>Capacity</th></tr></thead><tbody><tr v-for="member in members" :key="member.userId"><td><strong>{{ member.fullName }}</strong><small>{{ member.email }}</small></td><td>{{ roleLabel(member.role) }}</td><td><span v-for="project in member.projects??[]" :key="project.projectId" class="tag">{{ project.projectName }} ({{ project.projectCode }}) · {{ project.role }}</span><small v-if="!member.projects?.length">Chưa tham gia project</small></td><td><span v-for="skill in (member.skills??[]).slice(0,3)" :key="skill" class="tag skill">{{ skill }}</span><small v-if="!member.skills?.length">Chưa có bằng chứng</small></td><td>{{ member.weeklyCapacityHours==null?'Không có quyền xem':`${member.weeklyCapacityHours}h/tuần` }}<small v-if="member.capacityState==='assumed_default'">Mặc định</small></td></tr></tbody></table></div>
      </section>

      <section v-else class="panel tab-panel config-shell">
        <OrganizationSkillsTab v-if="activeTab==='skills'" :organization-id="organizationId" :can-manage="canManage" />
        <OrganizationProfessionalProfilesTab v-else-if="activeTab==='profiles'" :organization-id="organizationId" :can-manage="canManage" />
        <OrganizationCapacityTab v-else-if="activeTab==='capacity'" :organization-id="organizationId" :can-manage="canManage" :current-user-id="me?.id" />
        <AiUsageBudgetSettingsTab v-else-if="activeTab==='ai-budget'" :organization-id="organizationId" />
        <OrganizationRulebookTab v-else-if="activeTab==='rulebook'" :organization-id="organizationId" :can-manage="canManage" />
        <OrganizationActivityTab v-else-if="activeTab==='activity'" :organization-id="organizationId" />
      </section>
      <section class="next-step"><Settings2 :size="20" /><span><strong>Cấu hình tập trung</strong><small>Skills, Professional Profiles, Capacity, AI Budget và Work Rulebook đã nằm trong cùng organization.</small></span></section>
    </template>

    <div v-if="projectModalOpen" class="overlay" @click.self="projectModalOpen=false" @keydown.esc="projectModalOpen=false"><form class="modal" role="dialog" aria-modal="true" aria-labelledby="create-project-title" @submit.prevent="createProject"><header><div><span class="eyebrow">{{ projectForm.sourceGroupId?'From group':'Organization project' }}</span><h2 id="create-project-title">Tạo project mới</h2></div><button type="button" class="icon-button" aria-label="Đóng" @click="projectModalOpen=false"><X :size="18" /></button></header><label>Tên project<input v-model="projectForm.name" required maxlength="180" autofocus /></label><label>Mã project<input v-model="projectForm.code" maxlength="80" placeholder="Để trống để tự sinh" /></label><label>Mô tả<textarea v-model="projectForm.description" rows="3" /></label><p v-if="projectForm.sourceGroupId" class="modal-note">Thành viên group sẽ được đồng bộ sang project theo role tương ứng.</p><footer><button type="button" class="secondary" @click="projectModalOpen=false">Hủy</button><button class="primary" data-testid="submit-organization-project" :disabled="isSaving">Tạo và mở project</button></footer></form></div>
    <div v-if="groupModalOpen" class="overlay" @click.self="groupModalOpen=false" @keydown.esc="groupModalOpen=false"><form class="modal" role="dialog" aria-modal="true" aria-labelledby="create-group-title" @submit.prevent="createGroup"><header><div><span class="eyebrow">Organization group</span><h2 id="create-group-title">Thêm group</h2></div><button type="button" class="icon-button" aria-label="Đóng" @click="groupModalOpen=false"><X :size="18" /></button></header><label>Tên group<input v-model="groupForm.name" required maxlength="160" autofocus /></label><label>Màu nhận diện<input v-model="groupForm.color" type="color" /></label><footer><button type="button" class="secondary" @click="groupModalOpen=false">Hủy</button><button class="primary" data-testid="submit-organization-group" :disabled="isSaving">Thêm group</button></footer></form></div>
  </main>
</template>

<style scoped>
.organization-overview{max-width:1280px;margin:0 auto;padding:32px 28px 56px;color:#17221d}.overview-head{display:flex;justify-content:space-between;gap:24px;align-items:flex-end;margin-bottom:22px}.back-link,.text-link{border:0;background:transparent;color:#287a55;cursor:pointer;font-weight:700;padding:0}.back-link{display:block;margin-bottom:22px}.kicker,.eyebrow{display:inline-flex;align-items:center;gap:8px;color:#287a55;font-size:12px;font-weight:800;letter-spacing:.08em;text-transform:uppercase}h1{margin:8px 0 6px;font-size:clamp(32px,4vw,52px)}h2{margin:5px 0 0;font-size:19px}.overview-head p{max-width:660px;margin:0;color:#637169}.head-actions,.panel-head,.next-step{display:flex;align-items:center;gap:12px}button{font:inherit}.primary,.secondary,.icon-button{display:inline-flex;align-items:center;justify-content:center;gap:8px;border-radius:8px;cursor:pointer}.primary{border:1px solid #176b45;background:#176b45;color:#fff;padding:10px 15px;font-weight:750}.secondary{border:1px solid #c9d6cd;background:#fff;color:#245b43;padding:10px 14px;font-weight:750}.icon-button{width:40px;height:40px;border:1px solid #c9d6cd;background:#fff;color:#245b43}.org-tabs{display:flex;gap:4px;overflow:auto;margin-bottom:18px;padding:5px;border:1px solid #dbe5de;border-radius:10px;background:#f7faf8}.org-tabs button{flex:0 0 auto;border:0;border-radius:7px;background:transparent;color:#617067;padding:9px 12px;cursor:pointer;font-size:13px;font-weight:750}.org-tabs button.active{background:#fff;color:#176b45;box-shadow:0 2px 8px rgba(38,71,52,.1)}.metric-grid{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin-bottom:18px}.metric-grid article,.panel,.next-step{border:1px solid #dbe5de;background:#fff;box-shadow:0 10px 28px rgba(38,71,52,.06)}.metric-grid article{min-height:118px;padding:18px;border-radius:8px}.metric-grid span,.metric-grid small{display:flex;align-items:center;gap:7px;color:#6c7b72;font-size:13px}.metric-grid strong{display:block;margin:12px 0 2px;font-size:30px}.overview-grid{display:grid;grid-template-columns:1.08fr .92fr;gap:18px}.panel{min-width:0;padding:22px;border-radius:8px}.panel-head{justify-content:space-between;margin-bottom:18px}.text-link{display:inline-flex;align-items:center;gap:4px;white-space:nowrap}.list-row,.group-main{width:100%;display:flex;align-items:center;gap:12px;border:0;background:transparent;color:inherit;text-align:left;padding:14px 0;cursor:pointer}.list-row{border-top:1px solid #edf1ee}.list-row:hover,.group-main:hover{color:#176b45}.row-icon{display:grid;place-items:center;width:36px;height:36px;flex:0 0 36px;border-radius:8px;background:#eaf5ee;color:#176b45}.group-icon{background:#eef1f7;color:#4c638b}.row-copy{min-width:0;flex:1}.row-copy strong,.row-copy small{display:block;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.row-copy small{margin-top:4px;color:#718077;font-size:12px}.progress{width:80px;flex:0 0 80px}.progress>span{display:block;height:6px;border-radius:4px;background:#4e9a6c}.progress small{display:block;margin-top:4px;color:#718077;font-size:11px;text-align:right}.group-row{border-top:1px solid #edf1ee}.group-link{display:flex;align-items:center;justify-content:space-between;gap:10px;padding:0 0 12px 48px}.group-link span{padding:4px 8px;border-radius:99px;background:#fff3d8;color:#916315;font-size:11px}.group-link span.linked{background:#eaf5ee;color:#287a55}.group-link button{border:0;background:transparent;color:#287a55;cursor:pointer;font-size:12px;font-weight:750}.empty{padding:25px 0 10px;color:#718077;font-size:14px}.tab-panel{min-height:260px}.config-shell{padding:24px}.member-table{overflow:auto}.member-table table{width:100%;min-width:900px;border-collapse:collapse}.member-table th,.member-table td{padding:12px;text-align:left;border-top:1px solid #e8eeea;vertical-align:top}.member-table th{color:#718077;font-size:11px;text-transform:uppercase}.member-table td>strong,.member-table td>small{display:block}.member-table small{margin-top:3px;color:#718077;font-size:11px}.tag{display:inline-flex;margin:0 4px 4px 0;padding:4px 7px;border-radius:99px;background:#eef3f0;color:#385b49;font-size:11px}.tag.skill{background:#eef3ff;color:#45609a}.next-step{margin-top:18px;padding:16px 18px;border-radius:8px;color:#176b45}.next-step span{flex:1}.next-step strong,.next-step small{display:block}.next-step small{margin-top:3px;color:#637169}.overlay{position:fixed;inset:0;z-index:1000;display:grid;place-items:center;padding:18px;background:rgba(15,23,42,.5)}.modal{width:min(500px,100%);display:grid;gap:15px;padding:24px;border-radius:12px;background:#fff;box-shadow:0 28px 80px rgba(15,23,42,.28)}.modal header,.modal footer{display:flex;align-items:flex-start;justify-content:space-between;gap:12px}.modal h2{margin:4px 0 0}.modal label{display:grid;gap:6px;font-size:12px;font-weight:750}.modal input,.modal textarea{box-sizing:border-box;width:100%;border:1px solid #cad7ce;border-radius:8px;padding:10px;font:inherit}.modal input[type=color]{height:44px;padding:4px}.modal footer{justify-content:flex-end}.modal-note{margin:0;padding:10px;border-radius:8px;background:#f3f7f4;color:#5e6d64;font-size:12px}button:disabled{opacity:.55;cursor:not-allowed}@media(max-width:800px){.organization-overview{padding:24px 16px 40px}.overview-head{align-items:flex-start;flex-direction:column}.head-actions{flex-wrap:wrap}.metric-grid{grid-template-columns:repeat(2,1fr)}.overview-grid{grid-template-columns:1fr}.next-step{align-items:flex-start;flex-wrap:wrap}}@media(max-width:480px){.metric-grid{grid-template-columns:1fr}.panel{padding:17px}.panel-head{align-items:flex-start;flex-direction:column}.progress{display:none}.group-link{padding-left:0;align-items:flex-start;flex-direction:column}}
</style>
