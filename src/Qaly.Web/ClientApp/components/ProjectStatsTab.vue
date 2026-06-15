<script setup lang="ts">
import { computed } from 'vue'
import {
  AlertTriangle,
  ArrowUpRight,
  CheckCircle2,
  CircleDashed,
  Clock3,
  Gauge,
  ListChecks,
  ScanLine,
} from 'lucide-vue-next'

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

const activeTasks = computed(() => props.stats.inProgress + props.stats.inReview)
const openTasks = computed(() => Math.max(0, props.stats.total - props.stats.done))

const health = computed(() => {
  if (!props.stats.total) {
    return {
      label: 'Chưa có dữ liệu',
      tone: 'neutral',
      summary: 'Thêm nhiệm vụ để bắt đầu theo dõi sức khỏe dự án.',
    }
  }

  if (props.stats.overdue > 0) {
    return {
      label: 'Cần chú ý',
      tone: 'risk',
      summary: `${props.stats.overdue} nhiệm vụ quá hạn cần được ưu tiên xử lý.`,
    }
  }

  if (props.stats.completionRate >= 75) {
    return {
      label: 'Đúng tiến độ',
      tone: 'healthy',
      summary: 'Dự án đang ở trạng thái tốt và gần đạt mục tiêu.',
    }
  }

  return {
    label: 'Đang triển khai',
    tone: 'active',
    summary: `${openTasks.value} nhiệm vụ còn mở trong kế hoạch hiện tại.`,
  }
})

const statusSegments = computed(() => [
  { key: 'done', label: 'Hoàn thành', value: props.stats.done, color: '#10b981' },
  { key: 'progress', label: 'Đang làm', value: props.stats.inProgress, color: '#2563eb' },
  { key: 'review', label: 'Đang duyệt', value: props.stats.inReview, color: '#7c3aed' },
  { key: 'todo', label: 'Chưa bắt đầu', value: props.stats.todo, color: '#94a3b8' },
])

const donutBackground = computed(() => {
  if (!props.stats.total) return 'conic-gradient(var(--line) 0 100%)'

  let cursor = 0
  const stops = statusSegments.value.map((segment) => {
    const start = cursor
    cursor += (segment.value / props.stats.total) * 100
    return `${segment.color} ${start}% ${cursor}%`
  })
  return `conic-gradient(${stops.join(', ')})`
})

function percent(value: number) {
  return props.stats.total ? Math.round((value / props.stats.total) * 100) : 0
}
</script>

<template>
  <div class="stats-dashboard">
    <section class="executive-overview">
      <div class="health-panel" :class="`is-${health.tone}`">
        <div class="health-panel__top">
          <div class="section-kicker"><Gauge :size="15" /> Sức khỏe dự án</div>
          <span class="health-badge"><i></i>{{ health.label }}</span>
        </div>

        <div class="health-panel__body">
          <div>
            <span class="completion-label">Tiến độ hoàn thành</span>
            <strong class="completion-value">{{ stats.completionRate }}<small>%</small></strong>
            <p>{{ health.summary }}</p>
          </div>
          <div class="completion-gauge" :style="{ '--angle': `${stats.completionRate * 3.6}deg` }">
            <div>
              <CheckCircle2 :size="24" />
              <strong>{{ stats.done }}/{{ stats.total }}</strong>
              <span>đã hoàn thành</span>
            </div>
          </div>
        </div>

        <div class="overall-progress">
          <span :style="{ width: `${stats.completionRate}%` }"></span>
        </div>
        <div class="progress-scale"><span>0%</span><span>Mục tiêu 100%</span></div>
      </div>

      <div class="metrics-panel">
        <div class="metrics-panel__header">
          <div>
            <span class="section-kicker"><ScanLine :size="15" /> Chỉ số vận hành</span>
            <h3>Tình hình hiện tại</h3>
          </div>
          <span class="task-total">{{ stats.total }} nhiệm vụ</span>
        </div>

        <div class="metric-list">
          <article>
            <span class="metric-icon is-blue"><Clock3 :size="18" /></span>
            <div><small>Đang thực hiện</small><strong>{{ stats.inProgress }}</strong></div>
            <span class="metric-rate">{{ percent(stats.inProgress) }}%</span>
          </article>
          <article>
            <span class="metric-icon is-violet"><CircleDashed :size="18" /></span>
            <div><small>Chờ duyệt</small><strong>{{ stats.inReview }}</strong></div>
            <span class="metric-rate">{{ percent(stats.inReview) }}%</span>
          </article>
          <article>
            <span class="metric-icon is-green"><CheckCircle2 :size="18" /></span>
            <div><small>Đã hoàn thành</small><strong>{{ stats.done }}</strong></div>
            <span class="metric-rate">{{ percent(stats.done) }}%</span>
          </article>
          <article :class="{ 'is-risk': stats.overdue > 0 }">
            <span class="metric-icon is-red"><AlertTriangle :size="18" /></span>
            <div><small>Quá hạn</small><strong>{{ stats.overdue }}</strong></div>
            <span class="metric-rate">{{ percent(stats.overdue) }}%</span>
          </article>
        </div>
      </div>
    </section>

    <section class="analytics-grid">
      <article class="analytics-card distribution-card">
        <header class="card-header">
          <div>
            <span class="section-kicker"><ListChecks :size="15" /> Cơ cấu công việc</span>
            <h3>Phân bổ trạng thái</h3>
          </div>
          <span class="card-context">Theo tổng số task</span>
        </header>

        <div v-if="stats.total > 0" class="distribution-content">
          <div class="donut-chart" :style="{ background: donutBackground }">
            <div class="donut-center">
              <strong>{{ stats.total }}</strong>
              <span>Tổng task</span>
            </div>
          </div>

          <div class="status-breakdown">
            <div v-for="segment in statusSegments" :key="segment.key" class="status-row">
              <span class="status-color" :style="{ background: segment.color }"></span>
              <div>
                <span>{{ segment.label }}</span>
                <div class="mini-track"><i :style="{ width: `${percent(segment.value)}%`, background: segment.color }"></i></div>
              </div>
              <strong>{{ segment.value }}</strong>
              <small>{{ percent(segment.value) }}%</small>
            </div>
          </div>
        </div>
        <div v-else class="empty-state">Chưa có dữ liệu nhiệm vụ để phân tích.</div>
      </article>

      <article class="analytics-card delivery-card">
        <header class="card-header">
          <div>
            <span class="section-kicker"><ArrowUpRight :size="15" /> Khả năng bàn giao</span>
            <h3>Tiến độ thực thi</h3>
          </div>
          <span class="card-context">{{ openTasks }} việc còn mở</span>
        </header>

        <div v-if="stats.total > 0" class="delivery-content">
          <div class="delivery-summary">
            <div>
              <span>Đã hoàn thành</span>
              <strong>{{ stats.done }}<small>/{{ stats.total }}</small></strong>
            </div>
            <p>
              <template v-if="stats.overdue">
                Có <strong>{{ stats.overdue }} nhiệm vụ quá hạn</strong> trong {{ openTasks }} nhiệm vụ còn mở.
              </template>
              <template v-else>
                Không có nhiệm vụ quá hạn. Tiến độ hiện tại đang được kiểm soát.
              </template>
            </p>
          </div>

          <div class="delivery-bars">
            <div class="delivery-row">
              <div><span>Hoàn thành</span><strong>{{ percent(stats.done) }}%</strong></div>
              <div class="bar-track"><span class="is-done" :style="{ width: `${percent(stats.done)}%` }"></span></div>
              <small>{{ stats.done }} task</small>
            </div>
            <div class="delivery-row">
              <div><span>Đang xử lý</span><strong>{{ percent(activeTasks) }}%</strong></div>
              <div class="bar-track"><span class="is-active" :style="{ width: `${percent(activeTasks)}%` }"></span></div>
              <small>{{ activeTasks }} task</small>
            </div>
            <div class="delivery-row">
              <div><span>Chưa bắt đầu</span><strong>{{ percent(stats.todo) }}%</strong></div>
              <div class="bar-track"><span class="is-todo" :style="{ width: `${percent(stats.todo)}%` }"></span></div>
              <small>{{ stats.todo }} task</small>
            </div>
          </div>

          <div class="delivery-note" :class="{ risk: stats.overdue > 0 }">
            <AlertTriangle v-if="stats.overdue > 0" :size="17" />
            <CheckCircle2 v-else :size="17" />
            <span>{{ stats.overdue > 0 ? 'Ưu tiên xử lý các nhiệm vụ quá hạn trước khi nhận thêm việc mới.' : 'Nhịp độ thực thi ổn định, chưa phát hiện rủi ro quá hạn.' }}</span>
          </div>
        </div>
        <div v-else class="empty-state">Chưa có dữ liệu nhiệm vụ để phân tích.</div>
      </article>
    </section>
  </div>
</template>

<style scoped>
.stats-dashboard {
  display: flex;
  flex-direction: column;
  gap: 18px;
}

.executive-overview {
  display: grid;
  grid-template-columns: minmax(0, 1.08fr) minmax(380px, .92fr);
  overflow: hidden;
  border: 1px solid var(--line);
  border-radius: var(--radius-shell);
  background: var(--panel);
  box-shadow: 0 14px 36px rgba(15, 23, 42, .05);
}

.health-panel {
  position: relative;
  padding: 28px 30px;
  overflow: hidden;
  border-right: 1px solid var(--line);
  background:
    radial-gradient(circle at 90% 0%, rgba(37, 99, 235, .1), transparent 36%),
    linear-gradient(145deg, rgba(37, 99, 235, .045), transparent 62%);
}

.health-panel::before {
  content: '';
  position: absolute;
  inset: 0 auto 0 0;
  width: 4px;
  background: var(--primary);
}

.health-panel.is-risk::before { background: #dc2626; }
.health-panel.is-healthy::before { background: #10b981; }
.health-panel.is-neutral::before { background: #94a3b8; }

.health-panel__top,
.metrics-panel__header,
.card-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 18px;
}

.section-kicker {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  color: var(--primary);
  font-size: 11px;
  font-weight: 900;
  letter-spacing: .075em;
  text-transform: uppercase;
}

.health-badge {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  border: 1px solid var(--line);
  border-radius: 999px;
  padding: 6px 10px;
  color: var(--text);
  background: rgba(255, 255, 255, .7);
  font-size: 11px;
  font-weight: 800;
}

.health-badge i {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: var(--primary);
  box-shadow: 0 0 0 4px var(--primary-soft);
}

.is-risk .health-badge i { background: #dc2626; box-shadow: 0 0 0 4px rgba(220, 38, 38, .1); }
.is-healthy .health-badge i { background: #10b981; box-shadow: 0 0 0 4px rgba(16, 185, 129, .1); }
.is-neutral .health-badge i { background: #94a3b8; box-shadow: 0 0 0 4px rgba(148, 163, 184, .12); }

.health-panel__body {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 28px;
  margin: 24px 0 22px;
}

.completion-label {
  color: var(--muted);
  font-size: 12px;
  font-weight: 750;
}

.completion-value {
  display: block;
  margin: 5px 0 8px;
  color: var(--text-strong);
  font-size: clamp(48px, 6vw, 68px);
  line-height: .95;
  letter-spacing: -.065em;
}

.completion-value small {
  margin-left: 4px;
  color: var(--muted);
  font-size: .42em;
  letter-spacing: -.02em;
}

.health-panel__body p {
  max-width: 370px;
  margin: 0;
  color: var(--muted);
  font-size: 13px;
  line-height: 1.55;
}

.completion-gauge {
  --angle: 0deg;
  flex: 0 0 132px;
  width: 132px;
  height: 132px;
  padding: 8px;
  border-radius: 50%;
  background: conic-gradient(var(--primary) var(--angle), var(--line-light) 0);
}

.completion-gauge > div {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  color: var(--primary);
  background: var(--panel);
  box-shadow: inset 0 0 0 1px var(--line);
}

.completion-gauge strong { margin-top: 5px; color: var(--text-strong); font-size: 17px; }
.completion-gauge span { color: var(--muted); font-size: 9px; font-weight: 700; }

.overall-progress,
.mini-track,
.bar-track {
  overflow: hidden;
  border-radius: 999px;
  background: var(--line-light);
}

.overall-progress { height: 8px; }
.overall-progress span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, var(--primary), #38bdf8);
}

.progress-scale {
  display: flex;
  justify-content: space-between;
  margin-top: 7px;
  color: var(--text-muted);
  font-size: 9px;
  font-weight: 700;
}

.metrics-panel {
  padding: 26px 28px;
}

.metrics-panel__header h3,
.card-header h3 {
  margin: 7px 0 0;
  color: var(--text-strong);
  font-size: 19px;
  letter-spacing: -.025em;
}

.task-total,
.card-context {
  border: 1px solid var(--line);
  border-radius: 999px;
  padding: 6px 10px;
  color: var(--muted);
  background: var(--bg-soft);
  font-size: 10px;
  font-weight: 800;
}

.metric-list {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
  margin-top: 21px;
}

.metric-list article {
  display: grid;
  grid-template-columns: 38px 1fr auto;
  align-items: center;
  gap: 10px;
  min-height: 76px;
  border: 1px solid var(--line);
  border-radius: 12px;
  padding: 11px;
  background: var(--panel);
}

.metric-list article.is-risk {
  border-color: rgba(220, 38, 38, .22);
  background: rgba(254, 242, 242, .55);
}

.metric-icon {
  width: 38px;
  height: 38px;
  display: grid;
  place-items: center;
  border-radius: 10px;
}

.metric-icon.is-blue { color: #2563eb; background: rgba(37, 99, 235, .09); }
.metric-icon.is-violet { color: #7c3aed; background: rgba(124, 58, 237, .09); }
.metric-icon.is-green { color: #059669; background: rgba(16, 185, 129, .1); }
.metric-icon.is-red { color: #dc2626; background: rgba(220, 38, 38, .09); }

.metric-list article > div {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.metric-list small { color: var(--muted); font-size: 10px; font-weight: 700; }
.metric-list strong { color: var(--text-strong); font-size: 21px; line-height: 1; }
.metric-rate { color: var(--text-muted); font-size: 10px; font-weight: 800; }

.analytics-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  gap: 18px;
}

.analytics-card {
  min-height: 350px;
  padding: 25px 27px;
  border: 1px solid var(--line);
  border-radius: var(--radius-shell);
  background: var(--panel);
  box-shadow: 0 14px 36px rgba(15, 23, 42, .045);
}

.distribution-content {
  display: grid;
  grid-template-columns: 190px minmax(0, 1fr);
  align-items: center;
  gap: 34px;
  min-height: 260px;
}

.donut-chart {
  width: 174px;
  height: 174px;
  display: grid;
  place-items: center;
  border-radius: 50%;
  box-shadow: 0 12px 30px rgba(15, 23, 42, .08);
}

.donut-chart::after {
  display: none;
}

.donut-center {
  width: 116px;
  height: 116px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  background: linear-gradient(145deg, #ffffff 0%, #eef6ff 100%);
  box-shadow:
    inset 0 0 0 1px #d6e6f8,
    0 6px 18px rgba(37, 99, 235, .1);
}

.donut-center strong { color: #173b70; font-size: 31px; letter-spacing: -.04em; }
.donut-center span { color: #60738f; font-size: 10px; font-weight: 700; }

.status-breakdown {
  display: flex;
  flex-direction: column;
  gap: 15px;
}

.status-row {
  display: grid;
  grid-template-columns: 8px minmax(0, 1fr) 25px 36px;
  align-items: center;
  gap: 10px;
}

.status-color { width: 8px; height: 8px; border-radius: 50%; }
.status-row > div { display: flex; flex-direction: column; gap: 6px; }
.status-row > div > span { color: var(--text); font-size: 11px; font-weight: 750; }
.status-row strong { color: var(--text-strong); font-size: 13px; text-align: right; }
.status-row small { color: var(--muted); font-size: 10px; text-align: right; }
.mini-track { height: 4px; }
.mini-track i { display: block; height: 100%; border-radius: inherit; }

.delivery-content {
  display: flex;
  flex-direction: column;
  gap: 24px;
  margin-top: 25px;
}

.delivery-summary {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 24px;
  padding-bottom: 20px;
  border-bottom: 1px solid var(--line);
}

.delivery-summary > div { display: flex; flex-direction: column; gap: 5px; }
.delivery-summary span { color: var(--muted); font-size: 10px; font-weight: 800; text-transform: uppercase; letter-spacing: .05em; }
.delivery-summary strong { color: var(--text-strong); font-size: 34px; line-height: 1; }
.delivery-summary strong small { color: var(--muted); font-size: 15px; }
.delivery-summary p { max-width: 270px; margin: 0; color: var(--muted); font-size: 11px; line-height: 1.5; text-align: right; }
.delivery-summary p strong { color: #dc2626; font-size: inherit; }

.delivery-bars { display: flex; flex-direction: column; gap: 17px; }
.delivery-row { display: grid; grid-template-columns: minmax(0, 1fr) 58px; gap: 7px 12px; align-items: end; }
.delivery-row > div:first-child { display: flex; justify-content: space-between; grid-column: 1; }
.delivery-row span, .delivery-row strong, .delivery-row small { font-size: 10px; font-weight: 750; }
.delivery-row span { color: var(--text); }.delivery-row strong { color: var(--text-strong); }.delivery-row small { grid-column: 2; grid-row: 1 / span 2; align-self: center; color: var(--muted); text-align: right; }
.bar-track { grid-column: 1; height: 7px; }
.bar-track > span { display: block; height: 100%; border-radius: inherit; }
.bar-track .is-done { background: #10b981; }.bar-track .is-active { background: #2563eb; }.bar-track .is-todo { background: #94a3b8; }

.delivery-note {
  display: flex;
  align-items: center;
  gap: 9px;
  margin-top: auto;
  border: 1px solid rgba(16, 185, 129, .18);
  border-radius: 10px;
  padding: 10px 12px;
  color: #047857;
  background: rgba(236, 253, 245, .65);
  font-size: 10px;
  font-weight: 750;
}

.delivery-note.risk {
  border-color: rgba(220, 38, 38, .18);
  color: #b91c1c;
  background: rgba(254, 242, 242, .65);
}

.empty-state {
  min-height: 250px;
  display: grid;
  place-items: center;
  color: var(--muted);
  font-size: 13px;
  text-align: center;
}

@media (max-width: 1100px) {
  .executive-overview { grid-template-columns: 1fr; }
  .health-panel { border-right: 0; border-bottom: 1px solid var(--line); }
}

@media (max-width: 850px) {
  .analytics-grid { grid-template-columns: 1fr; }
}

@media (max-width: 620px) {
  .health-panel, .metrics-panel, .analytics-card { padding: 20px; }
  .health-panel__body { align-items: flex-start; }
  .completion-gauge { width: 100px; height: 100px; flex-basis: 100px; }
  .metric-list { grid-template-columns: 1fr; }
  .distribution-content { grid-template-columns: 1fr; justify-items: center; gap: 20px; padding-top: 24px; }
  .status-breakdown { width: 100%; }
  .delivery-summary { align-items: flex-start; flex-direction: column; }
  .delivery-summary p { text-align: left; }
}
</style>
