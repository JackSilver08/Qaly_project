<script setup lang="ts">
import { computed, ref, onMounted, type Ref } from 'vue'
import { Plus, AlertTriangle, TrendingUp, CheckCircle2, Activity, ChevronRight, LayoutDashboard, FolderKanban, ClipboardList, Users, BrainCircuit } from 'lucide-vue-next'
import { useRouter } from 'vue-router'
import { useDashboardContext } from '../composables/dashboard-context'
import { apiJson } from '../utils/api-client'
import { formatTimeAgo } from '../utils/formatters'
import AttentionRiskCard from '../components/dashboard/AttentionRiskCard.vue'
import RecentActivityWidget from '../components/dashboard/RecentActivityWidget.vue'
import StrategicOverviewAI from '../components/dashboard/StrategicOverviewAI.vue'
import type { DashboardMember, DashboardProject, DashboardTask } from '../types'

const router = useRouter()

const {
  projects,
  team,
  openCreateProject,
  selectProject,
  openChatWithPrompt,
} = useDashboardContext() as {
  projects: Ref<DashboardProject[]>
  team: Ref<DashboardMember[]>
  openCreateProject: () => void
  selectProject: (projectId: string) => void
  openChatWithPrompt: (prompt?: string) => void
}

// Time period for chart
const activeTab = ref('Q4')
const chartMode = ref<'2D' | '3D'>('2D')

onMounted(async () => {
  // components load their own data
})

// Classify projects based on overdue task count
const onTrackCount = computed(() => {
  return projects.value.filter(p => p.status !== 'Archived' && p.overdueTaskCount === 0).length
})

const atRiskCount = computed(() => {
  return projects.value.filter(p => p.status !== 'Archived' && p.overdueTaskCount > 0 && p.overdueTaskCount <= 1).length
})

const delayedCount = computed(() => {
  return projects.value.filter(p => p.status !== 'Archived' && p.overdueTaskCount > 1).length
})

const activeProjectsCount = computed(() => {
  return projects.value.filter(p => p.status !== 'Archived').length
})

const totalTasksCount = computed(() => {
  return projects.value.reduce((sum, p) => sum + (p.taskCount || 0), 0)
})

const allTasks = computed<DashboardTask[]>(() => projects.value.flatMap(project => project.tasks || []))
const openTasksCount = computed(() => allTasks.value.filter(task => !['done', 'cancelled'].includes(task.status.toLowerCase())).length)
const overdueTasksCount = computed(() => allTasks.value.filter(task => {
  if (!task.dueDate || ['done', 'cancelled'].includes(task.status.toLowerCase())) return false
  return new Date(task.dueDate).getTime() < Date.now()
}).length)
const newProjectsThisMonth = computed(() => {
  const now = new Date()
  return projects.value.filter(project => {
    const createdAt = new Date(project.createdAt)
    return createdAt.getFullYear() === now.getFullYear() && createdAt.getMonth() === now.getMonth()
  }).length
})
const nextDueTask = computed(() => allTasks.value
  .filter(task => task.dueDate && !['done', 'cancelled'].includes(task.status.toLowerCase()))
  .sort((left, right) => new Date(left.dueDate!).getTime() - new Date(right.dueDate!).getTime())[0] ?? null)
const nextTaskDetail = computed(() => {
  if (!nextDueTask.value?.dueDate) return 'Chưa có nhiệm vụ mở nào có hạn'
  return `Gần nhất: ${new Date(nextDueTask.value.dueDate).toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })}`
})
const averageTeamCapacity = computed(() => {
  if (!team.value.length) return 0
  return Math.round(team.value.reduce((sum, member) => sum + member.capacityPercent, 0) / team.value.length)
})
const overloadedTeamCount = computed(() => team.value.filter(member => member.capacityPercent >= 90).length)
const strategicOrganizationScopes = computed(() => {
  const scopes = new Map<string, string>()
  for (const project of projects.value) {
    const id = project.organizationId || project.id
    if (!scopes.has(id)) {
      scopes.set(id, project.organizationId ? `Tổ chức ${id.slice(0, 8)}` : `Dự án ${project.name}`)
    }
  }
  return Array.from(scopes, ([id, name]) => ({ id, name }))
})

function askDashboardAi(area: 'projects' | 'tasks' | 'team') {
  const prompts = {
    projects: `Phân tích ${activeProjectsCount.value} dự án đang hoạt động: ${delayedCount.value} dự án chậm và ${atRiskCount.value} dự án có rủi ro. Hãy đề xuất thứ tự kiểm tra và giải thích lý do.`,
    tasks: `Phân tích ${openTasksCount.value} nhiệm vụ đang mở, trong đó ${overdueTasksCount.value} nhiệm vụ quá hạn. Hãy đề xuất thứ tự xử lý có thể thực hiện ngay.`,
    team: `Phân tích công suất trung bình ${averageTeamCapacity.value}% của đội, có ${overloadedTeamCount.value} thành viên từ 90% công suất. Chỉ đề xuất cách cân bằng dựa trên dữ liệu dự án hiện có.`,
  }
  openChatWithPrompt(prompts[area])
}

const hoveredIndex = ref<number | null>(null)
const tooltipX = ref(0)
const tooltipY = ref(0)

const maxTasks = computed(() => {
  const values = projects.value.slice(0, 5).map(p => p.taskCount || 0)
  const maxVal = Math.max(...values, 0)
  return maxVal > 0 ? maxVal : 10
})

const splinePoints = computed(() => {
  return projects.value.slice(0, 5).map((project, i) => {
    const x = 65 + i * 90 + 16
    const tasks = project.taskCount || 0
    const y = 280 - (tasks / maxTasks.value) * 200
    return { x, y }
  })
})

const splinePath = computed(() => {
  const pts = splinePoints.value
  if (pts.length === 0) return ''
  if (pts.length === 1) return `M ${pts[0].x} ${pts[0].y}`
  
  let path = `M ${pts[0].x} ${pts[0].y}`
  for (let i = 0; i < pts.length - 1; i++) {
    const p0 = pts[i]
    const p1 = pts[i + 1]
    const cpX1 = p0.x + 35
    const cpY1 = p0.y
    const cpX2 = p1.x - 35
    const cpY2 = p1.y
    path += ` C ${cpX1} ${cpY1}, ${cpX2} ${cpY2}, ${p1.x} ${p1.y}`
  }
  return path
})

const handleMouseEnter = (index: number, event: MouseEvent) => {
  hoveredIndex.value = index
  const project = projects.value[index]
  if (project) {
    const barHeight = (project.progressPercentage / 100) * 240
    if (chartMode.value === '3D') {
      tooltipX.value = 65 + index * 90 + 16 + 7
      tooltipY.value = 280 - barHeight - 5 - 15
    } else {
      tooltipX.value = 65 + index * 90 + 16
      tooltipY.value = 280 - barHeight - 15
    }
  }
}

const handleMouseLeave = () => {
  hoveredIndex.value = null
}

const tooltipStyle = computed(() => {
  if (hoveredIndex.value === null) return { display: 'none' }
  return {
    position: 'absolute',
    left: `${tooltipX.value}px`,
    top: `${tooltipY.value}px`,
    transform: 'translate(-50%, -100%)',
    zIndex: 50,
    pointerEvents: 'none'
  }
})

const hoveredProject = computed(() => {
  if (hoveredIndex.value === null) return null
  return projects.value[hoveredIndex.value] ?? null
})
</script>

<template>
  <div class="dashboard-scroll dashboard-scroll--embedded no-scrollbar">
    <div class="dashboard-container no-scrollbar">
      
      <!-- Left Main Column -->
      <div class="dashboard-main-col no-scrollbar">
        
        <!-- Welcome Banner -->
        <header class="dashboard-welcome-banner">
          <div>
            <h1>Chào mừng bạn quay trở lại!</h1>
            <p>Dưới đây là tóm tắt hoạt động của không gian làm việc hôm nay.</p>
          </div>
          <button class="primary-button" type="button" @click="openCreateProject">
            <Plus :size="18" />
            <span>Tạo Dự Án Mới</span>
          </button>
        </header>

        <!-- Summary Cards Grid -->
        <section class="summary-card-grid" aria-label="Tổng quan dự án">
          <!-- Card 1: Active Projects -->
          <article class="summary-card glass-card summary-card--primary" tabindex="0">
            <div class="summary-card__header">
              <span class="summary-card__label">DỰ ÁN ĐANG HOẠT ĐỘNG</span>
              <div class="summary-card__icon-box">
                <FolderKanban :size="18" />
              </div>
            </div>
            <div class="summary-card__value-row">
              <strong>{{ activeProjectsCount }}</strong>
              <p class="summary-card__detail">{{ newProjectsThisMonth }} dự án mới tháng này</p>
            </div>
            <div class="summary-card__context">
              <strong>{{ delayedCount }} chậm · {{ atRiskCount }} có rủi ro</strong>
              <span>{{ delayedCount ? 'Gợi ý: kiểm tra dự án chậm trước.' : 'Các dự án chưa có dấu hiệu chậm theo task quá hạn.' }}</span>
              <div class="summary-card__actions">
                <button type="button" @click="router.push('/projects')">Mở dự án <ChevronRight :size="14" /></button>
                <button type="button" @click="askDashboardAi('projects')"><BrainCircuit :size="14" /> Hỏi AI</button>
              </div>
            </div>
          </article>

          <!-- Card 2: Tasks Due Today -->
          <article class="summary-card glass-card summary-card--warning" tabindex="0">
            <div class="summary-card__header">
              <span class="summary-card__label">NHIỆM VỤ CẦN LÀM</span>
              <div class="summary-card__icon-box">
                <ClipboardList :size="18" />
              </div>
            </div>
            <div class="summary-card__value-row">
              <strong>{{ openTasksCount }}</strong>
              <p class="summary-card__detail">{{ nextTaskDetail }}</p>
            </div>
            <div class="summary-card__context">
              <strong>{{ overdueTasksCount }} nhiệm vụ quá hạn</strong>
              <span>{{ nextDueTask ? `Gần nhất: ${nextDueTask.title}` : 'Chưa có hạn xử lý tiếp theo.' }}</span>
              <div class="summary-card__actions">
                <button type="button" @click="router.push('/tasks')">Xử lý task <ChevronRight :size="14" /></button>
                <button type="button" @click="askDashboardAi('tasks')"><BrainCircuit :size="14" /> Hỏi AI</button>
              </div>
            </div>
          </article>

          <!-- Card 3: Team Bandwidth -->
          <article class="summary-card glass-card summary-card--success" tabindex="0">
            <div class="summary-card__header">
              <span class="summary-card__label">CÔNG SUẤT ĐỘI NGŨ</span>
              <div class="summary-card__icon-box">
                <Users :size="18" />
              </div>
            </div>
            <div class="summary-card__value-row" style="flex-direction: column; align-items: flex-start; gap: 8px;">
              <div style="display: flex; justify-content: space-between; width: 100%;">
                <strong style="font-size: 28px; line-height: 1;">{{ averageTeamCapacity }}%</strong>
              </div>
              <div style="width: 100%; height: 6px; background: rgba(0,0,0,0.06); border-radius: 3px; overflow: hidden;">
                <div :style="{ width: `${averageTeamCapacity}%`, height: '100%', background: 'var(--primary)', borderRadius: '3px' }"></div>
              </div>
            </div>
            <div class="summary-card__context">
              <strong>{{ overloadedTeamCount }} thành viên từ 90% công suất</strong>
              <span>{{ overloadedTeamCount ? 'Gợi ý: cân bằng lại người phụ trách trước khi giao thêm task.' : 'Đội chưa có thành viên vượt ngưỡng 90%.' }}</span>
              <div class="summary-card__actions">
                <button type="button" @click="router.push('/analytics')">Xem tải đội <ChevronRight :size="14" /></button>
                <button type="button" @click="askDashboardAi('team')"><BrainCircuit :size="14" /> Hỏi AI</button>
              </div>
            </div>
          </article>
        </section>

        <!-- Project Statistics Chart Card -->
        <section class="custom-chart-card">
          <div class="custom-chart-header">
            <h3>Thống kê tiến độ Dự án</h3>
            <div class="chart-pills">
              <button 
                class="chart-pill" 
                :class="{ active: chartMode === '2D' }" 
                @click="chartMode = '2D'"
              >Biểu đồ 2D</button>
              <button 
                class="chart-pill" 
                :class="{ active: chartMode === '3D' }" 
                @click="chartMode = '3D'"
              >Biểu đồ 3D</button>
            </div>
          </div>

          <!-- Modern SVG Column + Line Combo Chart with floating glass tooltip -->
          <div class="custom-chart-body-wrapper" style="position: relative; width: 100%;">
            <!-- Floating glass tooltip -->
            <div 
              v-if="hoveredProject" 
              class="chart-tooltip-glass" 
              :style="tooltipStyle"
              style="
                background: rgba(255, 255, 255, 0.95);
                backdrop-filter: none;
                border: 1px solid rgba(15, 82, 186, 0.15);
                border-radius: var(--qaly-radius-lg);
                padding: 12px 14px;
                box-shadow: var(--qaly-shadow-md);
                min-width: 160px;
                display: flex;
                flex-direction: column;
                gap: 6px;
                transition: all 0.15s cubic-bezier(0.16, 1, 0.3, 1);
                pointer-events: none;
              "
            >
              <div class="tooltip-project-name" style="font-size: 12px; font-weight: 750; color: #0f172a; border-bottom: 1px solid #f1f5f9; padding-bottom: 4px; margin-bottom: 2px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 150px;">
                {{ hoveredProject.name }}
              </div>
              <div style="display: flex; justify-content: space-between; align-items: center; font-size: 11px;">
                <span style="color: #64748b; display: flex; align-items: center; gap: 6px;">
                  <span style="width: 6px; height: 6px; border-radius: 50%; background: #2563eb; display: inline-block;"></span>
                  Tiến độ
                </span>
                <strong style="color: #0f172a; font-weight: 800;">{{ hoveredProject.progressPercentage }}%</strong>
              </div>
              <div style="display: flex; justify-content: space-between; align-items: center; font-size: 11px;">
                <span style="color: #64748b; display: flex; align-items: center; gap: 6px;">
                  <span style="width: 6px; height: 6px; border-radius: 50%; background: #10b981; display: inline-block;"></span>
                  Nhiệm vụ
                </span>
                <strong style="color: #0f172a; font-weight: 800;">{{ hoveredProject.taskCount || 0 }}</strong>
              </div>
              <div v-if="hoveredProject.overdueTaskCount > 0" style="display: flex; justify-content: space-between; align-items: center; font-size: 11px;">
                <span style="color: #ef4444; display: flex; align-items: center; gap: 6px;">
                  <span style="width: 6px; height: 6px; border-radius: 50%; background: #ef4444; display: inline-block;"></span>
                  Quá hạn
                </span>
                <strong style="color: #ef4444; font-weight: 800;">{{ hoveredProject.overdueTaskCount }}</strong>
              </div>
            </div>

            <!-- SVG 2D Combo Chart -->
            <div v-if="chartMode === '2D'" class="custom-chart-body" style="height: 340px; border-bottom: none; overflow: visible; display: flex; justify-content: center; align-items: center; padding: 0; position: relative;">
              <svg viewBox="0 0 540 320" style="width: 100%; height: 100%; overflow: visible;" class="svg-modern-chart">
                <!-- Gradients Definitions -->
                <defs>
                  <linearGradient id="bar-grad" x1="0%" y1="0%" x2="0%" y2="100%">
                    <stop offset="0%" stop-color="#2563eb" stop-opacity="0.9" />
                    <stop offset="100%" stop-color="#3b82f6" stop-opacity="0.15" />
                  </linearGradient>
                  <linearGradient id="bar-grad-hover" x1="0%" y1="0%" x2="0%" y2="100%">
                    <stop offset="0%" stop-color="#1d4ed8" stop-opacity="1" />
                    <stop offset="100%" stop-color="#2563eb" stop-opacity="0.3" />
                  </linearGradient>
                  <linearGradient id="line-grad" x1="0%" y1="0%" x2="100%" y2="0%">
                    <stop offset="0%" stop-color="#10b981" />
                    <stop offset="50%" stop-color="#3b82f6" />
                    <stop offset="100%" stop-color="#6366f1" />
                  </linearGradient>
                  <filter id="shadow-glow" x="-10%" y="-10%" width="120%" height="120%">
                    <feDropShadow dx="0" dy="6" stdDeviation="4" flood-color="#2563eb" flood-opacity="0.18" />
                  </filter>
                  <filter id="line-glow" x="-10%" y="-10%" width="120%" height="120%">
                    <feDropShadow dx="0" dy="4" stdDeviation="3" flood-color="#10b981" flood-opacity="0.25" />
                  </filter>
                </defs>
                
                <!-- Y-Axis Grid Lines and Labels -->
                <g class="grid-lines" stroke="#e2e8f0" stroke-width="1" stroke-dasharray="4,4">
                  <!-- 100% -->
                  <text x="35" y="44" text-anchor="end" fill="#94a3b8" font-size="10" font-weight="600" stroke="none">100%</text>
                  <line x1="45" y1="40" x2="520" y2="40" />

                  <!-- 75% -->
                  <text x="35" y="104" text-anchor="end" fill="#94a3b8" font-size="10" font-weight="600" stroke="none">75%</text>
                  <line x1="45" y1="100" x2="520" y2="100" />

                  <!-- 50% -->
                  <text x="35" y="164" text-anchor="end" fill="#94a3b8" font-size="10" font-weight="600" stroke="none">50%</text>
                  <line x1="45" y1="160" x2="520" y2="160" />

                  <!-- 25% -->
                  <text x="35" y="224" text-anchor="end" fill="#94a3b8" font-size="10" font-weight="600" stroke="none">25%</text>
                  <line x1="45" y1="220" x2="520" y2="220" />

                  <!-- 0% -->
                  <text x="35" y="284" text-anchor="end" fill="#94a3b8" font-size="10" font-weight="600" stroke="none">0%</text>
                </g>
                
                <!-- Columns (Bars) Group -->
                <g v-for="(project, i) in projects.slice(0, 5)" :key="'bar-' + project.id" 
                   class="svg-bar-group" 
                   style="cursor: pointer;"
                   @click="selectProject(project.id)"
                   @mouseenter="handleMouseEnter(i, $event)"
                   @mouseleave="handleMouseLeave"
                >
                  <!-- Background Bar Track -->
                  <rect 
                    :x="65 + i * 90" 
                    y="40" 
                    width="32" 
                    height="240" 
                    fill="#f8fafc" 
                    rx="6"
                  />

                  <!-- Progress Bar Capsule -->
                  <rect 
                    :x="65 + i * 90" 
                    :y="280 - (project.progressPercentage / 100) * 240" 
                    width="32" 
                    :height="Math.max((project.progressPercentage / 100) * 240, 8)" 
                    :fill="hoveredIndex === i ? 'url(#bar-grad-hover)' : 'url(#bar-grad)'" 
                    rx="6"
                    :filter="hoveredIndex === i ? 'url(#shadow-glow)' : 'none'"
                    style="transition: all 0.3s cubic-bezier(0.16, 1, 0.3, 1);"
                  />
                  
                  <!-- Hover active ring overlay -->
                  <rect 
                    v-if="hoveredIndex === i"
                    :x="63 + i * 90" 
                    :y="278 - (project.progressPercentage / 100) * 240" 
                    width="36" 
                    :height="Math.max((project.progressPercentage / 100) * 240 + 4, 12)" 
                    fill="none"
                    stroke="#3b82f6"
                    stroke-width="1.5"
                    rx="8"
                    style="opacity: 0.8; transition: all 0.3s;"
                  />

                  <!-- Project progress percentage label on top of the bar -->
                  <text 
                    :x="65 + i * 90 + 16" 
                    :y="280 - (project.progressPercentage / 100) * 240 - 8" 
                    text-anchor="middle" 
                    :fill="hoveredIndex === i ? 'var(--primary)' : '#64748b'" 
                    font-size="10" 
                    font-weight="700"
                    style="transition: fill 0.2s;"
                  >
                    {{ project.progressPercentage }}%
                  </text>
                  
                  <!-- X-Axis Labels below the base line -->
                  <text 
                    :x="65 + i * 90 + 16" 
                    y="305" 
                    text-anchor="middle" 
                    :fill="hoveredIndex === i ? 'var(--text-strong)' : 'var(--muted)'" 
                    font-size="10.5" 
                    :font-weight="hoveredIndex === i ? '700' : '600'"
                    style="transition: all 0.2s;"
                  >
                    {{ project.name.length > 10 ? project.name.substring(0, 8) + '...' : project.name }}
                  </text>
                </g>
                
                <!-- Overlay line representing tasks/velocity -->
                <path 
                  v-if="splinePath"
                  :d="splinePath" 
                  fill="none" 
                  stroke="url(#line-grad)" 
                  stroke-width="3" 
                  stroke-linecap="round"
                  filter="url(#line-glow)"
                />

                <!-- Line points dots -->
                <g v-for="(pt, i) in splinePoints" :key="'dot-' + i">
                  <circle 
                    :cx="pt.x" 
                    :cy="pt.y" 
                    r="5" 
                    fill="#ffffff" 
                    stroke="var(--primary)" 
                    stroke-width="2.5"
                    style="cursor: pointer; transition: all 0.2s;"
                    :style="{ transform: hoveredIndex === i ? 'scale(1.4)' : 'none', transformOrigin: `${pt.x}px ${pt.y}px` }"
                    @mouseenter="handleMouseEnter(i, $event)"
                    @mouseleave="handleMouseLeave"
                  />
                  <circle 
                    v-if="hoveredIndex === i"
                    :cx="pt.x" 
                    :cy="pt.y" 
                    r="9" 
                    fill="var(--primary)" 
                    opacity="0.15"
                  />
                </g>

                <!-- Baseline -->
                <line x1="45" y1="280" x2="520" y2="280" stroke="#cbd5e1" stroke-width="1.5" />
              </svg>
            </div>

            <!-- SVG 3D Bar Chart -->
            <div v-else-if="chartMode === '3D'" class="custom-chart-body" style="height: 340px; border-bottom: none; overflow: visible; display: flex; justify-content: center; align-items: center; padding: 0; position: relative;">
              <svg viewBox="0 0 540 320" style="width: 100%; height: 100%; overflow: visible;" class="svg-3d-chart">
                <!-- Gradients Definitions -->
                <defs>
                  <linearGradient id="grad-3d-front" x1="0%" y1="0%" x2="0%" y2="100%">
                    <stop offset="0%" stop-color="#3b82f6" />
                    <stop offset="100%" stop-color="#0f52ba" />
                  </linearGradient>
                  <linearGradient id="grad-3d-right" x1="0%" y1="0%" x2="0%" y2="100%">
                    <stop offset="0%" stop-color="#1d4ed8" />
                    <stop offset="100%" stop-color="#0a3d91" />
                  </linearGradient>
                  <linearGradient id="grad-3d-top" x1="0%" y1="0%" x2="100%" y2="100%">
                    <stop offset="0%" stop-color="#93c5fd" />
                    <stop offset="100%" stop-color="#3b82f6" />
                  </linearGradient>
                  <!-- Grid line glow/shadow -->
                  <filter id="shadow-3d" x="-10%" y="-10%" width="120%" height="120%">
                    <feDropShadow dx="0" dy="8" stdDeviation="6" flood-color="#0f52ba" flood-opacity="0.15" />
                  </filter>
                </defs>
                
                <!-- Y-Axis Grid lines background -->
                <g stroke="#e2e8f0" stroke-width="1" stroke-dasharray="4,4">
                  <!-- 100% -->
                  <text x="35" y="44" text-anchor="end" fill="#94a3b8" font-size="10" font-weight="600" stroke="none">100%</text>
                  <line x1="45" y1="40" x2="520" y2="40" />

                  <!-- 75% -->
                  <text x="35" y="104" text-anchor="end" fill="#94a3b8" font-size="10" font-weight="600" stroke="none">75%</text>
                  <line x1="45" y1="100" x2="520" y2="100" />

                  <!-- 50% -->
                  <text x="35" y="164" text-anchor="end" fill="#94a3b8" font-size="10" font-weight="600" stroke="none">50%</text>
                  <line x1="45" y1="160" x2="520" y2="160" />

                  <!-- 25% -->
                  <text x="35" y="224" text-anchor="end" fill="#94a3b8" font-size="10" font-weight="600" stroke="none">25%</text>
                  <line x1="45" y1="220" x2="520" y2="220" />

                  <!-- 0% -->
                  <text x="35" y="284" text-anchor="end" fill="#94a3b8" font-size="10" font-weight="600" stroke="none">0%</text>
                </g>
                
                <!-- 3D Bars Loop -->
                <g v-for="(project, i) in projects.slice(0, 5)" :key="'bar3d-' + project.id" 
                   class="svg-bar-group" 
                   style="cursor: pointer; transition: transform 0.2s ease-in-out; transform-origin: center bottom;"
                   :style="{ transform: hoveredIndex === i ? 'translateY(-6px)' : 'none' }"
                   @click="selectProject(project.id)"
                   @mouseenter="handleMouseEnter(i, $event)"
                   @mouseleave="handleMouseLeave"
                >
                  <!-- Front Face -->
                  <rect 
                    :x="65 + i * 90" 
                    :y="280 - (project.progressPercentage / 100) * 240" 
                    width="32" 
                    :height="Math.max((project.progressPercentage / 100) * 240, 5)" 
                    fill="url(#grad-3d-front)" 
                    rx="1"
                    :filter="hoveredIndex === i ? 'url(#shadow-3d)' : 'none'"
                  />
                  
                  <!-- Right Face (3D depth) -->
                  <polygon 
                    :points="
                      (65 + i * 90 + 32) + ',' + (280 - (project.progressPercentage / 100) * 240) + ' ' + 
                      (65 + i * 90 + 32 + 14) + ',' + (280 - (project.progressPercentage / 100) * 240 - 10) + ' ' + 
                      (65 + i * 90 + 32 + 14) + ',' + (280 - 10) + ' ' + 
                      (65 + i * 90 + 32) + ',' + '280'
                    " 
                    fill="url(#grad-3d-right)" 
                  />
                  
                  <!-- Top Face (3D lid) -->
                  <polygon 
                    :points="
                      (65 + i * 90) + ',' + (280 - (project.progressPercentage / 100) * 240) + ' ' + 
                      (65 + i * 90 + 14) + ',' + (280 - (project.progressPercentage / 100) * 240 - 10) + ' ' + 
                      (65 + i * 90 + 32 + 14) + ',' + (280 - (project.progressPercentage / 100) * 240 - 10) + ' ' + 
                      (65 + i * 90 + 32) + ',' + (280 - (project.progressPercentage / 100) * 240)
                    " 
                    fill="url(#grad-3d-top)" 
                  />
                  
                  <!-- Value text centered on top of the bar -->
                  <text 
                    :x="65 + i * 90 + 22" 
                    :y="280 - (project.progressPercentage / 100) * 240 - 16" 
                    text-anchor="middle" 
                    :fill="hoveredIndex === i ? 'var(--primary)' : '#64748b'" 
                    font-size="10" 
                    font-weight="850"
                  >
                    {{ project.progressPercentage }}%
                  </text>
                  
                  <!-- Label text below the base line -->
                  <text 
                    :x="65 + i * 90 + 16" 
                    y="305" 
                    text-anchor="middle" 
                    :fill="hoveredIndex === i ? 'var(--text-strong)' : 'var(--muted)'" 
                    font-size="10.5" 
                    :font-weight="hoveredIndex === i ? '700' : '600'"
                  >
                    {{ project.name.length > 10 ? project.name.substring(0, 8) + '...' : project.name }}
                  </text>
                </g>
                
                <!-- Baseline -->
                <line x1="45" y1="280" x2="520" y2="280" stroke="#cbd5e1" stroke-width="1.5" />
              </svg>
            </div>
          </div>

            <!-- Chart Legend and Counters -->
          <div class="chart-summary-row">
            <div class="chart-summary-item">
              <span class="color-on-track">Đúng tiến độ</span>
              <strong>{{ onTrackCount }} Dự án</strong>
            </div>
            <div class="chart-summary-item">
              <span class="color-at-risk">Có rủi ro</span>
              <strong>{{ atRiskCount }} Dự án</strong>
            </div>
            <div class="chart-summary-item">
              <span class="color-delayed">Chậm trễ</span>
              <strong>{{ delayedCount }} Dự án</strong>
            </div>
          </div>
        </section>

        <!-- Strategic Performance Banner -->
        <StrategicOverviewAI :organization-scopes="strategicOrganizationScopes" />

      </div>

      <!-- Right Sidebar Column -->
      <aside class="dashboard-side-col">
        
        <!-- Attention Required Card -->
        <AttentionRiskCard />

        <!-- Recent Activity Card -->
        <RecentActivityWidget />

      </aside>

    </div>
  </div>
</template>

<style scoped>
.summary-card {
  position: relative;
  outline: none;
}

.summary-card:focus-visible {
  box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.2);
}

.summary-card__context {
  position: absolute;
  z-index: 20;
  inset: calc(100% + 7px) 0 auto 0;
  display: none;
  min-width: 240px;
  gap: 7px;
  border: 1px solid #dbe3ef;
  border-radius: 6px;
  padding: 12px;
  color: #0f172a;
  background: #ffffff;
  box-shadow: var(--qaly-shadow-md);
}

.summary-card:hover .summary-card__context,
.summary-card:focus-within .summary-card__context {
  display: grid;
}

.summary-card__context > strong {
  font-size: 0.82rem;
}

.summary-card__context > span {
  color: #64748b;
  font-size: 0.76rem;
  line-height: 1.45;
}

.summary-card__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 7px;
  padding-top: 3px;
}

.summary-card__actions button {
  min-height: 32px;
  display: inline-flex;
  align-items: center;
  gap: 5px;
  border: 1px solid #bfdbfe;
  border-radius: 6px;
  padding: 6px 9px;
  color: #1d4ed8;
  background: #eff6ff;
  font-size: 0.73rem;
  font-weight: 750;
  cursor: pointer;
}

.summary-card__actions button:hover {
  background: #dbeafe;
}

@media (max-width: 760px) {
  .summary-card__context {
    position: static;
    min-width: 0;
    margin-top: 12px;
  }
}
</style>
