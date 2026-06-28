<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { CircleAlert, Gauge, Loader2, UserRound } from 'lucide-vue-next'
import { apiResult } from '../utils/api-client'
import type { MemberWorkloadDto, ProjectWorkloadDto } from '../types'

const props = defineProps<{
  projectId: string
}>()

const isLoading = ref(true)
const workload = ref<ProjectWorkloadDto | null>(null)

async function loadWorkload() {
  isLoading.value = true
  try {
    workload.value = await apiResult<ProjectWorkloadDto>(`/api/projects/${props.projectId}/workload`)
  } catch (error) {
    console.error(error)
    workload.value = null
  } finally {
    isLoading.value = false
  }
}

watch(
  () => props.projectId,
  () => {
    void loadWorkload()
  },
  { immediate: true },
)

const members = computed(() => workload.value?.membersWorkload ?? [])
const totalTasks = computed(() => members.value.reduce((sum, item) => sum + item.taskCount, 0))
const totalEstimated = computed(() => members.value.reduce((sum, item) => sum + item.estimatedHours, 0))
const totalActual = computed(() => members.value.reduce((sum, item) => sum + item.actualHours, 0))
const workloadScores = computed(() => members.value.map((member) => memberWorkloadScore(member)))
const averageWorkloadScore = computed(() => {
  if (!workloadScores.value.length) return 0
  return Math.round((workloadScores.value.reduce((sum, score) => sum + score, 0) / workloadScores.value.length) * 10) / 10
})
const averageTasks = computed(() => {
  if (!members.value.length) return 0
  return Math.round((totalTasks.value / members.value.length) * 10) / 10
})

function memberWorkloadScore(member: MemberWorkloadDto) {
  return member.taskCount + member.estimatedHours / 8 + member.actualHours / 10
}

function isOverloaded(member: MemberWorkloadDto) {
  const score = memberWorkloadScore(member)
  return score >= Math.max(5, averageWorkloadScore.value * 1.45)
}

function loadPercent(member: MemberWorkloadDto) {
  const max = Math.max(1, ...workloadScores.value)
  return Math.round((memberWorkloadScore(member) / max) * 100)
}

function statusLabel(member: MemberWorkloadDto) {
  if (member.taskCount === 0) return 'Nhàn'
  if (isOverloaded(member)) return 'Quá tải'
  if (member.completedTaskCount >= member.taskCount && member.taskCount > 0) return 'Đang gỡ'
  if (member.taskCount >= averageTasks.value) return 'Bận'
  return 'Ổn định'
}
</script>

<template>
  <section class="workload-tab glass-card reveal">
    <div class="workload-hero">
      <div>
        <div class="eyebrow"><Gauge :size="14" /> Kế hoạch phân bổ nguồn lực</div>
        <h2>Workload theo thành viên</h2>
        <p>Theo dõi số task đang mở, ước lượng giờ và mức tải để phân công công việc hợp lý hơn.</p>
      </div>

      <div class="workload-stats">
        <article>
          <span>Tổng task</span>
          <strong>{{ totalTasks }}</strong>
        </article>
        <article>
          <span>Giờ ước lượng</span>
          <strong>{{ totalEstimated }}</strong>
        </article>
        <article>
          <span>Giờ thực tế</span>
          <strong>{{ totalActual }}</strong>
        </article>
      </div>
    </div>

    <div v-if="isLoading" class="workload-empty">
      <Loader2 :size="20" class="is-spinning" />
      <strong>Đang tải workload</strong>
    </div>

    <div v-else-if="members.length === 0" class="workload-empty">
      <CircleAlert :size="20" />
      <strong>Chưa có dữ liệu workload</strong>
      <p>Hãy gán task cho thành viên để hệ thống tính capacity.</p>
    </div>

    <div v-else class="workload-grid">
      <article
        v-for="member in members"
        :key="member.userId"
        class="workload-card"
        :class="{ 'is-overloaded': isOverloaded(member) }"
      >
        <div class="workload-card__top">
          <div class="workload-avatar">
            <UserRound :size="16" />
          </div>
          <div class="workload-title">
            <strong>{{ member.userName }}</strong>
            <span>{{ statusLabel(member) }}</span>
          </div>
          <div class="workload-count">{{ member.taskCount }} task</div>
        </div>

        <div class="workload-bar" :class="{ 'is-overloaded': isOverloaded(member) }">
          <span :style="{ width: `${loadPercent(member)}%` }"></span>
        </div>

        <div class="workload-meta">
          <span>Hoàn thành: <strong>{{ member.completedTaskCount }}</strong></span>
          <span>Ước lượng: <strong>{{ member.estimatedHours }}h</strong></span>
          <span>Thực tế: <strong>{{ member.actualHours }}h</strong></span>
        </div>
      </article>
    </div>
  </section>
</template>

<style scoped>
.workload-tab {
  padding: 24px;
  display: grid;
  gap: 18px;
  border: 1px solid var(--line);
  border-radius: var(--radius-shell);
  background: var(--panel);
}

.workload-hero {
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

.workload-hero h2 {
  margin: 8px 0 6px;
  color: var(--text-strong);
  font-size: 24px;
  font-weight: 850;
}

.workload-hero p {
  max-width: 68ch;
  color: var(--muted);
}

.workload-stats {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 10px;
  min-width: 340px;
}

.workload-stats article {
  min-width: 0;
  padding: 14px;
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
  background: var(--bg-soft);
}

.workload-stats span,
.workload-title span,
.workload-meta {
  color: var(--muted);
  font-size: 11px;
  font-weight: 700;
}

.workload-stats strong {
  color: var(--text-strong);
  font-size: 20px;
  font-weight: 900;
}

.workload-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.workload-card {
  padding: 16px;
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
  background: white;
  transition: border-color 0.2s ease, box-shadow 0.2s ease, transform 0.2s ease;
}

.workload-card.is-overloaded {
  border-color: rgba(239, 68, 68, 0.45);
  box-shadow: var(--qaly-shadow-md);
  background: linear-gradient(180deg, rgba(255, 241, 241, 0.95), #ffffff 58%);
}

.workload-card__top {
  display: grid;
  grid-template-columns: 38px minmax(0, 1fr) auto;
  gap: 12px;
  align-items: center;
}

.workload-avatar {
  width: 38px;
  height: 38px;
  border-radius: var(--qaly-radius-lg);
  display: grid;
  place-items: center;
  color: white;
  background: var(--primary);
}

.workload-title {
  display: grid;
  gap: 3px;
}

.workload-title strong {
  color: var(--text-strong);
  font-size: 15px;
  font-weight: 850;
}

.workload-count {
  color: var(--primary);
  font-size: 12px;
  font-weight: 900;
}

.workload-bar {
  height: 8px;
  overflow: hidden;
  margin: 14px 0 10px;
  border-radius: 999px;
  background: var(--bg-soft);
}

.workload-bar span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: var(--primary);
}

.workload-bar.is-overloaded span {
  background: linear-gradient(90deg, #ef4444, #fb7185);
}

.workload-card.is-overloaded .workload-count,
.workload-card.is-overloaded .workload-title span {
  color: #dc2626;
}

.workload-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.workload-meta strong {
  color: var(--text-strong);
}

.workload-empty {
  min-height: 160px;
  display: grid;
  place-items: center;
  gap: 8px;
  padding: 18px;
  border: 1px dashed var(--line);
  border-radius: var(--qaly-radius-lg);
  color: var(--muted);
  text-align: center;
  background: var(--bg-soft);
}

.is-spinning {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

@media (max-width: 900px) {
  .workload-hero {
    flex-direction: column;
  }

  .workload-stats,
  .workload-grid {
    grid-template-columns: 1fr;
    min-width: 0;
  }
}
</style>
