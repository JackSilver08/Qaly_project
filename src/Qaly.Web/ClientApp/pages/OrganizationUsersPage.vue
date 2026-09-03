<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { Award, Building2, MailPlus, RefreshCw, Search, ShieldCheck, Trash2, Users } from 'lucide-vue-next'
import MemberSkillEvidenceDrawer from '../components/MemberSkillEvidenceDrawer.vue'
import PageStatePanel from '../components/PageStatePanel.vue'
import type { UserDto } from '../types'
import { confirmDialog } from '../composables/use-confirm-dialog'
import { showError, showSuccess } from '../composables/use-toast'
import { apiCommand, apiResult, errorMessage } from '../utils/api-client'

interface Organization {
  id: string
  name: string
  code: string
  ownerId: string
  memberCount: number
  projectCount: number
  isActive: boolean
}

interface OrganizationPage {
  items: Organization[]
  totalCount: number
}

interface OrganizationMember {
  userId: string
  fullName: string
  email: string
  role: string
  joinedAt: string
}

const roles = ['OrganizationAdmin', 'PrivacyOperator', 'BillingAdmin', 'Member'] as const
const route = useRoute()

const me = ref<UserDto | null>(null)
const organizations = ref<Organization[]>([])
const members = ref<OrganizationMember[]>([])
const selectedId = ref('')
const search = ref('')
const isLoadingOrganizations = ref(true)
const isLoadingMembers = ref(false)
const loadingError = ref('')
const saving = ref<string | null>(null)
const inviteOpen = ref(false)
const evidenceMember = ref<OrganizationMember | null>(null)
const invite = ref({ email: '', role: 'Member' })
const moderatorCapabilities = ref<string[]>([])

const selectedOrganization = computed(
  () => organizations.value.find((item) => item.id === selectedId.value) ?? null,
)

const visibleMembers = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return members.value

  return members.value.filter(
    (item) =>
      item.fullName.toLowerCase().includes(term) ||
      item.email.toLowerCase().includes(term),
  )
})

const myMembership = computed(() => members.value.find((item) => item.userId === me.value?.id))

const canManage = computed(() => {
  if (me.value?.role === 'Admin') return true
  if (selectedOrganization.value?.ownerId === me.value?.id) return true

  const membershipRole = myMembership.value?.role ?? ''
  if (['Owner', 'OrganizationAdmin', 'Admin', 'Manager'].includes(membershipRole)) {
    return true
  }

  return hasCapability('organization.users.invite') ||
    hasCapability('organization.users.update_role') ||
    hasCapability('organization.users.remove')
})

function hasCapability(permission: string) {
  return moderatorCapabilities.value.length === 0 || moderatorCapabilities.value.includes(permission)
}

const hasOrganizations = computed(() => organizations.value.length > 0)
const isInitialLoad = computed(() => isLoadingOrganizations.value && !hasOrganizations.value)
const selectedMemberCount = computed(() => members.value.length)

async function loadOrganizations() {
  loadingError.value = ''
  isLoadingOrganizations.value = true

  try {
    const page = await apiResult<OrganizationPage>('/api/organizations?pageSize=100')
    organizations.value = page.items.filter((item) => item.isActive)

    const requestedId =
      typeof route.query.organization === 'string' ? route.query.organization : ''

    if (requestedId && organizations.value.some((item) => item.id === requestedId)) {
      selectedId.value = requestedId
    } else if (!selectedId.value && organizations.value.length) {
      selectedId.value = organizations.value[0].id
    }

    if (selectedId.value) {
      await loadMembers()
    } else {
      members.value = []
      moderatorCapabilities.value = []
    }
  } catch (error) {
    loadingError.value = errorMessage(error, 'Không thể tải danh sách tổ chức.')
    showError(loadingError.value)
  } finally {
    isLoadingOrganizations.value = false
  }
}

async function loadMembers() {
  if (!selectedId.value) return

  isLoadingMembers.value = true
  loadingError.value = ''

  try {
    const [loadedMembers, capabilities] = await Promise.all([
      apiResult<OrganizationMember[]>(`/api/organizations/${selectedId.value}/users`),
      apiResult<string[]>(`/api/organizations/${selectedId.value}/moderator-capabilities`),
    ])

    members.value = loadedMembers
    moderatorCapabilities.value = capabilities
  } catch (error) {
    members.value = []
    moderatorCapabilities.value = []
    loadingError.value = errorMessage(error, 'Không thể tải thành viên tổ chức.')
    showError(loadingError.value)
  } finally {
    isLoadingMembers.value = false
  }
}

async function addMember() {
  if (!selectedId.value || !invite.value.email.trim()) return

  saving.value = 'invite'
  try {
    await apiCommand(`/api/organizations/${selectedId.value}/users`, {
      method: 'POST',
      body: JSON.stringify(invite.value),
    })
    invite.value = { email: '', role: 'Member' }
    inviteOpen.value = false
    showSuccess('Đã thêm thành viên vào tổ chức.')
    await loadMembers()
  } catch (error) {
    showError(errorMessage(error, 'Không thể thêm thành viên.'))
  } finally {
    saving.value = null
  }
}

async function changeRole(member: OrganizationMember, nextRole: string) {
  if (member.role === 'Owner') return

  saving.value = member.userId
  const previous = member.role
  member.role = nextRole

  try {
    await apiCommand(`/api/organizations/${selectedId.value}/users/${member.userId}`, {
      method: 'PATCH',
      body: JSON.stringify({ role: nextRole }),
    })
    showSuccess('Đã cập nhật vai trò tổ chức.')
  } catch (error) {
    member.role = previous
    showError(errorMessage(error, 'Không thể cập nhật vai trò.'))
  } finally {
    saving.value = null
  }
}

async function removeMember(member: OrganizationMember) {
  if (member.role === 'Owner') return

  const confirmed = await confirmDialog({
    tone: 'critical',
    title: 'Gỡ thành viên khỏi tổ chức?',
    subject: member.fullName,
    message:
      'Người này sẽ mất quyền từ vai trò tổ chức. Quyền dự án riêng cần được kiểm tra độc lập.',
    confirmLabel: 'Gỡ thành viên',
  })

  if (!confirmed) return

  saving.value = member.userId
  try {
    await apiCommand(`/api/organizations/${selectedId.value}/users/${member.userId}`, {
      method: 'DELETE',
    })
    showSuccess('Đã gỡ thành viên khỏi tổ chức.')
    await loadMembers()
  } catch (error) {
    showError(errorMessage(error, 'Không thể gỡ thành viên.'))
  } finally {
    saving.value = null
  }
}

function roleLabel(role: string) {
  return (
    {
      Owner: 'Chủ sở hữu',
      OrganizationAdmin: 'Quản trị tổ chức',
      Admin: 'Quản trị tổ chức',
      Manager: 'Quản trị tổ chức',
      PrivacyOperator: 'Phụ trách riêng tư',
      BillingAdmin: 'Quản trị thanh toán',
      Member: 'Thành viên',
    } as Record<string, string>
  )[role] ?? role
}

function joinedLabel(value: string) {
  return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium' }).format(new Date(value))
}

onMounted(async () => {
  try {
    me.value = await apiResult<UserDto>('/api/auth/me')
  } finally {
    await loadOrganizations()
  }
})
</script>

<template>
  <main class="org-users-page">
    <header class="page-head">
      <div>
        <span class="kicker"><ShieldCheck :size="16" /> Phân quyền theo tổ chức</span>
        <h1>Thành viên tổ chức</h1>
        <p>
          Vai trò tại đây chỉ có hiệu lực trong tổ chức được chọn và không cấp quyền quản trị toàn
          hệ thống.
        </p>
      </div>
      <button
        v-if="canManage && hasCapability('organization.users.invite')"
        class="primary"
        aria-label="Thêm thành viên tổ chức"
        :disabled="isInitialLoad || !selectedId"
        @click="inviteOpen = true"
      >
        <MailPlus :size="18" /> Thêm thành viên
      </button>
    </header>

    <PageStatePanel
      v-if="isInitialLoad"
      variant="loading"
      title="Đang tải tổ chức"
      message="Hệ thống đang xác định tổ chức hiện tại và danh sách thành viên."
    />

    <PageStatePanel
      v-else-if="loadingError && !hasOrganizations"
      variant="error"
      title="Không thể tải dữ liệu"
      :message="loadingError"
    >
      <template #icon>
        <RefreshCw :size="22" />
      </template>
      <template #actions>
        <button class="primary" type="button" @click="loadOrganizations">Thử lại</button>
      </template>
    </PageStatePanel>

    <template v-else>
      <section v-if="hasOrganizations" class="context-bar">
        <label>
          <Building2 :size="18" />
          <span>Tổ chức</span>
          <select v-model="selectedId" @change="loadMembers">
            <option v-for="organization in organizations" :key="organization.id" :value="organization.id">
              {{ organization.name }} · {{ organization.code }}
            </option>
          </select>
        </label>
        <div class="context-stats">
          <span>{{ selectedMemberCount }} thành viên</span>
          <span>{{ selectedOrganization?.projectCount ?? 0 }} dự án</span>
        </div>
      </section>

      <section v-if="hasOrganizations" class="toolbar" aria-label="Công cụ thành viên">
        <label class="search">
          <Search :size="18" />
          <input v-model="search" placeholder="Tìm theo tên hoặc email" />
        </label>
        <button class="icon-button" :title="isLoadingMembers ? 'Đang tải' : 'Tải lại'" :aria-label="isLoadingMembers ? 'Đang tải lại danh sách thành viên' : 'Tải lại danh sách thành viên'" @click="loadMembers">
          <RefreshCw :size="18" :class="{ 'is-spinning': isLoadingMembers }" />
        </button>
      </section>

      <PageStatePanel
        v-if="!selectedId || !selectedOrganization"
        variant="empty"
        title="Chọn tổ chức"
        message="Hãy chọn một tổ chức để xem và quản lý thành viên."
      >
        <template #icon>
          <Users :size="22" />
        </template>
      </PageStatePanel>

      <PageStatePanel
        v-else-if="isLoadingMembers"
        variant="loading"
        title="Đang tải thành viên"
        message="Hệ thống đang cập nhật danh sách thành viên và quyền hạn."
      />

      <PageStatePanel
        v-else-if="!visibleMembers.length"
        variant="empty"
        title="Không tìm thấy thành viên"
        :message="
          search.trim()
            ? 'Thử thay đổi từ khóa hoặc xóa bộ lọc hiện tại.'
            : 'Tổ chức này chưa có thành viên nào.'
        "
      >
        <template #icon>
          <Users :size="22" />
        </template>
        <template #actions>
          <button
            v-if="canManage && hasCapability('organization.users.invite')"
            class="primary"
            type="button"
            aria-label="Thêm thành viên tổ chức"
            @click="inviteOpen = true"
          >
            <MailPlus :size="18" /> Thêm thành viên
          </button>
        </template>
      </PageStatePanel>

      <div v-else class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Thành viên</th>
              <th>Vai trò tổ chức</th>
              <th>Ngày tham gia</th>
              <th><span class="sr-only">Thao tác</span></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="member in visibleMembers" :key="member.userId">
              <td>
                <div class="member">
                  <span class="avatar">{{ member.fullName.slice(0, 1).toUpperCase() }}</span>
                  <span>
                    <strong>{{ member.fullName }}</strong>
                    <small>{{ member.email }}</small>
                  </span>
                </div>
              </td>
              <td>
                <span v-if="member.role === 'Owner'" class="role owner">
                  {{ roleLabel(member.role) }}
                </span>
                <select
                  v-else-if="canManage && hasCapability('organization.users.update_role')"
                  :value="member.role"
                  :disabled="saving === member.userId"
                  @change="changeRole(member, ($event.target as HTMLSelectElement).value)"
                >
                  <option v-for="role in roles" :key="role" :value="role">
                    {{ roleLabel(role) }}
                  </option>
                </select>
                <span v-else class="role">{{ roleLabel(member.role) }}</span>
              </td>
              <td>{{ joinedLabel(member.joinedAt) }}</td>
              <td class="row-action">
                <button
                  class="evidence"
                  type="button"
                  title="Xem bằng chứng kỹ năng"
                  :aria-label="`Xem bằng chứng kỹ năng ${member.fullName}`"
                  @click="evidenceMember = member"
                >
                  <Award :size="17" />
                </button>
                <button
                  v-if="canManage && member.role !== 'Owner' && hasCapability('organization.users.remove')"
                  class="remove"
                  :disabled="saving === member.userId"
                  title="Gỡ thành viên"
                  :aria-label="`Gỡ ${member.fullName}`"
                  @click="removeMember(member)"
                >
                  <Trash2 :size="17" />
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>

    <div v-if="inviteOpen" class="overlay" @click.self="inviteOpen = false">
      <form class="modal" @submit.prevent="addMember">
        <h2>Thêm thành viên</h2>
        <p>
          Nhập email của một tài khoản đang hoạt động. Người dùng chỉ được cấp quyền trong tổ chức
          này.
        </p>
        <label>
          Email
          <input
            v-model.trim="invite.email"
            type="email"
            autocomplete="email"
            required
            placeholder="name@company.com"
          />
        </label>
        <label>
          Vai trò
          <select v-model="invite.role">
            <option v-for="role in roles" :key="role" :value="role">
              {{ roleLabel(role) }}
            </option>
          </select>
        </label>
        <div class="modal-actions">
          <button type="button" class="secondary" @click="inviteOpen = false">Hủy</button>
          <button class="primary" :disabled="saving === 'invite'">Thêm thành viên</button>
        </div>
      </form>
    </div>

    <MemberSkillEvidenceDrawer
      v-if="evidenceMember && selectedId"
      :organization-id="selectedId"
      :member-id="evidenceMember.userId"
      :member-name="evidenceMember.fullName"
      @close="evidenceMember = null"
    />
  </main>
</template>

<style scoped>
.org-users-page {
  max-width: 1320px;
  margin: auto;
  padding: 32px;
  color: var(--text-primary, #162033);
  display: grid;
  gap: 14px;
}

.page-head {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 24px;
  margin-bottom: 10px;
}

.page-head h1 {
  font-size: 30px;
  margin: 7px 0;
}

.page-head p {
  max-width: 720px;
  margin: 0;
  color: var(--text-secondary, #687386);
}

.kicker {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  color: #166534;
  font-size: 13px;
  font-weight: 750;
}

.primary,
.secondary,
.icon-button,
.remove {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  border: 1px solid transparent;
  border-radius: 10px;
  padding: 10px 15px;
  font-weight: 700;
  cursor: pointer;
}

.primary {
  background: #166534;
  color: #fff;
}

.secondary,
.icon-button {
  background: var(--surface, #fff);
  border-color: var(--border-color, #dce2ea);
  color: inherit;
}

.context-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
  padding: 16px 18px;
  border: 1px solid var(--border-color, #dce2ea);
  border-radius: 14px;
  background: var(--surface, #fff);
}

.context-bar label {
  display: flex;
  align-items: center;
  gap: 10px;
  font-weight: 700;
}

.context-bar select,
.table-wrap select,
.modal input,
.modal select {
  height: 40px;
  border: 1px solid var(--border-color, #dce2ea);
  border-radius: 9px;
  padding: 0 11px;
  background: var(--surface, #fff);
  color: inherit;
}

.context-stats {
  display: flex;
  gap: 18px;
  color: var(--text-secondary, #687386);
  font-size: 14px;
}

.toolbar {
  display: grid;
  grid-template-columns: 1fr 44px;
  gap: 10px;
}

.search {
  display: flex;
  align-items: center;
  gap: 9px;
  height: 44px;
  padding: 0 12px;
  border: 1px solid var(--border-color, #dce2ea);
  border-radius: 10px;
  background: var(--surface, #fff);
}

.search input {
  width: 100%;
  border: 0;
  outline: 0;
  background: transparent;
  color: inherit;
}

.icon-button {
  padding: 0;
}

.table-wrap {
  overflow: auto;
  border: 1px solid var(--border-color, #dce2ea);
  border-radius: 14px;
  background: var(--surface, #fff);
}

table {
  width: 100%;
  min-width: 720px;
  border-collapse: collapse;
}

th,
td {
  text-align: left;
  padding: 14px 16px;
  border-bottom: 1px solid var(--border-color, #e8edf3);
}

th {
  font-size: 12px;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--text-secondary, #687386);
  background: var(--surface-muted, #f7f9fb);
}

.member {
  display: flex;
  align-items: center;
  gap: 11px;
}

.member > span:last-child {
  display: grid;
  gap: 3px;
}

.member small {
  color: var(--text-secondary, #687386);
}

.avatar {
  width: 38px;
  height: 38px;
  border-radius: 10px;
  display: grid;
  place-items: center;
  background: #dcfce7;
  color: #166534;
  font-weight: 800;
}

.role {
  display: inline-flex;
  padding: 5px 9px;
  border-radius: 999px;
  background: #f1f5f9;
  font-size: 12px;
  font-weight: 700;
}

.role.owner {
  background: #dcfce7;
  color: #166534;
}

.row-action {
  text-align: right;
  white-space: nowrap;
}

.evidence {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 8px;
  border: 1px solid transparent;
  border-radius: 10px;
  background: transparent;
  color: #0f766e;
  cursor: pointer;
}

.evidence:hover {
  background: #ecfdf5;
}

.remove {
  padding: 8px;
  background: transparent;
  color: #b91c1c;
}

.remove:hover {
  background: #fee2e2;
}

.overlay {
  position: fixed;
  inset: 0;
  z-index: 1000;
  display: grid;
  place-items: center;
  padding: 16px;
  background: rgba(15, 23, 42, 0.48);
}

.modal {
  width: min(440px, 100%);
  display: grid;
  gap: 16px;
  padding: 26px;
  border-radius: 14px;
  background: var(--surface, #fff);
  box-shadow: 0 24px 70px rgba(15, 23, 42, 0.22);
}

.modal h2,
.modal p {
  margin: 0;
}

.modal p {
  color: var(--text-secondary, #687386);
}

.modal label {
  display: grid;
  gap: 7px;
  font-size: 13px;
  font-weight: 700;
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 4px;
}

button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}

.is-spinning {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 720px) {
  .org-users-page {
    padding: 20px 16px;
  }

  .page-head {
    align-items: stretch;
    flex-direction: column;
  }

  .context-bar,
  .context-bar label {
    align-items: stretch;
    flex-direction: column;
  }

  .context-stats {
    justify-content: space-between;
  }

  .modal-actions {
    flex-direction: column-reverse;
  }

  .modal-actions button {
    width: 100%;
  }
}

@media (prefers-reduced-motion: reduce) {
  .is-spinning {
    animation: none;
  }
}
</style>
