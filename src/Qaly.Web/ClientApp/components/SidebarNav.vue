<script setup lang="ts">
import { Box, Settings } from 'lucide-vue-next'
import { useRoute, type NavigationFailure } from 'vue-router'
import type { ShellNavItem } from './shell-models'

defineProps<{
  items: ShellNavItem[]
}>()

const emit = defineEmits<{
  navigate: []
}>()

const route = useRoute()

function handleNavigate(
  event: MouseEvent,
  navigate: (event?: MouseEvent) => void | Promise<void | NavigationFailure>,
) {
  void navigate(event)
  emit('navigate')
}

function isItemActive(item: ShellNavItem, isActive: boolean, isExactActive: boolean) {
  if (item.to === '/dashboard') return isExactActive
  if (item.to === '/projects') return isActive && !route.path.startsWith('/projects/archived')

  return isActive
}
</script>

<template>
  <aside class="shell-sidebar no-scrollbar">
    <!-- Brand Header -->
    <div class="sidebar-brand">
      <div class="sidebar-brand-mark">Q</div>
      <div class="sidebar-brand-text">
        <strong>QALY</strong>
        <span>Vận hành Toàn cầu</span>
      </div>
    </div>

    <!-- Nav Items -->
    <nav class="shell-nav" aria-label="Main navigation">
      <RouterLink
        v-for="item in items"
        :key="item.to"
        v-slot="{ href, navigate, isActive, isExactActive }"
        :to="item.to"
        custom
      >
        <a
          :href="href"
          class="shell-nav-item"
          :class="{ 'is-active': isItemActive(item, isActive, isExactActive) }"
          @click="handleNavigate($event, navigate)"
        >
          <component :is="item.icon" :size="20" />
          <span>{{ item.label }}</span>
        </a>
      </RouterLink>
    </nav>

    <!-- Archive & Settings at the bottom -->
    <div class="sidebar-footer">
      <RouterLink v-slot="{ href, navigate, isExactActive }" to="/projects/archived" custom>
        <a
          :href="href"
          class="shell-archive-button"
          :class="{ 'is-active': isExactActive }"
          @click="handleNavigate($event, navigate)"
        >
          <Box :size="19" />
          <span>Dự án đã lưu trữ</span>
        </a>
      </RouterLink>

      <RouterLink v-slot="{ href, navigate, isExactActive }" to="/settings" custom>
        <a
          :href="href"
          class="shell-settings-button"
          :class="{ 'is-active': isExactActive }"
          @click="handleNavigate($event, navigate)"
        >
          <Settings :size="19" />
          <span>Cài đặt</span>
        </a>
      </RouterLink>
    </div>
  </aside>
</template>
