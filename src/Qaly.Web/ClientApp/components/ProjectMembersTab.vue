<script setup lang="ts">
import { ref } from 'vue'
import { Mail, Shield, Trash2, UserPlus } from 'lucide-vue-next'
import type { UserDto } from '../types'

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
}>()

const emit = defineEmits<{
  add: [userId: string]
  remove: [userId: string]
  'update-role': [userId: string, role: string]
  'update-permissions': [userId: string, permissions: Record<string, boolean>]
}>()

const showAddForm = ref(false)
const selectedUserId = ref('')

function handleAdd() {
  if (!selectedUserId.value) return
  emit('add', selectedUserId.value)
  selectedUserId.value = ''
  showAddForm.value = false
}

const roles = ['Manager', 'Member', 'Viewer']

function toggleTimelinePermission(member: Member, enabled: boolean) {
  emit('update-permissions', member.id, {
    canViewProjectTimeline: enabled,
    canViewTaskRisk: enabled,
    canNudgeAssignee: enabled,
    canViewUnseenTaskSignal: enabled,
  })
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
        </div>

        <div class="member-role-actions">
          <!-- Role Selector for Admin -->
          <div v-if="isAdmin && member.role !== 'Owner'" class="role-selector">
            <select :value="member.role" @change="e => $emit('update-role', member.id, (e.target as HTMLSelectElement).value)">
              <option v-for="role in roles" :key="role" :value="role">{{ role }}</option>
            </select>
          </div>
          <span v-else :class="`role-badge role-badge--${member.role.toLowerCase()}`">
            <Shield :size="14" />
            {{ member.role }}
          </span>

          <button 
            v-if="isAdmin && member.role !== 'Owner'" 
            class="icon-button icon-button--small" 
            style="color: var(--peach-500); margin-left: 12px;" 
            type="button" 
            @click="$emit('remove', member.id)"
          >
            <Trash2 :size="16" />
          </button>
        </div>

        <label v-if="isAdmin && member.role !== 'Owner'" class="timeline-permission-toggle">
          <input
            type="checkbox"
            :checked="member.canViewProjectTimeline && member.canViewTaskRisk"
            @change="e => toggleTimelinePermission(member, (e.target as HTMLInputElement).checked)"
          />
          <span>Được xem timeline dự án</span>
        </label>
      </article>

      <div v-if="members.length === 0" class="empty-state">
        Không có thành viên nào trong dự án này.
      </div>
    </div>
  </div>
</template>

<style scoped>
.add-member-form {
  padding: 20px;
  margin-bottom: 24px;
  background: var(--primary-soft);
  border: 1px solid var(--blue-100);
  border-radius: 12px;
}

.add-member-form h3 {
  font-size: 15px;
  margin-bottom: 12px;
  color: var(--primary);
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
  border-radius: 8px;
  border: 1px solid var(--blue-100);
  background: white;
}

.role-selector select {
  padding: 4px 8px;
  border-radius: 6px;
  border: 1px solid var(--line);
  font-size: 12px;
  font-weight: 600;
  color: var(--text);
  background: white;
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
  background: white;
  border: 1px solid var(--glass-border);
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
  border-radius: 12px;
  background: var(--surface-milk);
  border: 1px solid var(--line);
}

.member-avatar {
  width: 44px;
  height: 44px;
  border-radius: 12px;
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
  color: var(--text);
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
  border-radius: 20px;
  font-size: 12px;
  font-weight: 700;
}

.role-badge--admin, .role-badge--manager {
  background: var(--primary-soft);
  color: var(--primary);
}

.role-badge--member {
  background: var(--mint-100);
  color: #047857;
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
  color: var(--text);
}

.panel-heading span {
  color: var(--muted);
  font-size: 13px;
  font-weight: 600;
  text-transform: uppercase;
}
</style>
