<script setup lang="ts">
import { Plus } from "lucide-vue-next";
import type { ChatGroupModel } from "./chat-types";

defineProps<{
  groups: ChatGroupModel[];
  activeGroupId: string;
}>();

defineEmits<{
  select: [groupId: string];
  create: [];
}>();

function initials(name: string) {
  return name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");
}
</script>

<template>
  <aside class="team-chat-sidebar glass-card">
    <div class="team-chat-sidebar__header">
      <div>
        <span>Nhóm</span>
        <h2>Nhóm chat</h2>
      </div>
      <button
        class="icon-button icon-button--small"
        type="button"
        aria-label="Tạo nhóm"
        @click="$emit('create')"
      >
        <Plus :size="16" />
      </button>
    </div>

    <button
      v-for="group in groups"
      :key="group.id"
      type="button"
      class="team-chat-group"
      :data-group-id="group.id"
      :class="{ 'is-active': group.id === activeGroupId }"
      @click="$emit('select', group.id)"
    >
      <span class="team-chat-group__avatar">{{ initials(group.name) }}</span>
      <div>
        <strong>{{ group.name }}</strong>
        <span>{{ group.description }}</span>
      </div>
      <small v-if="group.unreadCount > 0">{{ group.unreadCount }}</small>
    </button>

    <div v-if="!groups.length" class="team-chat-sidebar__empty">
      <strong>Chưa có nhóm chat</strong>
      <span>Tạo nhóm đầu tiên để bắt đầu trao đổi.</span>
    </div>
  </aside>
</template>
