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
        <select v-model="selectedUserId">
          <option value="" disabled>Chọn người dùng...</option>
          <option
            v-for="user in users.filter(u => !members.some(m => m.id === u.id))"
            :key="user.id"
            :value="user.id"
          >
            {{ user.fullName }} ({{ user.email }})
          </option>
        </select>
        <select v-model="selectedRole" class="role-picker">
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

    <div class="members-list space-y-3">
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

        <div class="member-role-actions flex items-center space-x-2">
          <!-- Role Selector for Admin -->
          <div v-if="canManageMember(member)" class="role-selector">
            <select :value="member.role" @change="e => onRoleSelectChange(member, (e.target as HTMLSelectElement).value)">
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
            class="text-xs bg-purple-500/10 text-purple-300 hover:bg-purple-500/20 border border-purple-500/30 px-2.5 py-1 rounded-lg flex items-center gap-1 transition ms-2"
            @click="openRoleHistory(member)"
            title="Xem lịch sử thay đổi vai trò qua các giai đoạn (Single Active Role)"
          >
            <History :size="14" />
            <span>Lịch sử Role</span>
          </button>

          <button 
            v-if="canManageMember(member)" 
            class="icon-button icon-button--small" 
            style="color: var(--peach-500); margin-left: 12px;" 
            type="button" 
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
      :active-role-name="pendingRoleChange?.activeRole || 'Member'"
      :active-role-start-date="new Date().toLocaleDateString('vi-VN')"
      :new-role-name="pendingRoleChange?.newRole || ''"
      :new-role-start-date="new Date().toLocaleDateString('vi-VN')"
      @close="showOverlapModal = false"
      @confirm="confirmRoleChangeAfterOverlap"
    />

    <AssignRoleSystemConflictModal
      :show="showConflictModal"
      :member-name="pendingRoleChange?.userName || ''"
      :new-role-name="pendingRoleChange?.newRole || ''"
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
.add-member-form {
  padding: 20px;
  margin-bottom: 24px;
}
.members-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.form-row select {
  flex: 1;
  padding: 8px 12px;
  border-radius: var(--qaly-radius-lg);
  border: 1px solid var(--line);
  color: var(--text-strong);
  background: var(--panel);
}

.role-selector select {
  padding: 4px 8px;
  border-radius: 6px;
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

.member-role-actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: var(--radius-shell);
}
</style>
