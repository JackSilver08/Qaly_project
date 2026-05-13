<script setup lang="ts">
import { ref, computed } from 'vue'
import { X, Upload, FileSpreadsheet, ArrowLeft, ArrowRight, Check, Undo2, AlertTriangle } from 'lucide-vue-next'
import { showSuccess, showError } from '../../composables/use-toast'

const props = defineProps<{
  projectId?: string
  projectName?: string
}>()

const emit = defineEmits<{
  close: []
  imported: [result: any]
}>()

// ─── State ───────────────────────────────────────────
const step = ref(1)
const file = ref<File | null>(null)
const isDragging = ref(false)
const isLoading = ref(false)

// Parse result
const parseResult = ref<any>(null)

// Mapping state
const firstRowIsHeader = ref(true)
const skipDuplicates = ref(false)
const selectedSheet = ref<string | null>(null)
const mappings = ref<{ columnIndex: number; targetField: string }[]>([])
const newProjectName = ref('')

// Import result
const importResult = ref<any>(null)

const isNewProject = computed(() => !props.projectId)

const targetFields = [
  { value: 'Title', label: '📝 Tiêu đề (Title)', required: true },
  { value: 'Description', label: '📋 Mô tả' },
  { value: 'Status', label: '📊 Trạng thái (Cột Kanban)' },
  { value: 'Priority', label: '🔥 Độ ưu tiên' },
  { value: 'DueDate', label: '📅 Hạn chót' },
  { value: 'EstimatedHours', label: '⏱️ Giờ ước tính' },
  { value: 'Labels', label: '🏷️ Nhãn (Labels)' },
  { value: 'Skip', label: '⏭️ Bỏ qua' },
]

const hasTitleMapping = computed(() =>
  mappings.value.some(m => m.targetField === 'Title')
)

const acceptedTypes = '.csv,.xlsx,.tsv'

// ─── File handling ───────────────────────────────────

function onDragOver(e: DragEvent) {
  e.preventDefault()
  isDragging.value = true
}

function onDragLeave() {
  isDragging.value = false
}

function onDrop(e: DragEvent) {
  e.preventDefault()
  isDragging.value = false
  const droppedFile = e.dataTransfer?.files[0]
  if (droppedFile) selectFile(droppedFile)
}

function onFileInput(e: Event) {
  const input = e.target as HTMLInputElement
  if (input.files?.[0]) selectFile(input.files[0])
}

function selectFile(f: File) {
  const ext = f.name.split('.').pop()?.toLowerCase()
  if (!['csv', 'xlsx', 'tsv'].includes(ext || '')) {
    showError('Chỉ hỗ trợ file .csv, .xlsx, .tsv')
    return
  }
  if (f.size > 5 * 1024 * 1024) {
    showError('File vượt quá giới hạn 5MB')
    return
  }
  file.value = f
}

// ─── Step 1 → Step 2: Parse file ─────────────────────

async function parseFile() {
  if (!file.value) return
  isLoading.value = true

  try {
    const formData = new FormData()
    formData.append('file', file.value)

    const res = await fetch('/api/import/parse', {
      method: 'POST',
      body: formData,
    })
    const data = await res.json()

    if (!data.isSuccess) {
      showError(data.error || 'Không thể đọc file')
      return
    }

    parseResult.value = data.data
    // Initialize mappings from suggestions
    mappings.value = data.data.suggestions.map((s: any) => ({
      columnIndex: s.columnIndex,
      targetField: s.suggestedField || 'Skip',
    }))
    if (data.data.sheetNames?.length > 0) {
      selectedSheet.value = data.data.sheetNames[0]
    }
    if (isNewProject.value) {
      newProjectName.value = file.value!.name.replace(/\.(csv|xlsx|tsv)$/i, '')
    }
    step.value = 2
  } catch (e: any) {
    showError('Lỗi kết nối server')
  } finally {
    isLoading.value = false
  }
}

// ─── Step 2 → Step 3: Execute import ─────────────────

async function executeImport() {
  if (!file.value || !hasTitleMapping.value) return
  isLoading.value = true

  try {
    const formData = new FormData()
    formData.append('file', file.value)
    formData.append('request', JSON.stringify({
      projectId: props.projectId || null,
      newProjectName: isNewProject.value ? newProjectName.value : null,
      mappings: mappings.value,
      firstRowIsHeader: firstRowIsHeader.value,
      skipDuplicates: skipDuplicates.value,
      sheetName: selectedSheet.value,
    }))

    const res = await fetch('/api/import/execute', {
      method: 'POST',
      body: formData,
    })
    const data = await res.json()

    if (!data.isSuccess) {
      showError(data.error || 'Import thất bại')
      return
    }

    importResult.value = data.data
    step.value = 3
    showSuccess(`Đã import thành công ${data.data.importedCount} task!`)
  } catch (e: any) {
    showError('Lỗi kết nối server')
  } finally {
    isLoading.value = false
  }
}

async function undoImport() {
  if (!importResult.value?.importSessionId) return
  if (!confirm('Bạn có chắc chắn muốn hoàn tác? Tất cả task đã import sẽ bị xóa.')) return

  isLoading.value = true
  try {
    const res = await fetch(`/api/import/sessions/${importResult.value.importSessionId}`, {
      method: 'DELETE',
    })
    const data = await res.json()
    if (data.isSuccess) {
      showSuccess(`Đã hoàn tác ${data.data} task`)
      emit('close')
    } else {
      showError(data.error || 'Không thể hoàn tác')
    }
  } catch {
    showError('Lỗi kết nối')
  } finally {
    isLoading.value = false
  }
}

function finish() {
  emit('imported', importResult.value)
  emit('close')
}

function formatFileSize(bytes: number) {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / 1048576).toFixed(1) + ' MB'
}
</script>

<template>
  <Teleport to="body">
    <div class="import-backdrop" @click.self="$emit('close')">
      <div class="import-modal glass-card">
        <!-- Header -->
        <div class="import-header">
          <div class="import-header__left">
            <FileSpreadsheet :size="22" />
            <h2>Import CSV / Excel</h2>
          </div>
          <button class="icon-button" @click="$emit('close')"><X :size="18" /></button>
        </div>

        <!-- Stepper -->
        <div class="import-stepper">
          <div v-for="s in 3" :key="s" :class="['stepper-dot', { active: step >= s, current: step === s }]">
            {{ s }}
          </div>
          <div class="stepper-labels">
            <span :class="{ active: step >= 1 }">Upload</span>
            <span :class="{ active: step >= 2 }">Mapping</span>
            <span :class="{ active: step >= 3 }">Kết quả</span>
          </div>
        </div>

        <!-- Step 1: Upload -->
        <div v-if="step === 1" class="import-step">
          <div v-if="isNewProject" class="import-field">
            <label>Tên dự án mới</label>
            <input v-model="newProjectName" type="text" placeholder="Nhập tên dự án..." class="import-input" />
          </div>
          <div v-else class="import-info-banner">
            <span>📂</span>
            <p>Thêm task vào dự án: <strong>{{ projectName }}</strong></p>
          </div>

          <div
            :class="['import-dropzone', { dragging: isDragging, 'has-file': !!file }]"
            @dragover="onDragOver"
            @dragleave="onDragLeave"
            @drop="onDrop"
          >
            <template v-if="!file">
              <Upload :size="40" class="dropzone-icon" />
              <p class="dropzone-text">Kéo thả file vào đây</p>
              <p class="dropzone-hint">hoặc</p>
              <label class="btn btn--primary btn--sm">
                Chọn file
                <input type="file" :accept="acceptedTypes" hidden @change="onFileInput" />
              </label>
              <p class="dropzone-formats">.csv, .xlsx, .tsv · Tối đa 5MB · 2000 dòng</p>
            </template>
            <template v-else>
              <FileSpreadsheet :size="32" class="dropzone-icon--selected" />
              <p class="dropzone-filename">{{ file.name }}</p>
              <p class="dropzone-filesize">{{ formatFileSize(file.size) }}</p>
              <button class="btn btn--ghost btn--sm" @click="file = null">Chọn file khác</button>
            </template>
          </div>

          <div class="import-actions">
            <button class="btn btn--ghost" @click="$emit('close')">Hủy</button>
            <button class="btn btn--primary" :disabled="!file || isLoading" @click="parseFile">
              <template v-if="isLoading">Đang đọc...</template>
              <template v-else>Tiếp tục <ArrowRight :size="16" /></template>
            </button>
          </div>
        </div>

        <!-- Step 2: Mapping -->
        <div v-if="step === 2 && parseResult" class="import-step">
          <!-- Sheet selector -->
          <div v-if="parseResult.sheetNames?.length > 1" class="import-field">
            <label>Chọn Sheet</label>
            <select v-model="selectedSheet" class="import-select">
              <option v-for="name in parseResult.sheetNames" :key="name" :value="name">{{ name }}</option>
            </select>
          </div>

          <!-- Options row -->
          <div class="import-options-row">
            <label class="import-toggle">
              <input v-model="firstRowIsHeader" type="checkbox" />
              <span>Dòng đầu là header</span>
            </label>
            <label class="import-toggle">
              <input v-model="skipDuplicates" type="checkbox" />
              <span>Bỏ qua task trùng tên</span>
            </label>
          </div>

          <!-- Data preview -->
          <div class="import-preview-wrap">
            <p class="import-preview-title">Xem trước dữ liệu ({{ parseResult.totalRowCount }} dòng)</p>
            <div class="import-preview-table-wrap">
              <table class="import-preview-table">
                <thead>
                  <tr>
                    <th v-for="(h, i) in parseResult.headers" :key="i">{{ h }}</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(row, ri) in parseResult.previewRows" :key="ri">
                    <td v-for="(cell, ci) in row" :key="ci">{{ cell || '—' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <!-- Column mapping -->
          <div class="import-mapping">
            <p class="import-mapping-title">Mapping cột</p>
            <div v-for="(m, i) in mappings" :key="i" class="import-mapping-row">
              <span class="mapping-source">{{ parseResult.headers[m.columnIndex] }}</span>
              <ArrowRight :size="16" class="mapping-arrow" />
              <select v-model="m.targetField" class="import-select mapping-target">
                <option v-for="f in targetFields" :key="f.value" :value="f.value">{{ f.label }}</option>
              </select>
            </div>
          </div>

          <div v-if="!hasTitleMapping" class="import-warning">
            <AlertTriangle :size="16" />
            <span>Cần ít nhất 1 cột map vào "Tiêu đề (Title)"</span>
          </div>

          <div class="import-actions">
            <button class="btn btn--ghost" @click="step = 1"><ArrowLeft :size="16" /> Quay lại</button>
            <button class="btn btn--primary" :disabled="!hasTitleMapping || isLoading" @click="executeImport">
              <template v-if="isLoading">Đang import...</template>
              <template v-else>Import {{ parseResult.totalRowCount }} task <Check :size="16" /></template>
            </button>
          </div>
        </div>

        <!-- Step 3: Result -->
        <div v-if="step === 3 && importResult" class="import-step">
          <div class="import-result-hero">
            <div class="result-check">✅</div>
            <h3>Import hoàn tất!</h3>
          </div>

          <div class="import-result-stats">
            <div class="stat-item"><span class="stat-label">Tổng dòng</span><span class="stat-value">{{ importResult.totalRows }}</span></div>
            <div class="stat-item stat--success"><span class="stat-label">Đã import</span><span class="stat-value">{{ importResult.importedCount }}</span></div>
            <div v-if="importResult.skippedCount > 0" class="stat-item stat--warn"><span class="stat-label">Bỏ qua</span><span class="stat-value">{{ importResult.skippedCount }}</span></div>
            <div v-if="importResult.newLabelsCreated > 0" class="stat-item"><span class="stat-label">Label mới</span><span class="stat-value">{{ importResult.newLabelsCreated }}</span></div>
          </div>

          <!-- Status distribution -->
          <div v-if="Object.keys(importResult.statusDistribution).length" class="import-distribution">
            <p class="dist-title">Phân bố theo cột Kanban</p>
            <div v-for="(count, status) in importResult.statusDistribution" :key="status" class="dist-row">
              <span class="dist-status">{{ status }}</span>
              <div class="dist-bar-wrap">
                <div class="dist-bar" :style="{ width: (count / importResult.importedCount * 100) + '%' }"></div>
              </div>
              <span class="dist-count">{{ count }}</span>
            </div>
          </div>

          <!-- Unmapped statuses warning -->
          <div v-if="importResult.unmappedStatuses?.length" class="import-warning">
            <AlertTriangle :size="16" />
            <span>Các giá trị Status không nhận diện (đã đặt về Todo): {{ importResult.unmappedStatuses.join(', ') }}</span>
          </div>

          <div class="import-actions">
            <button class="btn btn--ghost btn--danger" @click="undoImport" :disabled="isLoading">
              <Undo2 :size="16" /> Hoàn tác import
            </button>
            <button class="btn btn--primary" @click="finish">
              Xong <Check :size="16" />
            </button>
          </div>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.import-backdrop {
  position: fixed; inset: 0; z-index: 9999;
  background: rgba(0,0,0,.55); backdrop-filter: blur(6px);
  display: flex; align-items: center; justify-content: center;
  animation: fadeIn .2s ease;
}
.import-modal {
  width: min(680px, 94vw); max-height: 88vh; overflow-y: auto;
  border-radius: 16px; padding: 0;
  background: var(--glass-bg, rgba(30,30,45,.92));
  border: 1px solid rgba(255,255,255,.08);
  box-shadow: 0 24px 80px rgba(0,0,0,.5);
}
.import-header {
  display: flex; align-items: center; justify-content: space-between;
  padding: 20px 24px; border-bottom: 1px solid rgba(255,255,255,.06);
}
.import-header__left { display: flex; align-items: center; gap: 10px; }
.import-header__left h2 { font-size: 1.1rem; font-weight: 600; margin: 0; }

/* Stepper */
.import-stepper {
  display: flex; align-items: center; justify-content: center;
  gap: 40px; padding: 16px 24px; position: relative;
}
.stepper-dot {
  width: 32px; height: 32px; border-radius: 50%;
  display: flex; align-items: center; justify-content: center;
  font-size: .8rem; font-weight: 700;
  background: rgba(255,255,255,.06); color: rgba(255,255,255,.3);
  transition: all .3s ease; position: relative; z-index: 1;
}
.stepper-dot.active { background: var(--accent, #6366f1); color: #fff; }
.stepper-dot.current { box-shadow: 0 0 0 4px rgba(99,102,241,.3); }
.stepper-labels {
  position: absolute; bottom: 2px; left: 0; right: 0;
  display: flex; justify-content: center; gap: 40px;
}
.stepper-labels span { font-size: .7rem; color: rgba(255,255,255,.3); width: 32px; text-align: center; }
.stepper-labels span.active { color: rgba(255,255,255,.7); }

/* Steps */
.import-step { padding: 20px 24px; }

/* Upload */
.import-dropzone {
  border: 2px dashed rgba(255,255,255,.12); border-radius: 12px;
  padding: 40px 24px; text-align: center;
  transition: all .25s ease; cursor: pointer;
  margin: 12px 0;
}
.import-dropzone.dragging { border-color: var(--accent, #6366f1); background: rgba(99,102,241,.08); }
.import-dropzone.has-file { border-color: rgba(34,197,94,.3); background: rgba(34,197,94,.04); }
.dropzone-icon { color: rgba(255,255,255,.2); margin-bottom: 12px; }
.dropzone-icon--selected { color: rgba(34,197,94,.7); margin-bottom: 8px; }
.dropzone-text { font-size: 1rem; margin: 0 0 4px; color: rgba(255,255,255,.6); }
.dropzone-hint { font-size: .8rem; color: rgba(255,255,255,.3); margin: 0 0 10px; }
.dropzone-formats { font-size: .72rem; color: rgba(255,255,255,.25); margin-top: 12px; }
.dropzone-filename { font-weight: 600; font-size: .95rem; margin: 0 0 4px; }
.dropzone-filesize { font-size: .8rem; color: rgba(255,255,255,.4); margin: 0 0 10px; }

.import-info-banner {
  display: flex; align-items: center; gap: 10px;
  padding: 10px 14px; border-radius: 8px;
  background: rgba(99,102,241,.08); border: 1px solid rgba(99,102,241,.15);
  margin-bottom: 8px;
}
.import-info-banner p { margin: 0; font-size: .85rem; }

/* Mapping */
.import-options-row {
  display: flex; gap: 20px; margin-bottom: 16px;
}
.import-toggle {
  display: flex; align-items: center; gap: 8px; font-size: .82rem;
  color: rgba(255,255,255,.6); cursor: pointer;
}
.import-toggle input { accent-color: var(--accent, #6366f1); }

.import-preview-wrap { margin-bottom: 20px; }
.import-preview-title { font-size: .82rem; color: rgba(255,255,255,.5); margin: 0 0 8px; }
.import-preview-table-wrap {
  overflow-x: auto; border-radius: 8px;
  border: 1px solid rgba(255,255,255,.06);
}
.import-preview-table { width: 100%; border-collapse: collapse; font-size: .78rem; }
.import-preview-table th {
  background: rgba(255,255,255,.04); padding: 8px 12px; text-align: left;
  font-weight: 600; white-space: nowrap; color: rgba(255,255,255,.7);
  border-bottom: 1px solid rgba(255,255,255,.06);
}
.import-preview-table td {
  padding: 6px 12px; white-space: nowrap; color: rgba(255,255,255,.5);
  border-bottom: 1px solid rgba(255,255,255,.03); max-width: 200px; overflow: hidden; text-overflow: ellipsis;
}

.import-mapping { margin-bottom: 16px; }
.import-mapping-title { font-size: .85rem; font-weight: 600; margin: 0 0 10px; color: rgba(255,255,255,.7); }
.import-mapping-row {
  display: flex; align-items: center; gap: 10px;
  padding: 6px 0; border-bottom: 1px solid rgba(255,255,255,.03);
}
.mapping-source {
  flex: 1; font-size: .82rem; font-weight: 500;
  padding: 6px 10px; border-radius: 6px;
  background: rgba(255,255,255,.04); color: rgba(255,255,255,.7);
}
.mapping-arrow { color: rgba(255,255,255,.2); flex-shrink: 0; }
.mapping-target { flex: 1.3; }

.import-field { margin-bottom: 14px; }
.import-field label { display: block; font-size: .8rem; color: rgba(255,255,255,.5); margin-bottom: 6px; }
.import-input, .import-select {
  width: 100%; padding: 8px 12px; border-radius: 8px; font-size: .85rem;
  background: rgba(255,255,255,.05); border: 1px solid rgba(255,255,255,.1);
  color: inherit; outline: none; transition: border-color .2s;
}
.import-input:focus, .import-select:focus { border-color: var(--accent, #6366f1); }
.import-select option { background: #1e1e2d; }

/* Warning */
.import-warning {
  display: flex; align-items: center; gap: 8px;
  padding: 10px 14px; border-radius: 8px; margin: 12px 0;
  background: rgba(245,158,11,.08); border: 1px solid rgba(245,158,11,.2);
  color: #f59e0b; font-size: .82rem;
}

/* Result */
.import-result-hero { text-align: center; padding: 20px 0 16px; }
.result-check { font-size: 3rem; margin-bottom: 8px; }
.import-result-hero h3 { margin: 0; font-size: 1.2rem; }

.import-result-stats {
  display: grid; grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));
  gap: 10px; margin: 16px 0;
}
.stat-item {
  padding: 12px; border-radius: 10px; text-align: center;
  background: rgba(255,255,255,.04); border: 1px solid rgba(255,255,255,.06);
}
.stat-item.stat--success { border-color: rgba(34,197,94,.2); background: rgba(34,197,94,.06); }
.stat-item.stat--warn { border-color: rgba(245,158,11,.2); background: rgba(245,158,11,.06); }
.stat-label { display: block; font-size: .72rem; color: rgba(255,255,255,.4); margin-bottom: 4px; }
.stat-value { display: block; font-size: 1.3rem; font-weight: 700; }

.import-distribution { margin: 16px 0; }
.dist-title { font-size: .82rem; font-weight: 600; color: rgba(255,255,255,.6); margin: 0 0 8px; }
.dist-row { display: flex; align-items: center; gap: 10px; margin-bottom: 6px; }
.dist-status { font-size: .78rem; width: 80px; color: rgba(255,255,255,.6); }
.dist-bar-wrap { flex: 1; height: 8px; border-radius: 4px; background: rgba(255,255,255,.05); overflow: hidden; }
.dist-bar { height: 100%; border-radius: 4px; background: var(--accent, #6366f1); transition: width .5s ease; }
.dist-count { font-size: .78rem; width: 28px; text-align: right; color: rgba(255,255,255,.5); }

/* Actions */
.import-actions {
  display: flex; justify-content: space-between; align-items: center;
  padding-top: 16px; border-top: 1px solid rgba(255,255,255,.06);
  margin-top: 12px;
}

/* Button overrides */
.btn { display: inline-flex; align-items: center; gap: 6px; padding: 8px 18px; border-radius: 8px; font-size: .85rem; font-weight: 500; border: none; cursor: pointer; transition: all .2s; }
.btn--primary { background: var(--accent, #6366f1); color: #fff; }
.btn--primary:hover { filter: brightness(1.15); }
.btn--primary:disabled { opacity: .5; cursor: not-allowed; }
.btn--ghost { background: transparent; color: rgba(255,255,255,.6); border: 1px solid rgba(255,255,255,.1); }
.btn--ghost:hover { background: rgba(255,255,255,.05); }
.btn--sm { padding: 6px 14px; font-size: .8rem; }
.btn--danger { color: #ef4444; border-color: rgba(239,68,68,.2); }
.btn--danger:hover { background: rgba(239,68,68,.08); }

@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
</style>
