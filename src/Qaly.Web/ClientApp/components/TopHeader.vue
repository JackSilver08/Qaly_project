<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { ChevronDown, Eye, LogOut, Menu, Moon, Sparkles, Sun, User, UserRoundSearch, X } from 'lucide-vue-next'
import { useTheme } from '../composables/use-theme'
import { usePermissions } from '../composables/use-permissions'
import HeaderNotificationButton from './HeaderNotificationButton.vue'
import SimulationUserDialog from './SimulationUserDialog.vue'
import type { SimulationUser } from './shell-models'

const props = defineProps<{
  brandName: string
  notificationCount: number
  notificationsOpen: boolean
  userInitials: string
  userName: string
  canStartSimulation: boolean
  simulationUsers: SimulationUser[]
}>()

const emit = defineEmits<{
  toggleSidebar: []
  notifications: []
  assistant: []
  search: []
  logout: []
}>()

const userMenuOpen = ref(false)
const userMenuRef = ref<HTMLElement | null>(null)
const userMenuTrigger = ref<HTMLButtonElement | null>(null)
const simulationPickerOpen = ref(false)
const { currentTheme, themeButtonLabel, toggleTheme } = useTheme()
const { isSimulationActive, simulatedUserName, startSimulation, stopSimulation } = usePermissions()

function closeUserMenu() {
  userMenuOpen.value = false
}

function toggleUserMenu() {
  userMenuOpen.value = !userMenuOpen.value
}

function handleLogout() {
  closeUserMenu()
  if (isSimulationActive.value) stopSimulation()
  emit('logout')
}

function openSimulationPicker() {
  closeUserMenu()
  simulationPickerOpen.value = true
}

async function closeSimulationPicker() {
  simulationPickerOpen.value = false
  await nextTick()
  userMenuTrigger.value?.focus()
}

function selectSimulationUser(user: SimulationUser) {
  startSimulation(user.id, `${user.fullName} (${user.role})`)
  simulationPickerOpen.value = false
  window.location.reload()
}

function exitSimulation() {
  stopSimulation()
  window.location.reload()
}

function handleDocumentPointerDown(event: PointerEvent) {
  const target = event.target as Node | null
  if (target && userMenuRef.value?.contains(target)) return
  closeUserMenu()
}

function handleDocumentKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    closeUserMenu()
  }
}

onMounted(() => {
  document.addEventListener('pointerdown', handleDocumentPointerDown)
  document.addEventListener('keydown', handleDocumentKeydown)
})

onBeforeUnmount(() => {
  document.removeEventListener('pointerdown', handleDocumentPointerDown)
  document.removeEventListener('keydown', handleDocumentKeydown)
})
</script>

<template>
  <header class="shell-header">
    <div class="shell-brand">
      <button class="shell-menu-button" type="button" aria-label="Mo menu" @click="$emit('toggleSidebar')">
        <Menu :size="20" />
      </button>
      <RouterLink class="shell-brand-link" to="/dashboard" aria-label="QALY trang chu">
        <div class="shell-brand-mark" aria-hidden="true">Q</div>
        <strong>{{ brandName }}</strong>
      </RouterLink>
    </div>

    <!-- Search Box (Mockup style) -->
    <button class="header-search" type="button" aria-label="Mo tim kiem" @click="$emit('search')">
      <svg class="search-icon" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>
      <span>Tìm kiếm dự án, nhiệm vụ, hoặc thành viên...</span>
    </button>

    <div class="shell-header-actions">
      <button
        class="shell-ai-action-button"
        type="button"
        aria-label="Mở Trợ lý AI"
        title="Chat, phân tích và soạn hành động có bước duyệt"
        @click="$emit('assistant')"
      >
        <Sparkles :size="16" />
        <span class="shell-ai-action-button__label">Trợ lý AI</span>
        <span class="shell-ai-action-button__model"><i></i> Ưu tiên DeepSeek V4 Pro</span>
      </button>

      <button
        class="shell-icon-button shell-theme-toggle"
        type="button"
        :aria-label="themeButtonLabel"
        :title="themeButtonLabel"
        @click="toggleTheme"
      >
        <Sun v-if="currentTheme === 'dark'" :size="18" />
        <Moon v-else :size="18" />
      </button>

      <div v-if="isSimulationActive" class="simulation-active-chip" role="status">
        <Eye :size="17" aria-hidden="true" />
        <span>
          <small>Đang xem với vai trò</small>
          <strong>{{ simulatedUserName }}</strong>
        </span>
        <button type="button" aria-label="Thoát chế độ xem với vai trò" title="Thoát View-As" @click="exitSimulation">
          <X :size="15" />
        </button>
      </div>

      <HeaderNotificationButton
        :count="notificationCount"
        :open="notificationsOpen"
        @click="$emit('notifications')"
      />

      <div ref="userMenuRef" class="shell-user-dropdown">
        <button
          ref="userMenuTrigger"
          class="shell-user-menu"
          :class="{ 'is-open': userMenuOpen }"
          type="button"
          aria-haspopup="menu"
          :aria-expanded="userMenuOpen"
          @click="toggleUserMenu"
        >
          <span>{{ userInitials }}</span>
          <div class="user-meta-text">
            <strong>{{ userName }}</strong>
            <span class="user-role-label">QUẢN LÝ DỰ ÁN</span>
          </div>
          <ChevronDown :size="16" />
        </button>

        <div v-if="userMenuOpen" class="shell-user-dropdown-menu" role="menu">
          <RouterLink class="shell-user-dropdown-item" to="/profile" role="menuitem" @click="closeUserMenu">
            <User :size="17" />
            <span>Trang cá nhân</span>
          </RouterLink>

          <button
            v-if="props.canStartSimulation && props.simulationUsers.length"
            class="shell-user-dropdown-item shell-user-dropdown-item--simulation"
            type="button"
            role="menuitem"
            @click="openSimulationPicker"
          >
            <UserRoundSearch :size="17" />
            <span class="shell-user-dropdown-item__copy">
              <strong>Xem với vai trò…</strong>
              <small>Kiểm tra quyền ở chế độ chỉ đọc</small>
            </span>
          </button>

          <button
            class="shell-user-dropdown-item shell-user-dropdown-item--danger"
            type="button"
            role="menuitem"
            @click="handleLogout"
          >
            <LogOut :size="17" />
            <span>Đăng xuất</span>
          </button>
        </div>
      </div>
    </div>

    <SimulationUserDialog
      :open="simulationPickerOpen"
      :users="props.simulationUsers"
      @close="closeSimulationPicker"
      @select="selectSimulationUser"
    />
  </header>
</template>

<style scoped>
.shell-ai-action-button {
  min-width: 0;
  height: 38px;
  display: inline-flex;
  align-items: center;
  gap: 7px;
  padding: 0 10px;
  border: 1px solid #bfdbfe;
  border-radius: 11px;
  color: #1d4ed8;
  background: #eff6ff;
  font-weight: 800;
  white-space: nowrap;
}

.shell-ai-action-button:hover,
.shell-ai-action-button:focus-visible {
  border-color: #60a5fa;
  background: #dbeafe;
  outline: none;
}

.shell-ai-action-button__model {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding-left: 7px;
  border-left: 1px solid #bfdbfe;
  color: #475569;
  font-size: 10px;
  font-weight: 700;
}

.shell-ai-action-button__model i {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: #3b82f6;
}

.shell-user-dropdown-menu {
  width: min(286px, calc(100vw - 24px));
}

.shell-user-dropdown-item--simulation {
  min-height: 54px;
  border-color: rgba(59, 130, 246, 0.13);
  background: rgba(239, 246, 255, 0.72);
}

.shell-user-dropdown-item__copy {
  min-width: 0;
  display: grid;
  gap: 2px;
}

.shell-user-dropdown-item__copy strong {
  color: var(--text-strong);
  font-size: 13px;
}

.shell-user-dropdown-item__copy small {
  color: var(--muted);
  font-size: 10px;
  font-weight: 650;
  line-height: 1.35;
}

.simulation-active-chip {
  min-width: 0;
  max-width: 260px;
  height: 40px;
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 8px;
  padding: 0 6px 0 10px;
  border: 1px solid #fbbf24;
  border-radius: 12px;
  color: #92400e;
  background: linear-gradient(135deg, #fffbeb, #fef3c7);
  box-shadow: 0 8px 22px rgba(217, 119, 6, 0.12);
}

.simulation-active-chip > svg { flex: 0 0 auto; color: #d97706; }
.simulation-active-chip > span { min-width: 0; display: grid; }
.simulation-active-chip small { font-size: 9px; font-weight: 800; letter-spacing: .03em; text-transform: uppercase; }
.simulation-active-chip strong { overflow: hidden; font-size: 11px; text-overflow: ellipsis; white-space: nowrap; }
.simulation-active-chip button {
  width: 27px;
  height: 27px;
  display: grid;
  place-items: center;
  border: 0;
  border-radius: 8px;
  color: #92400e;
  background: rgba(245, 158, 11, 0.14);
  cursor: pointer;
  transition: color 200ms ease-out, background 200ms ease-out, transform 200ms ease-out;
}

.simulation-active-chip button:hover,
.simulation-active-chip button:focus-visible {
  transform: translateY(-1px);
  color: #991b1b;
  background: rgba(239, 68, 68, 0.14);
  outline: none;
}

:global(:root[data-theme='dark'] .shell-user-dropdown-item--simulation) {
  border-color: rgba(96, 165, 250, .2);
  background: rgba(30, 64, 175, .16);
}

:global(:root[data-theme='dark'] .simulation-active-chip) {
  border-color: rgba(251, 191, 36, .5);
  color: #fde68a;
  background: linear-gradient(135deg, rgba(120, 53, 15, .5), rgba(69, 26, 3, .55));
}

:global(:root[data-theme='dark'] .shell-ai-action-button) {
  border-color: rgba(96, 165, 250, .38);
  color: #93c5fd;
  background: #172554;
}

:global(:root[data-theme='dark'] .shell-ai-action-button__model) {
  border-color: rgba(96, 165, 250, .28);
  color: #cbd5e1;
}

@media (max-width: 1280px) {
  .shell-ai-action-button__model { display: none; }
  .simulation-active-chip > span { display: none; }
  .simulation-active-chip { width: auto; }
}

@media (max-width: 760px) {
  .shell-ai-action-button { width: 36px; padding: 0; justify-content: center; }
  .shell-ai-action-button__label { display: none; }
  .simulation-active-chip { height: 36px; padding-left: 8px; gap: 5px; }
}

@media (prefers-reduced-motion: reduce) {
  .simulation-active-chip button { transition: none; }
}
</style>
