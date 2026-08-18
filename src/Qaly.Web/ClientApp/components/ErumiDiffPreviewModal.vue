<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import {
  Sparkles,
  Check,
  X,
  AlertTriangle,
  RotateCcw,
  ShieldCheck,
  UserCheck,
  Send,
  Sliders,
  Activity,
  Layers,
  Info
} from 'lucide-vue-next'
import type { ErumiRoadmapDiffProposalDto, ErumiTaskProposalDto } from '../types'

const props = withDefaults(
  defineProps<{
    show: boolean
    proposal: ErumiRoadmapDiffProposalDto | null
    isOwnerOrAuthorized?: boolean
    isSubmitting?: boolean
  }>(),
  {
    isOwnerOrAuthorized: true,
    isSubmitting: false
  }
)

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'approve', approvedTasks: ErumiTaskProposalDto[]): void
  (e: 'submitForReview', proposal: ErumiRoadmapDiffProposalDto): void
}>()

// Selective Task Checkboxes State
const selectedTasksMap = ref<Record<number, boolean>>({})

// Initialize selection whenever proposal changes
watch(
  () => props.proposal,
  (newVal) => {
    selectedTasksMap.value = {}
    if (newVal?.proposedTasks) {
      newVal.proposedTasks.forEach((_, idx) => {
        selectedTasksMap.value[idx] = true
      })
    }
  },
  { immediate: true }
)

const allSelected = computed(() => {
  if (!props.proposal?.proposedTasks?.length) return false
  return props.proposal.proposedTasks.every((_, idx) => selectedTasksMap.value[idx])
})

const selectedTasksList = computed<ErumiTaskProposalDto[]>(() => {
  if (!props.proposal?.proposedTasks) return []
  return props.proposal.proposedTasks.filter((_, idx) => selectedTasksMap.value[idx])
})

function toggleAll() {
  const target = !allSelected.value
  if (!props.proposal?.proposedTasks) return
  props.proposal.proposedTasks.forEach((_, idx) => {
    selectedTasksMap.value[idx] = target
  })
}

function handleApprove() {
  if (selectedTasksList.value.length === 0) return
  emit('approve', selectedTasksList.value)
}

function handleSubmitReview() {
  if (!props.proposal) return
  emit('submitForReview', props.proposal)
}
</script>

<template>
  <div
    v-if="show && proposal"
    class="fixed inset-0 z-50 bg-black/85 backdrop-blur-md flex items-center justify-center p-3 sm:p-6 overflow-y-auto"
  >
    <div
      class="glass-card bg-slate-900 border border-purple-500/50 rounded-2xl max-w-3xl w-full p-5 sm:p-7 space-y-5 shadow-2xl animate-in fade-in zoom-in duration-200 my-auto text-slate-200"
    >
      <!-- HEADER -->
      <div class="flex items-center justify-between border-b border-slate-800 pb-3.5">
        <div class="flex items-center gap-3">
          <div class="p-2 rounded-xl bg-purple-500/20 text-purple-400 border border-purple-500/30">
            <Sparkles class="w-5 h-5" />
          </div>
          <div>
            <div class="text-white font-extrabold text-base flex items-center gap-2">
              <span>✨ AI Roadmap Proposal (Diff Preview)</span>
              <span
                v-if="proposal.confidenceScore"
                class="bg-emerald-500/10 text-emerald-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-emerald-500/30"
              >
                Confidence: {{ Math.round(proposal.confidenceScore * 100) }}%
              </span>
            </div>
            <p class="text-[11px] text-slate-400 font-mono">
              Snapshot ID: #{{ proposal.snapshotId.substring(0, 8) }} • Kiểm duyệt an toàn trước khi lưu
            </p>
          </div>
        </div>

        <div class="flex items-center gap-2">
          <span
            v-if="isOwnerOrAuthorized"
            class="hidden sm:inline-flex items-center gap-1 bg-purple-950/60 text-purple-300 border border-purple-600/40 text-[11px] px-2.5 py-1 rounded-lg font-semibold"
          >
            <ShieldCheck class="w-3.5 h-3.5 text-emerald-400" /> Thẩm Quyền: Owner/Admin
          </span>
          <span
            v-else
            class="hidden sm:inline-flex items-center gap-1 bg-amber-950/60 text-amber-300 border border-amber-600/40 text-[11px] px-2.5 py-1 rounded-lg font-semibold"
          >
            <Info class="w-3.5 h-3.5 text-amber-400" /> Chế độ Đề Xuất (Member Sandbox)
          </span>

          <button
            @click="emit('close')"
            class="text-slate-400 hover:text-white p-1.5 rounded-lg hover:bg-slate-800 transition"
          >
            <X class="w-5 h-5" />
          </button>
        </div>
      </div>

      <!-- MAIN CONTENT BODY -->
      <div class="space-y-4 text-xs max-h-[65vh] overflow-y-auto pr-1">
        <!-- Summary Banner -->
        <div
          class="bg-gradient-to-r from-purple-950/40 via-slate-900 to-blue-950/40 border border-purple-500/30 p-4 rounded-xl flex flex-col sm:flex-row sm:items-center justify-between gap-3"
        >
          <div>
            <div class="text-white font-bold text-sm flex items-center gap-2">
              <Layers class="w-4 h-4 text-purple-400" />
              <span>{{ proposal.phaseName }}</span>
            </div>
            <div class="text-purple-300 text-xs mt-1">{{ proposal.summary }}</div>
          </div>
          <div class="text-right shrink-0">
            <span class="bg-purple-500/20 text-purple-300 px-2.5 py-1 rounded-full border border-purple-500/40 font-mono text-[10px]">
              Thời gian: 14 ngày
            </span>
          </div>
        </div>

        <!-- Risks & Warnings (if any) -->
        <div
          v-if="proposal.identifiedRisks && proposal.identifiedRisks.length > 0"
          class="bg-rose-950/30 border border-rose-500/40 p-3 rounded-xl space-y-1.5"
        >
          <div class="text-rose-400 font-bold flex items-center gap-1.5 text-xs">
            <AlertTriangle class="w-4 h-4" />
            <span>Cảnh Báo Rủi Ro Lộ Trình Phát Hiện Bởi AI:</span>
          </div>
          <div
            v-for="risk in proposal.identifiedRisks"
            :key="risk.description"
            class="text-[11px] text-slate-300 pl-5"
          >
            • <strong>{{ risk.description }}</strong> - <span class="text-amber-300">Khuyến nghị: {{ risk.mitigationAdvice }}</span>
          </div>
        </div>

        <!-- Proposed Tasks List (Selective Acceptance) -->
        <div class="space-y-2.5">
          <div class="flex items-center justify-between">
            <div class="text-emerald-400 font-bold flex items-center gap-1.5">
              <Check class="w-4 h-4" />
              <span>
                Danh sách Task đề xuất (+{{ proposal.proposedTasks.length }} công việc mới):
              </span>
            </div>
            <button
              @click="toggleAll"
              type="button"
              class="text-purple-400 hover:text-purple-300 font-semibold underline text-[11px]"
            >
              {{ allSelected ? 'Bỏ chọn tất cả' : 'Chọn tất cả' }}
            </button>
          </div>

          <div class="space-y-2">
            <div
              v-for="(task, idx) in proposal.proposedTasks"
              :key="task.title + idx"
              class="bg-slate-950 p-3 rounded-xl border transition flex items-start gap-3"
              :class="selectedTasksMap[idx] ? 'border-purple-500/40 bg-slate-950' : 'border-slate-800 opacity-60'"
            >
              <input
                type="checkbox"
                v-model="selectedTasksMap[idx]"
                class="mt-1 h-4 w-4 rounded border-slate-700 bg-slate-900 text-purple-600 focus:ring-purple-500 cursor-pointer"
              />
              <div class="flex-1 space-y-1">
                <div class="flex items-center justify-between text-white font-semibold">
                  <span class="text-purple-300 text-xs">{{ task.title }}</span>
                  <span class="bg-slate-800 text-slate-300 px-2 py-0.5 rounded text-[10px] font-mono">
                    {{ task.priority }} • {{ task.estimatedHours }}h
                  </span>
                </div>
                <p class="text-slate-400 text-[11px]">{{ task.description }}</p>
                <div class="text-[10px] text-slate-500 flex items-center gap-3 pt-0.5">
                  <span class="flex items-center gap-1 text-purple-400">
                    <UserCheck class="w-3 h-3" />
                    <span>Gán khuyến nghị: <strong class="text-slate-300">{{ task.recommendedAssigneeName || 'Auto' }}</strong> ({{ task.recommendedRole }})</span>
                  </span>
                  <span v-if="task.dependencyNote" class="text-slate-400">• Phụ thuộc: {{ task.dependencyNote }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Workload Impact Analysis -->
        <div class="space-y-2 pt-1">
          <div class="text-blue-400 font-bold flex items-center gap-1.5">
            <Activity class="w-4 h-4" />
            <span>📊 Đánh Giá Năng Lực & Tải Của Đội Ngũ (Workload Capacity):</span>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-2.5">
            <div
              v-for="impact in proposal.workloadImpacts"
              :key="impact.memberUserId"
              class="bg-slate-950 p-2.5 rounded-xl border text-[11px] space-y-1.5"
              :class="impact.isOverloaded ? 'border-rose-500/40 bg-rose-500/5' : 'border-emerald-500/30 bg-emerald-500/5'"
            >
              <div class="flex items-center justify-between font-semibold">
                <span class="text-white">{{ impact.memberName }}</span>
                <span :class="impact.isOverloaded ? 'text-rose-400 font-bold' : 'text-emerald-400'">
                  {{ impact.warningMessage }}
                </span>
              </div>
              <div class="text-slate-400 flex items-center justify-between text-[10px]">
                <span>Vai trò: {{ impact.currentRole }}</span>
                <span>Tải mới: <strong class="text-purple-300">+{{ impact.proposedAdditionalHours }}h</strong></span>
              </div>
              <div class="w-full bg-slate-800 h-1.5 rounded-full overflow-hidden">
                <div
                  :class="impact.isOverloaded ? 'bg-rose-500' : 'bg-emerald-500'"
                  class="h-full rounded-full transition-all duration-300"
                  :style="{ width: Math.min(((impact.currentWeeklyHours + impact.proposedAdditionalHours) / 40) * 100, 100) + '%' }"
                ></div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- MODAL FOOTER -->
      <div class="flex flex-col sm:flex-row items-center justify-between gap-3 pt-3 border-t border-slate-800 text-xs">
        <div class="text-slate-400 flex items-center gap-1">
          <RotateCcw class="w-3.5 h-3.5 text-purple-400" />
          <span>Bảo vệ với cơ chế <strong>1-Click Rollback</strong> trong 72 giờ.</span>
        </div>

        <div class="flex items-center space-x-3 w-full sm:w-auto justify-end">
          <button
            @click="emit('close')"
            type="button"
            class="bg-slate-800 hover:bg-slate-700 text-slate-300 px-4 py-2 rounded-xl transition"
          >
            Đóng
          </button>

          <!-- Button for Owner / Authorized Admin -->
          <button
            v-if="isOwnerOrAuthorized"
            @click="handleApprove"
            :disabled="isSubmitting || selectedTasksList.length === 0"
            type="button"
            class="bg-gradient-to-r from-purple-600 to-indigo-600 hover:from-purple-500 hover:to-indigo-500 disabled:opacity-50 text-white font-bold px-5 py-2 rounded-xl transition shadow-lg shadow-purple-600/30 flex items-center gap-1.5"
          >
            <Check class="w-4 h-4" />
            <span>{{ isSubmitting ? 'Đang duyệt...' : `Phê Duyệt (${selectedTasksList.length} Task) & Ghi DB` }}</span>
          </button>

          <!-- Button for Normal Member (Sandbox Proposal) -->
          <button
            v-else
            @click="handleSubmitReview"
            :disabled="isSubmitting"
            type="button"
            class="bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-500 hover:to-indigo-500 text-white font-bold px-5 py-2 rounded-xl transition shadow-lg shadow-blue-600/30 flex items-center gap-1.5"
          >
            <Send class="w-4 h-4" />
            <span>{{ isSubmitting ? 'Đang gửi...' : 'Gửi Trình Duyệt Cho Project Owner' }}</span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
