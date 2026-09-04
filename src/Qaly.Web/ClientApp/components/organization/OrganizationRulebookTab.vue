<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { BookOpenCheck, Check, Plus, RefreshCw } from 'lucide-vue-next'
import { showError, showSuccess } from '../../composables/use-toast'
import { apiResult, errorMessage } from '../../utils/api-client'

type Rule = { ruleKey: string; category: string; enforcement: string; description: string; numericValue: number | null; unit: string | null; values: string[] | null; enabled: boolean }
type RuleSet = { ruleSetId: string; version: number; status: string; rules: Rule[]; createdAt: string; activatedAt: string | null; revision: number }

const props = defineProps<{ organizationId: string; canManage: boolean }>()
const sets = ref<RuleSet[]>([])
const loading = ref(true)
const saving = ref('')
const editorOpen = ref(false)
const policy = reactive({ maxActiveProjects: 5, maxUtilizationPercent: 85, focusReservePercent: 15 })
const effective = computed(() => sets.value.find(item => item.status === 'active') ?? null)

async function load() {
  loading.value = true
  try {
    sets.value = await apiResult<RuleSet[]>(`/api/organizations/${props.organizationId}/work-rulebook`)
  } catch (error) {
    showError(errorMessage(error, 'Không thể tải Work Rulebook.'))
  } finally {
    loading.value = false
  }
}

async function createDraft() {
  saving.value = 'create'
  const rules: Rule[] = [
    { ruleKey: 'max_active_projects', category: 'capacity', enforcement: 'warn', description: 'Giới hạn số project active của một thành viên.', numericValue: policy.maxActiveProjects, unit: 'projects', values: null, enabled: true },
    { ruleKey: 'max_utilization_percent', category: 'capacity', enforcement: 'block', description: 'Không đề xuất phân công vượt ngưỡng utilization.', numericValue: policy.maxUtilizationPercent, unit: 'percent', values: null, enabled: true },
    { ruleKey: 'focus_reserve_percent', category: 'capacity', enforcement: 'warn', description: 'Giữ lại capacity dự phòng cho công việc không kế hoạch.', numericValue: policy.focusReservePercent, unit: 'percent', values: null, enabled: true },
    { ruleKey: 'active_membership_required', category: 'membership', enforcement: 'block', description: 'Chỉ phân công thành viên đang hoạt động.', numericValue: null, unit: null, values: null, enabled: true },
  ]
  try {
    await apiResult<RuleSet>(`/api/organizations/${props.organizationId}/work-rulebook`, { method: 'POST', body: JSON.stringify({ rules }) })
    editorOpen.value = false
    showSuccess('Đã lưu bản nháp Work Rulebook; hãy review trước khi kích hoạt.')
    await load()
  } catch (error) {
    showError(errorMessage(error, 'Không thể tạo bản nháp Rulebook.'))
  } finally {
    saving.value = ''
  }
}

async function activate(item: RuleSet) {
  saving.value = item.ruleSetId
  try {
    await apiResult<RuleSet>(`/api/organizations/${props.organizationId}/work-rulebook/${item.ruleSetId}/activate`, {
      method: 'POST',
      body: JSON.stringify({ revision: item.revision }),
    })
    showSuccess(`Đã kích hoạt Work Rulebook v${item.version}.`)
    await load()
  } catch (error) {
    showError(errorMessage(error, 'Không thể kích hoạt Rulebook.'))
  } finally {
    saving.value = ''
  }
}

function ruleValue(rule: Rule) {
  if (rule.numericValue != null) return `${rule.numericValue} ${rule.unit ?? ''}`.trim()
  if (rule.values?.length) return rule.values.join(', ')
  return rule.enabled ? 'Bật' : 'Tắt'
}

watch(() => props.organizationId, load)
onMounted(load)
</script>

<template>
  <section class="rulebook" data-testid="organization-rulebook-tab">
    <header>
      <div><span class="eyebrow">Governance</span><h2>Work Rulebook</h2><p>Policy deterministic cho staffing, capacity và điều phối công việc của organization.</p></div>
      <div class="actions"><button type="button" class="icon" aria-label="Tải lại Rulebook" @click="load"><RefreshCw :size="17" /></button><button v-if="canManage" type="button" class="primary" @click="editorOpen = !editorOpen"><Plus :size="17" /> Tạo bản nháp</button></div>
    </header>

    <form v-if="editorOpen" @submit.prevent="createDraft">
      <strong>Khởi tạo policy capacity</strong>
      <label>Project active tối đa<input v-model.number="policy.maxActiveProjects" type="number" min="1" max="50" /></label>
      <label>Utilization tối đa (%)<input v-model.number="policy.maxUtilizationPercent" type="number" min="10" max="100" /></label>
      <label>Capacity dự phòng (%)<input v-model.number="policy.focusReservePercent" type="number" min="0" max="50" /></label>
      <div class="form-actions"><button type="button" class="secondary" @click="editorOpen = false">Hủy</button><button class="primary" :disabled="saving === 'create'">Lưu bản nháp</button></div>
    </form>

    <div v-if="loading" class="state">Đang tải Work Rulebook…</div>
    <div v-else-if="!sets.length" class="state"><BookOpenCheck :size="25" /><strong>Chưa có Rulebook.</strong><span>Tạo bản nháp, review các ngưỡng rồi kích hoạt.</span></div>
    <div v-else class="sets">
      <article v-for="item in sets" :key="item.ruleSetId" :class="`status-${item.status}`">
        <header><div><strong>Rulebook v{{ item.version }}</strong><span>{{ item.status }}</span></div><button v-if="canManage && item.status === 'draft'" type="button" class="primary" :disabled="saving === item.ruleSetId" @click="activate(item)"><Check :size="15" /> Kích hoạt</button><span v-else-if="effective?.ruleSetId === item.ruleSetId" class="active-badge">Đang áp dụng</span></header>
        <ul><li v-for="rule in item.rules" :key="rule.ruleKey"><span><strong>{{ rule.ruleKey }}</strong><small>{{ rule.description }}</small></span><b>{{ ruleValue(rule) }}</b></li></ul>
      </article>
    </div>
  </section>
</template>

<style scoped>
.rulebook{display:grid;gap:18px}.rulebook>header,.actions,.form-actions,.sets article>header{display:flex;align-items:flex-start;justify-content:space-between;gap:12px}.eyebrow{color:#287a55;font-size:12px;font-weight:800;letter-spacing:.08em;text-transform:uppercase}h2{margin:5px 0;font-size:21px}p{margin:0;color:#66756d}.actions{align-items:center}.primary,.secondary,.icon{display:inline-flex;align-items:center;justify-content:center;gap:7px;border-radius:8px;padding:9px 13px;font:inherit;font-weight:750;cursor:pointer}.primary{border:1px solid #176b45;background:#176b45;color:#fff}.secondary,.icon{border:1px solid #ced9d1;background:#fff;color:#245b43}.icon{width:40px;padding:9px}form{display:grid;grid-template-columns:auto repeat(3,1fr);align-items:end;gap:12px;padding:16px;border:1px solid #dbe5de;border-radius:10px;background:#f8fbf9}form>strong{grid-column:1/-1}form label{display:grid;gap:6px;font-size:12px;font-weight:750}form input{border:1px solid #cad7ce;border-radius:8px;padding:9px;font:inherit}.form-actions{justify-content:flex-end}.state{display:grid;justify-items:center;gap:6px;padding:30px;color:#718077;border:1px dashed #cad7ce;border-radius:9px}.sets{display:grid;gap:11px}.sets article{padding:16px;border:1px solid #dbe5de;border-radius:10px}.sets article.status-active{border-color:#8bc1a2;background:#f7fcf9}.sets article>header>div{display:flex;align-items:center;gap:9px}.sets article>header span,.active-badge{padding:4px 7px;border-radius:99px;background:#eef4f0;color:#3f6752;font-size:11px;text-transform:uppercase}.sets ul{display:grid;gap:8px;margin:14px 0 0;padding:0;list-style:none}.sets li{display:flex;justify-content:space-between;gap:18px;padding-top:8px;border-top:1px solid #e7ece9}.sets li span,.sets li small{display:grid;gap:2px}.sets li small{color:#718077;font-weight:400}.sets li b{white-space:nowrap;color:#287a55;font-size:12px}button:disabled{opacity:.55}@media(max-width:760px){.rulebook>header,form{grid-template-columns:1fr;flex-direction:column}.form-actions{justify-content:flex-start}.sets li{flex-direction:column;gap:5px}}
</style>
