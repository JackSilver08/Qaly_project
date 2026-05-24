<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { Bell, ChevronDown, LogOut, Menu, User } from 'lucide-vue-next'

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
  logout: []
}>()

const userMenuOpen = ref(false)
const userMenuRef = ref<HTMLElement | null>(null)

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
    <div class="header-search">
      <svg class="search-icon" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>
      <input type="text" placeholder="Tìm kiếm dự án, nhiệm vụ, hoặc thành viên..." aria-label="Search" />
    </div>

    <div class="shell-header-actions">
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
