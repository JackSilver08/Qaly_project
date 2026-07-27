<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'
import SidebarNav from './SidebarNav.vue'
import type { ShellNavItem } from './shell-models'

defineProps<{
  navItems: ShellNavItem[]
  notificationCount: number
  userName: string
  userInitials: string
  userRole: string | null
  userAvatarUrl: string | null
  userLoading: boolean
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
</script>

<template>
  <!-- Shared shell adapts the old UI structure: full top header, left nav, single scrolling content panel. -->
  <div class="app-shell" :class="{ 'is-chat-shell': isChatShell }">
    <SidebarNav
      :class="{ 'is-open': sidebarOpen }"
      :items="navItems"
      :user-avatar-url="userAvatarUrl"
      :user-loading="userLoading"
      :user-name="userName"
      :user-role="userRole"
      @navigate="handleNavigate"
    />

    <div v-if="sidebarOpen" class="shell-backdrop" @click="sidebarOpen = false"></div>

    <main class="shell-main no-scrollbar">
      <slot></slot>
    </main>

    <slot name="overlays"></slot>
  </div>
</template>
