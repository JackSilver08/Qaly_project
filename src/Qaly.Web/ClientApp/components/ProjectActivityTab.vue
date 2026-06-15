<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Activity, CalendarDays, Clock3, Loader2, Newspaper } from 'lucide-vue-next'
import { apiJson } from '../utils/api-client'
import type { RecentActivitiesResponseDto } from '../types'
import { formatTimeAgo } from '../utils/formatters'

const props = defineProps<{
  projectId: string
  projectName: string
}>()

const isLoading = ref(true)
const data = ref<RecentActivitiesResponseDto | null>(null)

onMounted(async () => {
  isLoading.value = true
  try {
    data.value = await apiJson<RecentActivitiesResponseDto>('/api/dashboard/recent-activities')
  } catch (error) {
    console.error(error)
    data.value = null
  } finally {
    isLoading.value = false
  }
})

const projectActivities = computed(() => {
  const items = data.value?.latestActivities ?? []
  return items.filter((activity) => activity.projectId === props.projectId)
})

const projectSparkline = computed(() => {
  const today = new Date()
  const days = data.value?.activityByDay ?? []
  return days.map((day) => ({
    ...day,
    isToday: day.date === today.toISOString().slice(0, 10),
  }))
})

const maxActivityCount = computed(() => {
  if (!projectSparkline.value.length) return 10
  const max = Math.max(...projectSparkline.value.map((day) => day.count))
  return max > 0 ? max : 10
})

function sparklineHeight(count: number) {
  return `${Math.max(8, Math.round((count / maxActivityCount.value) * 100))}%`
}
</script>

<template>
  <section class="activity-tab glass-card reveal">
    <div class="activity-tab__header">
      <div>
        <div class="eyebrow"><Activity :size="14" /> Dòng thời gian hoạt động</div>
        <h2>Nhịp hoạt động của dự án</h2>
        <p>Dòng thời gian bên dưới chỉ hiển thị hoạt động liên quan đến dự án <strong>{{ projectName }}</strong>. Biểu đồ 7 ngày phản ánh nhịp hoạt động của không gian làm việc.</p>
      </div>
      <div class="activity-tab__stats">
        <article>
          <span>7 ngày</span>
          <strong>{{ data?.weekCount ?? 0 }}</strong>
        </article>
        <article>
          <span>Hôm nay</span>
          <strong>{{ data?.todayCount ?? 0 }}</strong>
        </article>
      </div>
    </div>

    <div v-if="isLoading" class="activity-empty">
      <Loader2 :size="20" class="is-spinning" />
      <strong>Đang tải hoạt động</strong>
    </div>

    <div v-else class="activity-tab__body">
      <div class="activity-sparkline glass-card">
        <div class="sparkline-grid">
          <div
            v-for="day in projectSparkline"
            :key="day.date"
            class="sparkline-bar-wrap"
            :title="`${day.date}: ${day.count} hoạt động`"
          >
            <div
              class="sparkline-bar"
              :class="{ 'is-today': day.isToday }"
              :style="{ height: sparklineHeight(day.count) }"
            ></div>
          </div>
        </div>
        <div class="sparkline-meta">
          <span>7 ngày gần đây</span>
          <span>Hôm nay: {{ data?.todayCount ?? 0 }}</span>
        </div>
      </div>

      <div class="activity-feed">
        <div class="activity-feed__header">
          <CalendarDays :size="16" />
          <strong>Lịch sử gần đây</strong>
          <span>{{ projectActivities.length }} mục</span>
        </div>

        <article v-for="activity in projectActivities.slice(0, 6)" :key="`${activity.createdAt}-${activity.title}`" class="activity-item">
          <div class="activity-item__icon">
            <Newspaper :size="14" />
          </div>
          <div class="activity-item__body">
            <strong>{{ activity.actorName }}</strong>
            <p>{{ activity.title }}</p>
            <small>{{ formatTimeAgo(activity.createdAt) }}</small>
          </div>
        </article>

        <div v-if="projectActivities.length === 0" class="activity-empty activity-empty--compact">
          <Clock3 :size="18" />
          <strong>Chưa có hoạt động riêng cho dự án này</strong>
          <p>Hãy tạo nhiệm vụ, bình luận hoặc cập nhật tiến độ để dòng thời gian xuất hiện ở đây.</p>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
.activity-tab {
  padding: 24px;
  display: grid;
  gap: 18px;
  border: 1px solid var(--line);
  border-radius: var(--radius-shell);
  background: var(--panel);
}

.activity-tab__header {
  display: flex;
  justify-content: space-between;
  gap: 18px;
  align-items: flex-start;
}

.eyebrow {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  color: var(--primary);
  font-size: 11px;
  font-weight: 900;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.activity-tab__header h2 {
  margin: 8px 0 6px;
  color: var(--text-strong);
  font-size: 24px;
  font-weight: 850;
}

.activity-tab__header p {
  max-width: 68ch;
  color: var(--muted);
}

.activity-tab__stats {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
  min-width: 220px;
}

.activity-tab__stats article {
  padding: 14px;
  border: 1px solid var(--line);
  border-radius: 16px;
  background: var(--bg-soft);
}

.activity-tab__stats span {
  color: var(--muted);
  font-size: 11px;
  font-weight: 700;
}

.activity-tab__stats strong {
  display: block;
  color: var(--text-strong);
  font-size: 20px;
  font-weight: 900;
}

.activity-tab__body {
  display: grid;
  grid-template-columns: minmax(220px, 280px) minmax(0, 1fr);
  gap: 18px;
}

.activity-sparkline {
  padding: 16px;
  border: 1px solid var(--line);
  border-radius: 18px;
  background: var(--bg-soft);
}

.sparkline-grid {
  height: 180px;
  display: grid;
  grid-template-columns: repeat(7, minmax(0, 1fr));
  align-items: end;
  gap: 8px;
}

.sparkline-bar-wrap {
  height: 100%;
  display: flex;
  align-items: end;
}

.sparkline-bar {
  width: 100%;
  min-height: 8px;
  border-radius: 12px 12px 6px 6px;
  background: linear-gradient(180deg, #38bdf8, #0f4cff);
  opacity: 0.72;
}

.sparkline-bar.is-today {
  opacity: 1;
  box-shadow: 0 0 0 1px rgba(15, 76, 255, 0.12);
}

.sparkline-meta {
  display: flex;
  justify-content: space-between;
  margin-top: 10px;
  color: var(--muted);
  font-size: 11px;
  font-weight: 700;
}

.activity-feed {
  display: grid;
  gap: 10px;
}

.activity-feed__header {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--text-strong);
}

.activity-feed__header span {
  margin-left: auto;
  color: var(--muted);
  font-size: 11px;
  font-weight: 700;
}

.activity-item {
  display: grid;
  grid-template-columns: 32px minmax(0, 1fr);
  gap: 12px;
  padding: 14px;
  border: 1px solid var(--line);
  border-radius: 16px;
  background: white;
}

.activity-item__icon {
  width: 32px;
  height: 32px;
  display: grid;
  place-items: center;
  border-radius: 999px;
  color: var(--primary);
  background: rgba(239, 246, 255, 0.9);
}

.activity-item__body strong {
  color: var(--text-strong);
  font-size: 13px;
}

.activity-item__body p {
  margin: 4px 0;
  color: var(--text);
}

.activity-item__body small {
  color: var(--muted);
  font-size: 11px;
}

.activity-empty {
  min-height: 160px;
  display: grid;
  place-items: center;
  gap: 8px;
  padding: 18px;
  border: 1px dashed var(--line);
  border-radius: 18px;
  color: var(--muted);
  text-align: center;
  background: var(--bg-soft);
}

.activity-empty--compact {
  min-height: 220px;
}

.is-spinning {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

@media (max-width: 900px) {
  .activity-tab__header,
  .activity-tab__body {
    grid-template-columns: 1fr;
    display: grid;
  }

  .activity-tab__stats {
    min-width: 0;
  }
}
</style>
