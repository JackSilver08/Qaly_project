<script setup lang="ts">
import { ref, watch } from 'vue'
import { Clock, History, Shield, X, UserCheck, Calendar } from 'lucide-vue-next'
import { ApiError, apiResult } from '../utils/api-client'
import { showError } from '../composables/use-toast'

interface RoleHistoryItem {
  id: string
  roleName: string
  roleColorCode: string
  phaseName?: string
  startDate: string
  endDate?: string
  isActive: boolean
  reasonOrNote?: string
  assignedByUserName: string
}

const props = defineProps<{
  show: boolean
  projectId: string
  memberId: string
  memberName: string
}>()

const emit = defineEmits<{
  (e: 'close'): void
}>()

const histories = ref<RoleHistoryItem[]>([])
const isLoading = ref(false)

const loadRoleHistory = async () => {
  if (!props.projectId || !props.memberId) return
  isLoading.value = true
  try {
    histories.value = await apiResult<RoleHistoryItem[]>(`/api/ProjectRoles/projects/${props.projectId}/members/${props.memberId}/history`)
  } catch (error) {
    if (error instanceof ApiError && error.status === 403) {
      showError('Bảo mật dữ liệu nhân sự: Chỉ bản thân thành viên và Ban quản lý mới được xem lịch sử vai trò.')
      emit('close')
    } else {
      showError('Không tải được lịch sử vai trò.')
    }
  } finally {
    isLoading.value = false
  }
}

watch(() => props.show, (val) => {
  if (val) loadRoleHistory()
})
</script>

<template>
  <div v-if="show" class="fixed inset-0 z-50 bg-black/80 backdrop-blur-sm flex items-center justify-center p-4">
    <div class="glass-card bg-slate-900 border border-purple-500/40 rounded-2xl max-w-xl w-full p-6 space-y-5 shadow-2xl animate-in fade-in zoom-in duration-200">
      <div class="flex items-center justify-between border-b border-slate-800 pb-3">
        <div class="flex items-center gap-2 text-purple-400 font-bold text-base">
          <History class="w-5 h-5" />
          <span>📜 Lịch Sử Vai Trò Dự Án • {{ memberName }}</span>
        </div>
        <button @click="emit('close')" class="text-slate-400 hover:text-white transition">
          <X class="w-5 h-5" />
        </button>
      </div>

      <div v-if="isLoading" class="py-8 text-center text-slate-400 text-xs flex items-center justify-center gap-2">
        <Clock class="w-4 h-4 animate-spin" />
        <span>Đang nạp lịch sử vai trò...</span>
      </div>

      <div v-else-if="histories.length === 0" class="py-8 text-center text-slate-400 text-xs">
        Chưa có dữ liệu lịch sử vai trò cho thành viên này.
      </div>

      <div v-else class="space-y-4 max-h-[400px] overflow-y-auto pr-2 text-xs">
        <div class="relative border-l-2 border-slate-800 pl-4 space-y-6">
          <div v-for="item in histories" :key="item.id" class="relative">
            <!-- Timeline dot -->
            <span
              class="absolute -left-[21px] top-1 w-2.5 h-2.5 rounded-full ring-4 ring-slate-900"
              :class="item.isActive ? 'bg-emerald-400' : 'bg-slate-600'"
            ></span>

            <div class="space-y-1">
              <div class="flex items-center justify-between">
                <span class="text-xs font-semibold" :class="item.isActive ? 'text-emerald-400' : 'text-slate-400'">
                  {{ item.phaseName || (item.isActive ? 'Giai đoạn Hiện tại (ACTIVE)' : 'Giai đoạn Đã đóng') }}
                </span>
                <span
                  v-if="item.isActive"
                  class="bg-emerald-500/20 text-emerald-300 border border-emerald-500/40 text-[10px] px-2 py-0.5 rounded-full font-mono font-bold"
                >
                  Single Active Role
                </span>
              </div>

              <div class="text-sm font-bold text-white flex items-center gap-2">
                <span
                  class="inline-block w-3 h-3 rounded-full"
                  :style="{ backgroundColor: item.roleColorCode || '#8B5CF6' }"
                ></span>
                <span>{{ item.roleName }}</span>
              </div>

              <div class="text-slate-400 text-[11px] flex items-center gap-1.5">
                <Calendar class="w-3.5 h-3.5 text-slate-500" />
                <span>
                  {{ new Date(item.startDate).toLocaleDateString('vi-VN') }}
                  -
                  {{ item.endDate ? new Date(item.endDate).toLocaleDateString('vi-VN') : 'Hiện tại' }}
                </span>
              </div>

              <div v-if="item.reasonOrNote" class="bg-slate-950 p-2.5 rounded-lg border border-slate-800 text-slate-300 italic">
                "{{ item.reasonOrNote }}"
              </div>

              <div class="text-[10px] text-slate-500 flex items-center gap-1 pt-0.5">
                <UserCheck class="w-3 h-3" />
                <span>Gán bởi: {{ item.assignedByUserName }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="flex justify-end pt-2 border-t border-slate-800">
        <button
          @click="emit('close')"
          class="bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs px-4 py-2 rounded-lg transition"
        >
          Đóng
        </button>
      </div>
    </div>
  </div>
</template>
