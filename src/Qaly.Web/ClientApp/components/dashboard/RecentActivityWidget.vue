<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { Activity } from 'lucide-vue-next'
import { apiJson } from '../../utils/api-client'
import type { RecentActivitiesResponseDto } from '../../types'
import { formatTimeAgo } from '../../utils/formatters'

const isLoading = ref(true)
const data = ref<RecentActivitiesResponseDto | null>(null)

onMounted(async () => {
  try {
    const res = await apiJson<RecentActivitiesResponseDto>('/api/dashboard/recent-activities')
    data.value = res
  } catch(e) {
    console.error(e)
  } finally {
    isLoading.value = false
  }
})

const maxActivityCount = computed(() => {
  if (!data.value || !data.value.activityByDay) return 10
  const max = Math.max(...data.value.activityByDay.map(d => d.count))
  return max > 0 ? max : 10
})

const getSparklineHeight = (count: number) => {
  return `${(count / maxActivityCount.value) * 100}%`
}
</script>

<template>
  <section class="activity-card glass-card">
    <div class="activity-card-header" style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px;">
      <div style="display: flex; align-items: center; gap: 8px;">
        <Activity :size="18" style="color: #1f80ff;" />
        <h3 style="font-weight: 700; color: #0f172a; margin: 0; font-size: 16px;">Hoạt động gần đây</h3>
      </div>
    </div>
    
    <div v-if="isLoading" style="padding: 20px; text-align: center; color: var(--text-muted)">
      Đang tải dữ liệu...
    </div>
    <div v-else-if="data" style="display: flex; flex-direction: column; gap: 20px;">
      
      <!-- Mini Sparkline (7 days) -->
      <div class="activity-sparkline" style="background: rgba(15, 82, 186, 0.03); border-radius: var(--qaly-radius-lg); padding: 12px;">
        <div style="display: flex; justify-content: space-between; align-items: flex-end; height: 48px; gap: 4px;">
          <div 
            v-for="(day, idx) in data.activityByDay" 
            :key="idx" 
            style="flex: 1; display: flex; flex-direction: column; justify-content: flex-end; align-items: center;"
            :title="day.date + ': ' + day.count + ' hoạt động'"
          >
            <div 
              style="width: 100%; background: #1f80ff; border-radius: 4px; min-height: 4px; transition: height 0.5s ease;"
              :style="{ height: getSparklineHeight(day.count), opacity: idx === data.activityByDay.length - 1 ? 1 : 0.6 }"
            ></div>
          </div>
        </div>
        <div style="display: flex; justify-content: space-between; margin-top: 8px; font-size: 11px; color: #64748b; font-weight: 600;">
          <span>7 ngày trước</span>
          <span>Hôm nay ({{ data.todayCount }})</span>
        </div>
      </div>

      <!-- Latest Activities Timeline -->
      <div class="activity-list" style="display: flex; flex-direction: column; gap: 16px;">
        <div v-if="data.latestActivities.length === 0" style="text-align: center; color: #94a3b8; font-size: 13px;">
          Chưa có hoạt động nào gần đây
        </div>
        
        <article 
          v-else
          v-for="(activity, index) in data.latestActivities" 
          :key="index" 
          style="display: flex; gap: 12px; position: relative;"
        >
          <!-- Timeline line -->
          <div v-if="index !== data.latestActivities.length - 1" style="position: absolute; left: 15px; top: 32px; bottom: -16px; width: 2px; background: #e2e8f0;"></div>
          
          <div style="width: 32px; height: 32px; border-radius: 50%; background: rgba(15, 82, 186, 0.1); color: #1f80ff; display: flex; align-items: center; justify-content: center; font-weight: 700; font-size: 12px; flex-shrink: 0; z-index: 1;">
            {{ activity.actorName.substring(0, 2).toUpperCase() }}
          </div>
          
          <div style="display: flex; flex-direction: column; gap: 2px;">
            <div style="font-size: 13px; color: #0f172a; line-height: 1.4;">
              <strong style="color: #1f80ff;">{{ activity.actorName }}</strong> 
              {{ activity.title }}
            </div>
            <div style="font-size: 11px; color: #94a3b8; font-weight: 500;">
              {{ formatTimeAgo(activity.createdAt) }}
            </div>
          </div>
        </article>
      </div>

    </div>
  </section>
</template>

<style scoped>
.activity-card {
  background: rgba(255, 255, 255, 0.9);
  backdrop-filter: none;
  border: 1px solid rgba(255, 255, 255, 0.5);
  border-radius: var(--qaly-radius-lg);
  padding: 20px;
  box-shadow: var(--qaly-shadow-md);
}
</style>
