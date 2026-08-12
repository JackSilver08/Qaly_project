<script setup lang="ts">
import { ref } from 'vue'
import { Eye, X, ShieldAlert, UserCheck } from 'lucide-vue-next'
import { usePermissions } from '../composables/use-permissions'

const { isSimulationActive, simulatedUserName, stopSimulation, startSimulation } = usePermissions()

const mockMembers = [
  { id: 'usr-001', name: 'Nguyễn Văn A (Dev Frontend)', role: 'Dev Frontend' },
  { id: 'usr-002', name: 'Trần Thị B (Dev Backend)', role: 'Dev Backend' },
  { id: 'usr-003', name: 'Lê Văn C (QA / Lead Tester)', role: 'QA Lead' },
  { id: 'usr-004', name: 'Phạm Minh D (Member / Restricted)', role: 'Member' }
]

const selectedMemberId = ref('usr-002')

const onChangeMember = (e: Event) => {
  const targetId = (e.target as HTMLSelectElement).value
  const found = mockMembers.find(m => m.id === targetId)
  if (found) {
    startSimulation(found.id, found.name)
  }
}
</script>

<template>
  <div v-if="isSimulationActive" class="bg-gradient-to-r from-amber-600 via-purple-600 to-blue-600 p-0.5 shadow-xl sticky top-0 z-50">
    <div class="bg-slate-950 px-4 py-2 flex flex-wrap items-center justify-between gap-3 text-xs">
      <div class="flex items-center space-x-2.5">
        <span class="px-2 py-0.5 rounded bg-amber-500/20 text-amber-300 font-mono font-bold border border-amber-500/40 flex items-center gap-1.5 animate-pulse">
          <Eye class="w-3.5 h-3.5" />
          <span>SIMULATION MODE ACTIVE</span>
        </span>
        <span class="text-slate-300">
          Đang xem giao diện dưới danh nghĩa: <strong class="text-white font-semibold">{{ simulatedUserName }}</strong>
        </span>
      </div>

      <div class="flex items-center space-x-2">
        <span class="text-slate-400">Chuyển View:</span>
        <select
          v-model="selectedMemberId"
          @change="onChangeMember"
          class="bg-slate-900 border border-slate-700 text-slate-200 text-xs px-2.5 py-1 rounded-md focus:outline-none focus:border-amber-500"
        >
          <option v-for="m in mockMembers" :key="m.id" :value="m.id">
            {{ m.name }}
          </option>
        </select>

        <button
          @click="stopSimulation"
          class="bg-rose-600 hover:bg-rose-500 text-white font-semibold px-3 py-1 rounded-md transition flex items-center gap-1"
        >
          <X class="w-3.5 h-3.5" />
          <span>Thoát View-As</span>
        </button>
      </div>
    </div>
  </div>
</template>
