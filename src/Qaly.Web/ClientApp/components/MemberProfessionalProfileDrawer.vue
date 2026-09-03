<script setup lang="ts">
import { computed, onBeforeUnmount, reactive, ref, watch } from 'vue'
import { AlertTriangle, BadgeCheck, BriefcaseBusiness, LoaderCircle, Plus, Save, ShieldCheck, X } from 'lucide-vue-next'
import { apiCommand, apiResult, errorMessage } from '../utils/api-client'
import { showError, showSuccess } from '../composables/use-toast'

type Definition = { id:string; organizationId:string; key:string; name:string; description:string|null; category:string; isSystemSeed:boolean; isActive:boolean; rowVersion:string }
type Assigned = { assignmentId:string; definitionId:string; key:string; name:string; category:string; description:string|null; proficiency:string; verificationStatus:string; source:string; effectiveFrom:string; effectiveTo:string|null; verifiedByUserId:string|null; verifiedByName:string|null; verifiedAt:string|null; note:string|null; rowVersion:string; canEdit:boolean }
type ProfileSet = { organizationId:string; userId:string; memberName:string; accessRole:string; isSelf:boolean; canManage:boolean; authorizationNotice:string; profiles:Assigned[] }
type EditableRow = { selected:boolean; proficiency:string; verificationStatus:string; effectiveFrom:string; effectiveTo:string; note:string; assignmentId:string|null; rowVersion:string; locked:boolean }

const props = defineProps<{ organizationId:string; memberId:string; memberName:string }>()
const emit = defineEmits<{ close: [] }>()
const definitions = ref<Definition[]>([])
const profileSet = ref<ProfileSet | null>(null)
const rows = reactive<Record<string, EditableRow>>({})
const loading = ref(true)
const saving = ref(false)
const error = ref('')
const createOpen = ref(false)
const creating = ref(false)
const custom = reactive({ name:'', category:'Engineering', description:'' })
let requestVersion = 0

const grouped = computed(() => {
  const groups = new Map<string, Definition[]>()
  for (const definition of definitions.value) {
    const list = groups.get(definition.category) ?? []
    list.push(definition)
    groups.set(definition.category, list)
  }
  return [...groups.entries()].map(([category, items]) => ({ category, items }))
})
const selectedCount = computed(() => Object.values(rows).filter((row) => row.selected).length)
const canEdit = computed(() => Boolean(profileSet.value?.canManage || profileSet.value?.isSelf))
const today = () => new Date().toISOString().slice(0, 10)

function hydrate(set: ProfileSet) {
  for (const key of Object.keys(rows)) delete rows[key]
  for (const definition of definitions.value) {
    const assigned = set.profiles.find((item) => item.definitionId === definition.id)
    rows[definition.id] = {
      selected: Boolean(assigned),
      proficiency: assigned?.proficiency ?? 'Practitioner',
      verificationStatus: assigned?.verificationStatus ?? 'Declared',
      effectiveFrom: assigned?.effectiveFrom?.slice(0, 10) ?? today(),
      effectiveTo: assigned?.effectiveTo?.slice(0, 10) ?? '',
      note: assigned?.note ?? '',
      assignmentId: assigned?.assignmentId ?? null,
      rowVersion: assigned?.rowVersion ?? '',
      locked: Boolean(assigned && !assigned.canEdit),
    }
  }
}

async function load() {
  const version = ++requestVersion
  loading.value = true
  error.value = ''
  try {
    const [catalog, set] = await Promise.all([
      apiResult<Definition[]>(`/api/organizations/${props.organizationId}/professional-profiles`),
      apiResult<ProfileSet>(`/api/organizations/${props.organizationId}/members/${props.memberId}/professional-profiles`),
    ])
    if (version !== requestVersion) return
    definitions.value = catalog
    profileSet.value = set
    hydrate(set)
  } catch (cause) {
    if (version === requestVersion) error.value = errorMessage(cause, 'Không thể tải hồ sơ nghề nghiệp.')
  } finally {
    if (version === requestVersion) loading.value = false
  }
}

function selectAll() {
  for (const row of Object.values(rows)) if (!row.locked) row.selected = true
}
function clearEditable() {
  for (const row of Object.values(rows)) if (!row.locked) row.selected = false
}
function selectSuggested() {
  const role = profileSet.value?.accessRole
  const suggested = role === 'Owner' || role === 'OrganizationAdmin'
    ? new Set(['product-project-manager', 'scrum-master-agile-coach', 'business-analyst'])
    : new Set(['business-analyst', 'frontend-engineer', 'backend-engineer', 'qa-manual'])
  for (const definition of definitions.value) {
    const row = rows[definition.id]
    if (!row.locked) row.selected = suggested.has(definition.key)
  }
}

async function save() {
  if (!profileSet.value || !canEdit.value) return
  saving.value = true
  try {
    const payloadProfiles = definitions.value.flatMap((definition) => {
      const row = rows[definition.id]
      if (!row.selected || (profileSet.value?.isSelf && row.locked)) return []
      return [{
        definitionId: definition.id,
        proficiency: row.proficiency,
        verificationStatus: profileSet.value?.canManage ? row.verificationStatus : 'Declared',
        source: profileSet.value?.canManage ? 'ManagerConfirmed' : 'MemberDeclared',
        effectiveFrom: row.effectiveFrom ? new Date(`${row.effectiveFrom}T00:00:00Z`).toISOString() : null,
        effectiveTo: row.effectiveTo ? new Date(`${row.effectiveTo}T00:00:00Z`).toISOString() : null,
        note: row.note || null,
      }]
    })
    const result = await apiResult<ProfileSet>(`/api/organizations/${props.organizationId}/members/${props.memberId}/professional-profiles`, {
      method: 'PUT',
      body: JSON.stringify({
        confirmed: true,
        profiles: payloadProfiles,
        knownRows: profileSet.value.profiles.map((item) => ({ assignmentId:item.assignmentId, rowVersion:item.rowVersion })),
      }),
    })
    profileSet.value = result
    hydrate(result)
    showSuccess('Đã lưu và đọc lại hồ sơ nghề nghiệp từ máy chủ.')
  } catch (cause) {
    showError(errorMessage(cause, 'Không thể lưu hồ sơ nghề nghiệp.'))
  } finally {
    saving.value = false
  }
}

async function createDefinition() {
  if (!profileSet.value?.canManage || !custom.name.trim()) return
  creating.value = true
  try {
    await apiCommand(`/api/organizations/${props.organizationId}/professional-profiles`, {
      method: 'POST',
      body: JSON.stringify({ name:custom.name, key:null, category:custom.category, description:custom.description || null }),
    })
    custom.name = ''; custom.description = ''; createOpen.value = false
    showSuccess('Đã thêm hồ sơ nghề nghiệp riêng cho tổ chức.')
    await load()
  } catch (cause) {
    showError(errorMessage(cause, 'Không thể thêm hồ sơ nghề nghiệp.'))
  } finally { creating.value = false }
}

function statusLabel(value:string) { return value === 'Verified' ? 'Đã xác minh' : value === 'Rejected' ? 'Không được xác nhận' : 'Tự khai báo' }
watch(() => [props.organizationId, props.memberId], load, { immediate:true })
onBeforeUnmount(() => { requestVersion += 1 })
</script>

<template>
  <div class="profile-backdrop" @click.self="emit('close')" @keydown.esc="emit('close')">
    <aside class="profile-drawer" role="dialog" aria-modal="true" :aria-label="`Hồ sơ nghề nghiệp của ${props.memberName}`">
      <header>
        <div><span class="kicker"><BriefcaseBusiness :size="15" /> Hồ sơ nghề nghiệp</span><h2>{{ props.memberName }}</h2><p>Vai trò truy cập hiện tại: <strong>{{ profileSet?.accessRole ?? '—' }}</strong></p></div>
        <button class="icon" type="button" aria-label="Đóng" @click="emit('close')"><X :size="19" /></button>
      </header>
      <div class="boundary"><ShieldCheck :size="17" /><span>{{ profileSet?.authorizationNotice ?? 'Hồ sơ nghề nghiệp không cấp quyền truy cập.' }}</span></div>
      <div v-if="loading" class="state"><LoaderCircle :size="22" class="spin" /> Đang tải taxonomy và bản ghi đã xác minh…</div>
      <div v-else-if="error" class="state error" role="alert"><AlertTriangle :size="22" /> {{ error }}</div>
      <template v-else-if="profileSet">
        <div class="quick-actions" aria-label="Chọn nhanh hồ sơ nghề nghiệp">
          <button type="button" class="secondary" :disabled="!canEdit" @click="selectSuggested">Đề cử theo bối cảnh</button>
          <button type="button" class="secondary" :disabled="!canEdit" @click="selectAll">Chọn tất cả</button>
          <button type="button" class="secondary" :disabled="!canEdit" @click="clearEditable">Bỏ phần có thể sửa</button>
          <span>{{ selectedCount }} đã chọn</span>
        </div>
        <p class="guidance">“Đề cử” chỉ là điểm bắt đầu theo bối cảnh vai trò; người quản lý vẫn phải xác minh. Staffing chỉ dùng profile <strong>Đã xác minh</strong> để xếp hạng nghề nghiệp và vẫn kiểm tra skill evidence/capacity riêng.</p>
        <section v-for="group in grouped" :key="group.category" class="category">
          <h3>{{ group.category }}</h3>
          <article v-for="definition in group.items" :key="definition.id" :class="['profile-card', { selected:rows[definition.id]?.selected, locked:rows[definition.id]?.locked }]">
            <label class="profile-title"><input v-model="rows[definition.id].selected" type="checkbox" :disabled="!canEdit || rows[definition.id].locked" /><span><strong>{{ definition.name }}</strong><small>{{ definition.description }}</small></span></label>
            <div v-if="rows[definition.id].selected" class="profile-controls">
              <label>Mức độ<select v-model="rows[definition.id].proficiency" :disabled="!canEdit || rows[definition.id].locked"><option>Foundation</option><option>Practitioner</option><option>Proficient</option><option>Expert</option></select></label>
              <label v-if="profileSet.canManage">Xác minh<select v-model="rows[definition.id].verificationStatus" :disabled="rows[definition.id].locked"><option value="Declared">Tự khai báo</option><option value="Verified">Đã xác minh</option><option value="Rejected">Không được xác nhận</option></select></label>
              <span v-else class="status"><BadgeCheck :size="14" /> {{ statusLabel(rows[definition.id].verificationStatus) }}</span>
              <label>Từ ngày<input v-model="rows[definition.id].effectiveFrom" type="date" :disabled="!canEdit || rows[definition.id].locked" /></label>
              <label>Đến ngày<input v-model="rows[definition.id].effectiveTo" type="date" :disabled="!canEdit || rows[definition.id].locked" /></label>
              <label class="note">Ghi chú<input v-model.trim="rows[definition.id].note" maxlength="500" placeholder="Nguồn xác minh hoặc phạm vi kinh nghiệm" :disabled="!canEdit || rows[definition.id].locked" /></label>
            </div>
            <p v-if="rows[definition.id].locked" class="locked-note"><BadgeCheck :size="14" /> Bản ghi đã được người quản lý xác minh; thành viên không thể tự sửa hoặc xóa.</p>
          </article>
        </section>
        <section v-if="profileSet.canManage" class="custom">
          <button type="button" class="secondary" @click="createOpen = !createOpen"><Plus :size="16" /> Thêm profile riêng</button>
          <form v-if="createOpen" @submit.prevent="createDefinition"><label>Tên<input v-model.trim="custom.name" required maxlength="120" /></label><label>Nhóm<input v-model.trim="custom.category" required maxlength="80" /></label><label class="wide">Mô tả<input v-model.trim="custom.description" maxlength="600" /></label><button class="primary" type="submit" :disabled="creating">Thêm vào taxonomy</button></form>
        </section>
        <footer><span>Mọi thay đổi được audit và đọc lại từ dữ liệu canonical; không làm thay đổi vai trò truy cập <strong>{{ profileSet.accessRole }}</strong>.</span><button v-if="canEdit" class="primary" type="button" :disabled="saving" @click="save"><Save :size="16" /> {{ saving ? 'Đang đối soát…' : 'Lưu và kiểm tra lại' }}</button></footer>
      </template>
    </aside>
  </div>
</template>

<style scoped>
.profile-backdrop{position:fixed;inset:0;z-index:1200;display:flex;justify-content:flex-end;background:rgba(15,23,42,.44)}.profile-drawer{width:min(760px,100%);height:100%;overflow:auto;background:var(--surface,#fff);color:var(--text-primary,#172033);padding:24px;box-shadow:-20px 0 55px rgba(15,23,42,.2)}header{display:flex;justify-content:space-between;gap:16px;border-bottom:1px solid var(--border-color,#e2e8f0);padding-bottom:16px}h2,h3,p{margin:0}.kicker{display:inline-flex;align-items:center;gap:6px;color:#1d4ed8;font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.04em}header h2{margin-top:6px;font-size:24px}header p{margin-top:4px;color:var(--text-secondary,#64748b);font-size:13px}.icon{height:36px;width:36px;border:0;border-radius:9px;background:var(--panel-soft,#f1f5f9);color:inherit;display:grid;place-items:center;cursor:pointer}.boundary,.state{display:flex;gap:8px;align-items:flex-start;padding:12px;margin-top:14px;border-radius:10px;font-size:13px;line-height:1.45}.boundary{background:#eff6ff;color:#1e40af}.state{justify-content:center;align-items:center;min-height:90px;background:var(--panel-soft,#f8fafc);color:var(--text-secondary,#64748b)}.state.error{background:#fef2f2;color:#b91c1c}.quick-actions{display:flex;gap:8px;align-items:center;flex-wrap:wrap;margin-top:14px}.quick-actions>span{margin-left:auto;color:var(--text-secondary,#64748b);font-size:12px;font-weight:700}.guidance{margin-top:10px;padding:10px 12px;background:#fffbeb;color:#92400e;border-radius:9px;font-size:12px;line-height:1.5}.category{display:grid;gap:8px;margin-top:18px}.category h3{font-size:13px;color:var(--text-secondary,#64748b);text-transform:uppercase;letter-spacing:.04em}.profile-card{padding:12px;border:1px solid var(--border-color,#dbe2ea);border-radius:12px}.profile-card.selected{border-color:#93c5fd;background:#f8fbff}.profile-card.locked{background:#f8fafc}.profile-title{display:flex;gap:9px;align-items:flex-start}.profile-title input{margin-top:3px}.profile-title span{display:grid;gap:3px}.profile-title small{color:var(--text-secondary,#64748b);line-height:1.4}.profile-controls{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:9px;margin:12px 0 0 25px}.profile-controls label,.custom label{display:grid;gap:4px;font-size:11px;font-weight:750;color:var(--text-secondary,#64748b)}select,input{width:100%;min-height:36px;border:1px solid var(--border-color,#dbe2ea);border-radius:8px;padding:6px 8px;background:var(--surface,#fff);color:var(--text-primary,#172033)}.profile-controls .note{grid-column:1/-1}.status{display:flex;align-items:center;gap:5px;color:#166534;font-size:12px;font-weight:750}.locked-note{display:flex;align-items:center;gap:5px;margin:9px 0 0 25px;color:#166534;font-size:11px}.custom{margin-top:18px;padding-top:14px;border-top:1px solid var(--border-color,#e2e8f0)}.custom form{display:grid;grid-template-columns:1fr 1fr;gap:9px;margin-top:10px}.custom .wide{grid-column:1/-1}.primary,.secondary{display:inline-flex;align-items:center;justify-content:center;gap:7px;min-height:38px;border-radius:9px;padding:8px 12px;font-weight:750;cursor:pointer}.primary{border:1px solid #2563eb;background:#2563eb;color:#fff}.secondary{border:1px solid var(--border-color,#dbe2ea);background:var(--surface,#fff);color:inherit}button:disabled{opacity:.5;cursor:not-allowed}footer{position:sticky;bottom:-24px;display:flex;align-items:center;justify-content:space-between;gap:14px;margin:20px -24px -24px;padding:14px 24px;background:var(--surface,#fff);border-top:1px solid var(--border-color,#e2e8f0);box-shadow:0 -8px 20px rgba(15,23,42,.05);font-size:12px;color:var(--text-secondary,#64748b)}footer span{max-width:480px}@keyframes spin{to{transform:rotate(360deg)}}.spin{animation:spin 1s linear infinite}@media(max-width:680px){.profile-drawer{padding:17px}.profile-controls{grid-template-columns:1fr 1fr;margin-left:0}.custom form{grid-template-columns:1fr}footer{bottom:-17px;margin:18px -17px -17px;padding:12px 17px;align-items:stretch;flex-direction:column}.quick-actions>span{width:100%;margin-left:0}}@media(prefers-reduced-motion:reduce){.spin{animation:none}}
</style>
