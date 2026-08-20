<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { Activity, AlertTriangle, Check, Info, Layers, RotateCcw, Send, ShieldCheck, Sparkles, UserCheck, X } from 'lucide-vue-next'
import type { ErumiRoadmapDiffProposalDto, ErumiTaskProposalDto } from '../types'

const props = withDefaults(defineProps<{
  show: boolean
  proposal: ErumiRoadmapDiffProposalDto | null
  isOwnerOrAuthorized?: boolean
  isSubmitting?: boolean
}>(), {
  isOwnerOrAuthorized: true,
  isSubmitting: false,
})

const emit = defineEmits<{
  (event: 'close'): void
  (event: 'approve', tasks: ErumiTaskProposalDto[]): void
  (event: 'submitForReview', proposal: ErumiRoadmapDiffProposalDto): void
}>()

type EditableTask = ErumiTaskProposalDto & { selected: boolean }
const editableTasks = ref<EditableTask[]>([])

watch(
  () => [props.show, props.proposal] as const,
  () => {
    editableTasks.value = (props.proposal?.proposedTasks ?? []).map(task => ({ ...task, selected: true }))
  },
  { immediate: true },
)

const selectedTasks = computed(() => editableTasks.value.filter(task => task.selected && task.title.trim()))
const allSelected = computed(() => editableTasks.value.length > 0 && editableTasks.value.every(task => task.selected))

function toggleAll() {
  const next = !allSelected.value
  editableTasks.value.forEach(task => { task.selected = next })
}

function approveSelected() {
  emit('approve', selectedTasks.value.map(({ selected: _selected, ...task }) => task))
}

function submitForReview() {
  if (!props.proposal) return
  const selected = selectedTasks.value.map(({ selected: _selected, ...task }) => task)
  emit('submitForReview', { ...props.proposal, proposedTasks: selected })
}
</script>

<template>
  <div v-if="show && proposal" class="fixed inset-0 z-50 bg-black/85 backdrop-blur-md flex items-center justify-center p-3 sm:p-6 overflow-y-auto">
    <div class="glass-card bg-slate-900 border border-purple-500/50 rounded-2xl max-w-4xl w-full p-5 sm:p-7 space-y-5 shadow-2xl my-auto text-slate-200">
      <div class="flex items-center justify-between border-b border-slate-800 pb-3.5">
        <div class="flex items-center gap-3 min-w-0">
          <div class="p-2 rounded-xl bg-purple-500/20 text-purple-400 border border-purple-500/30"><Sparkles class="w-5 h-5" /></div>
          <div class="min-w-0">
            <div class="text-white font-extrabold text-base flex flex-wrap items-center gap-2">
              <span>Phương án thay đổi Roadmap</span>
              <span v-if="proposal.confidenceScore" class="bg-emerald-500/10 text-emerald-400 text-[10px] px-2 py-0.5 rounded-full border border-emerald-500/30">
                Độ tin cậy {{ Math.round(proposal.confidenceScore * 100) }}%
              </span>
            </div>
            <p class="text-[11px] text-slate-400">Bản nháp #{{ proposal.snapshotId.substring(0, 8) }} · Chưa ghi dữ liệu</p>
          </div>
        </div>
        <div class="flex items-center gap-2">
          <span v-if="isOwnerOrAuthorized" class="hidden sm:inline-flex items-center gap-1 text-emerald-300 text-[11px]"><ShieldCheck class="w-3.5 h-3.5" /> Có quyền duyệt</span>
          <span v-else class="hidden sm:inline-flex items-center gap-1 text-amber-300 text-[11px]"><Info class="w-3.5 h-3.5" /> Chỉ gửi đề xuất</span>
          <button type="button" class="p-1.5 rounded-lg hover:bg-slate-800" aria-label="Đóng" @click="emit('close')"><X class="w-5 h-5" /></button>
        </div>
      </div>

      <div class="space-y-4 text-xs max-h-[65vh] overflow-y-auto pr-1">
        <div class="bg-gradient-to-r from-purple-950/40 via-slate-900 to-blue-950/40 border border-purple-500/30 p-4 rounded-xl">
          <div class="text-white font-bold text-sm flex items-center gap-2"><Layers class="w-4 h-4 text-purple-400" /> {{ proposal.phaseName }}</div>
          <div class="text-purple-300 text-xs mt-1">{{ proposal.summary }}</div>
          <div class="text-slate-400 text-[11px] mt-2">{{ new Date(proposal.estimatedStartDate).toLocaleDateString('vi-VN') }} → {{ new Date(proposal.estimatedEndDate).toLocaleDateString('vi-VN') }}</div>
        </div>

        <div v-if="proposal.identifiedRisks?.length" class="bg-rose-950/30 border border-rose-500/40 p-3 rounded-xl space-y-1.5">
          <div class="text-rose-400 font-bold flex items-center gap-1.5"><AlertTriangle class="w-4 h-4" /> Rủi ro cần xem trước khi duyệt</div>
          <div v-for="risk in proposal.identifiedRisks" :key="`${risk.riskType}-${risk.description}`" class="text-[11px] text-slate-300 pl-5">
            • <strong>{{ risk.description }}</strong> — <span class="text-amber-300">{{ risk.mitigationAdvice }}</span>
          </div>
        </div>

        <div class="space-y-2.5">
          <div class="flex items-center justify-between">
            <div class="text-emerald-400 font-bold flex items-center gap-1.5"><Check class="w-4 h-4" /> {{ selectedTasks.length }}/{{ editableTasks.length }} task được chọn</div>
            <button type="button" class="text-purple-400 hover:text-purple-300 underline text-[11px]" @click="toggleAll">{{ allSelected ? 'Bỏ chọn tất cả' : 'Chọn tất cả' }}</button>
          </div>

          <div class="space-y-2">
            <div v-for="(task, index) in editableTasks" :key="`${index}-${task.title}`" class="bg-slate-950 p-3 rounded-xl border space-y-2" :class="task.selected ? 'border-purple-500/40' : 'border-slate-800 opacity-60'">
              <div class="flex items-center gap-2">
                <input v-model="task.selected" type="checkbox" :aria-label="`Chọn task ${index + 1}`" />
                <input v-model="task.title" class="proposal-input flex-1 font-semibold" maxlength="200" aria-label="Tiêu đề task" />
              </div>
              <textarea v-model="task.description" class="proposal-input w-full" rows="2" maxlength="1000" aria-label="Mô tả task" />
              <div class="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <label class="field-label">Ưu tiên<select v-model="task.priority" class="proposal-input"><option v-for="priority in ['Low', 'Medium', 'High', 'Critical']" :key="priority" :value="priority">{{ priority }}</option></select></label>
                <label class="field-label">Giờ ước tính<input v-model.number="task.estimatedHours" class="proposal-input" type="number" min="1" max="80" /></label>
                <label class="field-label">Vai trò<input v-model="task.recommendedRole" class="proposal-input" maxlength="100" /></label>
              </div>
              <div class="text-[10px] text-slate-500 flex flex-wrap items-center gap-2">
                <span class="flex items-center gap-1"><UserCheck class="w-3 h-3 text-purple-400" /> Gợi ý: <strong class="text-slate-300">{{ task.recommendedAssigneeName || 'Chưa gán' }}</strong></span>
                <span v-if="task.dependencyNote">Phụ thuộc: {{ task.dependencyNote }}</span>
              </div>
            </div>
          </div>
        </div>

        <div class="space-y-2 pt-1">
          <div class="text-blue-400 font-bold flex items-center gap-1.5"><Activity class="w-4 h-4" /> Tác động tải công việc</div>
          <p class="text-[11px] text-slate-400">Giờ trống không đồng nghĩa với capacity đã xác nhận; vẫn cần đối chiếu lịch và tải đa dự án.</p>
          <div class="grid grid-cols-1 md:grid-cols-2 gap-2.5">
            <div v-for="impact in proposal.workloadImpacts" :key="impact.memberUserId" class="bg-slate-950 p-2.5 rounded-xl border text-[11px] space-y-1.5" :class="impact.isOverloaded ? 'border-rose-500/40' : 'border-emerald-500/30'">
              <div class="flex items-center justify-between gap-2 font-semibold"><span>{{ impact.memberName }}</span><span :class="impact.isOverloaded ? 'text-rose-400' : 'text-emerald-400'">{{ impact.currentWeeklyHours + impact.proposedAdditionalHours }}h</span></div>
              <div class="text-slate-400">{{ impact.currentRole }} · thêm {{ impact.proposedAdditionalHours }}h</div>
              <div class="text-slate-500">{{ impact.warningMessage }}</div>
            </div>
          </div>
        </div>
      </div>

      <div class="flex flex-col sm:flex-row items-center justify-between gap-3 pt-3 border-t border-slate-800 text-xs">
        <div class="text-slate-400 flex items-center gap-1"><RotateCcw class="w-3.5 h-3.5 text-purple-400" /> Có thể hoàn tác trong 72 giờ</div>
        <div class="flex items-center gap-3 w-full sm:w-auto justify-end">
          <button type="button" class="bg-slate-800 hover:bg-slate-700 text-slate-300 px-4 py-2 rounded-xl" @click="emit('close')">Đóng</button>
          <button v-if="isOwnerOrAuthorized" type="button" :disabled="isSubmitting || selectedTasks.length === 0" class="action-button" @click="approveSelected"><Check class="w-4 h-4" /> {{ isSubmitting ? 'Đang tạo...' : `Xác nhận tạo ${selectedTasks.length} task` }}</button>
          <button v-else type="button" :disabled="isSubmitting || selectedTasks.length === 0" class="action-button" @click="submitForReview"><Send class="w-4 h-4" /> {{ isSubmitting ? 'Đang gửi...' : 'Gửi người quản lý duyệt' }}</button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.proposal-input { min-width: 0; width: 100%; padding: 7px 8px; border: 1px solid rgba(148,163,184,.26); border-radius: 8px; color: #e2e8f0; background: rgba(15,23,42,.78); font: inherit; }
.field-label { display: grid; gap: 4px; color: #94a3b8; font-size: 10px; }
.action-button { display: inline-flex; align-items: center; gap: 6px; padding: 8px 18px; border-radius: 12px; color: white; font-weight: 700; background: linear-gradient(90deg,#7c3aed,#4f46e5); }
button:disabled { opacity: .5; cursor: not-allowed; }
</style>
