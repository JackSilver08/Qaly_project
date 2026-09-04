<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import { Plus, RefreshCw, Save } from 'lucide-vue-next'
import { showError, showSuccess } from '../../composables/use-toast'
import { apiResult, errorMessage } from '../../utils/api-client'

type Skill = {
  id: string
  name: string
  description: string | null
  category: string
  aliases: string[] | null
  defaultRequiredLevel: string
  isSystemSeed: boolean
  isActive: boolean
  rowVersion: string
}

const props = defineProps<{ organizationId: string; canManage: boolean }>()
const skills = ref<Skill[]>([])
const loading = ref(true)
const saving = ref('')
const createOpen = ref(false)
const form = reactive({ name: '', description: '', category: 'Chuyên môn', aliases: '', defaultRequiredLevel: 'Intermediate' })

async function load() {
  loading.value = true
  try {
    skills.value = await apiResult<Skill[]>(`/api/organizations/${props.organizationId}/skills?includeInactive=true`)
  } catch (error) {
    showError(errorMessage(error, 'Không thể tải danh mục kỹ năng.'))
  } finally {
    loading.value = false
  }
}

async function createSkill() {
  if (!form.name.trim()) return
  saving.value = 'create'
  try {
    await apiResult<Skill>(`/api/organizations/${props.organizationId}/skills`, {
      method: 'POST',
      body: JSON.stringify({
        name: form.name.trim(),
        description: form.description.trim() || null,
        category: form.category.trim() || 'Chuyên môn',
        aliases: form.aliases.split(',').map(item => item.trim()).filter(Boolean),
        defaultRequiredLevel: form.defaultRequiredLevel,
      }),
    })
    Object.assign(form, { name: '', description: '', category: 'Chuyên môn', aliases: '', defaultRequiredLevel: 'Intermediate' })
    createOpen.value = false
    showSuccess('Đã thêm kỹ năng vào organization.')
    await load()
  } catch (error) {
    showError(errorMessage(error, 'Không thể tạo kỹ năng.'))
  } finally {
    saving.value = ''
  }
}

async function toggleSkill(skill: Skill) {
  saving.value = skill.id
  try {
    await apiResult<Skill>(`/api/organizations/${props.organizationId}/skills/${skill.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        name: skill.name,
        description: skill.description,
        category: skill.category,
        aliases: skill.aliases ?? [],
        defaultRequiredLevel: skill.defaultRequiredLevel,
        isActive: !skill.isActive,
        rowVersion: skill.rowVersion,
      }),
    })
    showSuccess(skill.isActive ? 'Đã ngưng sử dụng kỹ năng.' : 'Đã kích hoạt kỹ năng.')
    await load()
  } catch (error) {
    showError(errorMessage(error, 'Không thể cập nhật kỹ năng.'))
  } finally {
    saving.value = ''
  }
}

watch(() => props.organizationId, load)
onMounted(load)
</script>

<template>
  <section class="config-panel" data-testid="organization-skills-tab">
    <header>
      <div><span class="eyebrow">Skill catalog</span><h2>Kỹ năng của organization</h2><p>Danh mục chuẩn dùng cho task, bằng chứng năng lực và gợi ý phân công.</p></div>
      <div class="actions">
        <button type="button" class="icon" aria-label="Tải lại kỹ năng" @click="load"><RefreshCw :size="17" /></button>
        <button v-if="canManage" type="button" class="primary" @click="createOpen = !createOpen"><Plus :size="17" /> Thêm kỹ năng</button>
      </div>
    </header>

    <form v-if="createOpen" class="create-form" @submit.prevent="createSkill">
      <label>Tên kỹ năng<input v-model="form.name" required maxlength="160" /></label>
      <label>Nhóm<input v-model="form.category" maxlength="100" /></label>
      <label>Mức mặc định<select v-model="form.defaultRequiredLevel"><option>Beginner</option><option>Intermediate</option><option>Advanced</option><option>Expert</option></select></label>
      <label class="wide">Alias, cách nhau bởi dấu phẩy<input v-model="form.aliases" /></label>
      <label class="wide">Mô tả<textarea v-model="form.description" rows="2" /></label>
      <div class="form-actions"><button type="button" class="secondary" @click="createOpen = false">Hủy</button><button class="primary" :disabled="saving === 'create'"><Save :size="16" /> Lưu kỹ năng</button></div>
    </form>

    <div v-if="loading" class="state">Đang tải danh mục kỹ năng…</div>
    <div v-else-if="!skills.length" class="state">Chưa có kỹ năng nào trong organization.</div>
    <div v-else class="catalog">
      <article v-for="skill in skills" :key="skill.id" :class="{ inactive: !skill.isActive }">
        <div><strong>{{ skill.name }}</strong><span>{{ skill.category }} · {{ skill.defaultRequiredLevel }}</span><small>{{ skill.description || 'Chưa có mô tả' }}</small></div>
        <button v-if="canManage" type="button" class="secondary" :disabled="saving === skill.id" @click="toggleSkill(skill)">{{ skill.isActive ? 'Ngưng dùng' : 'Kích hoạt' }}</button>
        <span v-else class="badge">{{ skill.isActive ? 'Active' : 'Inactive' }}</span>
      </article>
    </div>
  </section>
</template>

<style scoped>
.config-panel{display:grid;gap:18px}.config-panel>header,.actions,.form-actions{display:flex;align-items:flex-start;justify-content:space-between;gap:12px}.eyebrow{color:#287a55;font-size:12px;font-weight:800;letter-spacing:.08em;text-transform:uppercase}h2{margin:5px 0;font-size:21px}p{margin:0;color:#66756d}.actions{align-items:center}.primary,.secondary,.icon{display:inline-flex;align-items:center;justify-content:center;gap:7px;border-radius:8px;padding:9px 13px;font:inherit;font-weight:750;cursor:pointer}.primary{border:1px solid #176b45;background:#176b45;color:#fff}.secondary,.icon{border:1px solid #ced9d1;background:#fff;color:#245b43}.icon{width:40px;padding:9px}.create-form{display:grid;grid-template-columns:2fr 1fr 1fr;gap:12px;padding:16px;border:1px solid #dbe5de;border-radius:10px;background:#f8fbf9}.create-form label{display:grid;gap:6px;font-size:12px;font-weight:750}.create-form input,.create-form select,.create-form textarea{width:100%;box-sizing:border-box;border:1px solid #cad7ce;border-radius:8px;padding:9px;background:#fff;font:inherit}.wide{grid-column:span 2}.form-actions{grid-column:1/-1;justify-content:flex-end}.catalog{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:10px}.catalog article{display:flex;align-items:center;justify-content:space-between;gap:14px;padding:15px;border:1px solid #dbe5de;border-radius:9px;background:#fff}.catalog article.inactive{opacity:.62}.catalog article>div{display:grid;gap:3px}.catalog span,.catalog small{color:#718077;font-size:12px}.badge{padding:5px 8px;border-radius:99px;background:#eef4f0}.state{padding:28px;text-align:center;color:#718077;border:1px dashed #cad7ce;border-radius:9px}button:disabled{opacity:.55;cursor:not-allowed}@media(max-width:760px){.config-panel>header{flex-direction:column}.catalog,.create-form{grid-template-columns:1fr}.wide{grid-column:auto}}
</style>
