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
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  gap: 16px;
}

.stat-card {
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  background: white;
}

.stat-card__label {
  font-size: 13px;
  color: var(--muted);
  font-weight: 600;
}

.stat-card__value {
  font-size: 24px;
  font-weight: 800;
  color: var(--text);
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
  padding: 24px;
  background: white;
  min-height: 280px;
}

.chart-card h3 {
  margin-bottom: 24px;
  font-size: 16px;
  color: var(--text);
  font-weight: 700;
}

.donut-chart-wrapper {
  display: flex;
  align-items: center;
  justify-content: space-around;
  gap: 20px;
}

.chart-legend {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.legend-item {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  color: var(--text);
}

.dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
}

.dot.done { background: var(--mint-500); }
.dot.active { background: var(--primary); }

.progress-details {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.progress-row {
  display: grid;
  grid-template-columns: 100px 1fr;
  align-items: center;
  gap: 16px;
}

.progress-row span {
  font-size: 14px;
  color: var(--muted);
}

.bar-rail {
  height: 10px;
  background: var(--line);
  border-radius: 5px;
  overflow: hidden;
}

.bar-fill {
  height: 100%;
  border-radius: 5px;
  transition: width 0.4s ease;
}

.bar-fill.done { background: var(--mint-500); }
.bar-fill.doing { background: var(--primary); }
.bar-fill.todo { background: var(--violet-500); }

.donut-chart {
  --progress: 0%;
  position: relative;
  width: 140px;
  height: 140px;
  display: grid;
  place-items: center;
  border-radius: 50%;
  background: conic-gradient(var(--mint-500) 0 var(--progress), var(--primary) var(--progress) 100%);
  box-shadow: var(--shadow-card);
}

.donut-chart::after {
  content: '';
  position: absolute;
  width: 100px;
  height: 100px;
  border-radius: 50%;
  background: white;
}

.donut-chart strong {
  position: relative;
  z-index: 1;
  color: var(--text);
  font-size: 20px;
  font-weight: 800;
}

.empty-state {
  padding: 40px;
  border: 1px dashed var(--line);
  border-radius: 12px;
  color: var(--muted);
  text-align: center;
}
</style>
