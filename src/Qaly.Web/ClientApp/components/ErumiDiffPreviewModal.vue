<script setup lang="ts">
import { ref, watch } from 'vue'
import { Sparkles, Check, X, RotateCcw, UserCheck } from 'lucide-vue-next'
import type { ErumiRoadmapDiffProposalDto, ErumiTaskProposalDto } from '../types'

const props = defineProps<{
  show: boolean
  proposal: ErumiRoadmapDiffProposalDto | null
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'approve', tasks: ErumiTaskProposalDto[]): void
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

function approveSelected() {
  emit('approve', editableTasks.value.filter(task => task.selected).map(({ selected: _selected, ...task }) => task))
}
</script>

<template>
  <div v-if="show && proposal" class="fixed inset-0 z-50 bg-black/80 backdrop-blur-sm flex items-center justify-center p-4">
    <div class="glass-card bg-slate-900 border border-purple-500/50 rounded-2xl max-w-4xl w-full max-h-[92vh] overflow-y-auto p-6 space-y-5 shadow-2xl animate-in fade-in zoom-in duration-200">
      <div class="flex items-center justify-between border-b border-slate-800 pb-3">
        <div class="flex items-center gap-2 text-purple-400 font-bold text-base">
          <Sparkles class="w-5 h-5" />
          <span>Phương án thay đổi Roadmap</span>
        </div>
        <button @click="emit('close')" class="text-slate-400 hover:text-white transition">
          <X class="w-5 h-5" />
        </button>
      </div>

      <div class="space-y-4 text-xs">
        <div class="bg-purple-500/10 border border-purple-500/30 p-3 rounded-xl flex items-center justify-between">
          <div>
            <div class="text-white font-bold text-sm">{{ proposal.phaseName }}</div>
            <div class="text-purple-300 text-[11px]">{{ proposal.summary }}</div>
          </div>
          <span class="bg-purple-500/20 text-purple-300 px-2.5 py-1 rounded-full border border-purple-500/40 font-mono text-[10px]">
            Chưa ghi dữ liệu
          </span>
        </div>

        <!-- Proposed Tasks Grid -->
        <div class="space-y-2">
          <div class="text-emerald-400 font-bold flex items-center gap-1.5">
            <Check class="w-4 h-4" />
            <span>{{ editableTasks.filter(task => task.selected).length }} task được chọn để tạo:</span>
          </div>

          <div class="space-y-2 max-h-48 overflow-y-auto pr-1">
            <div
              v-for="task in editableTasks"
              :key="task.title"
              class="bg-slate-950 p-3 rounded-xl border border-slate-800 space-y-1"
            >
              <div class="flex items-center gap-2 text-white font-semibold">
                <input v-model="task.selected" type="checkbox" aria-label="Chọn task" />
                <input v-model="task.title" class="proposal-input flex-1" maxlength="200" />
              </div>
              <textarea v-model="task.description" class="proposal-input w-full" rows="2" maxlength="1000" />
              <div class="grid grid-cols-2 gap-2">
                <select v-model="task.priority" class="proposal-input">
                  <option v-for="priority in ['Low', 'Medium', 'High', 'Critical']" :key="priority" :value="priority">{{ priority }}</option>
                </select>
                <input v-model.number="task.estimatedHours" class="proposal-input" type="number" min="1" max="80" />
              </div>
              <div class="text-[10px] text-slate-500 flex items-center gap-1 pt-1">
                <UserCheck class="w-3 h-3 text-purple-400" />
                <span>Gợi ý: <strong class="text-slate-300">{{ task.recommendedAssigneeName || 'Chưa gán' }}</strong> ({{ task.recommendedRole }})</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Workload Impact Analysis -->
        <div class="space-y-2">
          <div class="text-blue-400 font-bold">Ảnh hưởng tải công việc (cần đối chiếu lịch trước khi giao):</div>
          <div class="grid grid-cols-1 md:grid-cols-2 gap-2">
            <div
              v-for="impact in proposal.workloadImpacts"
              :key="impact.memberUserId"
              class="bg-slate-950 p-2.5 rounded-lg border text-[11px] space-y-1"
              :class="impact.isOverloaded ? 'border-rose-500/40 bg-rose-500/5' : 'border-emerald-500/30 bg-emerald-500/5'"
            >
              <div class="flex items-center justify-between font-semibold">
                <span class="text-white">{{ impact.memberName }}</span>
                <span :class="impact.isOverloaded ? 'text-rose-400 font-bold' : 'text-emerald-400'">
                  {{ impact.warningMessage }}
                </span>
              </div>
              <div class="text-slate-400">
                Vai trò: {{ impact.currentRole }} • Tải mới thêm: +{{ impact.proposedAdditionalHours }}h
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="flex items-center justify-between pt-3 border-t border-slate-800 text-xs">
        <div class="text-slate-400 flex items-center gap-1">
          <RotateCcw class="w-3.5 h-3.5 text-purple-400" />
          <span>Hỗ trợ 1-Click Rollback / Undo trong 24h</span>
        </div>

        <div class="flex items-center space-x-3">
          <button
            @click="emit('close')"
            class="bg-slate-800 hover:bg-slate-700 text-slate-300 px-4 py-2 rounded-lg transition"
          >
            Hủy Bỏ
          </button>
          <button
            :disabled="!editableTasks.some(task => task.selected && task.title.trim())"
            @click="approveSelected"
            class="bg-gradient-to-r from-purple-600 to-indigo-600 hover:opacity-90 text-white font-bold px-5 py-2 rounded-lg transition shadow-md shadow-purple-500/30 flex items-center gap-1.5"
          >
            <Check class="w-4 h-4" />
            <span>Xác nhận tạo Sprint và task</span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.proposal-input {
  min-width: 0;
  padding: 7px 8px;
  border: 1px solid rgba(148, 163, 184, 0.26);
  border-radius: 8px;
  color: #e2e8f0;
  background: rgba(15, 23, 42, 0.78);
  font: inherit;
}

button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
