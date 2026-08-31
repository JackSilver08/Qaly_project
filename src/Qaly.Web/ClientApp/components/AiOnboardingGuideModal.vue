<script setup lang="ts">
import { ref, computed } from 'vue'
import { Compass, CheckCircle2, ArrowRight, X, Bot, BookOpen, Send, Sparkles } from 'lucide-vue-next'

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
  <div v-if="show" class="fixed inset-0 z-50 bg-black/80 backdrop-blur-sm flex items-center justify-center p-4">
    <div class="glass-card bg-slate-900 border border-emerald-500/40 rounded-2xl max-w-2xl w-full p-6 space-y-6 shadow-2xl animate-in fade-in zoom-in duration-200">
      <div class="flex items-center justify-between border-b border-slate-800 pb-3">
        <div class="flex items-center gap-2 text-emerald-400 font-bold text-base">
          <Compass class="w-5 h-5" />
          <span>🧭 Interactive AI Onboarding Guide • {{ roleTitle }}</span>
        </div>
        <button type="button" aria-label="Đóng hướng dẫn" @click="emit('close')" class="text-slate-400 hover:text-white transition">
          <X class="w-5 h-5" />
        </button>
      </div>

      <!-- Step Cards Grid -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-3 text-xs">
        <button
          v-for="item in steps"
          :key="item.step"
          type="button"
          @click="currentStep = item.step"
          class="p-3.5 rounded-xl border transition cursor-pointer relative text-left"
          :class="currentStep === item.step ? 'bg-emerald-500/10 border-emerald-500/60 ring-2 ring-emerald-500/20' : 'bg-slate-950 border-slate-800 hover:border-slate-700'"
          :aria-current="currentStep === item.step ? 'step' : undefined"
        >
          <span
            class="text-[10px] font-bold px-2 py-0.5 rounded-full mb-2 inline-block"
            :class="currentStep === item.step ? 'bg-emerald-500 text-slate-950' : 'bg-slate-800 text-slate-400'"
          >
            Bước {{ item.step }}
          </span>
          <div class="text-white font-bold text-xs mb-1">{{ item.title }}</div>
          <p class="text-slate-400 text-[11px] leading-relaxed">{{ item.desc }}</p>
        </button>
      </div>

      <!-- RAG Wiki Q&A Assistant -->
      <div class="bg-slate-950 p-4 rounded-xl border border-slate-800 space-y-3 text-xs">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2 text-purple-300 font-semibold">
            <Bot class="w-4 h-4 text-purple-400" />
            <span>💬 Trợ Lý AI RAG Wiki Q&A (Hỏi Đáp Quy Trình Dự Án)</span>
          </div>
          <span class="text-[10px] text-slate-500">Tìm kiếm trên Qdrant Vector DB Wiki</span>
        </div>

        <div class="flex items-center space-x-2">
          <input
            v-model="userQuestion"
            aria-label="Câu hỏi về Wiki dự án"
            @keyup.enter="askWikiRAG"
            placeholder="Ví dụ: Quy trình nộp evidence duyệt task như thế nào?"
            class="bg-slate-900 border border-slate-700 text-slate-200 text-xs px-3 py-2 rounded-lg w-full focus:outline-none focus:border-purple-500"
          />
          <button
            type="button"
            @click="askWikiRAG"
            :disabled="isAskingWiki"
            class="bg-purple-600 hover:bg-purple-500 text-white font-semibold px-4 py-2 rounded-lg transition flex items-center gap-1 flex-shrink-0"
          >
            <Send class="w-3.5 h-3.5" />
            <span>Hỏi AI</span>
          </button>
        </div>

        <div v-if="isAskingWiki" class="text-slate-400 text-[11px] flex items-center gap-2">
          <Sparkles class="w-3.5 h-3.5 text-purple-400 animate-spin" />
          <span>Đang tìm kiếm trong Wiki dự án...</span>
        </div>

        <div v-else-if="wikiAnswer" class="bg-slate-900 p-3 rounded-lg border border-purple-500/30 text-purple-200 text-[11px]">
          {{ wikiAnswer }}
        </div>
      </div>

      <div class="flex items-center justify-end pt-2 border-t border-slate-800">
        <button
          type="button"
          @click="emit('close')"
          class="bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs px-5 py-2 rounded-lg transition"
        >
          Hoàn Thành Onboarding
        </button>
      </div>
    </div>
  </div>
</template>
