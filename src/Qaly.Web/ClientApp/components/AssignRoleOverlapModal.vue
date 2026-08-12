<script setup lang="ts">
import { AlertTriangle, Clock, X, Check } from 'lucide-vue-next'

const props = defineProps<{
  show: boolean
  memberName: string
  activeRoleName: string
  activeRoleStartDate: string
  newRoleName: string
  newRoleStartDate: string
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'confirm'): void
}>()
</script>

<template>
  <div v-if="show" class="fixed inset-0 z-50 bg-black/80 backdrop-blur-sm flex items-center justify-center p-4">
    <div class="glass-card bg-slate-900 border border-amber-500/40 rounded-2xl max-w-lg w-full p-6 space-y-5 shadow-2xl animate-in fade-in zoom-in duration-200">
      <div class="flex items-center justify-between border-b border-slate-800 pb-3">
        <div class="flex items-center gap-2 text-amber-400 font-bold text-base">
          <AlertTriangle class="w-5 h-5" />
          <span>⚠️ Cảnh Báo Trùng Lặp Giai Đoạn & Kết Thúc Role Cũ</span>
        </div>
        <button @click="emit('close')" class="text-slate-400 hover:text-white transition">
          <X class="w-5 h-5" />
        </button>
      </div>

      <div class="space-y-3 text-xs text-slate-300">
        <p class="leading-relaxed">
          Thành viên <strong class="text-white">{{ memberName }}</strong> hiện đang giữ role active là
          <span class="text-blue-400 font-semibold">[{{ activeRoleName }}]</span> kể từ ngày
          <span class="text-slate-200 font-mono">{{ activeRoleStartDate }}</span>.
        </p>

        <div class="bg-amber-500/10 border border-amber-500/30 rounded-xl p-3.5 space-y-2">
          <div class="font-semibold text-amber-300 flex items-center gap-1.5">
            <Clock class="w-4 h-4" />
            <span>Xác nhận tự động chốt ngày kết thúc:</span>
          </div>
          <p class="text-slate-300">
            Việc gán role mới <strong class="text-purple-400">[{{ newRoleName }}]</strong> từ ngày
            <span class="text-white font-mono font-bold">{{ newRoleStartDate }}</span> sẽ tự động chốt ngày kết thúc
            của role <span class="text-blue-300">[{{ activeRoleName }}]</span> tại ngày
            <span class="text-white font-mono font-bold">{{ newRoleStartDate }}</span>.
          </p>
          <p class="text-slate-400 text-[11px]">
            ✔️ Đảm bảo nguyên tắc <strong>1 Single Active Role</strong> tại một thời điểm.
          </p>
        </div>
      </div>

      <div class="flex items-center justify-end space-x-3 pt-2">
        <button
          @click="emit('close')"
          class="bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs px-4 py-2 rounded-lg transition"
        >
          Hủy Bỏ
        </button>
        <button
          @click="emit('confirm')"
          class="bg-gradient-to-r from-amber-600 to-purple-600 hover:opacity-90 text-white font-semibold text-xs px-4 py-2 rounded-lg transition shadow-md shadow-amber-500/20 flex items-center gap-1.5"
        >
          <Check class="w-4 h-4" />
          <span>Chấp Nhận Chuyển Role</span>
        </button>
      </div>
    </div>
  </div>
</template>
