<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import SidebarNav from './SidebarNav.vue'
import TopHeader from './TopHeader.vue'
import type { ShellNavItem, SimulationUser } from './shell-models'

defineProps<{
  navItems: ShellNavItem[]
  notificationCount: number
  notificationsOpen: boolean
  userName: string
  userInitials: string
  userRole: string | null
  userAvatarUrl: string | null
  userLoading: boolean
  canAccessArchivedProjects: boolean
  canAccessSettings: boolean
  canStartSimulation: boolean
  simulationUsers: SimulationUser[]
}>()

const emit = defineEmits<{
  navigate: []
  notifications: []
  assistant: []
  search: []
  logout: []
}>()

const sidebarOpen = ref(false)
const route = useRoute()
const isChatShell = computed(() => route.path.startsWith('/groups'))

function handleNavigate() {
  sidebarOpen.value = false
  emit('navigate')
}

function closeSidebar() {
  sidebarOpen.value = false
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    closeSidebar()
  }
}

watch(
  () => route.fullPath,
  () => {
    closeSidebar()
  },
)

if (typeof window !== 'undefined') {
  window.addEventListener('keydown', handleKeydown)
}

onBeforeUnmount(() => {
  if (typeof window !== 'undefined') {
    window.removeEventListener('keydown', handleKeydown)
  }
})
</script>

<template>
  <!-- Shared shell adapts the old UI structure: full top header, left nav, single scrolling content panel. -->
  <div class="app-shell" :class="{ 'is-chat-shell': isChatShell }">
    <TopHeader
      brand-name="QALY"
      :notification-count="notificationCount"
      :notifications-open="notificationsOpen"
      :user-name="userName"
      :user-initials="userInitials"
      :can-start-simulation="canStartSimulation"
      :simulation-users="simulationUsers"
      @toggle-sidebar="sidebarOpen = !sidebarOpen"
      @notifications="$emit('notifications')"
      @assistant="$emit('assistant')"
      @search="$emit('search')"
      @logout="$emit('logout')"
    />

    <SidebarNav
      :class="{ 'is-open': sidebarOpen }"
      :items="navItems"
      :user-avatar-url="userAvatarUrl"
      :user-loading="userLoading"
      :user-name="userName"
      :user-role="userRole"
      :can-access-archived-projects="canAccessArchivedProjects"
      :can-access-settings="canAccessSettings"
      @navigate="handleNavigate"
    />

    <div
      v-if="sidebarOpen"
      class="shell-backdrop"
      role="presentation"
      aria-hidden="true"
      @click="closeSidebar"
    ></div>

    <main class="shell-main no-scrollbar">
      <slot></slot>
    </main>

    <slot name="overlays"></slot>
  </div>
</template>
