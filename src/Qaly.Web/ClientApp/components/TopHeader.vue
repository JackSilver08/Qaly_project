<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { Bell, ChevronDown, LogOut, Menu, Moon, Sparkles, Sun, User } from 'lucide-vue-next'
import { useTheme } from '../composables/use-theme'

defineProps<{
  brandName: string
  notificationCount: number
  userInitials: string
  userName: string
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
const { currentTheme, themeButtonLabel, toggleTheme } = useTheme()

function closeUserMenu() {
  userMenuOpen.value = false
}

function toggleUserMenu() {
  userMenuOpen.value = !userMenuOpen.value
}

function handleLogout() {
  closeUserMenu()
  emit('logout')
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

      <button class="shell-icon-button" type="button" aria-label="Thong bao" @click="$emit('notifications')">
        <Bell :size="18" />
        <span v-if="notificationCount > 0" class="shell-action-badge">{{ notificationCount }}</span>
      </button>

      <div ref="userMenuRef" class="shell-user-dropdown">
        <button
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
}

@media (max-width: 760px) {
  .shell-ai-action-button { width: 36px; padding: 0; justify-content: center; }
  .shell-ai-action-button__label { display: none; }
}
</style>
