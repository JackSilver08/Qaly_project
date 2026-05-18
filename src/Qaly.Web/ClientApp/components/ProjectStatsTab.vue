<script setup lang="ts">
import { computed } from 'vue';

const props = defineProps<{
  stats: {
    total: number
    todo: number
    inProgress: number
    inReview: number
    done: number
    overdue: number
    completionRate: number
  }
}>()
</script>

<template>
  <div class="stats-tab-content">
    <div class="stats-grid">
      <div class="stat-card glass-card">
        <span class="stat-card__label">Tổng số Task</span>
        <strong class="stat-card__value">{{ stats.total }}</strong>
      </div>
      <div class="stat-card glass-card">
        <span class="stat-card__label">Đang làm</span>
        <strong class="stat-card__value">{{ stats.inProgress }}</strong>
      </div>
      <div class="stat-card glass-card">
        <span class="stat-card__label">Đang Review</span>
        <strong class="stat-card__value">{{ stats.inReview }}</strong>
      </div>
      <div class="stat-card glass-card">
        <span class="stat-card__label">Đã hoàn thành</span>
        <strong class="stat-card__value" style="color: var(--mint-500)">{{ stats.done }}</strong>
      </div>
      <div class="stat-card glass-card">
        <span class="stat-card__label">Tỷ lệ hoàn thành</span>
        <strong class="stat-card__value">{{ stats.completionRate }}%</strong>
      </div>
      <div class="stat-card glass-card" :class="{ 'is-risk': stats.overdue > 0 }">
        <span class="stat-card__label">Quá hạn</span>
        <strong class="stat-card__value">{{ stats.overdue }}</strong>
      </div>
    </div>

    <div class="charts-row">
      <div class="chart-card glass-card">
        <h3>Phân bổ trạng thái</h3>
        <div v-if="stats.total > 0" class="donut-chart-wrapper">
          <div class="donut-chart" :style="{ '--progress': `${stats.completionRate}%` }">
            <strong>{{ stats.completionRate }}%</strong>
          </div>
          <div class="chart-legend">
            <div class="legend-item"><span class="dot done"></span> Đã xong ({{ stats.done }})</div>
            <div class="legend-item"><span class="dot active"></span> Đang làm ({{ stats.inProgress + stats.inReview + stats.todo }})</div>
          </div>
        </div>
        <div v-else class="empty-state">Chưa đủ dữ liệu thống kê</div>
      </div>

      <div class="chart-card glass-card progress-overview">
        <h3>Tiến độ dự án</h3>
        <div v-if="stats.total > 0" class="progress-details">
          <div class="progress-row">
            <span>Hoàn thành</span>
            <div class="bar-rail"><div class="bar-fill done" :style="{ width: `${stats.total > 0 ? (stats.done / stats.total) * 100 : 0}%` }"></div></div>
          </div>
          <div class="progress-row">
            <span>Đang thực hiện</span>
            <div class="bar-rail"><div class="bar-fill doing" :style="{ width: `${stats.total > 0 ? ((stats.inProgress + stats.inReview) / stats.total) * 100 : 0}%` }"></div></div>
          </div>
          <div class="progress-row">
            <span>Chưa bắt đầu</span>
            <div class="bar-rail"><div class="bar-fill todo" :style="{ width: `${stats.total > 0 ? (stats.todo / stats.total) * 100 : 0}%` }"></div></div>
          </div>
        </div>
        <div v-else class="empty-state">Chưa đủ dữ liệu thống kê</div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.stats-tab-content {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 16px;
}

.stat-card {
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 12px;
  background:
    linear-gradient(160deg, rgba(255, 255, 255, 0.08), rgba(15, 76, 255, 0.08)),
    rgba(8, 21, 39, 0.74);
  border: 1px solid rgba(182, 194, 217, 0.26);
  border-radius: var(--radius-card);
  box-shadow: var(--shadow-card);
  backdrop-filter: blur(10px);
  transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1), box-shadow 0.3s;
}

.stat-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 16px 32px rgba(15, 76, 255, 0.26);
  border-color: rgba(117, 182, 255, 0.62);
}

.stat-card__label {
  font-size: 14px;
  color: var(--muted);
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.stat-card__value {
  font-size: 32px;
  font-weight: 800;
  color: var(--surface-milk);
  line-height: 1;
}

.stat-card.is-risk {
  background: linear-gradient(145deg, rgba(239, 68, 68, 0.2), rgba(8, 21, 39, 0.72));
  border-color: rgba(251, 113, 133, 0.45);
}

.stat-card.is-risk .stat-card__value {
  color: var(--peach-500);
}

.charts-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(350px, 1fr));
  gap: 24px;
}

.chart-card {
  padding: 28px;
  background:
    linear-gradient(160deg, rgba(255, 255, 255, 0.08), rgba(15, 76, 255, 0.08)),
    rgba(8, 21, 39, 0.72);
  border-radius: var(--radius-shell);
  border: 1px solid rgba(182, 194, 217, 0.26);
  box-shadow: var(--shadow-card);
  min-height: 280px;
  display: flex;
  flex-direction: column;
}

.chart-card h3 {
  margin-bottom: 24px;
  font-size: 18px;
  color: var(--surface-milk);
  font-weight: 800;
  position: relative;
  padding-bottom: 12px;
}

.chart-card h3::after {
  content: '';
  position: absolute;
  bottom: 0;
  left: 0;
  width: 40px;
  height: 4px;
  background: var(--primary);
  border-radius: 2px;
}

.donut-chart-wrapper {
  display: flex;
  align-items: center;
  justify-content: space-around;
  gap: 20px;
  flex: 1;
}

.chart-legend {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.legend-item {
  display: flex;
  align-items: center;
  gap: 12px;
  font-size: 14px;
  color: var(--surface-milk);
  font-weight: 600;
}

.dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  box-shadow: inset 0 2px 4px rgba(0,0,0,0.1);
}

.dot.done { background: var(--mint-500); }
.dot.active { background: var(--primary); }

.progress-details {
  display: flex;
  flex-direction: column;
  gap: 24px;
  flex: 1;
  justify-content: center;
}

.progress-row {
  display: grid;
  grid-template-columns: 120px 1fr;
  align-items: center;
  gap: 16px;
}

.progress-row span {
  font-size: 13px;
  color: var(--muted);
  font-weight: 600;
}

.bar-rail {
  height: 12px;
  background: rgba(148, 163, 184, 0.24);
  border-radius: 6px;
  overflow: hidden;
  box-shadow: inset 0 2px 4px rgba(0,0,0,0.05);
}

.bar-fill {
  height: 100%;
  border-radius: 6px;
  transition: width 0.8s cubic-bezier(0.4, 0, 0.2, 1);
  position: relative;
  overflow: hidden;
}

.bar-fill::after {
  content: '';
  position: absolute;
  top: 0; left: 0; right: 0; bottom: 0;
  background: linear-gradient(90deg, rgba(255,255,255,0) 0%, rgba(255,255,255,0.18) 50%, rgba(255,255,255,0) 100%);
  animation: shimmer 2s infinite linear;
}

@keyframes shimmer {
  0% { transform: translateX(-100%); }
  100% { transform: translateX(100%); }
}

.bar-fill.done { background: linear-gradient(90deg, var(--mint-500), #10b981); }
.bar-fill.doing { background: linear-gradient(90deg, #0f4cff, #22d3ee); }
.bar-fill.todo { background: linear-gradient(90deg, var(--violet-500), #7c3aed); }

.donut-chart {
  --progress: 0%;
  position: relative;
  width: 160px;
  height: 160px;
  display: grid;
  place-items: center;
  border-radius: 50%;
  background: conic-gradient(var(--mint-500) 0 var(--progress), var(--primary) var(--progress) 100%);
  box-shadow: 0 12px 30px rgba(15, 76, 255, 0.22);
  transition: --progress 1s ease-out;
}

.donut-chart::after {
  content: '';
  position: absolute;
  width: 120px;
  height: 120px;
  border-radius: 50%;
  background: rgba(8, 21, 39, 0.9);
  box-shadow: inset 0 4px 12px rgba(2, 8, 23, 0.45);
}

.donut-chart strong {
  position: relative;
  z-index: 1;
  color: var(--surface-milk);
  font-size: 28px;
  font-weight: 800;
}

.empty-state {
  padding: 40px;
  border: 1px dashed rgba(182, 194, 217, 0.34);
  border-radius: 16px;
  color: var(--muted);
  text-align: center;
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 15px;
  font-weight: 600;
  background: rgba(255,255,255,0.04);
}
</style>

