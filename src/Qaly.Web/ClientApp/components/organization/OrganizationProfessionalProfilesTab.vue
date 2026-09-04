<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import { BriefcaseBusiness, Plus, RefreshCw } from 'lucide-vue-next'
import { showError, showSuccess } from '../../composables/use-toast'
import { apiResult, errorMessage } from '../../utils/api-client'

type Profile = {
  id: string
  key: string
  name: string
  description: string | null
  category: string
  isSystemSeed: boolean
  isActive: boolean
  rowVersion: string
}

const props = defineProps<{ organizationId: string; canManage: boolean }>()
const profiles = ref<Profile[]>([])
const loading = ref(true)
const saving = ref('')
const createOpen = ref(false)
const form = reactive({ name: '', category: 'Chuyên môn', description: '' })

async function load() {
  loading.value = true
  try {
    profiles.value = await apiResult<Profile[]>(`/api/organizations/${props.organizationId}/professional-profiles?includeInactive=true`)
  } catch (error) {
    showError(errorMessage(error, 'Không thể tải Professional Profiles.'))
  } finally {
    loading.value = false
  }
}

async function createProfile() {
  if (!form.name.trim()) return
  saving.value = 'create'
  try {
    await apiResult<Profile>(`/api/organizations/${props.organizationId}/professional-profiles`, {
      method: 'POST',
      body: JSON.stringify({ name: form.name.trim(), key: null, category: form.category.trim() || 'Chuyên môn', description: form.description.trim() || null }),
    })
    Object.assign(form, { name: '', category: 'Chuyên môn', description: '' })
    createOpen.value = false
    showSuccess('Đã tạo Professional Profile.')
    await load()
  } catch (error) {
    showError(errorMessage(error, 'Không thể tạo Professional Profile.'))
  } finally {
    saving.value = ''
  }
}

async function toggleProfile(profile: Profile) {
  saving.value = profile.id
  try {
    await apiResult<Profile>(`/api/organizations/${props.organizationId}/professional-profiles/${profile.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        name: profile.name,
        category: profile.category,
        description: profile.description,
        isActive: !profile.isActive,
        rowVersion: profile.rowVersion,
      }),
    })
    showSuccess(profile.isActive ? 'Đã ngưng sử dụng profile.' : 'Đã kích hoạt profile.')
    await load()
  } catch (error) {
    showError(errorMessage(error, 'Không thể cập nhật Professional Profile.'))
  } finally {
    saving.value = ''
  }
}

watch(() => props.organizationId, load)
onMounted(load)
</script>

<template>
  <section class="profiles" data-testid="organization-professional-profiles-tab">
    <header>
      <div><span class="eyebrow">Capability model</span><h2>Professional Profiles</h2><p>Chuẩn hóa hồ sơ nghề nghiệp để gán cho thành viên mà không làm thay đổi quyền truy cập.</p></div>
      <div class="actions"><button type="button" class="icon" aria-label="Tải lại profile" @click="load"><RefreshCw :size="17" /></button><button v-if="canManage" type="button" class="primary" @click="createOpen = !createOpen"><Plus :size="17" /> Thêm profile</button></div>
    </header>

    <form v-if="createOpen" @submit.prevent="createProfile">
      <label>Tên profile<input v-model="form.name" required /></label>
      <label>Nhóm<input v-model="form.category" required /></label>
      <label class="wide">Mô tả<textarea v-model="form.description" rows="2" /></label>
      <div class="form-actions"><button type="button" class="secondary" @click="createOpen = false">Hủy</button><button class="primary" :disabled="saving === 'create'">Lưu profile</button></div>
    </form>

    <div v-if="loading" class="state">Đang tải Professional Profiles…</div>
    <div v-else-if="!profiles.length" class="state">Chưa có Professional Profile.</div>
    <div v-else class="grid">
      <article v-for="profile in profiles" :key="profile.id" :class="{ inactive: !profile.isActive }">
        <span class="profile-icon"><BriefcaseBusiness :size="18" /></span>
        <div><strong>{{ profile.name }}</strong><span>{{ profile.category }} · {{ profile.key }}</span><small>{{ profile.description || 'Chưa có mô tả' }}</small></div>
        <button v-if="canManage" type="button" class="secondary" :disabled="saving === profile.id" @click="toggleProfile(profile)">{{ profile.isActive ? 'Ngưng dùng' : 'Kích hoạt' }}</button>
      </article>
    </div>
  </section>
</template>

<style scoped>
.profiles{display:grid;gap:18px}.profiles>header,.actions,.form-actions{display:flex;align-items:flex-start;justify-content:space-between;gap:12px}.eyebrow{color:#287a55;font-size:12px;font-weight:800;letter-spacing:.08em;text-transform:uppercase}h2{margin:5px 0;font-size:21px}p{margin:0;color:#66756d}.actions{align-items:center}.primary,.secondary,.icon{display:inline-flex;align-items:center;justify-content:center;gap:7px;border-radius:8px;padding:9px 13px;font:inherit;font-weight:750;cursor:pointer}.primary{border:1px solid #176b45;background:#176b45;color:#fff}.secondary,.icon{border:1px solid #ced9d1;background:#fff;color:#245b43}.icon{width:40px;padding:9px}form{display:grid;grid-template-columns:1fr 1fr;gap:12px;padding:16px;border:1px solid #dbe5de;border-radius:10px;background:#f8fbf9}form label{display:grid;gap:6px;font-size:12px;font-weight:750}form input,form textarea{box-sizing:border-box;width:100%;border:1px solid #cad7ce;border-radius:8px;padding:9px;font:inherit}.wide,.form-actions{grid-column:1/-1}.form-actions{justify-content:flex-end}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:10px}.grid article{display:grid;grid-template-columns:auto 1fr auto;align-items:center;gap:12px;padding:15px;border:1px solid #dbe5de;border-radius:9px}.grid article.inactive{opacity:.62}.profile-icon{display:grid;place-items:center;width:38px;height:38px;border-radius:9px;background:#eef3ff;color:#4566a1}.grid article>div{display:grid;gap:3px}.grid span,.grid small{color:#718077;font-size:12px}.state{padding:28px;text-align:center;color:#718077;border:1px dashed #cad7ce;border-radius:9px}button:disabled{opacity:.55}@media(max-width:760px){.profiles>header{flex-direction:column}.grid,form{grid-template-columns:1fr}.wide,.form-actions{grid-column:auto}.grid article{grid-template-columns:auto 1fr}.grid article button{grid-column:1/-1}}
</style>
