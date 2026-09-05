<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { Award, BriefcaseBusiness, Building2, MailPlus, RefreshCw, Search, ShieldCheck, SlidersHorizontal, Trash2, Users, X } from 'lucide-vue-next'
import MemberSkillEvidenceDrawer from '../components/MemberSkillEvidenceDrawer.vue'
import MemberProfessionalProfileDrawer from '../components/MemberProfessionalProfileDrawer.vue'
import PageStatePanel from '../components/PageStatePanel.vue'
import type { UserDto } from '../types'
import { confirmDialog } from '../composables/use-confirm-dialog'
import { showError, showSuccess } from '../composables/use-toast'
import { apiCommand, apiResult, errorMessage } from '../utils/api-client'
import {
  canManageOrganizationUsers,
  canUseOrganizationCapability,
  canViewProfessionalProfiles,
  type OrganizationActorContext,
} from '../utils/organization-access'
import { projectRoleLabel } from '../utils/project-roles'

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
  projects: Array<{ projectId: string; projectName: string; projectCode: string; role: string }> | null
  weeklyCapacityHours: number | null
  capacityState: string | null
  skills: string[] | null
}

const roles = ['OrganizationAdmin', 'PrivacyOperator', 'BillingAdmin', 'Member'] as const
const route = useRoute()

const me = ref<UserDto | null>(null)
const organizations = ref<Organization[]>([])
const members = ref<OrganizationMember[]>([])
const selectedId = ref('')
const search = ref('')
const roleFilter = ref('')
const joinedFilter = ref('')
const isLoadingOrganizations = ref(true)
const isLoadingMembers = ref(false)
const loadingError = ref('')
const saving = ref<string | null>(null)
const inviteOpen = ref(false)
const evidenceMember = ref<OrganizationMember | null>(null)
const professionalProfileMember = ref<OrganizationMember | null>(null)
const invite = ref({ email: '', role: 'Member' })
const moderatorCapabilities = ref<string[]>([])
let memberLoadVersion = 0

const selectedOrganization = computed(
  () => organizations.value.find((item) => item.id === selectedId.value) ?? null,
)

function searchable(value: string) {
  return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/gi, 'd').toLowerCase()
}

function filterRole(role: string) {
  const value = role.trim().toLowerCase()
  if (value === 'admin' || value === 'manager') return 'OrganizationAdmin'
  return ['Owner', ...roles].find(item => item.toLowerCase() === value) ?? role
}

const roleOptions = computed(() => {
  const counts = new Map<string, number>()
  for (const member of members.value) {
    const role = filterRole(member.role)
    counts.set(role, (counts.get(role) ?? 0) + 1)
  }
  return [...new Set(['Owner', ...roles, ...counts.keys()])].map(role => ({
    value: role, label: roleLabel(role), count: counts.get(role) ?? 0,
  }))
})

const hasFilters = computed(() => !!(search.value.trim() || roleFilter.value || joinedFilter.value))

const visibleMembers = computed(() => {
  const term = searchable(search.value.trim())
  const now = Date.now()
  const joinedSince = joinedFilter.value ? now - Number(joinedFilter.value) * 86400000 : null

  return members.value.filter(item => {
    const matchesText = !term ||
      searchable(item.fullName).includes(term) ||
      searchable(item.email).includes(term) ||
      (item.projects ?? []).some(project => searchable(`${project.projectName} ${project.projectCode} ${project.role}`).includes(term)) ||
      (item.skills ?? []).some(skill => searchable(skill).includes(term))
    if (!matchesText) return false
    if (roleFilter.value && filterRole(item.role) !== roleFilter.value) return false
    if (joinedSince !== null) {
      const joinedAt = Date.parse(item.joinedAt)
      if (!Number.isFinite(joinedAt) || joinedAt < joinedSince || joinedAt > now) return false
    }
    return true
  })
})

function clearFilters() {
  search.value = ''
  roleFilter.value = ''
  joinedFilter.value = ''
}

function projectRoleCount(projects: OrganizationMember['projects']) {
  return new Set((projects ?? []).map(project => projectRoleLabel(project.role))).size
}

function changeOrganization() {
  clearFilters()
  evidenceMember.value = null
  professionalProfileMember.value = null
  void loadMembers()
}

const myMembership = computed(() => members.value.find((item) => item.userId === me.value?.id))

const accessContext = computed<OrganizationActorContext>(() => ({
  systemRole: me.value?.role,
  actorId: me.value?.id,
  ownerId: selectedOrganization.value?.ownerId,
  membershipRole: myMembership.value?.role,
  hasMembership: !!myMembership.value,
  moderatorCapabilities: moderatorCapabilities.value,
}))

const canManage = computed(() => canManageOrganizationUsers(accessContext.value))
const canViewProfessionalProfile = computed(() => canViewProfessionalProfiles(accessContext.value))

function hasCapability(permission: string) {
  return canUseOrganizationCapability(accessContext.value, permission)
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

  const organizationId = selectedId.value
  const loadVersion = ++memberLoadVersion
  isLoadingMembers.value = true
  loadingError.value = ''
  members.value = []
  moderatorCapabilities.value = []

  try {
    const [loadedMembers, capabilities] = await Promise.all([
      apiResult<OrganizationMember[]>(`/api/organizations/${organizationId}/users`),
      apiResult<string[]>(`/api/organizations/${organizationId}/moderator-capabilities`),
    ])

    if (loadVersion !== memberLoadVersion || organizationId !== selectedId.value) return
    members.value = loadedMembers
    moderatorCapabilities.value = capabilities
  } catch (error) {
    if (loadVersion !== memberLoadVersion || organizationId !== selectedId.value) return
    members.value = []
    moderatorCapabilities.value = []
    loadingError.value = errorMessage(error, 'Không thể tải thành viên tổ chức.')
    showError(loadingError.value)
  } finally {
    if (loadVersion === memberLoadVersion) isLoadingMembers.value = false
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
  const date = new Date(value)
  return Number.isFinite(date.getTime())
    ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium' }).format(date)
    : 'Chưa có ngày tham gia'
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
      <button type="button"
        v-if="canManage && hasCapability('organization.users.invite')"
        class="primary"
        aria-label="Thêm thành viên tổ chức"
        :disabled="isInitialLoad || !selectedId"
        :title="isInitialLoad ? 'Đang xác định tổ chức hiện tại' : !selectedId ? 'Chọn một tổ chức trước khi thêm thành viên' : 'Thêm tài khoản đang hoạt động vào tổ chức này'"
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
          <select v-model="selectedId" aria-label="Chọn tổ chức" @change="changeOrganization">
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
          <Search :size="18" aria-hidden="true" />
          <input v-model="search" type="search" aria-label="Tìm thành viên theo tên hoặc email" placeholder="Tìm theo tên hoặc email" />
        </label>
        <label class="filter-control">
          <SlidersHorizontal :size="16" aria-hidden="true" />
          <select v-model="roleFilter" aria-label="Lọc theo vai trò tổ chức">
            <option value="">Tất cả vai trò</option>
            <option v-for="option in roleOptions" :key="option.value" :value="option.value">{{ option.label }} ({{ option.count }})</option>
          </select>
        </label>
        <label class="filter-control">
          <select v-model="joinedFilter" aria-label="Lọc theo ngày tham gia">
            <option value="">Mọi ngày tham gia</option>
            <option value="7">Tham gia 7 ngày qua</option>
            <option value="30">Tham gia 30 ngày qua</option>
            <option value="90">Tham gia 90 ngày qua</option>
          </select>
        </label>
        <button type="button" class="icon-button" :disabled="isLoadingMembers" :title="isLoadingMembers ? 'Đang tải' : 'Tải lại'" :aria-label="isLoadingMembers ? 'Đang tải lại danh sách thành viên' : 'Tải lại danh sách thành viên'" @click="loadMembers">
          <RefreshCw :size="18" :class="{ 'is-spinning': isLoadingMembers }" />
        </button>
      </section>

      <div v-if="hasOrganizations && !isLoadingMembers && !loadingError" class="filter-summary">
        <span role="status" aria-live="polite" aria-atomic="true">Hiển thị {{ visibleMembers.length }} / {{ selectedMemberCount }} thành viên<span v-if="hasFilters"> · đang lọc</span></span>
        <button v-if="hasFilters" type="button" class="clear-filters" @click="clearFilters"><X :size="14" aria-hidden="true" /> Xóa bộ lọc</button>
      </div>

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
        v-else-if="loadingError"
        variant="error"
        title="Không thể tải thành viên"
        :message="loadingError"
      >
        <template #actions><button type="button" class="secondary" @click="loadMembers">Thử lại</button></template>
      </PageStatePanel>

      <PageStatePanel
        v-else-if="!visibleMembers.length"
        variant="empty"
        title="Không tìm thấy thành viên"
        :message="
          hasFilters
            ? 'Thử thay đổi từ khóa hoặc xóa bộ lọc hiện tại.'
            : 'Tổ chức này chưa có thành viên nào.'
        "
      >
        <template #icon>
          <Users :size="22" />
        </template>
        <template #actions>
          <button v-if="hasFilters" type="button" class="secondary" @click="clearFilters">Xóa bộ lọc</button>
          <button
            v-else-if="canManage && hasCapability('organization.users.invite')"
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
        <table aria-label="Thành viên tổ chức">
          <colgroup>
            <col class="member-column" />
            <col class="organization-role-column" />
            <col class="project-column" />
            <col class="project-role-column" />
            <col class="signals-column" />
            <col class="joined-column" />
            <col class="actions-column" />
          </colgroup>
          <thead>
            <tr>
              <th scope="col">Thành viên</th>
              <th scope="col">Vai trò tổ chức</th>
              <th scope="col">Project</th>
              <th scope="col">Vai trò dự án</th>
              <th scope="col">Skill &amp; capacity</th>
              <th scope="col">Ngày tham gia</th>
              <th scope="col"><span class="sr-only">Thao tác</span></th>
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
                  :value="filterRole(member.role)"
                  :aria-label="`Vai trò tổ chức của ${member.fullName}`"
                  :disabled="saving === member.userId"
                  @change="changeRole(member, ($event.target as HTMLSelectElement).value)"
                >
                  <option v-for="role in roles" :key="role" :value="role">
                    {{ roleLabel(role) }}
                  </option>
                </select>
                <span v-else class="role">{{ roleLabel(member.role) }}</span>
              </td>
              <td class="project-memberships">
                <details v-if="member.projects?.length" class="summary-disclosure summary-disclosure--projects">
                  <summary :aria-label="`Xem ${member.projects.length} Project của ${member.fullName}`">
                    <BriefcaseBusiness :size="14" aria-hidden="true" />
                    <strong>{{ member.projects.length }}</strong>
                    <span>dự án</span>
                  </summary>
                  <div class="compact-popover">
                    <strong>Dự án đang tham gia</strong>
                    <RouterLink
                      v-for="project in member.projects"
                      :key="project.projectId"
                      class="popover-row"
                      :to="{ name: 'project-detail', params: { projectId: project.projectId } }"
                    >
                      <span>{{ project.projectName }}</span>
                      <small>{{ project.projectCode }}</small>
                    </RouterLink>
                  </div>
                </details>
                <small v-else class="empty-summary">Chưa có dự án</small>
              </td>
              <td class="project-roles">
                <details v-if="member.projects?.length" class="summary-disclosure summary-disclosure--roles">
                  <summary :aria-label="`Xem vai trò theo ${member.projects.length} Project của ${member.fullName}`">
                    <ShieldCheck :size="14" aria-hidden="true" />
                    <strong>{{ projectRoleCount(member.projects) }}</strong>
                    <span>vai trò</span>
                  </summary>
                  <div class="compact-popover compact-popover--roles">
                    <strong>Vai trò theo dự án</strong>
                    <span v-for="project in member.projects" :key="project.projectId" class="popover-row">
                      <b>{{ project.projectName }}</b>
                      <small>{{ projectRoleLabel(project.role) }}</small>
                    </span>
                  </div>
                </details>
                <small v-else class="empty-summary">Chưa có vai trò</small>
              </td>
              <td class="people-signals">
                <div v-if="member.weeklyCapacityHours != null || member.skills?.length" class="capacity-summary">
                  <span v-if="member.weeklyCapacityHours != null" class="capacity-value">
                    <strong>{{ member.weeklyCapacityHours }}h</strong><small>/tuần</small>
                  </span>
                  <details v-if="member.skills?.length" class="summary-disclosure summary-disclosure--skills">
                    <summary :aria-label="`Xem ${member.skills.length} kỹ năng của ${member.fullName}`">
                      <Award :size="14" aria-hidden="true" />
                      <strong>{{ member.skills.length }}</strong>
                      <span>kỹ năng</span>
                    </summary>
                    <div class="compact-popover compact-popover--skills">
                      <strong>Kỹ năng chuyên môn</strong>
                      <span v-for="skill in member.skills" :key="skill" class="popover-row">{{ skill }}</span>
                    </div>
                  </details>
                </div>
                <small v-if="member.capacityState === 'assumed_default'" class="capacity-note">Capacity mặc định</small>
                <small v-else-if="member.weeklyCapacityHours == null">Không có quyền xem dữ liệu năng lực</small>
              </td>
              <td class="joined-at">{{ joinedLabel(member.joinedAt) }}</td>
              <td class="row-action">
                <button
                  v-if="canViewProfessionalProfile"
                  class="professional-profile"
                  type="button"
                  :aria-label="`Xem hồ sơ nghề nghiệp ${member.fullName}`"
                  title="Hồ sơ nghề nghiệp — không cấp quyền truy cập"
                  @click="professionalProfileMember = member"
                >
                  <BriefcaseBusiness :size="17" />
                </button>
                <button
                  class="evidence"
                  type="button"
                  title="Xem bằng chứng kỹ năng"
                  :aria-label="`Xem bằng chứng kỹ năng ${member.fullName}`"
                  @click="evidenceMember = member"
                >
                  <Award :size="17" />
                </button>
                <button type="button"
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

    <div v-if="inviteOpen" class="overlay" @click.self="inviteOpen = false" @keydown.esc="inviteOpen = false">
      <form class="modal" role="dialog" aria-modal="true" aria-labelledby="invite-member-title" @submit.prevent="addMember">
        <h2 id="invite-member-title">Thêm thành viên</h2>
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
          <button type="submit" class="primary" :disabled="saving === 'invite'" :title="saving === 'invite' ? 'Đang lưu và đọc lại thành viên' : 'Nhập email hợp lệ; trình duyệt sẽ đưa bạn tới trường còn thiếu'">Thêm thành viên</button>
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
    <MemberProfessionalProfileDrawer
      v-if="professionalProfileMember && selectedId"
      :organization-id="selectedId"
      :member-id="professionalProfileMember.userId"
      :member-name="professionalProfileMember.fullName"
      @close="professionalProfileMember = null"
    />
  </main>
</template>

<style scoped>
.org-users-page {
  width: 100%;
  min-width: 0;
  max-width: 1320px;
  box-sizing: border-box;
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

.table-wrap select {
  width: 100%;
  min-width: 0;
  max-width: 100%;
}

.context-stats {
  display: flex;
  gap: 18px;
  color: var(--text-secondary, #687386);
  font-size: 14px;
}

.toolbar {
  display: grid;
  grid-template-columns: minmax(180px, 1fr) minmax(190px, 220px) minmax(170px, 200px) 44px;
  gap: 10px;
}

.search,
.filter-control {
  display: flex;
  align-items: center;
  gap: 9px;
  min-width: 0;
  height: 44px;
  padding: 0 12px;
  border: 1px solid var(--border-color, #dce2ea);
  border-radius: 10px;
  background: var(--surface, #fff);
}

.search input,
.search input:focus,
.search input:focus-visible {
  min-width: 0;
  width: 100%;
  padding: 0;
  border: 0 !important;
  outline: 0 !important;
  box-shadow: none !important;
  appearance: none;
  background: transparent !important;
  color: inherit;
  font: inherit;
}

.search input::-webkit-search-decoration,
.search input::-webkit-search-cancel-button { appearance: none; }

.filter-control select {
  width: 100%;
  min-width: 0;
  height: 100%;
  border: 0;
  background: var(--surface, #fff);
  color: inherit;
  font: inherit;
  font-size: 13px;
  cursor: pointer;
}

.search:focus-within,
.filter-control:focus-within {
  outline: 2px solid var(--primary, #2563eb);
  outline-offset: 2px;
}

.search svg,
.filter-control svg { flex-shrink: 0; }

.filter-summary {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
  color: var(--text-secondary, #687386);
  font-size: 13px;
}

.clear-filters {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  border: 0;
  background: transparent;
  color: var(--primary, #2563eb);
  padding: 6px 0;
  font: inherit;
  cursor: pointer;
}

.icon-button {
  padding: 0;
}

.table-wrap {
  width: 100%;
  max-width: 100%;
  min-width: 0;
  overflow: auto;
  overscroll-behavior-inline: contain;
  border: 1px solid var(--border-color, #dce2ea);
  border-radius: 14px;
  background: var(--surface, #fff);
}

table {
  width: 100%;
  min-width: 920px;
  table-layout: fixed;
  border-collapse: collapse;
}

.member-column { width: 19%; }
.organization-role-column { width: 16%; }
.project-column { width: 15%; }
.project-role-column { width: 14%; }
.signals-column { width: 17%; }
.joined-column { width: 8%; }
.actions-column { width: 11%; }

th,
td {
  text-align: left;
  padding: 14px 12px;
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
  min-width: 0;
}

.member strong,
.member small {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.member small {
  color: var(--text-secondary, #687386);
}

.avatar {
  flex: 0 0 auto;
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

.project-memberships,
.project-roles,
.people-signals {
  min-width: 0;
}

.summary-disclosure {
  position: relative;
  width: 100%;
  min-width: 0;
}

.summary-disclosure > summary {
  width: 100%;
  min-height: 34px;
  box-sizing: border-box;
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 8px;
  border: 1px solid #dbe5f0;
  border-radius: 9px;
  background: var(--surface-muted, #f8fafc);
  color: #475569;
  cursor: pointer;
  list-style: none;
  white-space: nowrap;
  transition: border-color 140ms ease, background-color 140ms ease, transform 140ms ease;
}

.summary-disclosure > summary::-webkit-details-marker { display: none; }
.summary-disclosure > summary:hover,
.summary-disclosure > summary:focus-visible {
  border-color: #93b4f5;
  background: #fff;
  outline: none;
  transform: translateY(-1px);
}

.summary-disclosure > summary strong {
  margin: 0;
  color: #0f172a;
  font-size: 13px;
}

.summary-disclosure > summary span {
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 11px;
  font-weight: 700;
}

.summary-disclosure--projects > summary { background: #f0fdf4; color: #166534; }
.summary-disclosure--roles > summary { background: #fff7ed; color: #9a3412; }
.summary-disclosure--skills > summary { background: #eff6ff; color: #1d4ed8; }

.compact-popover {
  position: absolute;
  z-index: 30;
  top: calc(100% + 7px);
  left: 0;
  width: min(340px, 70vw);
  display: none;
  gap: 7px;
  padding: 12px;
  border: 1px solid var(--border-color, #dce2ea);
  border-radius: 11px;
  background: var(--surface, #fff);
  box-shadow: 0 14px 35px rgba(15, 23, 42, 0.16);
}

.summary-disclosure[open] > .compact-popover { display: grid; }
.summary-disclosure--skills > .compact-popover { right: 0; left: auto; }

.compact-popover > strong {
  color: #334155;
  font-size: 12px;
}

.popover-row {
  min-width: 0;
  display: flex !important;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin: 0 !important;
  padding: 7px 8px !important;
  border-radius: 8px !important;
  background: #f8fafc !important;
  color: #334155 !important;
  font-size: 11px !important;
  font-weight: 650 !important;
  text-decoration: none;
  white-space: normal;
}

.popover-row:hover { background: #eef4ff !important; }
.popover-row > span,
.popover-row > b {
  min-width: 0;
  overflow-wrap: anywhere;
}

.popover-row > small {
  flex: 0 0 auto;
  margin: 0;
  color: #64748b;
  font-size: 10px;
  white-space: nowrap;
}

.capacity-summary {
  display: flex;
  align-items: center;
  gap: 7px;
  min-width: 0;
}

.capacity-summary .summary-disclosure { flex: 1 1 auto; }

.capacity-value {
  flex: 0 0 auto;
  display: inline-flex;
  align-items: baseline;
  gap: 2px;
}

.capacity-value strong {
  margin: 0;
  color: #166534;
  font-size: 13px;
}

.capacity-value small,
.capacity-note,
.empty-summary,
.people-signals > small {
  color: var(--text-secondary, #687386);
  font-size: 10px;
}

.capacity-note {
  display: block;
  margin-top: 4px;
}

.empty-summary {
  display: inline-block;
  padding: 6px 0;
  font-size: 11px;
}

.summary-disclosure:hover > .compact-popover,
.summary-disclosure:focus-within > .compact-popover {
  display: grid;
}

.row-action {
  padding-right: 8px;
  padding-left: 8px;
  text-align: right;
  white-space: nowrap;
}

.row-action .professional-profile,
.row-action .evidence,
.row-action .remove {
  padding: 5px;
}

.joined-at {
  color: var(--text-secondary, #687386);
  font-size: 12px;
  line-height: 1.35;
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

.professional-profile {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 8px;
  border: 1px solid transparent;
  border-radius: 10px;
  background: transparent;
  color: #1d4ed8;
  cursor: pointer;
}

.professional-profile:hover {
  background: #eff6ff;
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

@media (max-width: 1100px) {
  .toolbar { grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) 44px; }
  .toolbar .search { grid-column: 1 / -1; }
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

@media (max-width: 440px) {
  .toolbar { grid-template-columns: minmax(0, 1fr) 44px; }
  .toolbar .filter-control { grid-column: 1; }
  .toolbar .icon-button { grid-column: 2; grid-row: 3; }
}

@media (prefers-reduced-motion: reduce) {
  .is-spinning {
    animation: none;
  }
}
</style>
