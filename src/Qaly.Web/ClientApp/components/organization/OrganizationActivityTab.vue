<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { Activity, RefreshCw } from 'lucide-vue-next'
import { showError } from '../../composables/use-toast'
import { apiResult, errorMessage } from '../../utils/api-client'
import type { PagedResult } from '../../types'

type AuditLog = { id: number; action: string; entityType: string; entityId: string; changesJson: string | null; userName: string | null; timestamp: string }
const props = defineProps<{ organizationId: string }>()
const logs = ref<AuditLog[]>([])
const loading = ref(true)

async function load() {
  loading.value = true
  try {
    logs.value = (await apiResult<PagedResult<AuditLog>>(`/api/organizations/${props.organizationId}/activity?pageSize=100`)).items
  } catch (error) {
    showError(errorMessage(error, 'Không thể tải activity của organization.'))
  } finally {
    loading.value = false
  }
}

function actionLabel(log: AuditLog) {
  const labels: Record<string, string> = {
    Create: log.entityType === 'Project' ? 'đã tạo project' : log.entityType === 'WorkGroup' ? 'đã tạo group' : 'đã tạo organization',
    AddMember: 'đã thêm thành viên',
    RemoveMember: 'đã gỡ thành viên',
    UpdateMemberRole: 'đã đổi vai trò thành viên',
    Update: log.entityType === 'WorkGroup' ? 'đã cập nhật group' : log.entityType === 'Project' ? 'đã cập nhật project' : 'đã cập nhật organization',
    CreateOrganizationSkill: 'đã thêm skill vào policy',
    UpdateOrganizationSkill: 'đã cập nhật skill policy',
    CreateProfessionalProfileDefinition: 'đã thêm professional profile',
    UpdateProfessionalProfileDefinition: 'đã cập nhật professional profile',
    UpdateAiBudgetPolicy: 'đã cập nhật AI Budget policy',
    CreateWorkRulebookDraft: 'đã tạo bản nháp Work Rulebook',
    ActivateWorkRulebook: 'đã kích hoạt Work Rulebook',
    LinkProjectToGroup: 'đã liên kết project với group',
    UnlinkProjectFromGroup: 'đã bỏ liên kết project khỏi group',
    UpdateMemberCapacityProfile: 'đã cập nhật capacity',
  }
  return labels[log.action] ?? `${log.action} ${log.entityType}`
}

function detail(log: AuditLog) {
  if (!log.changesJson) return ''
  try {
    const value = JSON.parse(log.changesJson)
    return value.name ?? value.Name ?? value.role ?? value.NewRole ?? value.newRole ?? ''
  } catch {
    return ''
  }
}

function dateLabel(value: string) {
  return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

watch(() => props.organizationId, load)
onMounted(load)
</script>

<template>
  <section class="activity" data-testid="organization-activity-tab">
    <header><div><span class="eyebrow">Audit trail</span><h2>Activity của organization</h2><p>Theo dõi người thực hiện và các thay đổi nghiệp vụ quan trọng.</p></div><button type="button" class="icon" aria-label="Tải lại activity" @click="load"><RefreshCw :size="17" /></button></header>
    <div v-if="loading" class="state">Đang tải audit trail…</div>
    <div v-else-if="!logs.length" class="state"><Activity :size="25" /><strong>Chưa có activity.</strong></div>
    <ol v-else>
      <li v-for="log in logs" :key="log.id">
        <span class="marker"><Activity :size="15" /></span>
        <div><strong>{{ log.userName || 'Hệ thống' }} {{ actionLabel(log) }}</strong><small v-if="detail(log)">{{ detail(log) }}</small><time :datetime="log.timestamp">{{ dateLabel(log.timestamp) }}</time></div>
        <span class="entity">{{ log.entityType }}</span>
      </li>
    </ol>
  </section>
</template>

<style scoped>
.activity{display:grid;gap:18px}.activity>header{display:flex;align-items:flex-start;justify-content:space-between;gap:12px}.eyebrow{color:#287a55;font-size:12px;font-weight:800;letter-spacing:.08em;text-transform:uppercase}h2{margin:5px 0;font-size:21px}p{margin:0;color:#66756d}.icon{display:inline-flex;align-items:center;justify-content:center;width:40px;padding:9px;border:1px solid #ced9d1;border-radius:8px;background:#fff;color:#245b43;cursor:pointer}.state{display:grid;justify-items:center;gap:6px;padding:30px;color:#718077;border:1px dashed #cad7ce;border-radius:9px}ol{margin:0;padding:0;list-style:none}li{display:grid;grid-template-columns:auto 1fr auto;gap:12px;align-items:start;padding:14px 0;border-top:1px solid #e7ece9}.marker{display:grid;place-items:center;width:34px;height:34px;border-radius:50%;background:#eaf5ee;color:#176b45}li div{display:grid;gap:3px}li small,li time{color:#718077;font-size:12px}.entity{padding:4px 7px;border-radius:99px;background:#eef3f0;color:#4f6659;font-size:11px}@media(max-width:600px){.activity>header{flex-direction:column}li{grid-template-columns:auto 1fr}.entity{grid-column:2}}
</style>
