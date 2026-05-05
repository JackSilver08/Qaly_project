<script setup lang="ts">
import { Bell, ChevronDown, Menu, Plus, Search, X } from 'lucide-vue-next'
import ChatbotAvatar from './ChatbotAvatar.vue'

defineProps<{
  brandName: string
  search: string
  notificationCount: number
  userInitials: string
  userName: string
}>()

defineEmits<{
  'update:search': [value: string]
  toggleSidebar: []
  create: []
  notifications: []
  assistant: []
}>()
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

    <label class="shell-search" aria-label="Tim du an, cong viec, thanh vien">
      <Search :size="18" />
      <input
        :value="search"
        type="search"
        placeholder="Search projects, tasks, members..."
        @input="$emit('update:search', ($event.target as HTMLInputElement).value)"
      />
      <button v-if="search" type="button" aria-label="Xoa tim kiem" @click="$emit('update:search', '')">
        <X :size="15" />
      </button>
    </label>

    <div class="shell-header-actions">
      <button class="shell-icon-button" type="button" aria-label="Thong bao" @click="$emit('notifications')">
        <Bell :size="18" />
        <span v-if="notificationCount > 0" class="shell-action-badge">{{ notificationCount }}</span>
      </button>

      <button class="shell-icon-button" type="button" aria-label="Tro ly Qaly" @click="$emit('assistant')">
        <ChatbotAvatar size="launcher" />
      </button>

      <button class="shell-create-button" type="button" @click="$emit('create')">
        <Plus :size="18" />
        <span>Create</span>
      </button>

      <button class="shell-user-menu" type="button">
        <span>{{ userInitials }}</span>
        <strong>{{ userName }}</strong>
        <ChevronDown :size="16" />
      </button>
    </div>
  </header>
</template>
