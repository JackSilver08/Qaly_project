<script setup lang="ts">
import { computed, ref } from 'vue'
import { Archive, Pencil, RotateCcw, Search, Trash2 } from 'lucide-vue-next'
import type { ConversationHistoryItem } from './types'

const props = defineProps<{ items: ConversationHistoryItem[] }>()
const emit = defineEmits<{
  restore: [item: ConversationHistoryItem]
  rename: [item: ConversationHistoryItem]
  archive: [item: ConversationHistoryItem]
  delete: [item: ConversationHistoryItem]
}>()

const query = ref('')
const filteredItems = computed(() => {
  const value = query.value.trim().toLowerCase()
  if (!value) return props.items
  return props.items.filter(item => [item.title, item.prompt, item.projectLabel, item.assistantSnippet]
    .filter(Boolean).join(' ').toLowerCase().includes(value))
})

function timeLabel(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '' : date.toLocaleString('vi-VN', { dateStyle: 'short', timeStyle: 'short' })
}
</script>

<template>
  <section class="conversation-history-drawer" aria-label="Lịch sử trò chuyện">
    <label class="history-search">
      <Search :size="15" aria-hidden="true" />
      <input v-model="query" type="search" placeholder="Tìm cuộc trò chuyện..." aria-label="Tìm lịch sử" />
    </label>

    <div v-if="filteredItems.length" class="history-list">
      <article v-for="item in filteredItems" :key="item.sessionId" class="history-item">
        <button class="history-main" type="button" @click="emit('restore', item)">
          <strong>{{ item.title }}</strong>
          <span>{{ item.projectLabel }} · {{ timeLabel(item.createdAt) }} · {{ item.turnCount || 0 }} lượt</span>
          <small v-if="item.assistantSnippet">{{ item.assistantSnippet }}</small>
        </button>
        <div class="history-actions">
          <button type="button" title="Mở lại" aria-label="Mở lại cuộc trò chuyện" @click="emit('restore', item)"><RotateCcw :size="14" /></button>
          <button type="button" title="Đổi tên" aria-label="Đổi tên cuộc trò chuyện" @click="emit('rename', item)"><Pencil :size="14" /></button>
          <button v-if="item.status !== 'archived'" type="button" title="Lưu trữ" aria-label="Lưu trữ cuộc trò chuyện" @click="emit('archive', item)"><Archive :size="14" /></button>
          <button type="button" title="Xóa" aria-label="Xóa cuộc trò chuyện" @click="emit('delete', item)"><Trash2 :size="14" /></button>
        </div>
      </article>
    </div>
    <div v-else class="history-empty">
      <strong>Chưa có lịch sử</strong>
      <span>Các cuộc trò chuyện được lưu trên máy chủ và có thể mở lại trên thiết bị khác.</span>
    </div>
  </section>
</template>

<style scoped>
.conversation-history-drawer,.history-list{min-width:0;display:grid;gap:10px}.history-search{height:38px;display:flex;align-items:center;gap:8px;border:1px solid var(--line);border-radius:10px;background:var(--panel);padding:0 10px;color:var(--muted)}.history-search input{min-width:0;width:100%;border:0;outline:0;background:transparent;color:var(--text);font:inherit}.history-item{min-width:0;display:grid;grid-template-columns:minmax(0,1fr) auto;gap:8px;border:1px solid var(--line);border-radius:10px;background:var(--panel-soft);padding:9px}.history-main{min-width:0;display:grid;gap:3px;border:0;background:transparent;padding:0;text-align:left;cursor:pointer}.history-main strong,.history-main span,.history-main small{overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.history-main strong{color:var(--text-strong);font-size:13px}.history-main span,.history-main small,.history-empty span{color:var(--muted);font-size:11px}.history-actions{display:flex;gap:5px;align-items:flex-start}.history-actions button{width:30px;height:30px;display:grid;place-items:center;border:1px solid var(--line);border-radius:8px;background:var(--panel);color:var(--primary);cursor:pointer}.history-actions button:focus-visible{outline:2px solid var(--primary)}.history-empty{display:grid;gap:4px;border:1px dashed var(--line);border-radius:10px;padding:16px;text-align:center}.history-empty strong{color:var(--text-strong)}@media(max-width:520px){.history-item{grid-template-columns:1fr}}
</style>
