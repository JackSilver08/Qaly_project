<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  group?: { name?: string; summary?: string }
}>()

const title = computed(() => props.group?.name || 'Chọn nhóm chat')
const summary = computed(() => props.group?.summary || 'Chọn một nhóm để bắt đầu trò chuyện')
</script>

<template>
  <section class="team-chat-window">
    <header class="team-chat-window__header">
      <strong>{{ title }}</strong>
      <span>{{ summary }}</span>
    </header>
    <div class="team-chat-window__empty">
      Khung chat đang được tối giản tạm thời để đảm bảo bản dựng ổn định.
    </div>
  </section>
</template>

<style scoped>
.team-chat-window {
  padding: 16px;
  border-radius: 16px;
  background: #fff;
  border: 1px solid rgba(148, 163, 184, 0.24);
}

.team-chat-window__header {
  display: flex;
  flex-direction: column;
  gap: 4px;
  margin-bottom: 12px;
}

.team-chat-window__header strong {
  font-size: 16px;
  color: #0f172a;
}

.team-chat-window__header span {
  font-size: 13px;
  color: #64748b;
}

.team-chat-window__empty {
  border: 1px dashed rgba(148, 163, 184, 0.35);
  border-radius: 12px;
  padding: 18px;
  color: #64748b;
  background: rgba(248, 250, 252, 0.8);
}
</style>
