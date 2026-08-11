<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { Mail, Shield, Trash2, UserPlus } from 'lucide-vue-next'
import type { ProjectPermissionsDto, UserDto } from '../types'
import { groupedProjectRoles, projectRoleHint, projectRoleLabel } from '../utils/project-roles'
import { apiResult } from '../utils/api-client'
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
  members: Member[]
  users: UserDto[]
  isAdmin: boolean
  permissions?: ProjectPermissionsDto | null
  projectId?: string | null
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

function handleAdd() {
  if (!selectedUserId.value) return
  emit('add', selectedUserId.value, selectedRole.value)
  selectedUserId.value = ''
  selectedRole.value = 'Member'
  showAddForm.value = false
}

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
        <span>Members</span>
        <h2>DANH SÁCH THÀNH VIÊN</h2>
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
            <select :value="member.role" @change="e => $emit('update-role', member.id, (e.target as HTMLSelectElement).value)">
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
  background: var(--blue-50);
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
}

.add-member-form h3 {
  font-size: 15px;
  margin-bottom: 12px;
  color: var(--text-strong);
  font-weight: 700;
}

.form-row {
  display: flex;
  gap: 12px;
  align-items: center;
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
}

.timeline-permission-toggle {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  font-weight: 700;
  color: var(--muted);
  white-space: nowrap;
}

.timeline-permission-toggle input {
  accent-color: var(--primary);
}
.members-tab-content {
  padding: 24px;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: var(--radius-shell);
}

.members-list {
  display: grid;
  gap: 16px;
  margin-top: 20px;
}

.member-item {
  display: grid;
  grid-template-columns: auto 1fr auto auto;
  align-items: center;
  gap: 16px;
  padding: 16px;
  border-radius: var(--qaly-radius-lg);
  background: var(--bg-soft);
  border: 1px solid var(--line);
}

.member-avatar {
  width: 44px;
  height: 44px;
  border-radius: var(--qaly-radius-lg);
  background: var(--primary);
  color: white;
  display: grid;
  place-items: center;
  font-weight: 700;
  font-size: 16px;
}

.member-info {
  flex: 1;
}

.member-info strong {
  display: block;
  font-size: 16px;
  margin-bottom: 4px;
  color: var(--text-strong);
}

.member-meta {
  display: flex;
  gap: 16px;
  color: var(--muted);
}

.member-email {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
}

.role-badge {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  border-radius: var(--qaly-radius-lg);
  font-size: 12px;
  font-weight: 700;
}

.role-badge--admin, .role-badge--manager {
  background: rgba(31, 128, 255, 0.2);
  color: #d9ebff;
}

.role-badge--member {
  background: rgba(34, 211, 238, 0.2);
  color: #d8f8ff;
}

.panel-heading {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.panel-heading h2 {
  font-size: 18px;
  font-weight: 800;
  color: var(--text-strong);
}

.panel-heading span {
  color: var(--muted);
  font-size: 13px;
  font-weight: 600;
  text-transform: uppercase;
}

.panel-heading .primary-button {
  color: #ffffff;
}

.panel-heading .primary-button span {
  color: #ffffff;
}

.empty-state {
  border: 1px dashed var(--line);
  border-radius: var(--qaly-radius-lg);
  background: var(--bg-soft);
  padding: 16px;
  color: var(--muted);
}
</style>
