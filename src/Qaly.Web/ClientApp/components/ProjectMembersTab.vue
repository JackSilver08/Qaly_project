<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { Mail, Shield, Trash2, UserPlus, History, AlertTriangle } from 'lucide-vue-next'
import type { ProjectPermissionsDto, UserDto } from '../types'
import { groupedProjectRoles, projectRoleHint, projectRoleLabel } from '../utils/project-roles'
import { apiResult } from '../utils/api-client'
import RoleHistoryModal from './RoleHistoryModal.vue'
import AssignRoleOverlapModal from './AssignRoleOverlapModal.vue'
import AssignRoleSystemConflictModal from './AssignRoleSystemConflictModal.vue'
import ProjectRoleCapabilityCard from './ProjectRoleCapabilityCard.vue'
import ProjectRoleManagerPanel from './ProjectRoleManagerPanel.vue'

/** One entry of the role picker, from `/api/projects/{id}/assignable-roles`. */
interface AssignableRole {
  key: string
  displayName: string
  baseRole: string
  isCustom: boolean
  permissionSummary: string
  aiTier: string
  aiTierDescription: string
  skillTags: string[]
}

interface Member {
  id: string
  fullName: string
  role: string
  email: string
  initials: string
  canViewProjectTimeline: boolean
  canViewTaskRisk: boolean
  canNudgeAssignee: boolean
  canViewUnseenTaskSignal: boolean
}

const props = defineProps<{
  projectId: string
  members: Member[]
  users: UserDto[]
  isAdmin: boolean
  permissions?: ProjectPermissionsDto | null
  organizationId?: string | null
}>()

const emit = defineEmits<{
  add: [userId: string, role: string]
  remove: [userId: string]
  'update-role': [userId: string, role: string]
  'update-permissions': [userId: string, permissions: Record<string, boolean>]
}>()

const showAddForm = ref(false)
const selectedUserId = ref('')
const selectedRole = ref('Member')

// Modals State
const showHistoryModal = ref(false)
const selectedMemberForHistory = ref<{ id: string; name: string } | null>(null)

const showOverlapModal = ref(false)
const showConflictModal = ref(false)
const pendingRoleChange = ref<{ userId: string; newRole: string; userName: string; activeRole: string } | null>(null)

/**
 * Roles offered in the picker. Loaded from the server so the organization's own roles appear
 * alongside the built-in ones; falls back to the built-in list if the request fails.
 */
const assignableRoles = ref<AssignableRole[]>([])

async function loadAssignableRoles() {
  if (!props.projectId) return
  try {
    assignableRoles.value = await apiResult<AssignableRole[]>(
      `/api/projects/${props.projectId}/assignable-roles`,
    )
  } catch {
    assignableRoles.value = []
  }
}

watch(() => props.projectId, loadAssignableRoles, { immediate: true })

const roleGroups = computed(() => {
  const custom = assignableRoles.value.filter((role) => role.isCustom)
  const builtIn = groupedProjectRoles()
  if (custom.length === 0) return builtIn

  return [
    ...builtIn,
    {
      group: 'Vai trò riêng của tổ chức',
      options: custom.map((role) => ({
        value: role.key,
        label: role.displayName,
        hint: role.permissionSummary,
        group: 'Vai trò riêng của tổ chức' as const,
      })),
    },
  ]
})

function roleHintFor(roleKey: string) {
  const custom = assignableRoles.value.find((role) => role.isCustom && role.key === roleKey)
  if (custom) return `${custom.permissionSummary} · ${custom.aiTierDescription}`
  return projectRoleHint(roleKey)
}

function roleLabelFor(roleKey: string) {
  const custom = assignableRoles.value.find((role) => role.key === roleKey)
  if (custom?.isCustom) return custom.displayName
  return projectRoleLabel(roleKey)
}

function handleAdd() {
  if (!selectedUserId.value) return
  emit('add', selectedUserId.value, selectedRole.value)
  selectedUserId.value = ''
  selectedRole.value = 'Member'
  showAddForm.value = false
}

function openRoleHistory(member: Member) {
  selectedMemberForHistory.value = { id: member.id, name: member.fullName }
  showHistoryModal.value = true
}

function onRoleSelectChange(member: Member, newRole: string) {
  pendingRoleChange.value = {
    userId: member.id,
    newRole,
    userName: member.fullName,
    activeRole: member.role
  }

  // Luôn cảnh báo Overlap ngày (modal a) để chốt EndDate=null cho active role cũ
  showOverlapModal.value = true
}

function confirmRoleChangeAfterOverlap() {
  showOverlapModal.value = false
  if (pendingRoleChange.value) {
    // Nếu role mới là PO/PM/QA và member có System Role là Restricted User -> Hiện Modal b (System conflict)
    if (pendingRoleChange.value.newRole.includes('PO') || pendingRoleChange.value.newRole.includes('PM')) {
      showConflictModal.value = true
    } else {
      executeRoleChange()
    }
  }
}

function executeRoleChange() {
  showConflictModal.value = false
  if (pendingRoleChange.value) {
    emit('update-role', pendingRoleChange.value.userId, pendingRoleChange.value.newRole)
    pendingRoleChange.value = null
  }
}

function toggleTimelinePermission(member: Member, enabled: boolean) {
  emit('update-permissions', member.id, {
    canViewProjectTimeline: enabled,
    canViewTaskRisk: enabled,
    canNudgeAssignee: enabled,
    canViewUnseenTaskSignal: enabled,
  })
}

function canManageMember(member: Member) {
  return props.isAdmin && member.role !== 'Owner'
}

function canToggleTimeline(member: Member) {
  return member.canViewProjectTimeline && member.canViewTaskRisk
}
</script>

<template>
  <div class="members-tab-content glass-card">
    <div class="panel-heading">
      <div>
        <span>Members & Single Active Roles</span>
        <h2>DANH SÁCH THÀNH VIÊN VÀ QUYỀN HẠN</h2>
      </div>
      <button v-if="isAdmin" class="primary-button primary-button--compact" type="button" @click="showAddForm = !showAddForm">
        <UserPlus :size="16" />
        <span>Thêm thành viên</span>
      </button>
    </div>

    <ProjectRoleCapabilityCard v-if="permissions" :permissions="permissions" />

    <!-- Add Member Form -->
    <div v-if="showAddForm" class="add-member-form glass-card reveal">
      <h3>Thêm thành viên mới</h3>
      <div class="form-row">
        <select v-model="selectedUserId" aria-label="Người dùng cần thêm">
          <option value="" disabled>Chọn người dùng...</option>
          <option
            v-for="user in users.filter(u => !members.some(m => m.id === u.id))"
            :key="user.id"
            :value="user.id"
          >
            {{ user.fullName }} ({{ user.email }})
          </option>
        </select>
        <select v-model="selectedRole" aria-label="Vai trò trong dự án" class="role-picker">
          <optgroup v-for="group in roleGroups" :key="group.group" :label="group.group">
            <option v-for="option in group.options" :key="option.value" :value="option.value">
              {{ option.label }}
            </option>
          </optgroup>
        </select>
        <button class="primary-button" type="button" :disabled="!selectedUserId" @click="handleAdd">Thêm</button>
        <button class="text-button" type="button" @click="showAddForm = false">Hủy</button>
      </div>
      <p class="role-hint">{{ roleHintFor(selectedRole) }}</p>
    </div>

    <div class="members-list">
      <article v-for="member in members" :key="member.id" class="member-item">
        <div class="member-avatar">{{ member.initials }}</div>

        <div class="member-info">
          <strong>{{ member.fullName }}</strong>
          <div class="member-meta">
            <span class="member-email">
              <Mail :size="14" />
              {{ member.email }}
            </span>
          </div>
          <small class="member-role-hint">{{ roleHintFor(member.role) }}</small>
        </div>

        <div class="member-role-actions">
          <!-- Role Selector for Admin -->
          <div v-if="canManageMember(member)" class="role-selector">
            <select :value="member.role" :aria-label="`Vai trò của ${member.fullName}`" @change="e => onRoleSelectChange(member, (e.target as HTMLSelectElement).value)">
              <optgroup v-for="group in roleGroups" :key="group.group" :label="group.group">
                <option v-for="option in group.options" :key="option.value" :value="option.value">
                  {{ option.label }}
                </option>
              </optgroup>
            </select>
          </div>
          <span v-else :class="`role-badge role-badge--${member.role.toLowerCase()}`">
            <Shield :size="14" />
            {{ roleLabelFor(member.role) }}
          </span>

          <!-- Role History Button -->
          <button
            type="button"
            class="member-role-history"
            @click="openRoleHistory(member)"
            title="Xem lịch sử thay đổi vai trò qua các giai đoạn (Single Active Role)"
          >
            <History :size="14" />
            <span>Lịch sử Role</span>
          </button>

          <button 
            v-if="canManageMember(member)" 
            class="icon-button icon-button--small member-remove-button"
            type="button" 
            :aria-label="`Xóa ${member.fullName} khỏi dự án`"
            title="Xóa thành viên khỏi dự án"
            @click="$emit('remove', member.id)"
          >
            <Trash2 :size="16" />
          </button>
        </div>

        <label v-if="canManageMember(member)" class="timeline-permission-toggle">
          <input
            type="checkbox"
            :checked="canToggleTimeline(member)"
            @change="e => toggleTimelinePermission(member, (e.target as HTMLInputElement).checked)"
          />
          <span>Được xem timeline dự án</span>
        </label>
      </article>

      <div v-if="members.length === 0" class="empty-state">
        Không có thành viên nào trong dự án này.
      </div>
    </div>

    <!-- Modals -->
    <RoleHistoryModal
      :show="showHistoryModal"
      :project-id="projectId"
      :member-id="selectedMemberForHistory?.id || ''"
      :member-name="selectedMemberForHistory?.name || ''"
      @close="showHistoryModal = false"
    />

    <AssignRoleOverlapModal
      :show="showOverlapModal"
      :member-name="pendingRoleChange?.userName || ''"
      :active-role-name="roleLabelFor(pendingRoleChange?.activeRole || 'Member')"
      :active-role-start-date="new Date().toLocaleDateString('vi-VN')"
      :new-role-name="roleLabelFor(pendingRoleChange?.newRole || 'Member')"
      :new-role-start-date="new Date().toLocaleDateString('vi-VN')"
      @close="showOverlapModal = false"
      @confirm="confirmRoleChangeAfterOverlap"
    />

    <AssignRoleSystemConflictModal
      :show="showConflictModal"
      :member-name="pendingRoleChange?.userName || ''"
      :new-role-name="roleLabelFor(pendingRoleChange?.newRole || 'Member')"
      system-role-name="User (Restricted AI Tier)"
      system-ai-tier="SummaryOnly"
      @close="showConflictModal = false"
      @confirm="executeRoleChange"
    />

    <ProjectRoleManagerPanel
      :organization-id="organizationId ?? null"
      :can-manage="isAdmin"
      @changed="loadAssignableRoles"
    />
  </div>
</template>

<style scoped>
.members-tab-content {
  min-width: 0;
  padding: 24px;
  color: var(--text-strong);
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: var(--radius-shell);
}

.panel-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
  margin-bottom: 24px;
}

.panel-heading h2 {
  margin: 2px 0 0;
  color: var(--text-strong);
  font-size: 18px;
  font-weight: 800;
  line-height: 1.35;
}

.panel-heading > div > span {
  color: var(--muted);
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.panel-heading .primary-button,
.panel-heading .primary-button span {
  flex: 0 0 auto;
  color: #fff;
  font-size: 13px;
  letter-spacing: normal;
  text-transform: none;
}

.add-member-form {
  padding: 20px;
  margin-bottom: 24px;
  background: var(--bg-soft);
  border: 1px solid var(--line);
  box-shadow: none;
}

.add-member-form h3 {
  margin: 0 0 12px;
  color: var(--text-strong);
  font-size: 15px;
  font-weight: 700;
}

.form-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.members-list {
  display: grid;
  gap: 12px;
  margin-top: 20px;
}

.form-row select {
  min-width: 0;
  flex: 1;
  min-height: 40px;
  padding: 8px 12px;
  border-radius: var(--qaly-radius-lg);
  border: 1px solid var(--line);
  color: var(--text-strong);
  background: var(--panel);
}

.role-selector select {
  min-width: 150px;
  min-height: 36px;
  padding: 6px 10px;
  border-radius: var(--qaly-radius-lg);
  border: 1px solid var(--line);
  font-size: 12px;
  font-weight: 600;
  color: var(--text-strong);
  background: var(--panel);
}

.form-row .role-picker {
  flex: 0 0 190px;
}

.role-hint {
  margin-top: 10px;
  font-size: 12px;
  color: var(--muted);
}

.member-role-hint {
  display: block;
  margin-top: 4px;
  font-size: 12px;
  color: var(--muted);
}

.form-row select option,
.role-selector select option {
  color: var(--text-strong);
  background: var(--panel);
}

.form-row select:focus,
.role-selector select:focus {
  border-color: var(--primary);
  outline: 0;
  box-shadow: 0 0 0 3px var(--primary-soft);
}

.member-item {
  display: grid;
  grid-template-columns: 44px minmax(0, 1fr) auto;
  grid-template-areas:
    "avatar info actions"
    "avatar info timeline";
  align-items: center;
  gap: 8px 16px;
  min-width: 0;
  padding: 16px;
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
  background: var(--bg-soft);
}

.member-avatar {
  grid-area: avatar;
  width: 44px;
  height: 44px;
  display: grid;
  place-items: center;
  align-self: start;
  border-radius: var(--qaly-radius-lg);
  color: #fff;
  background: var(--primary);
  font-size: 15px;
  font-weight: 800;
}

.member-info {
  grid-area: info;
  min-width: 0;
}

.member-info > strong {
  display: block;
  overflow-wrap: anywhere;
  color: var(--text-strong);
  font-size: 15px;
  font-weight: 800;
}

.member-meta {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-top: 3px;
  color: var(--muted);
}

.member-email {
  min-width: 0;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  overflow-wrap: anywhere;
  font-size: 13px;
}

.member-role-actions {
  grid-area: actions;
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
  flex-wrap: wrap;
}

.role-badge {
  min-height: 34px;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 10px;
  border: 1px solid color-mix(in srgb, var(--primary) 28%, var(--line));
  border-radius: var(--qaly-radius-lg);
  color: var(--primary-strong);
  background: var(--primary-soft);
  font-size: 12px;
  font-weight: 700;
  white-space: nowrap;
}

.member-role-history {
  min-height: 34px;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 10px;
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
  color: var(--text-strong);
  background: var(--panel);
  font-size: 12px;
  font-weight: 700;
  transition: border-color 160ms ease, color 160ms ease, background 160ms ease;
}

.member-role-history:hover,
.member-role-history:focus-visible {
  border-color: color-mix(in srgb, var(--primary) 45%, var(--line));
  color: var(--primary-strong);
  background: var(--primary-soft);
  outline: 0;
}

.member-remove-button {
  margin-left: 4px;
  color: var(--danger) !important;
}

.member-remove-button:hover,
.member-remove-button:focus-visible {
  border-color: color-mix(in srgb, var(--danger) 45%, var(--line)) !important;
  color: var(--danger) !important;
  background: var(--danger-soft) !important;
}

.timeline-permission-toggle {
  grid-area: timeline;
  display: inline-flex;
  align-items: center;
  justify-content: flex-end;
  gap: 7px;
  color: var(--muted);
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
}

.timeline-permission-toggle input {
  width: 15px;
  height: 15px;
  margin: 0;
  accent-color: var(--primary);
}

.members-list > .empty-state {
  background: var(--bg-soft);
}

@media (max-width: 900px) {
  .member-item {
    grid-template-columns: 44px minmax(0, 1fr);
    grid-template-areas:
      "avatar info"
      "actions actions"
      "timeline timeline";
  }

  .member-role-actions,
  .timeline-permission-toggle {
    justify-content: flex-start;
  }
}

@media (max-width: 640px) {
  .members-tab-content {
    padding: 16px;
  }

  .panel-heading {
    align-items: stretch;
    flex-direction: column;
  }

  .panel-heading .primary-button {
    align-self: flex-start;
  }

  .form-row {
    align-items: stretch;
    flex-direction: column;
  }

  .form-row .role-picker {
    flex-basis: auto;
  }

  .member-role-actions {
    align-items: stretch;
    flex-direction: column;
  }

  .role-selector select,
  .member-role-history {
    width: 100%;
  }

  .member-remove-button {
    margin-left: 0;
  }
}
</style>
