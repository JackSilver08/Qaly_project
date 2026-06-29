<script setup lang="ts">
import { computed, ref } from 'vue'
import { Copy, RotateCcw, Search, Trash2 } from 'lucide-vue-next'
import { showError, showSuccess } from '../../composables/use-toast'
import type { ConversationHistoryItem } from './types'

const props = defineProps<{
  items: ConversationHistoryItem[]
}>()

const emit = defineEmits<{
  restore: [item: ConversationHistoryItem]
  delete: [id: string]
  clear: []
}>()

const query = ref('')

const filteredItems = computed(() => {
  const value = query.value.trim().toLowerCase()
  if (!value) return props.items
  return props.items.filter(item => {
    return [item.prompt, item.projectLabel, item.assistantSnippet]
      .filter(Boolean)
      .join(' ')
      .toLowerCase()
      .includes(value)
  })
})

const groupedItems = computed(() => {
  const today = new Date()
  const startOfToday = new Date(today.getFullYear(), today.getMonth(), today.getDate()).getTime()
  const startOfWeek = startOfToday - 6 * 24 * 60 * 60 * 1000

  const groups = [
    { key: 'today', title: 'Hôm nay', items: [] as ConversationHistoryItem[] },
    { key: 'week', title: 'Tuần này', items: [] as ConversationHistoryItem[] },
    { key: 'older', title: 'Cũ hơn', items: [] as ConversationHistoryItem[] }
  ]

  for (const item of filteredItems.value) {
    const time = new Date(item.createdAt).getTime()
    if (Number.isNaN(time) || time < startOfWeek) {
      groups[2].items.push(item)
    } else if (time >= startOfToday) {
      groups[0].items.push(item)
    } else {
      groups[1].items.push(item)
    }
  }

  return groups.filter(group => group.items.length > 0)
})

function timeLabel(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return date.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
}

async function copyPrompt(item: ConversationHistoryItem) {
  try {
    await navigator.clipboard.writeText(item.prompt)
    showSuccess('Đã sao chép prompt.')
  } catch {
    showError('Không thể sao chép prompt.')
  }
}
</script>

<template>
  <section class="conversation-history-drawer" aria-label="Lịch sử trò chuyện">
    <div class="history-search">
      <Search :size="15" aria-hidden="true" />
      <input v-model="query" type="search" placeholder="Tìm prompt cũ..." aria-label="Tìm lịch sử" />
    </div>

    <div v-if="groupedItems.length" class="history-group-list">
      <section v-for="group in groupedItems" :key="group.key" class="history-group">
        <h3>{{ group.title }}</h3>
        <article v-for="item in group.items" :key="item.id" class="history-item">
          <button class="history-main" type="button" @click="emit('restore', item)">
            <strong>{{ item.prompt }}</strong>
            <span>
              {{ item.projectLabel }} · {{ timeLabel(item.createdAt) }}
              <template v-if="item.attachmentCount"> · {{ item.attachmentCount }} file</template>
            </span>
            <small v-if="item.assistantSnippet">{{ item.assistantSnippet }}</small>
          </button>

          <div class="history-actions">
            <button type="button" title="Khôi phục prompt" aria-label="Khôi phục prompt" @click="emit('restore', item)">
              <RotateCcw :size="14" aria-hidden="true" />
            </button>
            <button type="button" title="Sao chép prompt" aria-label="Sao chép prompt" @click="copyPrompt(item)">
              <Copy :size="14" aria-hidden="true" />
            </button>
            <button type="button" title="Xóa khỏi lịch sử" aria-label="Xóa khỏi lịch sử" @click="emit('delete', item.id)">
              <Trash2 :size="14" aria-hidden="true" />
            </button>
          </div>
        </article>
      </section>
    </div>

    <div v-else class="history-empty">
      <strong>Chưa có lịch sử</strong>
      <span>Những câu hỏi analytics gần đây sẽ được lưu cục bộ trên trình duyệt này.</span>
    </div>

    <footer v-if="items.length" class="history-footer">
      <span>Lưu tối đa 20 prompt gần nhất, không lưu nội dung file đính kèm.</span>
      <button type="button" @click="emit('clear')">Xóa tất cả</button>
    </footer>
  </section>
</template>

<style scoped>
.conversation-history-drawer {
  min-width: 0;
  display: grid;
  gap: 12px;
}

.history-search {
  min-width: 0;
  height: 38px;
  display: flex;
  align-items: center;
  gap: 8px;
  border: 1px solid #dbeafe;
  border-radius: var(--qaly-radius-lg);
  background: #ffffff;
  color: #64748b;
  padding: 0 10px;
}

.history-search input {
  min-width: 0;
  width: 100%;
  border: 0;
  outline: 0;
  background: transparent;
  color: #0f172a;
  font: inherit;
  font-size: 13px;
  font-weight: 650;
}

.history-search input::placeholder {
  color: #94a3b8;
}

.history-group-list,
.history-group {
  min-width: 0;
  display: grid;
  gap: 8px;
}

.history-group h3 {
  margin: 2px 0 0;
  color: #64748b;
  font-size: 11px;
  font-weight: 900;
  text-transform: uppercase;
}

.history-item {
  min-width: 0;
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 8px;
  border: 1px solid #e2e8f0;
  border-radius: var(--qaly-radius-lg);
  background: #f8fafc;
  padding: 9px;
}

.history-main {
  min-width: 0;
  display: grid;
  gap: 3px;
  border: 0;
  background: transparent;
  padding: 0;
  text-align: left;
  cursor: pointer;
}

.history-main strong,
.history-main span,
.history-main small {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.history-main strong {
  color: #0f172a;
  font-size: 13px;
  font-weight: 900;
}

.history-main span,
.history-main small,
.history-footer span,
.history-empty span {
  color: #64748b;
  font-size: 11px;
  line-height: 1.4;
}

.history-actions {
  display: inline-flex;
  align-items: flex-start;
  gap: 5px;
}

.history-actions button,
.history-footer button {
  flex: 0 0 auto;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid #dbeafe;
  border-radius: var(--qaly-radius-lg);
  background: #ffffff;
  color: #1d4ed8;
  cursor: pointer;
}

.history-actions button {
  width: 30px;
  height: 30px;
}

.history-footer button {
  height: 30px;
  padding: 0 9px;
  font-size: 12px;
  font-weight: 850;
}

.history-actions button:hover,
.history-actions button:focus-visible,
.history-footer button:hover,
.history-footer button:focus-visible,
.history-main:hover strong,
.history-main:focus-visible strong {
  color: #0f5fd6;
  outline: none;
}

.history-empty {
  min-width: 0;
  display: grid;
  gap: 4px;
  border: 1px dashed #cbd5e1;
  border-radius: var(--qaly-radius-lg);
  background: #f8fafc;
  padding: 16px;
  text-align: center;
}

.history-empty strong {
  color: #0f172a;
  font-size: 13px;
  font-weight: 900;
}

.history-footer {
  min-width: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  border-top: 1px solid #e2e8f0;
  padding-top: 10px;
}

:global(:root[data-theme='dark']) .history-search,
:global(:root[data-theme='dark']) .history-item,
:global(:root[data-theme='dark']) .history-empty,
:global(:root[data-theme='dark']) .history-actions button,
:global(:root[data-theme='dark']) .history-footer button {
  border-color: var(--line) !important;
  background: var(--panel-soft) !important;
  color: var(--text) !important;
}

:global(:root[data-theme='dark']) .history-main strong,
:global(:root[data-theme='dark']) .history-search input,
:global(:root[data-theme='dark']) .history-empty strong {
  color: var(--text-strong) !important;
}

:global(:root[data-theme='dark']) .history-group h3,
:global(:root[data-theme='dark']) .history-main span,
:global(:root[data-theme='dark']) .history-main small,
:global(:root[data-theme='dark']) .history-footer span,
:global(:root[data-theme='dark']) .history-empty span {
  color: var(--muted) !important;
}

@media (max-width: 520px) {
  .history-item {
    grid-template-columns: 1fr;
  }

  .history-actions {
    justify-content: flex-start;
  }

  .history-footer {
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>
