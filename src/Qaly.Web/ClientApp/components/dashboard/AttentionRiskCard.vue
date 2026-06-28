<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { AlertTriangle } from 'lucide-vue-next'
import { apiJson } from '../../utils/api-client'
import type { AttentionSummaryDto } from '../../types'
import { Doughnut } from 'vue-chartjs'
import { Chart as ChartJS, ArcElement, Tooltip, Legend } from 'chart.js'
import { useRouter } from 'vue-router'

ChartJS.register(ArcElement, Tooltip, Legend)

const router = useRouter()
const isLoading = ref(true)
const data = ref<AttentionSummaryDto | null>(null)

onMounted(async () => {
  try {
    const res = await apiJson<AttentionSummaryDto>('/api/dashboard/attention-summary')
    data.value = res
  } catch(e) {
    console.error(e)
  } finally {
    isLoading.value = false
  }
})

const chartData = computed(() => {
  if (!data.value) return { labels: [], datasets: [] }
  
  if (data.value.totalAttentionItems === 0) {
    return {
      labels: ['An toàn'],
      datasets: [
        {
          backgroundColor: ['#10b981'],
          data: [1]
        }
      ]
    }
  }

  return {
    labels: ['Trễ hạn', 'Sắp đến hạn', 'Dự án rủi ro'],
    datasets: [
      {
        backgroundColor: ['#ef4444', '#f59e0b', '#1f80ff'],
        data: [
          data.value.overdueTasks,
          data.value.dueSoonTasks,
          data.value.riskProjects
        ]
      }
    ]
  }
})

const chartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  cutout: '70%',
  plugins: {
    legend: {
      display: false
    }
  }
}
</script>

<template>
  <section class="attention-card glass-card">
    <div class="attention-card-header" style="display: flex; align-items: center; gap: 8px; margin-bottom: 16px;">
      <AlertTriangle :size="18" style="color: #ef4444;" />
      <h3 style="font-weight: 700; color: #0f172a; margin: 0; font-size: 16px;">Cần chú ý</h3>
    </div>
    
    <div v-if="isLoading" class="attention-loading" style="padding: 20px; text-align: center; color: var(--text-muted)">
      Đang tải dữ liệu...
    </div>
    <div v-else-if="data" class="attention-content" style="display: flex; flex-direction: column; gap: 16px;">
      
      <div v-if="data.totalAttentionItems === 0" class="attention-safe" style="text-align: center;">
        <div style="height: 120px; position: relative; display: flex; justify-content: center; align-items: center; margin-bottom: 16px;">
          <Doughnut :data="chartData" :options="chartOptions" />
          <div style="position: absolute; text-align: center;">
            <span style="display: block; font-size: 24px; font-weight: 800; color: #10b981;">100%</span>
            <span style="font-size: 11px; color: #64748b;">An toàn</span>
          </div>
        </div>
        <p style="color: #10b981; font-weight: 600; font-size: 14px;">Hôm nay hệ thống ổn định</p>
      </div>

      <div v-else class="attention-risk">
        <div style="height: 140px; position: relative; display: flex; justify-content: center; align-items: center; margin-bottom: 16px;">
          <Doughnut :data="chartData" :options="chartOptions" />
          <div style="position: absolute; text-align: center;">
            <span style="display: block; font-size: 24px; font-weight: 800; color: #ef4444;">{{ data.totalAttentionItems }}</span>
            <span style="font-size: 11px; color: #64748b;">Vấn đề</span>
          </div>
        </div>

        <ul style="list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 8px;">
          <li v-if="data.overdueTasks > 0" style="display: flex; justify-content: space-between; font-size: 13px;">
            <span style="display: flex; align-items: center; gap: 6px; color: #64748b;">
              <span style="width: 8px; height: 8px; border-radius: 50%; background: #ef4444;"></span>
              Trễ hạn
            </span>
            <strong style="color: #0f172a;">{{ data.overdueTasks }} nhiệm vụ</strong>
          </li>
          <li v-if="data.dueSoonTasks > 0" style="display: flex; justify-content: space-between; font-size: 13px;">
            <span style="display: flex; align-items: center; gap: 6px; color: #64748b;">
              <span style="width: 8px; height: 8px; border-radius: 50%; background: #f59e0b;"></span>
              Sắp đến hạn
            </span>
            <strong style="color: #0f172a;">{{ data.dueSoonTasks }} nhiệm vụ</strong>
          </li>
          <li v-if="data.riskProjects > 0" style="display: flex; justify-content: space-between; font-size: 13px;">
            <span style="display: flex; align-items: center; gap: 6px; color: #64748b;">
              <span style="width: 8px; height: 8px; border-radius: 50%; background: #1f80ff;"></span>
              Dự án rủi ro
            </span>
            <strong style="color: #0f172a;">{{ data.riskProjects }} dự án</strong>
          </li>
        </ul>
      </div>

      <button 
        @click="router.push('/tasks')" 
        class="attention-view-all" 
        style="width: 100%; margin-top: 8px; padding: 10px; border-radius: var(--qaly-radius-lg); border: 1px solid rgba(15, 82, 186, 0.2); background: rgba(15, 82, 186, 0.05); color: #1f80ff; font-weight: 600; font-size: 13px; cursor: pointer; transition: all 0.2s;"
        onmouseover="this.style.background='rgba(15, 82, 186, 0.1)'"
        onmouseout="this.style.background='rgba(15, 82, 186, 0.05)'"
      >
        Xem Tất cả đầu việc
      </button>

    </div>
  </section>
</template>

<style scoped>
.attention-card {
  background: rgba(255, 255, 255, 0.9);
  backdrop-filter: none;
  border: 1px solid rgba(255, 255, 255, 0.5);
  border-radius: var(--qaly-radius-lg);
  padding: 20px;
  box-shadow: var(--qaly-shadow-md);
}
</style>
