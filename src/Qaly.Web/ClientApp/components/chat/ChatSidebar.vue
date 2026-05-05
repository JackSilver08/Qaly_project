<script setup lang="ts">
import { Plus } from 'lucide-vue-next'
import type { ChatGroupModel } from './chat-types'

defineProps<{
  groups: ChatGroupModel[]
  activeGroupId: string
}>()

defineEmits<{
  select: [groupId: string]
  create: []
}>()
</script>

<template>
  <aside class="team-chat-sidebar glass-card">
    <div class="team-chat-sidebar__header">
      <div>
        <span>Groups</span>
        <h2>Nhóm chat</h2>
      </div>
      <button class="icon-button icon-button--small" type="button" aria-label="Tạo nhóm mới" @click="$emit('create')">
        <Plus :size="16" />
      </button>
    </div>

    <button
      v-for="group in groups"
      :key="group.id"
      type="button"
      class="team-chat-group"
      :class="{ 'is-active': group.id === activeGroupId }"
      @click="$emit('select', group.id)"
    >
      <div>
        <strong>{{ group.name }}</strong>
        <span>{{ group.description }}</span>
      </div>
      <small v-if="group.unreadCount > 0">{{ group.unreadCount }}</small>
    </button>
  </aside>
</template>
