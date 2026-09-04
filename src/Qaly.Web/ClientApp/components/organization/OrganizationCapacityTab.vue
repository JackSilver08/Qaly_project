<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { Clock3, PencilLine, RefreshCw, X } from 'lucide-vue-next'
import { showError, showSuccess } from '../../composables/use-toast'
import { apiResult, errorMessage } from '../../utils/api-client'

type Availability = { id: string; startsAt: string; endsAt: string; kind: string; availableHours: number | null; rowVersion: string | null }
type ProjectRole = { projectId: string; projectName: string; projectCode: string; role: string }
type Member = {
  userId: string
  fullName: string
  email: string
  role: string
  projects: ProjectRole[] | null
  weeklyCapacityHours: number | null
  capacityState: string | null
  timeZoneId: string | null
  availabilityWindows: Availability[] | null
  capacityRowVersion: string | null
  skills: string[] | null
}

const props = defineProps<{ organizationId: string; canManage: boolean; currentUserId?: string | null }>()
const members = ref<Member[]>([])
const loading = ref(true)
const saving = ref(false)
const editing = ref<Member | null>(null)
const hours = ref(40)
const timeZoneId = ref('Asia/Ho_Chi_Minh')
const declaredCount = computed(() => members.value.filter(item => item.capacityState === 'declared').length)

async function load() {
  loading.value = true
  try {
    members.value = await apiResult<Member[]>(`/api/organizations/${props.organizationId}/users`)
  } catch (error) {
    showError(errorMessage(error, 'Không thể tải capacity thành viên.'))
  } finally {
    loading.value = false
  }
}

function canEdit(member: Member) {
  return props.canManage || member.userId === props.currentUserId
}

function openEditor(member: Member) {
  editing.value = member
  hours.value = member.weeklyCapacityHours ?? 40
  timeZoneId.value = member.timeZoneId || 'Asia/Ho_Chi_Minh'
}

async function save() {
  if (!editing.value || hours.value < 1 || hours.value > 168) return
  saving.value = true
  try {
    await apiResult(`/api/organizations/${props.organizationId}/members/${editing.value.userId}/capacity`, {
      method: 'PUT',
      body: JSON.stringify({
        weeklyCapacityHours: hours.value,
        timeZoneId: timeZoneId.value.trim() || 'Asia/Ho_Chi_Minh',
        availabilityWindows: editing.value.availabilityWindows ?? [],
        rowVersion: editing.value.capacityRowVersion,
        confirmed: true,
      }),
    })
    editing.value = null
    showSuccess('Đã cập nhật capacity thành viên.')
    await load()
  } catch (error) {
    showError(errorMessage(error, 'Không thể cập nhật capacity.'))
  } finally {
    saving.value = false
  }
}

watch(() => props.organizationId, load)
onMounted(load)
</script>

<template>
  <section class="capacity" data-testid="organization-capacity-tab">
    <header>
      <div><span class="eyebrow">People capacity</span><h2>Capacity toàn organization</h2><p>Đối chiếu capacity đã khai báo với project role và kỹ năng có bằng chứng.</p></div>
      <div class="summary"><span>{{ declaredCount }}/{{ members.length }} đã khai báo</span><button type="button" class="icon" aria-label="Tải lại capacity" @click="load"><RefreshCw :size="17" /></button></div>
    </header>

    <div v-if="loading" class="state">Đang tải capacity…</div>
    <div v-else-if="!members.length" class="state">Organization chưa có thành viên.</div>
    <div v-else class="table-wrap">
      <table>
        <thead><tr><th>Thành viên</th><th>Project / role</th><th>Kỹ năng</th><th>Capacity</th><th></th></tr></thead>
        <tbody>
          <tr v-for="member in members" :key="member.userId">
            <td><strong>{{ member.fullName }}</strong><small>{{ member.email }}</small></td>
            <td><span v-for="project in member.projects ?? []" :key="project.projectId" class="tag">{{ project.projectName }} ({{ project.projectCode }}) · {{ project.role }}</span><small v-if="!member.projects?.length">Chưa tham gia project</small></td>
            <td><span v-for="skill in (member.skills ?? []).slice(0, 3)" :key="skill" class="tag skill">{{ skill }}</span><small v-if="!member.skills?.length">Chưa có bằng chứng kỹ năng</small></td>
            <td><strong v-if="member.weeklyCapacityHours != null">{{ member.weeklyCapacityHours }}h/tuần</strong><small>{{ member.capacityState === 'declared' ? 'Đã khai báo' : member.capacityState === 'assumed_default' ? 'Mặc định' : 'Không có quyền xem' }}</small></td>
            <td><button v-if="canEdit(member)" type="button" class="icon" :aria-label="`Sửa capacity ${member.fullName}`" @click="openEditor(member)"><PencilLine :size="16" /></button></td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="editing" class="backdrop" @click.self="editing = null" @keydown.esc="editing = null">
      <form class="modal" role="dialog" aria-modal="true" aria-labelledby="capacity-title" @submit.prevent="save">
        <header><div><span class="eyebrow">Capacity profile</span><h3 id="capacity-title">{{ editing.fullName }}</h3></div><button type="button" class="icon" aria-label="Đóng" @click="editing = null"><X :size="17" /></button></header>
        <label>Giờ làm việc mỗi tuần<input v-model.number="hours" type="number" min="1" max="168" step="0.5" required /></label>
        <label>Múi giờ<input v-model="timeZoneId" maxlength="100" required /></label>
        <p><Clock3 :size="15" /> {{ editing.availabilityWindows?.length ?? 0 }} khoảng availability hiện có được giữ nguyên.</p>
        <footer><button type="button" class="secondary" @click="editing = null">Hủy</button><button class="primary" :disabled="saving">Xác nhận cập nhật</button></footer>
      </form>
    </div>
  </section>
</template>

<style scoped>
.capacity{display:grid;gap:18px}.capacity>header,.summary,.modal header,.modal footer{display:flex;align-items:flex-start;justify-content:space-between;gap:12px}.eyebrow{color:#287a55;font-size:12px;font-weight:800;letter-spacing:.08em;text-transform:uppercase}h2{margin:5px 0;font-size:21px}p{margin:0;color:#66756d}.summary{align-items:center;color:#66756d;font-size:13px}.icon,.primary,.secondary{display:inline-flex;align-items:center;justify-content:center;border-radius:8px;padding:9px 13px;font:inherit;font-weight:750;cursor:pointer}.icon,.secondary{border:1px solid #ced9d1;background:#fff;color:#245b43}.icon{width:40px;padding:9px}.primary{border:1px solid #176b45;background:#176b45;color:#fff}.table-wrap{overflow:auto;border:1px solid #dbe5de;border-radius:10px}table{width:100%;min-width:850px;border-collapse:collapse}th,td{padding:13px 14px;text-align:left;border-bottom:1px solid #e8eeea;vertical-align:top}th{background:#f6f9f7;color:#67756d;font-size:11px;text-transform:uppercase}td>strong,td>small{display:block}td small{margin-top:4px;color:#748078;font-size:12px}.tag{display:inline-flex;margin:0 5px 5px 0;padding:4px 7px;border-radius:99px;background:#eef3f0;color:#385b49;font-size:11px}.tag.skill{background:#eef3ff;color:#45609a}.state{padding:28px;text-align:center;color:#718077;border:1px dashed #cad7ce;border-radius:9px}.backdrop{position:fixed;inset:0;z-index:1000;display:grid;place-items:center;padding:18px;background:rgba(15,23,42,.48)}.modal{width:min(480px,100%);display:grid;gap:15px;padding:22px;border-radius:12px;background:#fff;box-shadow:0 24px 70px rgba(15,23,42,.25)}.modal h3{margin:4px 0 0}.modal label{display:grid;gap:6px;font-size:12px;font-weight:750}.modal input{border:1px solid #cad7ce;border-radius:8px;padding:10px;font:inherit}.modal p{display:flex;gap:7px;align-items:center;font-size:12px}.modal footer{justify-content:flex-end}button:disabled{opacity:.55}@media(max-width:760px){.capacity>header{flex-direction:column}}
</style>
