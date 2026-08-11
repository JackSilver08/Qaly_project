<script setup lang="ts">
/**
 * Shows the signed-in user which role they hold in this project and exactly what that role may do.
 *
 * Everything rendered here comes from the server's `permissions` payload, so the card is a faithful
 * picture of what the API will actually allow — which is what makes it usable as demo evidence.
 */
import { computed } from 'vue'
import { Bot, Check, Minus, Shield } from 'lucide-vue-next'
import type { ProjectPermissionsDto } from '../types'

const props = defineProps<{
  permissions: ProjectPermissionsDto | null
  compact?: boolean
}>()

interface CapabilityRow {
  label: string
  granted: boolean
}

const capabilities = computed<CapabilityRow[]>(() => {
  const p = props.permissions
  if (!p) return []
  return [
    { label: 'Sửa cấu hình dự án', granted: p.canManageProject },
    { label: 'Thêm / đổi vai trò thành viên', granted: p.canManageMembers },
    { label: 'Giao và sắp xếp mọi task', granted: p.canManageAllTasks },
    { label: 'Tạo task mới', granted: p.canCreateTask },
    { label: 'Cập nhật task của mình', granted: p.canUpdateOwnTasks },
    { label: 'Bình luận', granted: p.canComment },
    { label: 'Chấm công', granted: p.canTrackTime },
    { label: 'Duyệt evidence', granted: p.canReviewEvidence },
    { label: 'Xem wiki nội bộ', granted: p.canReadInternalWiki },
    { label: 'Sửa wiki', granted: p.canWriteWiki },
    { label: 'Quản lý tích hợp (GitHub, webhook)', granted: p.canManageIntegrations },
  ]
})

const grantedCount = computed(() => capabilities.value.filter((item) => item.granted).length)

const aiToneClass = computed(() => `ai-tier ai-tier--${(props.permissions?.aiTier ?? 'None').toLowerCase()}`)
</script>

<template>
  <section v-if="permissions" class="role-capability-card" :class="{ 'is-compact': compact }">
    <header class="role-header">
      <div class="role-identity">
        <span class="role-icon"><Shield :size="16" /></span>
        <div>
          <small>Vai trò của bạn trong dự án</small>
          <strong>{{ permissions.roleLabel }}</strong>
        </div>
      </div>
      <span class="role-count">{{ grantedCount }}/{{ capabilities.length }} quyền</span>
    </header>

    <ul class="capability-list">
      <li v-for="item in capabilities" :key="item.label" :class="{ denied: !item.granted }">
        <Check v-if="item.granted" :size="14" class="icon-allow" />
        <Minus v-else :size="14" class="icon-deny" />
        <span>{{ item.label }}</span>
      </li>
    </ul>

    <footer :class="aiToneClass">
      <Bot :size="15" />
      <span>{{ permissions.aiTierDescription }}</span>
    </footer>
  </section>
</template>

<style scoped>
.role-capability-card {
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
  background: var(--bg-soft);
  padding: 16px;
  display: grid;
  gap: 14px;
}

.role-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.role-identity {
  display: flex;
  align-items: center;
  gap: 10px;
}

.role-icon {
  display: grid;
  place-items: center;
  width: 32px;
  height: 32px;
  border-radius: 10px;
  background: var(--primary);
  color: #fff;
}

.role-identity small {
  display: block;
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--muted);
  font-weight: 700;
}

.role-identity strong {
  font-size: 15px;
  color: var(--text-strong);
}

.role-count {
  font-size: 12px;
  font-weight: 700;
  color: var(--muted);
  white-space: nowrap;
}

.capability-list {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(210px, 1fr));
  gap: 6px 16px;
  list-style: none;
  margin: 0;
  padding: 0;
}

.capability-list li {
  display: flex;
  align-items: center;
  gap: 7px;
  font-size: 13px;
  color: var(--text-strong);
}

.capability-list li.denied {
  color: var(--muted);
  text-decoration: line-through;
  text-decoration-color: var(--line);
}

.icon-allow {
  color: #16a34a;
  flex: 0 0 auto;
}

.icon-deny {
  color: var(--muted);
  flex: 0 0 auto;
}

.ai-tier {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  border-radius: var(--qaly-radius-lg);
  font-size: 12.5px;
  font-weight: 600;
  border: 1px solid var(--line);
  background: var(--panel);
  color: var(--text-strong);
}

.ai-tier--full {
  border-color: rgba(31, 128, 255, 0.45);
  background: rgba(31, 128, 255, 0.12);
}

.ai-tier--specialist {
  border-color: rgba(34, 211, 238, 0.45);
  background: rgba(34, 211, 238, 0.12);
}

.ai-tier--contributor {
  border-color: rgba(148, 163, 184, 0.45);
  background: rgba(148, 163, 184, 0.12);
}

.ai-tier--readonly,
.ai-tier--none {
  color: var(--muted);
}

.is-compact {
  padding: 12px;
  gap: 10px;
}

.is-compact .capability-list {
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
}
</style>
