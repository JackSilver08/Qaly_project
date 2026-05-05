<script setup lang="ts">
import { ref } from 'vue'
import SidebarNav from './SidebarNav.vue'
import TopHeader from './TopHeader.vue'
import type { ShellNavItem } from './shell-models'

defineProps<{
  navItems: ShellNavItem[]
  search: string
  notificationCount: number
  userName: string
  userInitials: string
}>()

const emit = defineEmits<{
  'update:search': [value: string]
  navigate: []
  create: []
  notifications: []
  assistant: []
}>()

const sidebarOpen = ref(false)

function handleNavigate() {
  sidebarOpen.value = false
  emit('navigate')
}
</script>

<template>
  <!-- Shared shell adapts the old UI structure: full top header, left nav, single scrolling content panel. -->
  <div class="app-shell">
    <TopHeader
      brand-name="QALY"
      :search="search"
      :notification-count="notificationCount"
      :user-name="userName"
      :user-initials="userInitials"
      @update:search="$emit('update:search', $event)"
      @toggle-sidebar="sidebarOpen = true"
      @create="$emit('create')"
      @notifications="$emit('notifications')"
      @assistant="$emit('assistant')"
    />

    <SidebarNav
      :class="{ 'is-open': sidebarOpen }"
      :items="navItems"
      @navigate="handleNavigate"
    />

    <div v-if="sidebarOpen" class="shell-backdrop" @click="sidebarOpen = false"></div>

    <main class="shell-main no-scrollbar">
      <slot></slot>
    </main>

    <slot name="overlays"></slot>
  </div>
</template>
