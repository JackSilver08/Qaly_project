<script setup lang="ts">
import { onBeforeUnmount, onErrorCaptured, onMounted, ref } from 'vue'
import ErumiChatPanel from '../components/chat/ErumiChatPanel.vue'

type ComposeActionPayload = { message: string; projectId: string; providerHint: string; modelProfile: string }

function openActionComposer(payload: ComposeActionPayload) {
  window.dispatchEvent(new CustomEvent('qaly:open-ai-action', { detail: payload }))
}

const externalPrompt = ref('')
const externalPromptToken = ref(0)
const externalHistoryToken = ref(0)
const externalProjectId = ref<string | null>(null)
const assistantRenderError = ref<string | null>(null)
const assistantRenderKey = ref(0)

onErrorCaptured((error) => {
  console.error('Analytics assistant renderer failed safely.', error)
  assistantRenderError.value = 'Không thể hiển thị một kết quả AI. Dữ liệu phiên vẫn được giữ nguyên.'
  return false
})

function recoverAssistantRenderer() {
  assistantRenderError.value = null
  assistantRenderKey.value += 1
}

function focusPrimaryRuntime(event: Event) {
  const detail = event instanceof CustomEvent ? event.detail : null
  externalPrompt.value = String(detail?.prompt || '').trim()
  externalProjectId.value = String(detail?.projectId || '') || null
  if (detail?.openHistory) externalHistoryToken.value += 1
  else externalPromptToken.value += 1
}

onMounted(() => window.addEventListener('qaly:focus-ai-primary-runtime', focusPrimaryRuntime))
onBeforeUnmount(() => window.removeEventListener('qaly:focus-ai-primary-runtime', focusPrimaryRuntime))
</script>

<template>
  <section v-if="assistantRenderError" class="assistant-render-fallback" role="alert">
    <strong>Trang phân tích chưa thể hiển thị kết quả vừa nhận</strong>
    <p>{{ assistantRenderError }}</p>
    <button type="button" @click="recoverAssistantRenderer">Khôi phục Trợ lý AI</button>
  </section>
  <ErumiChatPanel
    v-else
    :key="assistantRenderKey"
    :external-prompt="externalPrompt"
    :external-prompt-token="externalPromptToken"
    :external-history-token="externalHistoryToken"
    :external-project-id="externalProjectId"
    @compose-action="openActionComposer"
  />
</template>

<style scoped>
/* Scoped styles are encapsulated inside ErumiChatPanel */
.assistant-render-fallback {
  display: grid;
  place-content: center;
  min-height: 420px;
  gap: 10px;
  padding: 32px;
  text-align: center;
  color: #334155;
}

.assistant-render-fallback strong {
  color: #0f172a;
  font-size: 1.1rem;
}

.assistant-render-fallback button {
  justify-self: center;
  border: 0;
  border-radius: 8px;
  padding: 10px 16px;
  color: #fff;
  background: #2563eb;
  font-weight: 700;
  cursor: pointer;
}
</style>
