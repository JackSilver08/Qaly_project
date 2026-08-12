<script setup lang="ts">
/**
 * Short, role-aware guide for someone new to a project.
 *
 * Deliberately compact: what your role is, what to do first, the five-step workflow, and AI
 * prompts that actually work at your tier. Dismissed state is remembered per project.
 */
import { ref, watch } from 'vue'
import { Bot, ChevronDown, Compass, X } from 'lucide-vue-next'
import { apiResult } from '../utils/api-client'

interface GuideStep {
  title: string
  detail: string
}

interface OnboardingGuide {
  roleLabel: string
  summary: string
  firstActions: GuideStep[]
  workflow: GuideStep[]
  aiHint: string
  sampleQuestions: string[]
}

const props = defineProps<{ projectId: string | null }>()
const emit = defineEmits<{ 'ask-ai': [question: string] }>()

const guide = ref<OnboardingGuide | null>(null)
const dismissed = ref(false)
const expanded = ref(true)

function storageKey(projectId: string) {
  return `qaly_guide_dismissed:${projectId}`
}

async function load() {
  guide.value = null
  if (!props.projectId) return

  dismissed.value = localStorage.getItem(storageKey(props.projectId)) === 'true'
  if (dismissed.value) return

  try {
    guide.value = await apiResult<OnboardingGuide>(
      `/api/projects/${props.projectId}/onboarding-guide`,
    )
  } catch {
    guide.value = null
  }
}

watch(() => props.projectId, load, { immediate: true })

function dismiss() {
  if (props.projectId) localStorage.setItem(storageKey(props.projectId), 'true')
  dismissed.value = true
}
</script>

<template>
  <section v-if="guide && !dismissed" class="guide-card">
    <header>
      <span class="guide-icon"><Compass :size="16" /></span>
      <div class="guide-heading">
        <strong>Bắt đầu với vai trò {{ guide.roleLabel }}</strong>
        <p>{{ guide.summary }}</p>
      </div>
      <button class="icon-button" type="button" :title="expanded ? 'Thu gọn' : 'Mở rộng'" @click="expanded = !expanded">
        <ChevronDown :size="16" :style="expanded ? 'transform: rotate(180deg)' : ''" />
      </button>
      <button class="icon-button" type="button" title="Ẩn hướng dẫn" @click="dismiss">
        <X :size="16" />
      </button>
    </header>

    <div v-if="expanded" class="guide-body">
      <div class="guide-column">
        <h4>Làm gì trước</h4>
        <ol class="step-list">
          <li v-for="step in guide.firstActions" :key="step.title">
            <strong>{{ step.title }}</strong>
            <span>{{ step.detail }}</span>
          </li>
        </ol>
      </div>

      <div class="guide-column">
        <h4>Quy trình làm việc</h4>
        <ol class="step-list step-list--flow">
          <li v-for="step in guide.workflow" :key="step.title">
            <strong>{{ step.title }}</strong>
            <span>{{ step.detail }}</span>
          </li>
        </ol>
      </div>
    </div>

    <footer v-if="expanded && guide.sampleQuestions.length" class="guide-ai">
      <div class="ai-hint"><Bot :size="15" /><span>{{ guide.aiHint }}</span></div>
      <div class="ai-prompts">
        <button
          v-for="question in guide.sampleQuestions"
          :key="question"
          class="prompt-chip"
          type="button"
          @click="emit('ask-ai', question)"
        >
          {{ question }}
        </button>
      </div>
    </footer>
  </section>
</template>

<style scoped>
.guide-card {
  border: 1px solid var(--line);
  border-radius: var(--radius-shell);
  background: var(--panel);
  padding: 16px 18px;
  display: grid;
  gap: 14px;
  margin-bottom: 18px;
}

.guide-card > header {
  display: flex;
  align-items: flex-start;
  gap: 12px;
}

.guide-icon {
  display: grid;
  place-items: center;
  width: 32px;
  height: 32px;
  border-radius: 10px;
  background: var(--primary);
  color: #fff;
  flex: 0 0 auto;
}

.guide-heading {
  flex: 1;
  min-width: 0;
}

.guide-heading strong {
  display: block;
  font-size: 15px;
  color: var(--text-strong);
}

.guide-heading p {
  margin-top: 3px;
  font-size: 13px;
  color: var(--muted);
}

.guide-body {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
  gap: 18px;
}

.guide-column h4 {
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: var(--muted);
  margin-bottom: 8px;
}

.step-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  gap: 8px;
  counter-reset: step;
}

.step-list li {
  display: grid;
  gap: 1px;
  padding-left: 22px;
  position: relative;
  counter-increment: step;
}

.step-list li::before {
  content: counter(step);
  position: absolute;
  left: 0;
  top: 1px;
  width: 16px;
  height: 16px;
  border-radius: 50%;
  background: var(--bg-soft);
  border: 1px solid var(--line);
  color: var(--muted);
  font-size: 10px;
  font-weight: 800;
  display: grid;
  place-items: center;
}

/* The workflow already numbers its own titles. */
.step-list--flow li {
  padding-left: 0;
}

.step-list--flow li::before {
  content: none;
}

.step-list strong {
  font-size: 13px;
  color: var(--text-strong);
}

.step-list span {
  font-size: 12.5px;
  color: var(--muted);
}

.guide-ai {
  display: grid;
  gap: 9px;
  padding-top: 12px;
  border-top: 1px solid var(--line);
}

.ai-hint {
  display: flex;
  align-items: center;
  gap: 7px;
  font-size: 12.5px;
  font-weight: 600;
  color: var(--text-strong);
}

.ai-prompts {
  display: flex;
  flex-wrap: wrap;
  gap: 7px;
}

.prompt-chip {
  border: 1px solid var(--line);
  background: var(--bg-soft);
  color: var(--text-strong);
  border-radius: 999px;
  padding: 5px 12px;
  font-size: 12.5px;
  cursor: pointer;
  transition: border-color 0.15s ease, background 0.15s ease;
}

.prompt-chip:hover {
  border-color: var(--primary);
  background: rgba(31, 128, 255, 0.1);
}
</style>
