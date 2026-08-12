<script setup lang="ts">
import { ref } from 'vue'
import { Mail, Shield, Trash2, UserPlus, History, AlertTriangle } from 'lucide-vue-next'
import type { UserDto } from '../types'
import RoleHistoryModal from './RoleHistoryModal.vue'
import AssignRoleOverlapModal from './AssignRoleOverlapModal.vue'
import AssignRoleSystemConflictModal from './AssignRoleSystemConflictModal.vue'

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
}>()

const emit = defineEmits<{
  add: [userId: string]
  remove: [userId: string]
  'update-role': [userId: string, role: string]
  'update-permissions': [userId: string, permissions: Record<string, boolean>]
}>()

const showAddForm = ref(false)
const selectedUserId = ref('')

// Modals State
const showHistoryModal = ref(false)
const selectedMemberForHistory = ref<{ id: string; name: string } | null>(null)

const showOverlapModal = ref(false)
const showConflictModal = ref(false)
const pendingRoleChange = ref<{ userId: string; newRole: string; userName: string; activeRole: string } | null>(null)

const projectCustomRoles = [
  'Product Owner (PO)',
  'Project Manager (PM)',
  'QA Lead / Tester',
  'Dev Backend',
  'Dev Frontend',
  'Member',
  'Viewer'
]

function handleAdd() {
  if (!selectedUserId.value) return
  emit('add', selectedUserId.value)
  selectedUserId.value = ''
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
        <button class="primary-button" type="button" :disabled="!selectedUserId" @click="handleAdd">Thêm</button>
        <button class="text-button" type="button" @click="showAddForm = false">Hủy</button>
      </div>
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
        </div>

        <div class="member-role-actions flex items-center space-x-2">
          <!-- Role Selector for Admin -->
          <div v-if="canManageMember(member)" class="role-selector">
            <select :value="member.role" @change="e => onRoleSelectChange(member, (e.target as HTMLSelectElement).value)">
              <option v-for="role in projectCustomRoles" :key="role" :value="role">{{ role }}</option>
            </select>
          </div>
          <span v-else :class="`role-badge role-badge--${member.role.toLowerCase()}`">
            <Shield :size="14" />
            {{ member.role }}
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
.member-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: var(--radius-shell);
}
</style>
