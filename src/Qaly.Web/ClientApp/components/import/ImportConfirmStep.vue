<script setup lang="ts">
import { ArrowLeft, Check, AlertTriangle } from 'lucide-vue-next'
import { computed } from 'vue'

const props = defineProps<{
  parseResult: any
  mappings: { columnIndex: number; targetField: string }[]
  skipDuplicates: boolean
  isNewProject: boolean
  newProjectName: string
  projectName?: string
  isLoading: boolean
  defaultAssigneeId: string | null
  assignToMeIfEmpty: boolean
  defaultPriority: string | null
  enableAiCategorization: boolean
}>()

const emit = defineEmits<{
  back: []
  confirm: []
}>()

// ─── Client-side preview summary ──────────────────────────
// We compute the preview from parseResult + mappings locally,
// no extra API call needed.

const statusAliases: Record<string, string> = {
  'todo': 'Todo', 'backlog': 'Todo', 'new': 'Todo', 'mới': 'Todo', 'open': 'Todo',
  'in progress': 'InProgress', 'inprogress': 'InProgress', 'doing': 'InProgress',
  'đang làm': 'InProgress', 'wip': 'InProgress', 'active': 'InProgress',
  'review': 'InReview', 'in review': 'InReview', 'inreview': 'InReview',
  'đang review': 'InReview',
  'done': 'Done', 'completed': 'Done', 'hoàn thành': 'Done', 'xong': 'Done',
  'finished': 'Done', 'closed': 'Done',
  'hold': 'OnHold', 'on hold': 'OnHold', 'onhold': 'OnHold',
  'blocked': 'OnHold', 'tạm dừng': 'OnHold', 'paused': 'OnHold',
}

const validStatuses = ['Todo', 'InProgress', 'OnHold', 'InReview', 'Done']

function normalizeStatus(raw: string | null): { status: string; unmapped: boolean } {
  if (!raw || !raw.trim()) return { status: 'Todo', unmapped: false }
  const trimmed = raw.trim()
  const directMatch = validStatuses.find(s => s.toLowerCase() === trimmed.toLowerCase())
  if (directMatch) return { status: directMatch, unmapped: false }
  const aliasMatch = statusAliases[trimmed.toLowerCase()]
  if (aliasMatch) return { status: aliasMatch, unmapped: false }
  return { status: 'Todo', unmapped: true }
}

const fieldMap = computed(() => {
  const map: Record<string, number> = {}
  for (const m of props.mappings) {
    if (m.targetField !== 'Skip') map[m.targetField] = m.columnIndex
  }
  return map
})

const previewSummary = computed(() => {
  const rows = props.parseResult?.previewRows ?? []
  const totalRowCount = props.parseResult?.totalRowCount ?? 0
  const allRows = totalRowCount // We only have preview rows but know the total

  const statusIdx = fieldMap.value['Status']
  const priorityIdx = fieldMap.value['Priority']
  const titleIdx = fieldMap.value['Title']
  const labelsIdx = fieldMap.value['Labels']

  // Analyze preview rows to estimate distribution
  const statusDist: Record<string, number> = {}
  const unmappedStatuses = new Set<string>()
  const labelsSet = new Set<string>()
  let emptyTitleCount = 0
  let aiPreviewCount = 0

  // Use all available preview rows for analysis
  for (const row of rows) {
    // Check title
    if (titleIdx !== undefined) {
      const title = row[titleIdx]?.trim()
      if (!title) emptyTitleCount++
      if (title && props.enableAiCategorization) {
        const rawStatus = statusIdx !== undefined ? row[statusIdx]?.trim() : ''
        const rawPriority = priorityIdx !== undefined ? row[priorityIdx]?.trim() : ''
        if (!rawStatus || !rawPriority) aiPreviewCount++
      }
    }

    // Analyze status
    if (statusIdx !== undefined) {
      const rawStatus = row[statusIdx]
      const { status, unmapped } = normalizeStatus(rawStatus)
      statusDist[status] = (statusDist[status] || 0) + 1
      if (unmapped && rawStatus?.trim()) unmappedStatuses.add(rawStatus.trim())
    } else {
      statusDist['Todo'] = (statusDist['Todo'] || 0) + 1
    }

    // Collect labels
    if (labelsIdx !== undefined && row[labelsIdx]) {
      const parts = row[labelsIdx].split(/[,;]/).map((l: string) => l.trim()).filter(Boolean)
      for (const p of parts) labelsSet.add(p)
    }
  }

  const estimatedAiCategorization = rows.length > 0
    ? Math.round((aiPreviewCount / rows.length) * totalRowCount)
    : 0

  return {
    totalRows: totalRowCount,
    estimatedImport: totalRowCount - emptyTitleCount,
    estimatedSkip: emptyTitleCount,
    statusDistribution: statusDist,
    unmappedStatuses: [...unmappedStatuses],
    newLabelsEstimate: labelsSet.size,
    estimatedAiCategorization,
    previewRowCount: rows.length,
    targetProjectName: props.isNewProject ? props.newProjectName : props.projectName,
  }
})

const statusIcons: Record<string, string> = {
  'Todo': '📌',
  'InProgress': '🔄',
  'OnHold': '⏸️',
  'InReview': '👀',
  'Done': '✅',
}

const statusLabels: Record<string, string> = {
  'Todo': 'Cần làm',
  'InProgress': 'Đang làm',
  'OnHold': 'Tạm dừng',
  'InReview': 'Đang duyệt',
  'Done': 'Hoàn thành',
}
</script>

<template>
  <div class="import-step">
    <div class="confirm-hero">
      <div class="confirm-icon">📋</div>
      <h3>Xác nhận Import</h3>
      <p class="confirm-subtitle">
        Kiểm tra thông tin trước khi import vào
        <strong>{{ previewSummary.targetProjectName }}</strong>
      </p>
    </div>

    <!-- Summary stats -->
    <div class="confirm-stats">
      <div class="confirm-stat">
        <span class="confirm-stat__label">Tổng số dòng đọc được</span>
        <span class="confirm-stat__value">{{ previewSummary.totalRows }}</span>
      </div>
      <div class="confirm-stat confirm-stat--success">
        <span class="confirm-stat__label">Tasks sẽ được tạo</span>
        <span class="confirm-stat__value">~{{ previewSummary.estimatedImport }}</span>
      </div>
      <div v-if="previewSummary.estimatedSkip > 0" class="confirm-stat confirm-stat--warn">
        <span class="confirm-stat__label">Dòng bỏ qua (trống/lỗi)</span>
        <span class="confirm-stat__value">~{{ previewSummary.estimatedSkip }}</span>
      </div>
      <div v-if="previewSummary.newLabelsEstimate > 0" class="confirm-stat">
        <span class="confirm-stat__label">Labels phát hiện</span>
        <span class="confirm-stat__value">{{ previewSummary.newLabelsEstimate }}</span>
      </div>
    </div>

    <!-- Status distribution -->
    <div v-if="Object.keys(previewSummary.statusDistribution).length" class="confirm-distribution">
      <p class="confirm-section-title">Phân bố theo cột Kanban <span class="hint">(ước lượng từ {{ previewSummary.previewRowCount }} dòng preview)</span></p>
      <div v-for="(count, status) in previewSummary.statusDistribution" :key="status" class="dist-row">
        <span class="dist-status">{{ statusIcons[status as string] || '📌' }} {{ statusLabels[status as string] || status }}</span>
        <div class="dist-bar-wrap">
          <div class="dist-bar" :style="{ width: (count / previewSummary.previewRowCount * 100) + '%' }"></div>
        </div>
        <span class="dist-count">{{ count }}</span>
      </div>
    </div>

    <!-- Unmapped statuses warning -->
    <div v-if="previewSummary.unmappedStatuses.length" class="import-warning">
      <AlertTriangle :size="16" />
      <div>
        <strong>Status không nhận diện được</strong> (sẽ đặt về Todo):
        <span class="unmapped-list">{{ previewSummary.unmappedStatuses.join(', ') }}</span>
      </div>
    </div>

    <!-- Options reminder -->
    <div class="confirm-options">
      <span v-if="defaultAssigneeId" class="option-badge">Có người phụ trách mặc định</span>
      <span v-if="enableAiCategorization" class="option-badge option-badge--ai">
        AI sẽ phân loại khoảng {{ previewSummary.estimatedAiCategorization }} task
      </span>
      <span v-if="skipDuplicates" class="option-badge option-badge--active">✓ Bỏ qua task trùng tên</span>
        <span v-else class="option-badge">Thêm tất cả, không kiểm tra trùng</span>

      <span v-if="assignToMeIfEmpty" class="option-badge">Giao cho tôi (nếu trống)</span>
      <span v-if="defaultPriority" class="option-badge">Ưu tiên mặc định: {{ defaultPriority }}</span>
      <span v-if="enableAiCategorization" class="option-badge option-badge--ai">✨ Dùng AI phân loại</span>

      <span v-if="isNewProject" class="option-badge option-badge--new">+ Tạo dự án mới</span>
      <span v-else class="option-badge option-badge--merge">↗ Merge vào dự án có sẵn</span>
    </div>

    <!-- Safety notice -->
    <div class="confirm-notice">
      <span>⚠️</span>
      <p>Card cũ không bị thay đổi. Bạn có thể hoàn tác (undo) trong vòng 30 phút sau khi import.</p>
    </div>

    <div class="confirm-notice confirm-notice--subtle">
      <span>i</span>
      <p>Những con số ở bước này là ước lượng từ preview. Số thành công, thất bại và trùng bỏ qua chính xác sẽ hiện ở màn hình kết quả sau import.</p>
    </div>

    <div class="import-actions">
      <button class="btn btn--ghost" type="button" @click="emit('back')"><ArrowLeft :size="16" /> Quay lại</button>
      <button class="btn btn--primary btn--import-confirm" :disabled="isLoading" @click="emit('confirm')">
        <template v-if="isLoading">
          <span class="spinner"></span> Đang import...
        </template>
        <template v-else>
          Nhập {{ previewSummary.totalRows }} task <Check :size="16" />
        </template>
      </button>
    </div>
  </div>
</template>

<style scoped>
.confirm-hero {
  text-align: center;
  padding: 16px 0 12px;
}
.confirm-icon {
  font-size: 2.5rem;
  margin-bottom: 6px;
  animation: bounceIn .5s ease;
}
.confirm-hero h3 {
  margin: 0 0 4px;
  font-size: 1.15rem;
}
.confirm-subtitle {
  font-size: .82rem;
  color: #6b7280;
  margin: 0;
}

.confirm-stats {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(130px, 1fr));
  gap: 10px;
  margin: 18px 0 14px;
}
.confirm-stat {
  padding: 14px;
  border-radius: 12px;
  text-align: center;
  background: rgba(255,255,255,.04);
  border: 1px solid #e5e7eb;
  transition: transform .2s ease, box-shadow .2s ease;
}
.confirm-stat:hover {
  transform: translateY(-2px);
  box-shadow: 0 8px 24px rgba(0,0,0,.2);
}
.confirm-stat--success {
  border-color: rgba(34,197,94,.25);
  background: rgba(34,197,94,.06);
}
.confirm-stat--warn {
  border-color: rgba(245,158,11,.25);
  background: rgba(245,158,11,.06);
}
.confirm-stat__label {
  display: block;
  font-size: .72rem;
  color: #6b7280;
  margin-bottom: 6px;
}
.confirm-stat__value {
  display: block;
  font-size: 1.5rem;
  font-weight: 700;
}

.confirm-distribution {
  margin: 14px 0;
}
.confirm-section-title {
  font-size: .82rem;
  font-weight: 600;
  color: #374151;
  margin: 0 0 10px;
}
.confirm-section-title .hint {
  font-weight: 400;
  font-size: .72rem;
  color: #9ca3af;
}

.dist-row { display: flex; align-items: center; gap: 10px; margin-bottom: 6px; }
.dist-status { font-size: .8rem; width: 110px; color: #4b5563; }
.dist-bar-wrap { flex: 1; height: 8px; border-radius: 4px; background: #eef2f7; overflow: hidden; }
.dist-bar {
  height: 100%; border-radius: 4px;
  background: linear-gradient(90deg, #0f4cff, #22d3ee);
  transition: width .6s cubic-bezier(.22,1,.36,1);
}
.dist-count { font-size: .78rem; width: 28px; text-align: right; color: #6b7280; }

.confirm-options {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin: 14px 0;
}
.option-badge {
  padding: 4px 12px;
  border-radius: 20px;
  font-size: .72rem;
  font-weight: 500;
  background: rgba(255,255,255,.05);
  border: 1px solid #e5e7eb;
  color: #4b5563;
}
.option-badge--active {
  background: rgba(34,197,94,.08);
  border-color: rgba(34,197,94,.2);
  color: #22c55e;
}
.option-badge--new {
  background: rgba(31,128,255,.14);
  border-color: rgba(117,182,255,.34);
  color: #9fd3ff;
}
.option-badge--merge {
  background: rgba(245,158,11,.08);
  border-color: rgba(245,158,11,.2);
  color: #f59e0b;
}
.option-badge--ai {
  background: rgba(34,211,238,.12);
  border-color: rgba(34,211,238,.3);
  color: #9deefb;
}

.confirm-notice {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  padding: 12px 16px;
  border-radius: 10px;
  background: rgba(31,128,255,.08);
  border: 1px solid rgba(117,182,255,.24);
  font-size: .8rem;
  color: rgba(255,255,255,.6);
  color: #4b5563;
  margin: 8px 0;
}
.confirm-notice p { margin: 0; }

.confirm-notice--subtle {
  background: #f8fafc;
  border-color: #e5e7eb;
}

.unmapped-list {
  color: #f59e0b;
  font-weight: 600;
}

.btn--import-confirm {
  padding: 10px 24px;
  font-size: .9rem;
  font-weight: 600;
}

.spinner {
  display: inline-block;
  width: 14px; height: 14px;
  border: 2px solid rgba(255,255,255,.34);
  border-top-color: #fff;
  border-radius: 50%;
  animation: spin .6s linear infinite;
}

@keyframes spin { to { transform: rotate(360deg); } }
@keyframes bounceIn {
  0% { transform: scale(0.3); opacity: 0; }
  50% { transform: scale(1.05); }
  70% { transform: scale(0.95); }
  100% { transform: scale(1); opacity: 1; }
}
</style>
