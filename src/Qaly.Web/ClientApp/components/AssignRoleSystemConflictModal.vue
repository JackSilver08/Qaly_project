<script setup lang="ts">
import { ShieldAlert, X, Check } from 'lucide-vue-next'

const props = defineProps<{
  show: boolean
  memberName: string
  newRoleName: string
  systemRoleName: string
  systemAiTier: string
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'confirm'): void
}>()
</script>

<template>
  <div v-if="show" class="fixed inset-0 z-50 bg-black/80 backdrop-blur-sm flex items-center justify-center p-4">
    <div class="glass-card bg-slate-900 border border-rose-500/40 rounded-2xl max-w-lg w-full p-6 space-y-5 shadow-2xl animate-in fade-in zoom-in duration-200">
      <div class="flex items-center justify-between border-b border-slate-800 pb-3">
        <div class="flex items-center gap-2 text-rose-400 font-bold text-base">
          <ShieldAlert class="w-5 h-5" />
          <span>⚠️ Cảnh Báo Mâu Thuẫn Giới Hạn Tầng Hệ Thống</span>
        </div>
        <button @click="emit('close')" class="text-slate-400 hover:text-white transition">
          <X class="w-5 h-5" />
        </button>
      </div>

      <div class="space-y-3 text-xs text-slate-300">
        <p class="leading-relaxed">
          Bạn đang gán Project Role <strong class="text-purple-400">[{{ newRoleName }}]</strong> cho thành viên
          <strong class="text-white">{{ memberName }}</strong>.
        </p>

        <div class="bg-rose-500/10 border border-rose-500/30 rounded-xl p-3.5 space-y-2">
          <div class="font-semibold text-rose-300">Mâu thuẫn Explicit System Deny (Level 1):</div>
          <p class="text-slate-300">
            Project Role <span class="text-purple-300 font-bold">[{{ newRoleName }}]</span> có mặc định Full AI, nhưng
            <strong>System Role</strong> của user này là <span class="text-rose-400 font-bold">[{{ systemRoleName }} - {{ systemAiTier }}]</span>.
          </p>
          <p class="text-slate-400 text-[11px] leading-relaxed">
            Theo quy tắc phân quyền Level 1: <strong>Explicit System Deny luôn thắng</strong>. Các tính năng AI nâng cao làm biến đổi dữ liệu vẫn sẽ bị chặn đối với tài khoản này.
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
          class="bg-rose-600 hover:bg-rose-500 text-white font-semibold text-xs px-4 py-2 rounded-lg transition shadow-md shadow-rose-500/20 flex items-center gap-1.5"
        >
          <Check class="w-4 h-4" />
          <span>Vẫn Tiếp Tục Gán Role</span>
        </button>
      </div>
    </div>
  </div>
</template>
