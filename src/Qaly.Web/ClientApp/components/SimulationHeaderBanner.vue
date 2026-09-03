<script setup lang="ts">
import { ref } from 'vue'
import { Eye, X } from 'lucide-vue-next'
import { usePermissions } from '../composables/use-permissions'

const { isSimulationActive, simulatedUserName, stopSimulation, startSimulation } = usePermissions()

const props = defineProps<{
  canStart: boolean
  users: Array<{ id: string; fullName: string; role: string }>
}>()

const selectedMemberId = ref('')

const onChangeMember = (e: Event) => {
  const targetId = (e.target as HTMLSelectElement).value
  const found = props.users.find(user => user.id === targetId)
  if (found) {
    startSimulation(found.id, `${found.fullName} (${found.role})`)
    window.location.reload()
  }
}

const exitSimulation = () => {
  stopSimulation()
  window.location.reload()
}
</script>

<template>
  <div v-if="isSimulationActive" class="bg-gradient-to-r from-amber-600 via-purple-600 to-blue-600 p-0.5 shadow-xl sticky top-0 z-50">
    <div class="bg-slate-950 px-4 py-2 flex flex-wrap items-center justify-between gap-3 text-xs">
      <div class="flex items-center space-x-2.5">
        <span class="px-2 py-0.5 rounded bg-amber-500/20 text-amber-300 font-mono font-bold border border-amber-500/40 flex items-center gap-1.5 animate-pulse">
          <Eye class="w-3.5 h-3.5" />
          <span>VIEW-AS · CHỈ ĐỌC</span>
        </span>
        <span class="text-slate-300">
          Đang xem giao diện dưới danh nghĩa: <strong class="text-white font-semibold">{{ simulatedUserName }}</strong>
        </span>
      </div>

      <div class="flex items-center space-x-2">
        <button type="button"
          @click="exitSimulation"
          class="bg-rose-600 hover:bg-rose-500 text-white font-semibold px-3 py-1 rounded-md transition flex items-center gap-1"
        >
          <X class="w-3.5 h-3.5" />
          <span>Thoát View-As</span>
        </button>
      </div>
    </div>
  </div>
  <div v-else-if="canStart && users.length" class="simulation-launcher">
    <label for="qaly-view-as-user">Kiểm tra giao diện theo người dùng</label>
    <select
      id="qaly-view-as-user"
      v-model="selectedMemberId"
      @change="onChangeMember"
    >
      <option value="" disabled>Chọn người dùng thật…</option>
      <option v-for="user in users" :key="user.id" :value="user.id">
        {{ user.fullName }} · {{ user.role }}
      </option>
    </select>
    <span>Chỉ đọc; mọi thao tác thay đổi dữ liệu sẽ bị máy chủ chặn.</span>
  </div>
</template>

<style scoped>
.simulation-launcher {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 10px;
  min-height: 36px;
  padding: 5px 18px;
  border-bottom: 1px solid #f6d98b;
  background: #fff9e8;
  color: #6b4f12;
  font-size: 12px;
}

.simulation-launcher label { font-weight: 700; }
.simulation-launcher select {
  max-width: 320px;
  padding: 5px 9px;
  border: 1px solid #d9bd69;
  border-radius: 7px;
  background: white;
  color: #29364a;
}

@media (max-width: 760px) {
  .simulation-launcher { align-items: stretch; flex-direction: column; }
  .simulation-launcher select { max-width: none; }
}
</style>
