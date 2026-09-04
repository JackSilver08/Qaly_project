<script setup lang="ts">
import { ref, computed, nextTick, onBeforeUnmount, watch } from 'vue'
import { Compass, CheckCircle2, ArrowRight, X, Bot, Send, Sparkles } from 'lucide-vue-next'

const props = defineProps<{
  show: boolean
  userRole?: string
}>()

const emit = defineEmits<{
  (e: 'close'): void
}>()

const currentStep = ref(1)
const userQuestion = ref('')
const isAskingWiki = ref(false)
const wikiAnswer = ref<string | null>(null)
const modalRoot = ref<HTMLElement | null>(null)
let previousFocus: HTMLElement | null = null

const roleTitle = computed(() => {
  if (props.userRole?.includes('Backend')) return 'Developer Backend'
  if (props.userRole?.includes('Frontend')) return 'Developer Frontend'
  if (props.userRole?.includes('QA') || props.userRole?.includes('Tester')) return 'QA / Tester'
  return 'Thành Viên Mới'
})

const steps = computed(() => {
  if (props.userRole?.includes('Backend')) {
    return [
      { step: 1, title: 'DB & Entity Schema', desc: 'Kiểm tra EF Core Migrations & DB connection string trong appsettings.json.' },
      { step: 2, title: 'Swagger API & Policy Auth', desc: 'Tham khảo Swagger UI và quy tắc System Explicit Deny (Level 1).' },
      { step: 3, title: 'Kanban & Private Task', desc: 'Thực hiện task trên Kanban Board, chú ý thẻ Task Ẩn (Private Tasks).' },
      { step: 4, title: 'Erumi AI Assistance', desc: 'Tương tác với Erumi AI Assistant để nhận trợ giúp phát triển code.' }
    ]
  } else if (props.userRole?.includes('QA') || props.userRole?.includes('Tester')) {
    return [
      { step: 1, title: 'Sprint & Test Cases', desc: 'Xem danh sách Sprint trong Project Roadmap & bộ 300 Test Cases.' },
      { step: 2, title: 'Playwright E2E Suite', desc: 'Chạy npx playwright test kiểm tra UI & Visibility Health Check.' },
      { step: 3, title: 'Private Bug Tasks', desc: 'Mở Private Bug Task cho Dev, đảm bảo chỉ Assigner & Assignee xem được.' },
      { step: 4, title: 'Sprint Sign-off', desc: 'Nộp báo cáo bằng chứng test (Evidence) để duyệt Done Task.' }
    ]
  } else {
    return [
      { step: 1, title: 'Nhận Vai Trò & Quyền', desc: 'Xem thông báo và kiểm tra vai trò chuyên môn được giao trong dự án.' },
      { step: 2, title: 'Theo Dõi Lộ Trình', desc: 'Truy cập Tab Project Roadmap để nắm mục tiêu phát triển chung.' },
      { step: 3, title: 'Kéo Thả Kanban Task', desc: 'Cập nhật trạng thái công việc từ Todo -> In Progress -> Done.' },
      { step: 4, title: 'Hỏi Đáp Trợ Lý AI Wiki', desc: 'Đặt câu hỏi cho AI Onboarding để tự tìm kiếm câu trả lời trên Wiki.' }
    ]
  }
})

const activeStep = computed(() =>
  steps.value.find(item => item.step === currentStep.value) ?? steps.value[0],
)

const closeGuide = () => emit('close')

const handleModalKeydown = (event: KeyboardEvent) => {
  if (!props.show) return

  if (event.key === 'Escape') {
    event.preventDefault()
    closeGuide()
    return
  }

  if (event.key !== 'Tab' || !modalRoot.value) return
  const focusable = Array.from(
    modalRoot.value.querySelectorAll<HTMLElement>('button:not([disabled]), input:not([disabled]), [tabindex]:not([tabindex="-1"])'),
  ).filter(element => element.offsetParent !== null)
  if (!focusable.length) return

  const first = focusable[0]
  const last = focusable[focusable.length - 1]
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault()
    first.focus()
  }
}

watch(
  () => props.show,
  async show => {
    if (show) {
      previousFocus = document.activeElement as HTMLElement | null
      window.addEventListener('keydown', handleModalKeydown)
      await nextTick()
      modalRoot.value?.querySelector<HTMLElement>('[data-initial-focus]')?.focus()
    } else {
      window.removeEventListener('keydown', handleModalKeydown)
      previousFocus?.focus?.()
      previousFocus = null
    }
  },
)

onBeforeUnmount(() => window.removeEventListener('keydown', handleModalKeydown))

const askWikiRAG = async () => {
  if (!userQuestion.value.trim()) return
  isAskingWiki.value = true
  wikiAnswer.value = null

  setTimeout(() => {
    isAskingWiki.value = false
    wikiAnswer.value = `[Wiki RAG Answer]: Dựa trên tài liệu Wiki dự án Qaly, quy trình làm việc chuẩn gồm: (1) Nhận task trên Kanban -> (2) Tạo branch git đặt tên theo task key (VD: QALY-102) -> (3) Đẩy code và đính kèm evidence khi hoàn thành.`
  }, 1000)
}
</script>

<template>
  <Teleport to="body">
    <Transition name="guide-modal">
      <div v-if="show" class="onboarding-backdrop" @mousedown.self="closeGuide">
        <section
          ref="modalRoot"
          class="onboarding-dialog"
          role="dialog"
          aria-modal="true"
          aria-labelledby="onboarding-title"
          tabindex="-1"
        >
          <header class="onboarding-header">
            <div class="onboarding-heading">
              <span class="onboarding-heading__icon"><Compass :size="23" /></span>
              <div>
                <span class="onboarding-eyebrow">Bắt đầu nhanh</span>
                <h2 id="onboarding-title">Hướng dẫn lộ trình dự án</h2>
                <p>Lộ trình dành cho {{ roleTitle.toLowerCase() }}</p>
              </div>
            </div>
            <button
              type="button"
              class="onboarding-close"
              data-initial-focus
              aria-label="Đóng hướng dẫn"
              title="Đóng hướng dẫn"
              @click="closeGuide"
            >
              <X :size="19" />
            </button>
          </header>

          <div class="onboarding-body">
            <nav class="onboarding-steps" aria-label="Các bước làm quen">
              <button
                v-for="item in steps"
                :key="item.step"
                type="button"
                class="onboarding-step"
                :class="{ 'is-active': currentStep === item.step, 'is-passed': currentStep > item.step }"
                :aria-current="currentStep === item.step ? 'step' : undefined"
                @click="currentStep = item.step"
              >
                <span class="onboarding-step__number">
                  <CheckCircle2 v-if="currentStep > item.step" :size="16" />
                  <span v-else>{{ item.step }}</span>
                </span>
                <span class="onboarding-step__copy">
                  <small>Bước {{ item.step }}</small>
                  <strong>{{ item.title }}</strong>
                </span>
              </button>
            </nav>

            <article class="onboarding-focus-card">
              <div class="onboarding-focus-card__icon"><ArrowRight :size="20" /></div>
              <div>
                <span>Việc cần làm tiếp theo</span>
                <h3>{{ activeStep.title }}</h3>
                <p>{{ activeStep.desc }}</p>
              </div>
            </article>

            <section class="wiki-assistant" aria-labelledby="wiki-assistant-title">
              <div class="wiki-assistant__header">
                <span class="wiki-assistant__icon"><Bot :size="19" /></span>
                <div>
                  <h3 id="wiki-assistant-title">Hỏi trợ lý quy trình</h3>
                  <p>Tìm câu trả lời nhanh từ Wiki của dự án.</p>
                </div>
              </div>

              <div class="wiki-assistant__form">
                <input
                  v-model="userQuestion"
                  aria-label="Câu hỏi về Wiki dự án"
                  placeholder="Ví dụ: Quy trình nộp bằng chứng duyệt task như thế nào?"
                  @keyup.enter="askWikiRAG"
                />
                <button type="button" :disabled="isAskingWiki || !userQuestion.trim()" @click="askWikiRAG">
                  <Sparkles v-if="isAskingWiki" :size="16" class="is-spinning" />
                  <Send v-else :size="16" />
                  <span>{{ isAskingWiki ? 'Đang tìm…' : 'Hỏi trợ lý' }}</span>
                </button>
              </div>

              <div v-if="isAskingWiki" class="wiki-assistant__status" role="status">
                Đang tìm kiếm trong tài liệu dự án…
              </div>
              <div v-else-if="wikiAnswer" class="wiki-assistant__answer">
                <Sparkles :size="16" />
                <p>{{ wikiAnswer }}</p>
              </div>
            </section>
          </div>

          <footer class="onboarding-footer">
            <span>{{ currentStep }}/{{ steps.length }} bước đã xem</span>
            <button type="button" class="onboarding-done" @click="closeGuide">
              <CheckCircle2 :size="17" />
              Hoàn thành hướng dẫn
            </button>
          </footer>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.onboarding-backdrop {
  position: fixed;
  z-index: 2147482000;
  inset: 0;
  display: grid;
  place-items: center;
  overflow-y: auto;
  padding: 24px;
  background: rgba(13, 27, 49, 0.64);
  backdrop-filter: blur(8px);
}

.onboarding-dialog {
  width: min(780px, calc(100vw - 32px));
  max-height: min(820px, calc(100vh - 40px));
  overflow: hidden auto;
  color: #17233a;
  background: #ffffff;
  border: 1px solid #dce5ef;
  border-radius: 22px;
  box-shadow: 0 34px 90px rgba(9, 26, 52, 0.32);
}

.onboarding-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 20px;
  padding: 24px 26px 20px;
  background:
    radial-gradient(circle at 90% 0%, rgba(96, 165, 250, 0.17), transparent 38%),
    linear-gradient(135deg, #f8fbff, #ffffff);
  border-bottom: 1px solid #e2e9f2;
}

.onboarding-heading {
  display: flex;
  align-items: center;
  gap: 14px;
}

.onboarding-heading__icon {
  width: 48px;
  height: 48px;
  display: grid;
  flex: 0 0 auto;
  place-items: center;
  color: #ffffff;
  background: linear-gradient(135deg, #2563eb, #164db2);
  border-radius: 14px;
  box-shadow: 0 9px 22px rgba(37, 99, 235, 0.24);
}

.onboarding-eyebrow {
  display: block;
  margin-bottom: 3px;
  color: #2563eb;
  font-size: 10px;
  font-weight: 800;
  letter-spacing: 0.12em;
  text-transform: uppercase;
}

.onboarding-heading h2 {
  margin: 0;
  font-size: 21px;
  line-height: 1.3;
  letter-spacing: -0.025em;
}

.onboarding-heading p {
  margin: 4px 0 0;
  color: #6a7a91;
  font-size: 12px;
}

.onboarding-close {
  width: 38px;
  height: 38px;
  display: grid;
  flex: 0 0 auto;
  place-items: center;
  color: #52637a;
  background: #ffffff;
  border: 1px solid #d3dce8;
  border-radius: 10px;
  cursor: pointer;
  transition: color 0.3s ease, background-color 0.3s ease, border-color 0.3s ease;
}

.onboarding-close:hover {
  color: #183e85;
  background: #f1f6fd;
  border-color: #9eb4d3;
}

.onboarding-close:focus-visible,
.onboarding-step:focus-visible,
.wiki-assistant input:focus-visible,
.wiki-assistant button:focus-visible,
.onboarding-done:focus-visible {
  outline: 3px solid rgba(37, 99, 235, 0.2);
  outline-offset: 2px;
}

.onboarding-body {
  display: grid;
  gap: 18px;
  padding: 22px 26px;
}

.onboarding-steps {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 8px;
}

.onboarding-step {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 9px;
  padding: 11px;
  color: #53647b;
  text-align: left;
  background: #f8fafc;
  border: 1px solid #e0e7f0;
  border-radius: 12px;
  cursor: pointer;
  transition: color 0.3s ease, background-color 0.3s ease, border-color 0.3s ease, transform 0.3s ease;
}

.onboarding-step:hover {
  border-color: #9cb8df;
  transform: translateY(-1px);
}

.onboarding-step.is-active {
  color: #1748a7;
  background: #eff6ff;
  border-color: #76a3e8;
  box-shadow: inset 0 0 0 1px rgba(37, 99, 235, 0.08);
}

.onboarding-step__number {
  width: 28px;
  height: 28px;
  display: grid;
  flex: 0 0 auto;
  place-items: center;
  color: #5e7088;
  background: #ffffff;
  border: 1px solid #d6dfeb;
  border-radius: 50%;
  font-size: 11px;
  font-weight: 800;
}

.onboarding-step.is-active .onboarding-step__number {
  color: #ffffff;
  background: #2563eb;
  border-color: #2563eb;
}

.onboarding-step.is-passed .onboarding-step__number {
  color: #087a55;
  background: #e9f8f1;
  border-color: #a8dec9;
}

.onboarding-step__copy {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.onboarding-step__copy small {
  color: #8090a5;
  font-size: 9px;
  font-weight: 750;
  letter-spacing: 0.07em;
  text-transform: uppercase;
}

.onboarding-step__copy strong {
  overflow: hidden;
  font-size: 11px;
  line-height: 1.25;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.onboarding-focus-card {
  display: flex;
  align-items: flex-start;
  gap: 13px;
  padding: 17px 18px;
  background: linear-gradient(125deg, #eff6ff, #f8fbff);
  border: 1px solid #d5e4fa;
  border-radius: 14px;
}

.onboarding-focus-card__icon {
  width: 34px;
  height: 34px;
  display: grid;
  flex: 0 0 auto;
  place-items: center;
  color: #2563eb;
  background: #ffffff;
  border-radius: 10px;
  box-shadow: 0 4px 12px rgba(37, 99, 235, 0.12);
}

.onboarding-focus-card span {
  color: #687b95;
  font-size: 10px;
  font-weight: 800;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.onboarding-focus-card h3 {
  margin: 3px 0 4px;
  color: #172a49;
  font-size: 15px;
}

.onboarding-focus-card p {
  margin: 0;
  color: #5d6f87;
  font-size: 12px;
  line-height: 1.55;
}

.wiki-assistant {
  padding: 17px;
  background: #fbfcff;
  border: 1px solid #dfe6f0;
  border-radius: 14px;
}

.wiki-assistant__header {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 13px;
}

.wiki-assistant__icon {
  width: 36px;
  height: 36px;
  display: grid;
  place-items: center;
  color: #6d42c7;
  background: #f2edff;
  border-radius: 10px;
}

.wiki-assistant h3 {
  margin: 0;
  color: #25344e;
  font-size: 13px;
}

.wiki-assistant__header p {
  margin: 2px 0 0;
  color: #77879c;
  font-size: 11px;
}

.wiki-assistant__form {
  display: flex;
  gap: 8px;
}

.wiki-assistant input {
  min-width: 0;
  min-height: 42px;
  flex: 1;
  padding: 10px 12px;
  color: #233650;
  background: #ffffff;
  border: 1px solid #cfd9e6;
  border-radius: 10px;
  font: inherit;
  font-size: 12px;
}

.wiki-assistant input::placeholder {
  color: #98a5b6;
}

.wiki-assistant button,
.onboarding-done {
  min-height: 42px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 7px;
  padding: 10px 15px;
  color: #ffffff;
  background: #2563eb;
  border: 1px solid #2563eb;
  border-radius: 10px;
  font: inherit;
  font-size: 12px;
  font-weight: 750;
  white-space: nowrap;
  cursor: pointer;
  transition: background-color 0.3s ease, border-color 0.3s ease, transform 0.3s ease;
}

.wiki-assistant button:hover,
.onboarding-done:hover {
  background: #1748a7;
  border-color: #1748a7;
  transform: translateY(-1px);
}

.wiki-assistant button:disabled {
  opacity: 0.55;
  cursor: not-allowed;
  transform: none;
}

.wiki-assistant__status {
  margin-top: 10px;
  color: #687b93;
  font-size: 11px;
}

.wiki-assistant__answer {
  display: flex;
  align-items: flex-start;
  gap: 9px;
  margin-top: 11px;
  padding: 12px;
  color: #5630a4;
  background: #f6f2ff;
  border: 1px solid #ded2fa;
  border-radius: 10px;
}

.wiki-assistant__answer svg {
  flex: 0 0 auto;
  margin-top: 2px;
}

.wiki-assistant__answer p {
  margin: 0;
  font-size: 11px;
  line-height: 1.55;
}

.onboarding-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 16px 26px 20px;
  border-top: 1px solid #e4eaf2;
}

.onboarding-footer > span {
  color: #78889d;
  font-size: 11px;
}

.onboarding-done {
  background: #087a55;
  border-color: #087a55;
}

.onboarding-done:hover {
  background: #066844;
  border-color: #066844;
}

.is-spinning {
  animation: onboarding-spin 0.9s linear infinite;
}

.guide-modal-enter-active,
.guide-modal-leave-active {
  transition: opacity 0.3s ease;
}

.guide-modal-enter-active .onboarding-dialog,
.guide-modal-leave-active .onboarding-dialog {
  transition: transform 0.3s ease, opacity 0.3s ease;
}

.guide-modal-enter-from,
.guide-modal-leave-to {
  opacity: 0;
}

.guide-modal-enter-from .onboarding-dialog,
.guide-modal-leave-to .onboarding-dialog {
  opacity: 0;
  transform: translateY(10px) scale(0.985);
}

@keyframes onboarding-spin {
  to { transform: rotate(360deg); }
}

@media (max-width: 720px) {
  .onboarding-backdrop {
    align-items: end;
    padding: 0;
  }

  .onboarding-dialog {
    width: 100%;
    max-height: 92vh;
    border-radius: 20px 20px 0 0;
  }

  .onboarding-header,
  .onboarding-body,
  .onboarding-footer {
    padding-inline: 18px;
  }

  .onboarding-steps {
    grid-template-columns: 1fr 1fr;
  }
}

@media (max-width: 460px) {
  .onboarding-header {
    padding-top: 18px;
  }

  .onboarding-heading__icon {
    display: none;
  }

  .onboarding-steps {
    grid-template-columns: 1fr;
  }

  .wiki-assistant__form,
  .onboarding-footer {
    align-items: stretch;
    flex-direction: column;
  }

  .wiki-assistant button,
  .onboarding-done {
    width: 100%;
  }
}

@media (prefers-reduced-motion: reduce) {
  .onboarding-close,
  .onboarding-step,
  .wiki-assistant button,
  .onboarding-done,
  .guide-modal-enter-active,
  .guide-modal-leave-active,
  .guide-modal-enter-active .onboarding-dialog,
  .guide-modal-leave-active .onboarding-dialog {
    animation: none;
    transition: none;
  }
}
</style>
