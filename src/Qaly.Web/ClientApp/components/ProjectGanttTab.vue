<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { Calendar, AlertCircle } from 'lucide-vue-next'

const props = defineProps<{
  projectId: string
}>()

const tasks = ref<any[]>([])
const isLoading = ref(true)

async function fetchGanttData() {
  isLoading.value = true
  const res = await fetch(`/api/tasks/project/${props.projectId}/gantt`)
  if (res.ok) {
    const data = await res.json()
    tasks.value = Array.isArray(data) ? data : (data.data || [])
  }
  isLoading.value = false
}

const minDate = computed(() => {
  const dates = tasks.value.filter(t => t.startDate).map(t => new Date(t.startDate).getTime())
  return dates.length ? new Date(Math.min(...dates)) : new Date()
})

const maxDate = computed(() => {
  const dates = tasks.value.filter(t => t.endDate).map(t => new Date(t.endDate).getTime())
  return dates.length ? new Date(Math.max(...dates)) : new Date(new Date().getTime() + 7 * 24 * 60 * 60 * 1000)
})

const totalDays = computed(() => {
  return Math.ceil((maxDate.value.getTime() - minDate.value.getTime()) / (24 * 60 * 60 * 1000)) + 5
})

function getTaskStyle(task: any) {
  if (!task.startDate || !task.endDate) return { display: 'none' }
  
  const start = new Date(task.startDate)
  const end = new Date(task.endDate)
  
  const left = Math.ceil((start.getTime() - minDate.value.getTime()) / (24 * 60 * 60 * 1000))
  const width = Math.ceil((end.getTime() - start.getTime()) / (24 * 60 * 60 * 1000))
  
  return {
    gridColumnStart: left + 1,
    gridColumnEnd: `span ${Math.max(1, width)}`,
    backgroundColor: task.isCriticalPath ? 'var(--peach-500)' : 'var(--primary-soft)',
    color: task.isCriticalPath ? 'white' : 'var(--primary)'
  }
}

onMounted(fetchGanttData)
</script>

<template>
  <div class="gantt-chart glass-card reveal">
    <div class="panel-header">
      <Calendar :size="20" class="icon-primary" />
      <h3>Timeline & Gantt Chart</h3>
    </div>

    <div v-if="isLoading" class="loading-state">Loading timeline...</div>
    
    <div v-else-if="tasks.length === 0" class="empty-state">
      <AlertCircle :size="48" />
      <p>Chưa có dữ liệu ngày tháng cho các task trong dự án này.</p>
    </div>

    <div v-else class="gantt-container no-scrollbar">
      <div class="gantt-grid" :style="{ gridTemplateColumns: `repeat(${totalDays}, 40px)` }">
        <!-- Time Header -->
        <div v-for="d in totalDays" :key="d" class="grid-header">
          {{ new Date(minDate.getTime() + (d-1) * 24 * 60 * 60 * 1000).getDate() }}
        </div>

        <!-- Task Rows -->
        <template v-for="task in tasks" :key="task.id">
          <div class="task-label-row">
            <span :class="{ 'critical': task.isCriticalPath }">{{ task.title }}</span>
          </div>
          <div class="task-bar-row">
             <div v-if="task.startDate" class="task-bar" :style="getTaskStyle(task)">
                <div class="progress-inner" :style="{ width: task.progress + '%' }"></div>
                <span class="bar-text">{{ task.progress }}%</span>
             </div>
          </div>
        </template>
      </div>
    </div>

    <div class="gantt-legend">
      <div class="legend-item"><span class="box normal"></span> Normal Task</div>
      <div class="legend-item"><span class="box critical"></span> Critical Path</div>
    </div>
  </div>
</template>

<style scoped>
.gantt-chart { padding: 24px; display: flex; flex-direction: column; gap: 20px; min-height: 400px; }
.panel-header { display: flex; align-items: center; gap: 12px; }

.gantt-container { overflow-x: auto; padding-bottom: 16px; border: 1px solid var(--line); border-radius: 12px; background: white; }

.gantt-grid { display: grid; position: relative; }

.grid-header { 
  height: 40px; display: flex; align-items: center; justify-content: center; 
  font-size: 11px; font-weight: 700; color: var(--muted); border-right: 1px solid var(--line-light);
  border-bottom: 2px solid var(--line); background: var(--bg-soft);
}

.task-label-row { grid-column: 1 / -1; padding: 12px 16px 4px; font-size: 12px; font-weight: 700; background: #fafafa; }
.task-label-row span.critical { color: var(--peach-500); }

.task-bar-row { grid-column: 1 / -1; height: 32px; position: relative; margin-bottom: 8px; border-bottom: 1px solid var(--line-light); }

.task-bar { 
  position: relative; height: 24px; top: 4px; border-radius: 4px; 
  display: flex; align-items: center; padding: 0 8px; font-size: 10px; font-weight: 800;
  box-shadow: 0 2px 4px rgba(0,0,0,0.05); z-index: 2;
}

.progress-inner { 
  position: absolute; left: 0; top: 0; bottom: 0; 
  background: rgba(255,255,255,0.3); border-radius: 4px 0 0 4px; 
}
.bar-text { position: relative; z-index: 3; }

.gantt-legend { display: flex; gap: 20px; font-size: 12px; color: var(--muted); margin-top: 12px; }
.legend-item { display: flex; align-items: center; gap: 8px; }
.box { width: 12px; height: 12px; border-radius: 3px; }
.box.normal { background: var(--primary-soft); }
.box.critical { background: var(--peach-500); }

.loading-state, .empty-state { 
  flex: 1; display: flex; flex-direction: column; align-items: center; 
  justify-content: center; gap: 16px; color: var(--muted); 
}
</style>
