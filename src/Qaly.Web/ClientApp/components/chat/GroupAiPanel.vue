<script setup lang="ts">
import { computed, ref } from 'vue'
import { ListChecks, Loader2, Sparkles } from 'lucide-vue-next'
import { showError } from '../../composables/use-toast'
import { apiResult, errorMessage } from '../../utils/api-client'

interface GroupAiActionItem {
  title: string
  description: string | null
  suggestedOwnerName: string | null
  dueDateSuggestion: string | null
  confidence: number
  sourceEvidence: string | null
}

interface GroupAiActionItemsResponse {
  groupId: string
  source: string
  items: GroupAiActionItem[]
  warnings: string[]
}

const props = defineProps<{
  groupId: string
}>()

const isLoading = ref(false)
const actionItems = ref<GroupAiActionItem[]>([])
const warnings = ref<string[]>([])

const hasGroup = computed(() => Boolean(props.groupId))

async function extractActionItems() {
  if (!hasGroup.value || isLoading.value) return

  isLoading.value = true
  warnings.value = []

  try {
    const result = await apiResult<GroupAiActionItemsResponse>(`/api/groups/${props.groupId}/ai/action-items`, {
      method: 'POST',
      body: JSON.stringify({
        source: 'chat',
        messageLimit: 80,
      }),
    })

    actionItems.value = result.items
    warnings.value = result.warnings ?? []
  } catch (error) {
    showError(errorMessage(error, 'Không thể trích xuất việc cần làm.'))
  } finally {
    isLoading.value = false
  }
}

function confidenceLabel(value: number) {
  return `${Math.round(value * 100)}%`
}
</script>

<template>
  <aside class="group-ai-panel glass-card">
    <header class="group-ai-panel__header">
      <div>
        <span>AI</span>
        <h2>Việc cần làm</h2>
      </div>
      <button
        class="icon-button icon-button--small"
        type="button"
        aria-label="Trích xuất việc cần làm"
        :disabled="!hasGroup || isLoading"
        @click="extractActionItems"
      >
        <Loader2 v-if="isLoading" :size="16" class="group-ai-panel__spin" />
        <Sparkles v-else :size="16" />
      </button>
    </header>

    <div v-if="warnings.length" class="group-ai-panel__warnings">
      <span v-for="warning in warnings" :key="warning">{{ warning }}</span>
    </div>

    <div v-if="actionItems.length" class="group-ai-panel__list">
      <article v-for="item in actionItems" :key="`${item.title}-${item.confidence}`" class="group-ai-item">
        <div class="group-ai-item__title">
          <ListChecks :size="15" />
          <strong>{{ item.title }}</strong>
        </div>
        <p v-if="item.description">{{ item.description }}</p>
        <div class="group-ai-item__meta">
          <span v-if="item.suggestedOwnerName">{{ item.suggestedOwnerName }}</span>
          <span v-if="item.dueDateSuggestion">{{ new Date(item.dueDateSuggestion).toLocaleDateString('vi') }}</span>
          <span>{{ confidenceLabel(item.confidence) }}</span>
        </div>
        <small v-if="item.sourceEvidence">{{ item.sourceEvidence }}</small>
      </article>
    </div>

    <div v-else class="group-ai-panel__empty">
      <ListChecks :size="22" />
      <span>{{ isLoading ? 'Đang xử lý...' : 'Chưa có việc cần làm' }}</span>
    </div>
  </aside>
</template>

<style scoped>
.group-ai-panel {
  min-height: calc(100dvh - 130px);
  display: grid;
  grid-template-rows: auto auto minmax(0, 1fr);
  gap: 12px;
  padding: 16px;
}

.group-ai-panel__header,
.group-ai-item__title,
.group-ai-item__meta,
.group-ai-panel__empty {
  display: flex;
  align-items: center;
}

.group-ai-panel__header {
  justify-content: space-between;
  gap: 12px;
}

.group-ai-panel__header span {
  color: var(--muted);
  font-weight: 800;
  text-transform: uppercase;
}

.group-ai-panel__warnings {
  display: grid;
  gap: 6px;
}

.group-ai-panel__warnings span {
  border: 1px solid rgba(245, 158, 11, 0.24);
  border-radius: 8px;
  background: rgba(255, 251, 235, 0.92);
  color: #92400e;
  padding: 8px 10px;
  font-size: 0.78rem;
  font-weight: 700;
}

.group-ai-panel__list {
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
  overflow-y: auto;
}

.group-ai-item {
  display: grid;
  gap: 8px;
  border: 1px solid rgba(193, 211, 232, 0.72);
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.82);
  padding: 12px;
}

.group-ai-item__title {
  gap: 7px;
  color: var(--text-strong);
}

.group-ai-item p,
.group-ai-item small {
  margin: 0;
  color: var(--muted);
  line-height: 1.45;
}

.group-ai-item__meta {
  flex-wrap: wrap;
  gap: 6px;
}

.group-ai-item__meta span {
  border-radius: 999px;
  background: rgba(234, 244, 255, 0.86);
  color: var(--primary);
  padding: 5px 8px;
  font-size: 0.74rem;
  font-weight: 800;
}

.group-ai-panel__empty {
  min-height: 220px;
  justify-content: center;
  flex-direction: column;
  gap: 10px;
  color: var(--muted);
  font-weight: 800;
  text-align: center;
}

.group-ai-panel__spin {
  animation: group-ai-spin 0.9s linear infinite;
}

@keyframes group-ai-spin {
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 980px) {
  .group-ai-panel {
    min-height: auto;
  }
}
</style>
