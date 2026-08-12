<script setup lang="ts">
import { computed, ref } from 'vue'
import { Archive, MessageSquarePlus, Pencil, RotateCcw, Search, Trash2 } from 'lucide-vue-next'
import type { ConversationHistoryItem } from './types'

const props = withDefaults(defineProps<{
  items: ConversationHistoryItem[]
  currentSessionId?: string | null
  loading?: boolean
}>(), {
  currentSessionId: null,
  loading: false,
})
const emit = defineEmits<{
  create: []
  restore: [item: ConversationHistoryItem]
  rename: [item: ConversationHistoryItem]
  archive: [item: ConversationHistoryItem]
  delete: [item: ConversationHistoryItem]
}>()

const query = ref('')
const filter = ref<'active' | 'archived' | 'all'>('active')
const activeCount = computed(() => props.items.filter(item => item.status !== 'archived').length)
const archivedCount = computed(() => props.items.filter(item => item.status === 'archived').length)
const filteredItems = computed(() => {
  const value = query.value.trim().toLowerCase()
  return props.items
    .filter(item => filter.value === 'all'
      || (filter.value === 'archived' ? item.status === 'archived' : item.status !== 'archived'))
    .filter(item => !value || [item.title, item.prompt, item.projectLabel, item.assistantSnippet]
    .filter(Boolean).join(' ').toLowerCase().includes(value))
})

function timeLabel(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '' : date.toLocaleString('vi-VN', { dateStyle: 'short', timeStyle: 'short' })
}
</script>

<template>
  <section class="conversation-history-drawer" aria-label="Lịch sử phiên Trợ lý AI">
    <header class="history-header">
      <div>
        <strong>Lịch sử phiên Trợ lý AI</strong>
        <span>{{ items.length }} phiên được lưu trên máy chủ</span>
      </div>
      <button type="button" class="history-create" data-testid="assistant-session-create" @click="emit('create')">
        <MessageSquarePlus :size="15" aria-hidden="true" /> Phiên mới
      </button>
    </header>

    <label class="history-search">
      <Search :size="15" aria-hidden="true" />
      <input v-model="query" type="search" placeholder="Tìm theo tên, dự án hoặc nội dung..." aria-label="Tìm phiên Trợ lý AI" />
    </label>

    <div class="history-filters" role="tablist" aria-label="Lọc phiên Trợ lý AI">
      <button type="button" role="tab" :aria-selected="filter === 'active'" :class="{ active: filter === 'active' }" @click="filter = 'active'">Đang dùng {{ activeCount }}</button>
      <button type="button" role="tab" :aria-selected="filter === 'archived'" :class="{ active: filter === 'archived' }" @click="filter = 'archived'">Đã lưu trữ {{ archivedCount }}</button>
      <button type="button" role="tab" :aria-selected="filter === 'all'" :class="{ active: filter === 'all' }" @click="filter = 'all'">Tất cả</button>
    </div>

    <div v-if="loading" class="history-empty" aria-live="polite">
      <strong>Đang tải các phiên…</strong>
    </div>
    <div v-else-if="filteredItems.length" class="history-list">
      <article
        v-for="item in filteredItems"
        :key="item.sessionId"
        class="history-item"
        :class="{ 'is-current': item.sessionId === currentSessionId }"
        :data-session-id="item.sessionId"
      >
        <button class="history-main" type="button" @click="emit('restore', item)">
          <span class="history-title-row">
            <strong>{{ item.title }}</strong>
            <em v-if="item.sessionId === currentSessionId">Đang mở</em>
          </span>
          <span>{{ item.projectLabel }} · Cập nhật {{ timeLabel(item.updatedAt || item.createdAt) }} · {{ item.turnCount || 0 }} lượt</span>
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
      <strong>{{ query ? 'Không tìm thấy phiên phù hợp' : filter === 'archived' ? 'Chưa có phiên lưu trữ' : 'Chưa có phiên nào' }}</strong>
      <span>Mỗi phiên giữ context, câu trả lời và các lượt AI riêng; bạn có thể mở lại sau khi reload.</span>
    </div>
  </section>
</template>

<style scoped>
.conversation-history-drawer,.history-list{min-width:0;display:grid;gap:10px}.history-header{display:flex;align-items:center;justify-content:space-between;gap:12px}.history-header>div{min-width:0;display:grid;gap:2px}.history-header strong{color:var(--text-strong);font-size:14px}.history-header span{color:var(--muted);font-size:11px}.history-create{display:inline-flex;align-items:center;gap:6px;flex:0 0 auto;border:1px solid color-mix(in srgb,var(--primary) 38%,var(--line));border-radius:9px;background:color-mix(in srgb,var(--primary) 9%,var(--panel));color:var(--primary);padding:8px 10px;font:inherit;font-size:12px;font-weight:700;cursor:pointer}.history-search{height:38px;display:flex;align-items:center;gap:8px;border:1px solid var(--line);border-radius:10px;background:var(--panel);padding:0 10px;color:var(--muted)}.history-search input{min-width:0;width:100%;border:0;outline:0;background:transparent;color:var(--text);font:inherit}.history-filters{display:flex;gap:6px;overflow-x:auto}.history-filters button{border:1px solid var(--line);border-radius:999px;background:var(--panel);color:var(--muted);padding:6px 9px;font:inherit;font-size:11px;white-space:nowrap;cursor:pointer}.history-filters button.active{border-color:color-mix(in srgb,var(--primary) 42%,var(--line));background:color-mix(in srgb,var(--primary) 9%,var(--panel));color:var(--primary);font-weight:700}.history-item{min-width:0;display:grid;grid-template-columns:minmax(0,1fr) auto;gap:8px;border:1px solid var(--line);border-radius:10px;background:var(--panel-soft);padding:9px}.history-item.is-current{border-color:color-mix(in srgb,var(--primary) 45%,var(--line));box-shadow:0 0 0 1px color-mix(in srgb,var(--primary) 10%,transparent)}.history-main{min-width:0;display:grid;gap:3px;border:0;background:transparent;padding:0;text-align:left;cursor:pointer}.history-title-row{display:flex;align-items:center;gap:7px}.history-title-row strong{min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.history-title-row em{flex:0 0 auto;border-radius:999px;background:color-mix(in srgb,var(--primary) 12%,var(--panel));color:var(--primary);padding:2px 6px;font-size:10px;font-style:normal;font-weight:700}.history-main>span:not(.history-title-row),.history-main small{overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.history-main strong{color:var(--text-strong);font-size:13px}.history-main>span,.history-main small,.history-empty span{color:var(--muted);font-size:11px}.history-actions{display:flex;gap:5px;align-items:flex-start}.history-actions button{width:30px;height:30px;display:grid;place-items:center;border:1px solid var(--line);border-radius:8px;background:var(--panel);color:var(--primary);cursor:pointer}.history-actions button:focus-visible,.history-create:focus-visible,.history-filters button:focus-visible{outline:2px solid var(--primary)}.history-empty{display:grid;gap:4px;border:1px dashed var(--line);border-radius:10px;padding:16px;text-align:center}.history-empty strong{color:var(--text-strong)}@media(max-width:520px){.history-header{align-items:flex-start}.history-item{grid-template-columns:1fr}}
</style>
