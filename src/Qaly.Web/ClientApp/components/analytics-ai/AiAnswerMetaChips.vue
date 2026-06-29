<script setup lang="ts">
defineProps<{
  latencyMs?: number
  confidence?: number
  confidenceReason?: string | null
  freshness?: string | null
  sources?: string[]
  usedAi?: boolean
  modelLabel?: string | null
}>()

const emit = defineEmits<{
  'open-sources': []
}>()

function confidenceLabel(value?: number) {
  if (value === undefined || value === null) return ''
  return `${Math.round(value * 100)}%`
}
</script>

<template>
  <div class="ai-answer-meta-chips" aria-label="Metadata phản hồi">
    <span v-if="latencyMs !== undefined" class="ai-answer-meta-chip">
      <small>Phản hồi</small>
      <strong>{{ latencyMs }}ms</strong>
    </span>

    <span v-if="confidence !== undefined" class="ai-answer-meta-chip" :title="confidenceReason || undefined">
      <small>Độ tin cậy</small>
      <strong>{{ confidenceLabel(confidence) }}</strong>
    </span>

    <button
      v-if="sources?.length"
      class="ai-answer-meta-chip ai-answer-meta-chip-btn"
      type="button"
      aria-label="Mở nguồn tham chiếu"
      @click="emit('open-sources')"
    >
      <small>Nguồn</small>
      <strong>{{ sources.length }}</strong>
    </button>

    <span v-if="freshness" class="ai-answer-meta-chip">
      <small>Dữ liệu</small>
      <strong>{{ freshness }}</strong>
    </span>

    <span v-if="usedAi !== undefined" class="ai-answer-meta-chip">
      <small>Mode</small>
      <strong>{{ usedAi ? 'AI' : 'Hệ thống' }}</strong>
    </span>

    <span v-if="modelLabel" class="ai-answer-meta-chip">
      <small>Model</small>
      <strong>{{ modelLabel }}</strong>
    </span>
  </div>
</template>

<style scoped>
.ai-answer-meta-chips {
  min-width: 0;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
}

.ai-answer-meta-chip {
  min-width: 0;
  display: inline-flex;
  align-items: center;
  gap: 5px;
  border: 1px solid #e2e8f0;
  border-radius: 999px;
  background: #ffffff;
  color: #475569;
  padding: 5px 8px;
  font-size: 11px;
  font-weight: 800;
}

.ai-answer-meta-chip small {
  color: #94a3b8;
  font-size: 10px;
  font-weight: 900;
  text-transform: uppercase;
}

.ai-answer-meta-chip strong {
  min-width: 0;
  overflow: hidden;
  color: #334155;
  font-size: 11px;
  font-weight: 900;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ai-answer-meta-chip-btn {
  cursor: pointer;
}

.ai-answer-meta-chip-btn:hover,
.ai-answer-meta-chip-btn:focus-visible {
  border-color: #bfdbfe;
  background: #eff6ff;
  outline: none;
}

:global(:root[data-theme='dark']) .ai-answer-meta-chip {
  border-color: var(--line) !important;
  background: var(--panel-soft) !important;
  color: var(--text) !important;
}

:global(:root[data-theme='dark']) .ai-answer-meta-chip small {
  color: var(--muted) !important;
}

:global(:root[data-theme='dark']) .ai-answer-meta-chip strong {
  color: var(--text-strong) !important;
}
</style>
