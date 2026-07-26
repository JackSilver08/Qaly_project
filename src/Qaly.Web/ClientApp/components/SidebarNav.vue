<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { Box, Settings } from 'lucide-vue-next'
import { useRoute, type NavigationFailure } from 'vue-router'
import type { ShellNavItem } from './shell-models'

const props = defineProps<{
  items: ShellNavItem[]
  userName: string
  userRole: string | null
  userAvatarUrl: string | null
  userLoading: boolean
}>()

const emit = defineEmits<{
  navigate: []
}>()

const route = useRoute()
const defaultAvatarUrl = '/images/avatars/default-avatar.svg'
const avatarLoadFailed = ref(false)

const resolvedUserName = computed(() => props.userName?.trim() || 'Qaly user')
const resolvedRoleLabel = computed(() => formatRoleLabel(props.userRole))
const resolvedAvatarUrl = computed(() =>
  !props.userAvatarUrl || avatarLoadFailed.value ? defaultAvatarUrl : props.userAvatarUrl,
)

watch(
  () => props.userAvatarUrl,
  () => {
    avatarLoadFailed.value = false
  },
)

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

function handleAvatarError() {
  avatarLoadFailed.value = true
}

function formatRoleLabel(role: string | null | undefined) {
  const value = role?.trim()
  if (!value) return 'Member'

  const normalized = value
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/[_-]+/g, ' ')
    .trim()

  return normalized
    .split(/\s+/)
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1).toLowerCase())
    .join(' ')
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

    <section class="sidebar-profile" aria-label="Hồ sơ người dùng">
      <template v-if="userLoading">
        <div class="sidebar-profile__avatar sidebar-profile__avatar--skeleton" aria-hidden="true"></div>
        <div class="sidebar-profile__content sidebar-profile__content--skeleton" aria-hidden="true">
          <span class="sidebar-skeleton sidebar-skeleton--role"></span>
          <span class="sidebar-skeleton sidebar-skeleton--name"></span>
        </div>
      </template>
      <template v-else>
        <div class="sidebar-profile__avatar">
          <img
            :src="resolvedAvatarUrl"
            :alt="resolvedUserName"
            loading="lazy"
            @error="handleAvatarError"
          />
        </div>
        <div class="sidebar-profile__content">
          <span class="sidebar-profile__role">{{ resolvedRoleLabel }}</span>
          <strong class="sidebar-profile__name">{{ resolvedUserName }}</strong>
        </div>
      </template>
    </section>

    <div class="sidebar-divider" aria-hidden="true"></div>

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

<style scoped>
.sidebar-profile {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr);
  align-items: center;
  gap: 12px;
  padding: 14px 12px 12px;
  border: 1px solid var(--line);
  border-radius: var(--radius-panel);
  background: linear-gradient(180deg, rgba(248, 250, 252, 0.92), rgba(255, 255, 255, 0.72));
}

.sidebar-profile__avatar {
  width: 52px;
  height: 52px;
  overflow: hidden;
  border: 1px solid rgba(148, 163, 184, 0.22);
  border-radius: 50%;
  background: var(--qaly-surface-muted);
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.65);
}

.sidebar-profile__avatar img {
  width: 100%;
  height: 100%;
  display: block;
  object-fit: cover;
}

.sidebar-profile__content {
  min-width: 0;
  display: grid;
  gap: 2px;
}

.sidebar-profile__role {
  color: var(--primary);
  font-size: 12px;
  font-weight: 800;
  line-height: 1.2;
  text-transform: none;
}

.sidebar-profile__name {
  overflow: hidden;
  color: var(--text-strong);
  font-size: 14px;
  line-height: 1.35;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sidebar-divider {
  width: 100%;
  height: 1px;
  margin: -2px 0 0;
  background: linear-gradient(90deg, rgba(148, 163, 184, 0), rgba(148, 163, 184, 0.42), rgba(148, 163, 184, 0));
}

.sidebar-profile__avatar--skeleton,
.sidebar-skeleton {
  position: relative;
  overflow: hidden;
  background: linear-gradient(90deg, rgba(226, 232, 240, 0.78) 0%, rgba(241, 245, 249, 1) 50%, rgba(226, 232, 240, 0.78) 100%);
  background-size: 200% 100%;
  animation: sidebarPulse 1.2s ease-in-out infinite;
}

.sidebar-profile__avatar--skeleton {
  border-color: transparent;
}

.sidebar-profile__content--skeleton {
  gap: 6px;
  padding-right: 4px;
}

.sidebar-skeleton {
  height: 12px;
  border-radius: 999px;
}

.sidebar-skeleton--role {
  width: 42px;
}

.sidebar-skeleton--name {
  width: 128px;
  height: 14px;
}

@keyframes sidebarPulse {
  0% {
    background-position: 0 50%;
  }

  100% {
    background-position: 200% 50%;
  }
}

@media (max-width: 768px) {
  .sidebar-profile {
    padding: 12px 10px;
  }

  .sidebar-profile__avatar {
    width: 48px;
    height: 48px;
  }
}

:global(.app-shell.is-chat-shell .sidebar-profile) {
  width: 100%;
  grid-template-columns: 1fr;
  justify-items: center;
  gap: 10px;
  padding: 12px 8px;
}

:global(.app-shell.is-chat-shell .sidebar-profile__content) {
  display: none;
}

:global(.app-shell.is-chat-shell .sidebar-divider) {
  width: calc(100% - 8px);
}
</style>
