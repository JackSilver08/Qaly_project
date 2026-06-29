<script setup lang="ts">
import { computed } from 'vue'
import { Copy, ExternalLink, MessageSquareText } from 'lucide-vue-next'
import { showError, showSuccess } from '../../composables/use-toast'
import type { SourceRef } from './types'

const props = withDefaults(defineProps<{
  sources?: string[]
  sourceRefs?: SourceRef[] | null
}>(), {
  sources: () => []
})

const emit = defineEmits<{
  'ask-source': [label: string]
}>()

const structuredSources = computed(() => props.sourceRefs?.filter(source => source?.label) ?? [])
const fallbackSources = computed(() => props.sources.filter(Boolean))
const hasStructuredSources = computed(() => structuredSources.value.length > 0)
const hasAnySources = computed(() => hasStructuredSources.value || fallbackSources.value.length > 0)

async function copySources() {
  const labels = hasStructuredSources.value
    ? structuredSources.value.map(source => source.label)
    : fallbackSources.value

  if (!labels.length) return
  try {
    await navigator.clipboard.writeText(labels.join('\n'))
    showSuccess('Đã sao chép nguồn tham chiếu.')
  } catch {
    showError('Không thể sao chép nguồn tham chiếu.')
  }
}

function confidencePercent(value?: number | null) {
  if (value === undefined || value === null) return ''
  return `${Math.round(value * 100)}%`
}
</script>

<template>
  <section class="source-refs-drawer" aria-label="Nguồn tham chiếu">
    <div class="source-refs-toolbar">
      <p v-if="!hasStructuredSources && fallbackSources.length">
        Nguồn hiện là nhãn hệ thống, chưa có link chi tiết.
      </p>
      <p v-else-if="hasStructuredSources">
        Các nguồn có cấu trúc sẽ mở được link khi backend cung cấp URL.
      </p>
      <p v-else>
        Chưa có nguồn tham chiếu cho phản hồi gần nhất.
      </p>

      <button
        v-if="hasAnySources"
        class="source-copy-btn"
        type="button"
        aria-label="Sao chép danh sách nguồn"
        @click="copySources"
      >
        <Copy :size="14" aria-hidden="true" />
        <span>Copy</span>
      </button>
    </div>

    <div v-if="hasStructuredSources" class="source-ref-list">
      <article v-for="source in structuredSources" :key="`${source.type || 'source'}-${source.id || source.label}`" class="source-ref-item">
        <div class="source-ref-main">
          <span class="source-ref-type">{{ source.type || 'Source' }}</span>
          <strong>{{ source.label }}</strong>
          <p v-if="source.evidence">{{ source.evidence }}</p>
          <small v-if="source.timestamp || source.confidence !== undefined">
            {{ source.timestamp || 'Không có thời gian' }}
            <template v-if="confidencePercent(source.confidence)"> · {{ confidencePercent(source.confidence) }}</template>
          </small>
        </div>

        <div class="source-ref-actions">
          <a v-if="source.url" class="source-ref-action" :href="source.url" target="_blank" rel="noreferrer">
            <ExternalLink :size="14" aria-hidden="true" />
            <span>Mở</span>
          </a>
          <button class="source-ref-action" type="button" @click="emit('ask-source', source.label)">
            <MessageSquareText :size="14" aria-hidden="true" />
            <span>Hỏi</span>
          </button>
        </div>
      </article>
    </div>

    <div v-else-if="fallbackSources.length" class="source-label-list">
      <button
        v-for="source in fallbackSources"
        :key="source"
        class="source-label-card"
        type="button"
        @click="emit('ask-source', source)"
      >
        <span>Nguồn hệ thống</span>
        <strong>{{ source }}</strong>
      </button>
    </div>

    <div v-else class="source-empty">
      <strong>Chưa có nguồn</strong>
      <span>Hãy hỏi Erumi một câu có dữ liệu dự án để xem nguồn tại đây.</span>
    </div>
  </section>
</template>

<style scoped>
.source-refs-drawer {
  min-width: 0;
  display: grid;
  gap: 12px;
}

.source-refs-toolbar {
  min-width: 0;
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 10px;
}

.source-refs-toolbar p {
  min-width: 0;
  margin: 0;
  color: #64748b;
  font-size: 12px;
  line-height: 1.45;
}

.source-copy-btn,
.source-ref-action {
  flex: 0 0 auto;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  border: 1px solid #dbeafe;
  border-radius: var(--qaly-radius-lg);
  background: #ffffff;
  color: #1d4ed8;
  padding: 7px 9px;
  font-size: 12px;
  font-weight: 800;
  text-decoration: none;
  cursor: pointer;
}

.source-ref-list,
.source-label-list {
  min-width: 0;
  display: grid;
  gap: 8px;
}

.source-ref-item,
.source-label-card,
.source-empty {
  min-width: 0;
  border: 1px solid #e2e8f0;
  border-radius: var(--qaly-radius-lg);
  background: #f8fafc;
  padding: 12px;
}

.source-ref-item {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 10px;
}

.source-ref-main {
  min-width: 0;
  display: grid;
  gap: 4px;
}

.source-ref-type {
  width: max-content;
  border-radius: 999px;
  background: #eef4ff;
  color: #1d4ed8;
  padding: 2px 7px;
  font-size: 10px;
  font-weight: 900;
  text-transform: uppercase;
}

.source-ref-main strong,
.source-label-card strong,
.source-empty strong {
  min-width: 0;
  overflow: hidden;
  color: #0f172a;
  font-size: 13px;
  font-weight: 900;
  text-overflow: ellipsis;
}

.source-ref-main p,
.source-ref-main small,
.source-label-card span,
.source-empty span {
  min-width: 0;
  margin: 0;
  color: #64748b;
  font-size: 12px;
  line-height: 1.42;
}

.source-ref-actions {
  display: flex;
  align-items: flex-start;
  gap: 6px;
}

.source-label-card {
  display: grid;
  gap: 4px;
  width: 100%;
  text-align: left;
  cursor: pointer;
}

.source-label-card:hover,
.source-label-card:focus-visible,
.source-copy-btn:hover,
.source-copy-btn:focus-visible,
.source-ref-action:hover,
.source-ref-action:focus-visible {
  border-color: #bfdbfe;
  background: #eff6ff;
  outline: none;
}

.source-empty {
  display: grid;
  gap: 4px;
  text-align: center;
}

:global(:root[data-theme='dark']) .source-refs-toolbar p,
:global(:root[data-theme='dark']) .source-ref-main p,
:global(:root[data-theme='dark']) .source-ref-main small,
:global(:root[data-theme='dark']) .source-label-card span,
:global(:root[data-theme='dark']) .source-empty span {
  color: var(--muted) !important;
}

:global(:root[data-theme='dark']) .source-copy-btn,
:global(:root[data-theme='dark']) .source-ref-action,
:global(:root[data-theme='dark']) .source-ref-item,
:global(:root[data-theme='dark']) .source-label-card,
:global(:root[data-theme='dark']) .source-empty {
  border-color: var(--line) !important;
  background: var(--panel-soft) !important;
  color: var(--text) !important;
}

:global(:root[data-theme='dark']) .source-ref-main strong,
:global(:root[data-theme='dark']) .source-label-card strong,
:global(:root[data-theme='dark']) .source-empty strong {
  color: var(--text-strong) !important;
}

@media (max-width: 520px) {
  .source-ref-item {
    grid-template-columns: 1fr;
  }
}
</style>
