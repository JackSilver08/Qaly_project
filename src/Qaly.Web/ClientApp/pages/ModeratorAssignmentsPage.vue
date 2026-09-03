<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Clock3, Plus, ShieldCheck, Trash2 } from 'lucide-vue-next'
import { confirmDialog } from '../composables/use-confirm-dialog'
import { showError, showSuccess } from '../composables/use-toast'
import { apiCommand, apiJson, apiResult, errorMessage } from '../utils/api-client'

interface AdminUser { id:string; fullName:string; email:string; role:string }
interface UserPage { items:AdminUser[] }
interface Organization { id:string; name:string; code:string }
interface OrganizationPage { items:Organization[] }
interface Assignment { id:string; moderatorUserId:string; moderatorName:string; moderatorEmail:string; organizationId:string; organizationName:string; capability:string; expiresAt?:string|null; isActive:boolean; revokedAt?:string|null }
const capabilityOptions=[
  ['organization.users.view','Xem danh sách thành viên'],
  ['organization.users.invite','Thêm thành viên'],
  ['organization.users.update_role','Cập nhật vai trò'],
  ['organization.users.remove','Gỡ thành viên'],
  ['organization.professional_profiles.view','Xem hồ sơ nghề nghiệp'],
  ['organization.professional_profiles.manage','Quản lý hồ sơ nghề nghiệp'],
] as const
const moderators=ref<AdminUser[]>([]), organizations=ref<Organization[]>([]), assignments=ref<Assignment[]>([])
const loading=ref(true), saving=ref(false), grantOpen=ref(false)
const form=ref({moderatorUserId:'',organizationId:'',capabilities:capabilityOptions.map(item=>item[0]),expiresAt:''})
const activeAssignments=computed(()=>assignments.value.filter(item=>item.isActive&&!item.revokedAt&&(!item.expiresAt||new Date(item.expiresAt)>new Date())))
async function load(){loading.value=true;try{const [users,orgs,scopes]=await Promise.all([apiJson<UserPage>('/api/admin/users?role=Moderator&pageSize=100'),apiResult<OrganizationPage>('/api/organizations?pageSize=100'),apiJson<Assignment[]>('/api/admin/moderator-assignments')]);moderators.value=users.items;organizations.value=orgs.items;assignments.value=scopes;if(!form.value.moderatorUserId&&moderators.value.length)form.value.moderatorUserId=moderators.value[0].id;if(!form.value.organizationId&&organizations.value.length)form.value.organizationId=organizations.value[0].id}catch(e){showError(errorMessage(e,'Không thể tải phạm vi Moderator.'))}finally{loading.value=false}}
async function grant(){if(!form.value.moderatorUserId||!form.value.organizationId){showError('Hãy chọn Moderator và tổ chức cần hỗ trợ.');return}if(!form.value.capabilities.length){showError('Chọn ít nhất một capability; Qaly không cấp phạm vi rỗng hoặc quyền ngầm định.');return}saving.value=true;try{await apiCommand('/api/admin/moderator-assignments',{method:'POST',body:JSON.stringify({...form.value,expiresAt:form.value.expiresAt?new Date(form.value.expiresAt).toISOString():null})});grantOpen.value=false;showSuccess('Đã cấp phạm vi cho Moderator.');await load()}catch(e){showError(errorMessage(e,'Không thể cấp phạm vi.'))}finally{saving.value=false}}
async function revoke(item:Assignment){if(!await confirmDialog({tone:'critical',title:'Thu hồi quyền Moderator?',subject:item.moderatorName,message:`Quyền tại ${item.organizationName} sẽ ngừng hiệu lực ngay.`,confirmLabel:'Thu hồi quyền'}))return;try{await apiCommand(`/api/admin/moderator-assignments/${item.id}`,{method:'DELETE'});showSuccess('Đã thu hồi quyền Moderator.');await load()}catch(e){showError(errorMessage(e,'Không thể thu hồi quyền.'))}}
function capabilityLabel(value:string){return capabilityOptions.find(item=>item[0]===value)?.[1]??value}
function dateLabel(value?:string|null){return value?new Intl.DateTimeFormat('vi-VN',{dateStyle:'medium',timeStyle:'short'}).format(new Date(value)):'Không hết hạn'}
onMounted(load)
</script>

<template>
  <main class="scope-page">
    <header>
      <div>
        <span class="kicker"><ShieldCheck :size="16" /> Phạm vi hỗ trợ có kiểm soát</span>
        <h1>Ủy quyền Moderator</h1>
        <p>Moderator chỉ nhận đúng capability, tổ chức và thời hạn được cấp. Không assignment nào cấp quyền toàn hệ thống.</p>
      </div>
      <button type="button" class="primary" :disabled="!moderators.length || !organizations.length" :title="!moderators.length ? 'Cần một tài khoản có System role Moderator; mở Quản lý người dùng để tạo hoặc đổi role' : !organizations.length ? 'Cần tạo ít nhất một tổ chức trước khi ủy quyền' : 'Cấp capability theo tổ chức và thời hạn'" @click="grantOpen = true">
        <Plus :size="18" /> Cấp phạm vi
      </button>
    </header>

    <div v-if="loading" class="loading" role="status" aria-live="polite">Đang tải phạm vi...</div>

    <section v-else-if="!activeAssignments.length" class="empty">
      <ShieldCheck :size="38" />
      <h2>Chưa có quyền đang hiệu lực</h2>
      <p>Cấp từng capability cần thiết thay vì trao quyền quản trị rộng.</p>
    </section>

    <div v-else class="table-wrap">
      <table aria-label="Phạm vi ủy quyền Moderator">
        <thead>
          <tr><th scope="col">Moderator</th><th scope="col">Tổ chức</th><th scope="col">Capability</th><th scope="col">Hết hạn</th><th scope="col"><span class="sr-only">Thao tác</span></th></tr>
        </thead>
        <tbody>
          <tr v-for="item in activeAssignments" :key="item.id">
            <td><strong>{{ item.moderatorName }}</strong><small>{{ item.moderatorEmail }}</small></td>
            <td>{{ item.organizationName }}</td>
            <td><span class="capability">{{ capabilityLabel(item.capability) }}</span></td>
            <td><span class="expiry"><Clock3 :size="15" />{{ dateLabel(item.expiresAt) }}</span></td>
            <td>
              <button type="button" class="revoke" :aria-label="`Thu hồi ${capabilityLabel(item.capability)}`" @click="revoke(item)">
                <Trash2 :size="17" />
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="grantOpen" class="overlay" @click.self="grantOpen = false" @keydown.esc="grantOpen = false">
      <form class="modal" role="dialog" aria-modal="true" aria-labelledby="grant-moderator-title" @submit.prevent="grant">
        <h2 id="grant-moderator-title">Cấp phạm vi hỗ trợ</h2>
        <label>
          Moderator
          <select v-model="form.moderatorUserId" required>
            <option v-for="user in moderators" :key="user.id" :value="user.id">{{ user.fullName }} · {{ user.email }}</option>
          </select>
        </label>
        <label>
          Tổ chức
          <select v-model="form.organizationId" required>
            <option v-for="org in organizations" :key="org.id" :value="org.id">{{ org.name }} · {{ org.code }}</option>
          </select>
        </label>
        <fieldset>
          <legend>Capabilities</legend>
          <label v-for="option in capabilityOptions" :key="option[0]" class="check">
            <input v-model="form.capabilities" type="checkbox" :value="option[0]" />{{ option[1] }}
          </label>
        </fieldset>
        <label>
          Hết hạn <small>Để trống nếu không giới hạn</small>
          <input v-model="form.expiresAt" type="datetime-local" />
        </label>
        <div class="actions">
          <button type="button" class="secondary" @click="grantOpen = false">Hủy</button>
          <button type="submit" class="primary" :disabled="saving" :title="saving ? 'Đang lưu và đọc lại assignment' : !form.capabilities.length ? 'Chọn ít nhất một capability; bấm để nhận hướng dẫn' : 'Cấp đúng các capability đã chọn'">Cấp quyền</button>
        </div>
      </form>
    </div>
  </main>
</template>

<style scoped>
.scope-page{max-width:1320px;margin:auto;padding:32px;color:var(--text-primary,#162033)}header{display:flex;align-items:flex-end;justify-content:space-between;gap:24px;margin-bottom:28px}h1{font-size:30px;margin:7px 0}header p{max-width:720px;margin:0;color:var(--text-secondary,#687386)}.kicker{display:inline-flex;align-items:center;gap:7px;color:#166534;font-size:13px;font-weight:750}.primary,.secondary,.revoke{display:inline-flex;align-items:center;justify-content:center;gap:8px;border:1px solid transparent;border-radius:10px;padding:10px 15px;font-weight:700;cursor:pointer}.primary{background:#166534;color:#fff}.secondary{background:var(--surface,#fff);border-color:var(--border-color,#dce2ea);color:inherit}.table-wrap{overflow:auto;border:1px solid var(--border-color,#dce2ea);border-radius:14px;background:var(--surface,#fff)}table{width:100%;min-width:850px;border-collapse:collapse}th,td{text-align:left;padding:14px 16px;border-bottom:1px solid var(--border-color,#e8edf3)}th{font-size:12px;text-transform:uppercase;letter-spacing:.04em;color:var(--text-secondary,#687386);background:var(--surface-muted,#f7f9fb)}td:first-child{display:grid;gap:3px}td small{color:var(--text-secondary,#687386)}.capability{padding:5px 9px;border-radius:999px;background:#dcfce7;color:#166534;font-size:12px;font-weight:700}.expiry{display:inline-flex;align-items:center;gap:6px}.revoke{padding:8px;background:transparent;color:#b91c1c}.revoke:hover{background:#fee2e2}.empty,.loading{text-align:center;padding:70px 20px;color:var(--text-secondary,#687386)}.empty h2{color:var(--text-primary,#162033)}.overlay{position:fixed;inset:0;z-index:1000;display:grid;place-items:center;padding:16px;background:rgba(15,23,42,.48)}.modal{width:min(500px,100%);display:grid;gap:16px;padding:26px;border-radius:14px;background:var(--surface,#fff);box-shadow:0 24px 70px rgba(15,23,42,.22)}.modal h2{margin:0}.modal>label{display:grid;gap:7px;font-size:13px;font-weight:700}.modal select,.modal>label input{height:42px;border:1px solid var(--border-color,#dce2ea);border-radius:9px;padding:0 11px;background:var(--surface,#fff);color:inherit}fieldset{display:grid;gap:10px;border:1px solid var(--border-color,#dce2ea);border-radius:10px;padding:14px}legend{font-weight:700;padding:0 6px}.check{display:flex;align-items:center;gap:9px}.check input{width:17px;height:17px}.actions{display:flex;justify-content:flex-end;gap:10px}button:disabled{opacity:.5;cursor:not-allowed}@media(max-width:720px){.scope-page{padding:20px 16px}header{align-items:stretch;flex-direction:column}.actions{flex-direction:column-reverse}.actions button{width:100%}}
</style>
