<script setup lang="ts">
import { computed, ref } from 'vue'
import { Plus, AlertTriangle, TrendingUp, CheckCircle2, Activity, ChevronRight, LayoutDashboard, FolderKanban, ClipboardList, Users } from 'lucide-vue-next'
import { useDashboardContext } from '../composables/dashboard-context'

const {
  projects,
  openCreateProject,
  selectProject
} = useDashboardContext()

// Time period for chart
const activeTab = ref('Q4')
const chartMode = ref<'2D' | '3D'>('2D')

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

// Projects requiring attention (overdue tasks)
const attentionProjects = computed(() => {
  const list = projects.value
    .filter(p => p.status !== 'Archived' && p.overdueTaskCount > 0)
    .map(p => ({
      id: p.id,
      name: p.name,
      overdueCount: p.overdueTaskCount,
      desc: p.overdueTaskCount > 1 ? `${p.overdueTaskCount} nhiệm vụ quá hạn • Cần phê duyệt` : `${p.overdueTaskCount} nhiệm vụ quá hạn • Cần kiểm tra`,
      badge: 'Quá hạn',
      type: 'overdue'
    }))

  // If empty, return some default placeholders so the UI matches the mockup style
  if (list.length === 0) {
    return [
      { id: '1', name: 'Đồng bộ cơ sở dữ liệu', overdueCount: 1, desc: 'Trễ 2 ngày • Cần phê duyệt khẩn cấp', badge: 'Quá hạn', type: 'overdue' },
      { id: '2', name: 'Dự thảo Ngân sách Q4', overdueCount: 0, desc: 'Đến hạn trong 4 giờ • Ưu tiên cao', badge: 'Sắp tới', type: 'upcoming' }
    ]
  }
  return list
})

// Recent activities (translated to Vietnamese with mockup style)
const recentActivities = [
  { id: 1, user: 'Nguyễn Văn A', action: 'đã chuyển Thiết kế khung làm việc sang', target: 'Đang đánh giá', time: '15 phút trước', initials: 'VA' },
  { id: 2, user: 'Quản trị viên', action: 'đã thêm 4 nhiệm vụ mới vào', target: 'Qaly MVP', time: '1 giờ trước', initials: 'QT' },
  { id: 3, user: 'Trần Thị B', action: 'đã hoàn thành rà soát hệ màu trong', target: 'Ngôn ngữ thiết kế', time: '3 giờ trước', initials: 'TB' }
]

// Interactive state for chart
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
            <p>Dưới đây là tóm tắt hoạt động của Workspace ngày hôm nay.</p>
          </div>
          <button class="primary-button" type="button" @click="openCreateProject">
            <Plus :size="18" />
            <span>Tạo Dự Án Mới</span>
          </button>
        </header>

        <!-- Summary Cards Grid -->
        <section class="summary-card-grid" aria-label="Tổng quan dự án">
          <!-- Card 1: Active Projects -->
          <article class="summary-card glass-card summary-card--primary">
            <div class="summary-card__header">
              <span class="summary-card__label">DỰ ÁN ĐANG HOẠT ĐỘNG</span>
              <div class="summary-card__icon-box">
                <FolderKanban :size="18" />
              </div>
            </div>
            <div class="summary-card__value-row">
              <strong>{{ activeProjectsCount }}</strong>
              <p class="summary-card__detail">↗ 2 dự án mới tháng này</p>
            </div>
          </article>

          <!-- Card 2: Tasks Due Today -->
          <article class="summary-card glass-card summary-card--warning">
            <div class="summary-card__header">
              <span class="summary-card__label">NHIỆM VỤ CẦN LÀM</span>
              <div class="summary-card__icon-box">
                <ClipboardList :size="18" />
              </div>
            </div>
            <div class="summary-card__value-row">
              <strong>{{ totalTasksCount }}</strong>
              <p class="summary-card__detail">Nhiệm vụ tiếp theo sau 2 giờ</p>
            </div>
          </article>

          <!-- Card 3: Team Bandwidth -->
          <article class="summary-card glass-card summary-card--success">
            <div class="summary-card__header">
              <span class="summary-card__label">CÔNG SUẤT ĐỘI NGŨ</span>
              <div class="summary-card__icon-box">
                <Users :size="18" />
              </div>
            </div>
            <div class="summary-card__value-row" style="flex-direction: column; align-items: flex-start; gap: 8px;">
              <div style="display: flex; justify-content: space-between; width: 100%;">
                <strong style="font-size: 28px; line-height: 1;">84%</strong>
              </div>
              <div style="width: 100%; height: 6px; background: rgba(0,0,0,0.06); border-radius: 3px; overflow: hidden;">
                <div style="width: 84%; height: 100%; background: var(--primary); border-radius: 3px;"></div>
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
              v-if="hoveredIndex !== null && projects[hoveredIndex]" 
              class="chart-tooltip-glass" 
              :style="tooltipStyle"
              style="
                background: rgba(255, 255, 255, 0.95);
                backdrop-filter: blur(8px);
                border: 1px solid rgba(15, 82, 186, 0.15);
                border-radius: 12px;
                padding: 12px 14px;
                box-shadow: 0 10px 25px -5px rgba(15, 82, 186, 0.15), 0 8px 16px -6px rgba(0, 0, 0, 0.04);
                min-width: 160px;
                display: flex;
                flex-direction: column;
                gap: 6px;
                transition: all 0.15s cubic-bezier(0.16, 1, 0.3, 1);
                pointer-events: none;
              "
            >
              <div class="tooltip-project-name" style="font-size: 12px; font-weight: 750; color: #0f172a; border-bottom: 1px solid #f1f5f9; padding-bottom: 4px; margin-bottom: 2px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 150px;">
                {{ projects[hoveredIndex].name }}
              </div>
              <div style="display: flex; justify-content: space-between; align-items: center; font-size: 11px;">
                <span style="color: #64748b; display: flex; align-items: center; gap: 6px;">
                  <span style="width: 6px; height: 6px; border-radius: 50%; background: #2563eb; display: inline-block;"></span>
                  Tiến độ
                </span>
                <strong style="color: #0f172a; font-weight: 800;">{{ projects[hoveredIndex].progressPercentage }}%</strong>
              </div>
              <div style="display: flex; justify-content: space-between; align-items: center; font-size: 11px;">
                <span style="color: #64748b; display: flex; align-items: center; gap: 6px;">
                  <span style="width: 6px; height: 6px; border-radius: 50%; background: #10b981; display: inline-block;"></span>
                  Nhiệm vụ
                </span>
                <strong style="color: #0f172a; font-weight: 800;">{{ projects[hoveredIndex].taskCount || 0 }}</strong>
              </div>
              <div v-if="projects[hoveredIndex].overdueTaskCount > 0" style="display: flex; justify-content: space-between; align-items: center; font-size: 11px;">
                <span style="color: #ef4444; display: flex; align-items: center; gap: 6px;">
                  <span style="width: 6px; height: 6px; border-radius: 50%; background: #ef4444; display: inline-block;"></span>
                  Quá hạn
                </span>
                <strong style="color: #ef4444; font-weight: 800;">{{ projects[hoveredIndex].overdueTaskCount }}</strong>
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
                
                <!-- Y-Axis Grid Lines & Labels -->
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

          <!-- Chart Legend & Counters -->
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
        <section class="performance-banner">
          <div class="performance-banner__content">
            <span class="performance-banner__label">TỔNG QUAN CHIẾN LƯỢC</span>
            <h2>Nâng cấp Cơ sở Hạ tầng</h2>
            <p>Sáng kiến toàn cầu của chúng tôi nhằm hiện đại hóa hạ tầng cốt lõi hiện đã đạt 78% tiến độ hoàn thành. Hệ thống đang hoạt động với hiệu suất vượt trội hơn 24% so với quý trước.</p>
            <div class="performance-banner__stats">
              <div>
                <strong>78%</strong>
                <span>Tiến trình chung</span>
              </div>
              <div>
                <strong>08</strong>
                <span>Tính năng mới tuần này</span>
              </div>
            </div>
          </div>
          <div class="performance-banner__visual">
            <div class="abstract-ui">
              <div class="abstract-ui-header" style="background: rgba(255, 255, 255, 0.08); display: flex; align-items: center; padding: 0 12px; gap: 8px;">
                <div style="width: 8px; height: 8px; border-radius: 50%; background: #ef4444;"></div>
                <div style="width: 8px; height: 8px; border-radius: 50%; background: #f59e0b;"></div>
                <div style="width: 8px; height: 8px; border-radius: 50%; background: #10b981;"></div>
              </div>
              <div class="abstract-ui-body">
                <div class="abstract-ui-card" style="display: flex; flex-direction: column; justify-content: space-between; padding: 12px; background: rgba(255,255,255,0.04);">
                  <div style="width: 40%; height: 8px; background: rgba(255,255,255,0.2); border-radius: 4px;"></div>
                  <div style="width: 80%; height: 32px; background: rgba(15, 82, 186, 0.2); border-radius: 6px; border: 1px solid rgba(15, 82, 186, 0.4); display: flex; align-items: center; justify-content: center; font-size: 10px; color: #ffffff; font-weight: 700;">Hạ tầng cốt lõi</div>
                </div>
                <div class="abstract-ui-card" style="display: flex; flex-direction: column; justify-content: space-between; padding: 12px; background: rgba(255,255,255,0.04);">
                  <div style="width: 40%; height: 8px; background: rgba(255,255,255,0.2); border-radius: 4px;"></div>
                  <div style="width: 80%; height: 32px; background: rgba(16, 185, 129, 0.2); border-radius: 6px; border: 1px solid rgba(16, 185, 129, 0.4); display: flex; align-items: center; justify-content: center; font-size: 10px; color: #ffffff; font-weight: 700;">Bảo mật: Đạt</div>
                </div>
              </div>
            </div>
          </div>
        </section>

      </div>

      <!-- Right Sidebar Column -->
      <aside class="dashboard-side-col">
        
        <!-- Attention Required Card -->
        <section class="attention-card">
          <div class="attention-card-header">
            <AlertTriangle :size="18" />
            <h3>Cần chú ý</h3>
          </div>
          
          <div class="attention-list">
            <article 
              v-for="item in attentionProjects" 
              :key="item.id" 
              class="attention-item"
            >
              <div class="attention-item-info">
                <span class="attention-item-title">{{ item.name }}</span>
                <span class="attention-item-desc">{{ item.desc }}</span>
              </div>
              <span 
                class="attention-badge"
                :class="item.type === 'overdue' ? 'attention-badge--overdue' : 'attention-badge--upcoming'"
              >
                {{ item.badge }}
              </span>
            </article>
          </div>

          <RouterLink to="/tasks" class="attention-view-all">
            Xem Tất cả đầu việc
          </RouterLink>
        </section>

        <!-- Recent Activity Card -->
        <section class="activity-card">
          <div class="activity-card-header">
            <h3>Hoạt động gần đây</h3>
          </div>

          <div class="activity-list">
            <div 
              v-for="activity in recentActivities" 
              :key="activity.id" 
              class="activity-item"
            >
              <div class="activity-avatar">
                {{ activity.initials }}
              </div>
              <div class="activity-content">
                <span class="activity-text">
                  <strong>{{ activity.user }}</strong> {{ activity.action }} <strong>{{ activity.target }}</strong>
                </span>
                <span class="activity-time">{{ activity.time }}</span>
              </div>
            </div>
          </div>
        </section>

      </aside>

    </div>
  </div>
</template>
