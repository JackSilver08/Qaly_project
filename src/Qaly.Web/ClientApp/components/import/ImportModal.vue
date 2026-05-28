<script setup lang="ts">
import { ref, computed } from 'vue'
import { ArrowLeft, Check, FileText, FileUp, X } from 'lucide-vue-next'
import ImportUploadStep from './ImportUploadStep.vue'
import ImportMappingStep from './ImportMappingStep.vue'
import ImportConfirmStep from './ImportConfirmStep.vue'
import { showSuccess, showError } from '../../composables/use-toast'

const props = defineProps<{
  projectId?: string
  projectName?: string
  projectMembers?: { userId: string; fullName: string; email?: string }[]
}>()

const emit = defineEmits<{
  close: []
  imported: [result: any]
}>()

// ─── State ───────────────────────────────────────────
const step = ref(1)
const file = ref<File | null>(null)
const isLoading = ref(false)

// Parse result
const parseResult = ref<any>(null)
const importMode = ref<'table' | 'document'>('table')
const documentTitle = ref('')

// Mapping state
const firstRowIsHeader = ref(true)
const skipDuplicates = ref(false)
const selectedSheet = ref<string | null>(null)
const defaultAssigneeId = ref<string | null>(null)
const assignToMeIfEmpty = ref(true)
const defaultPriority = ref<string | null>(null)
const enableAiCategorization = ref(false)
const mappings = ref<{ columnIndex: number; targetField: string }[]>([])
const newProjectName = ref('')

// Import result
const importResult = ref<any>(null)

const isNewProject = computed(() => !props.projectId)

const stepLabels = computed(() => importMode.value === 'document'
  ? ['Discover', 'Preview', 'Confirm', 'Done']
  : ['Discover', 'Map columns', 'Confirm', 'Done'])

const tableExtensions = ['csv', 'xlsx', 'tsv', 'dsv', 'psv', 'json']
const documentExtensions = ['md', 'markdown', 'txt', 'html', 'htm']

function extensionOf(name: string) {
  return name.split('.').pop()?.toLowerCase() || ''
}

function detectImportMode(selectedFile: File) {
  const ext = extensionOf(selectedFile.name)
  if (tableExtensions.includes(ext)) return 'table'
  if (documentExtensions.includes(ext)) return 'document'
  return 'table'
}

// ─── Step 1 → Step 2: Parse file ─────────────────────

async function parseFile(sheetName?: string | null) {
  if (!file.value) return
  isLoading.value = true
  importMode.value = detectImportMode(file.value)

  try {
    const formData = new FormData()
    formData.append('file', file.value)

    if (importMode.value === 'document') {
      const res = await fetch('/api/import/documents/preview', {
        method: 'POST',
        body: formData,
      })
      const data = await res.json()

      if (!data.isSuccess) {
        showError(data.error || 'Khong the doc file')
        return
      }

      parseResult.value = data.data
      documentTitle.value = data.data.title
      step.value = 2
      return
    }

    if (sheetName) formData.append('sheetName', sheetName)
    formData.append('firstRowIsHeader', String(firstRowIsHeader.value))

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
      selectedSheet.value = sheetName || data.data.sheetNames[0]
    }
    if (isNewProject.value) {
      newProjectName.value = file.value!.name.replace(/\.(csv|xlsx|tsv|dsv|psv|json)$/i, '')
    }
    step.value = 2
  } catch (e: any) {
    showError('Lỗi kết nối server')
  } finally {
    isLoading.value = false
  }
}

async function onSheetSelected(sheetName: string | null) {
  selectedSheet.value = sheetName
  if (file.value?.name.toLowerCase().endsWith('.xlsx')) {
    await parseFile(sheetName)
  }
}

async function onFirstRowIsHeaderChanged(value: boolean) {
  firstRowIsHeader.value = value
  if (file.value && parseResult.value) {
    await parseFile(selectedSheet.value)
  }
}

// ─── Step 2 → Step 3: Go to confirm ──────────────────

function goToConfirm() {
  step.value = 3
}

// ─── Step 3 → Step 4: Execute import ─────────────────

async function executeImport() {
  if (!file.value) return
  isLoading.value = true

  try {
    const formData = new FormData()
    formData.append('file', file.value)

    if (importMode.value === 'document') {
      if (!props.projectId) {
        showError('Hay vao mot du an cu the de import document thanh Wiki page.')
        return
      }

      formData.append('projectId', props.projectId)
      formData.append('title', documentTitle.value)

      const res = await fetch('/api/import/documents/execute', {
        method: 'POST',
        body: formData,
      })
      const data = await res.json()

      if (!data.isSuccess) {
        showError(data.error || 'Import document that bai')
        return
      }

      importResult.value = { ...data.data, kind: 'document' }
      step.value = 4
      showSuccess(`Da tao Wiki page "${data.data.title}"`)
      return
    }

    formData.append('request', JSON.stringify({
      projectId: props.projectId || null,
      newProjectName: isNewProject.value ? newProjectName.value : null,
      mappings: mappings.value,
      firstRowIsHeader: firstRowIsHeader.value,
      skipDuplicates: skipDuplicates.value,
      sheetName: selectedSheet.value,
      defaultAssigneeId: defaultAssigneeId.value,
      assignToMeIfEmpty: assignToMeIfEmpty.value,
      defaultPriority: defaultPriority.value,
      enableAiCategorization: enableAiCategorization.value,
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
    step.value = 4
    showSuccess(`Đã import thành công ${data.data.importedCount} task!`)
  } catch (e: any) {
    showError('Lỗi kết nối server')
  } finally {
    isLoading.value = false
  }
}

// ─── Undo ────────────────────────────────────────────

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
</script>

<template>
  <Teleport to="body">
    <div class="import-backdrop" @click.self="$emit('close')">
      <div class="import-modal glass-card">
        <!-- Header -->
        <div class="import-header">
          <div class="import-header__left">
            <FileUp :size="22" />
            <h2>Import</h2>
          </div>
          <button class="icon-button" @click="$emit('close')"><X :size="18" /></button>
        </div>

        <!-- Stepper -->
        <div class="import-stepper">
          <div class="stepper-track">
            <div
              v-for="s in 4" :key="s"
              :class="['stepper-dot', { active: step >= s, current: step === s }]"
            >
              <span class="stepper-num">{{ s }}</span>
            </div>
            <div class="stepper-line">
              <div class="stepper-line__fill" :style="{ width: ((step - 1) / 3 * 100) + '%' }"></div>
            </div>
          </div>
          <div class="stepper-labels">
            <span v-for="(label, i) in stepLabels" :key="i" :class="{ active: step >= i + 1 }">{{ label }}</span>
          </div>
        </div>

        <!-- Step 1: Upload -->
        <ImportUploadStep
          v-if="step === 1"
          :project-id="projectId"
          :project-name="projectName"
          :file="file"
          :new-project-name="newProjectName"
          :is-loading="isLoading"
          @update:file="file = $event"
          @update:new-project-name="newProjectName = $event"
          @cancel="$emit('close')"
          @next="parseFile"
        />

        <div v-if="step === 2 && parseResult && importMode === 'document'" class="import-step document-preview">
          <div class="document-preview__hero">
            <FileText :size="26" />
            <div>
              <span>{{ parseResult.fileType }}</span>
              <h3>Preview Wiki page import</h3>
            </div>
          </div>

          <div class="import-field">
            <label>Page title</label>
            <input v-model="documentTitle" class="import-input" type="text" />
          </div>

          <div class="document-preview__stats">
            <div>
              <span>Blocks detected</span>
              <strong>{{ parseResult.blockCount }}</strong>
            </div>
            <div>
              <span>Target</span>
              <strong>{{ projectName || 'Current project' }}</strong>
            </div>
          </div>

          <div v-if="parseResult.description" class="document-description">
            {{ parseResult.description }}
          </div>

          <div v-if="parseResult.warnings?.length" class="import-warning">
            <span>{{ parseResult.warnings.join(' ') }}</span>
          </div>

          <div class="document-preview__blocks">
            <p>First blocks</p>
            <ul>
              <li v-for="(block, index) in parseResult.previewBlocks" :key="index">{{ block }}</li>
            </ul>
          </div>

          <div class="import-actions">
            <button class="btn btn--ghost" type="button" @click="step = 1"><ArrowLeft :size="16" /> Back</button>
            <button class="btn btn--primary" :disabled="!documentTitle.trim()" type="button" @click="step = 3">
              Continue
            </button>
          </div>
        </div>

        <!-- Step 2: Mapping -->
        <ImportMappingStep
          v-if="step === 2 && parseResult && importMode === 'table'"
          :parse-result="parseResult"
          :mappings="mappings"
          :first-row-is-header="firstRowIsHeader"
          :skip-duplicates="skipDuplicates"
          :selected-sheet="selectedSheet"
          :project-members="projectMembers"
          :default-assignee-id="defaultAssigneeId"
          :assign-to-me-if-empty="assignToMeIfEmpty"
          :default-priority="defaultPriority"
          :enable-ai-categorization="enableAiCategorization"
          @update:mappings="mappings = $event"
          @update:first-row-is-header="onFirstRowIsHeaderChanged"
          @update:skip-duplicates="skipDuplicates = $event"
          @update:selected-sheet="onSheetSelected"
          @update:default-assignee-id="defaultAssigneeId = $event"
          @update:assign-to-me-if-empty="assignToMeIfEmpty = $event"
          @update:default-priority="defaultPriority = $event"
          @update:enable-ai-categorization="enableAiCategorization = $event"
          @back="step = 1"
          @next="goToConfirm"
        />

        <div v-if="step === 3 && parseResult && importMode === 'document'" class="import-step document-confirm">
          <div class="confirm-hero">
            <div class="confirm-icon"><FileText :size="34" /></div>
            <h3>Confirm document import</h3>
            <p class="confirm-subtitle">
              QALY will create a Wiki page in <strong>{{ projectName }}</strong>.
            </p>
          </div>

          <div class="confirm-stats">
            <div class="confirm-stat">
              <span class="confirm-stat__label">File type</span>
              <span class="confirm-stat__value confirm-stat__value--text">{{ parseResult.fileType }}</span>
            </div>
            <div class="confirm-stat confirm-stat--success">
              <span class="confirm-stat__label">Blocks</span>
              <span class="confirm-stat__value">{{ parseResult.blockCount }}</span>
            </div>
          </div>

          <div class="confirm-notice">
            <span>Preview</span>
            <p>{{ documentTitle }}</p>
          </div>

          <div class="import-actions">
            <button class="btn btn--ghost" type="button" @click="step = 2"><ArrowLeft :size="16" /> Back</button>
            <button class="btn btn--primary btn--import-confirm" :disabled="isLoading" @click="executeImport">
              <template v-if="isLoading">Importing...</template>
              <template v-else>Create Wiki page <Check :size="16" /></template>
            </button>
          </div>
        </div>

        <!-- Step 3: Confirm (NEW — preview BEFORE import) -->
        <ImportConfirmStep
          v-if="step === 3 && parseResult && importMode === 'table'"
          :parse-result="parseResult"
          :mappings="mappings"
          :skip-duplicates="skipDuplicates"
          :is-new-project="isNewProject"
          :new-project-name="newProjectName"
          :project-name="projectName"
          :is-loading="isLoading"
          :default-assignee-id="defaultAssigneeId"
          :assign-to-me-if-empty="assignToMeIfEmpty"
          :default-priority="defaultPriority"
          :enable-ai-categorization="enableAiCategorization"
          @back="step = 2"
          @confirm="executeImport"
        />

        <!-- Step 4: Result -->
        <div v-if="step === 4 && importResult" class="import-step">
          <div class="import-result-hero">
            <div class="result-check-anim">
              <svg viewBox="0 0 52 52" class="checkmark-svg">
                <circle class="checkmark-circle" cx="26" cy="26" r="25" fill="none"/>
                <path class="checkmark-check" fill="none" d="M14.1 27.2l7.1 7.2 16.7-16.8"/>
              </svg>
            </div>
            <h3>Import hoàn tất!</h3>
          </div>

          <div v-if="importMode === 'document'" class="document-success">
            <FileText :size="24" />
            <div>
              <strong>{{ importResult.title }}</strong>
              <p>Created a Wiki page with {{ importResult.blockCount }} blocks.</p>
            </div>
          </div>

          <div v-if="importMode === 'table'" class="import-result-stats">
            <div class="stat-item"><span class="stat-label">Tổng dòng</span><span class="stat-value">{{ importResult.totalRows }}</span></div>
            <div class="stat-item stat--success"><span class="stat-label">Đã import</span><span class="stat-value">{{ importResult.importedCount }}</span></div>
            <div v-if="importResult.skippedCount > 0" class="stat-item stat--warn"><span class="stat-label">Bỏ qua</span><span class="stat-value">{{ importResult.skippedCount }}</span></div>
            <div v-if="importResult.newLabelsCreated > 0" class="stat-item"><span class="stat-label">Label mới</span><span class="stat-value">{{ importResult.newLabelsCreated }}</span></div>
          </div>

          <!-- Status distribution -->
          <div v-if="importMode === 'table' && Object.keys(importResult.statusDistribution).length" class="import-distribution">
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
            <span>⚠️</span>
            <span>Các giá trị Status không nhận diện (đã đặt về Todo): {{ importResult.unmappedStatuses.join(', ') }}</span>
          </div>

          <!-- Skipped Rows Detail -->
          <div v-if="importResult.skippedRows?.length" class="import-skipped-rows">
            <details>
              <summary>Hiển thị chi tiết {{ importResult.skippedRows.length }} dòng bị lỗi/bỏ qua</summary>
              <ul class="skipped-list">
                <li v-for="(row, idx) in importResult.skippedRows" :key="idx">
                  <strong>Dòng {{ row.rowIndex }}:</strong> {{ row.reason }}
                </li>
              </ul>
            </details>
          </div>

          <div class="import-actions">
            <button v-if="importMode === 'table'" class="btn btn--ghost btn--danger" @click="undoImport" :disabled="isLoading">
              Hoàn tác import
            </button>
            <button class="btn btn--primary" @click="finish">
              Xong ✓
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
  background: rgba(15, 23, 42, 0.38); backdrop-filter: blur(6px);
  display: flex; align-items: center; justify-content: center;
  animation: fadeIn .2s ease;
}
.import-modal {
  --accent: #1f80ff;
  width: min(1000px, 94vw); max-height: 90vh; overflow-y: auto;
  border-radius: 14px; padding: 0;
  background: #ffffff;
  color: #111827;
  border: 1px solid rgba(15, 23, 42, 0.08);
  box-shadow: 0 24px 80px rgba(15, 23, 42, 0.22);
}
.import-header {
  display: flex; align-items: center; justify-content: space-between;
  padding: 18px 24px; border-bottom: 1px solid #eef2f7;
}
.import-header__left { display: flex; align-items: center; gap: 10px; }
.import-header__left h2 { font-size: 1.1rem; font-weight: 600; margin: 0; }

/* ── Enhanced Stepper ── */
.import-stepper {
  padding: 20px 32px 8px;
}
.stepper-track {
  display: flex; align-items: center; justify-content: space-between;
  position: relative; margin-bottom: 8px;
}
.stepper-line {
  position: absolute; top: 50%; left: 16px; right: 16px;
  height: 3px; background: #eef2f7;
  border-radius: 2px; transform: translateY(-50%);
  z-index: 0;
}
.stepper-line__fill {
  height: 100%; border-radius: 2px;
  background: linear-gradient(90deg, #6366f1, #818cf8);
  transition: width .4s cubic-bezier(.22,1,.36,1);
}
.stepper-dot {
  width: 34px; height: 34px; border-radius: 50%;
  display: flex; align-items: center; justify-content: center;
  font-size: .8rem; font-weight: 700;
  background: #f3f4f6; color: #9ca3af;
  transition: all .35s cubic-bezier(.22,1,.36,1);
  position: relative; z-index: 1;
}
.stepper-dot.active {
  background: var(--accent, #6366f1); color: #fff;
  box-shadow: 0 4px 16px rgba(99,102,241,.3);
}
.stepper-dot.current {
  box-shadow: 0 0 0 4px rgba(99,102,241,.25), 0 4px 16px rgba(99,102,241,.3);
  transform: scale(1.08);
}
.stepper-labels {
  display: flex; justify-content: space-between;
  padding: 0 4px;
}
.stepper-labels span {
  font-size: .7rem; color: #9ca3af;
  text-align: center; width: 34px;
  transition: color .3s;
}
.stepper-labels span.active { color: #374151; }

/* ── Steps ── */
.import-step { padding: 20px 24px; }

/* ── Upload (shared styles for sub-components) ── */
:deep(.import-dropzone) {
  border: 2px dashed rgba(255,255,255,.1); border-radius: 14px;
  padding: 40px 24px; text-align: center;
  transition: all .25s ease; cursor: pointer;
  margin: 12px 0;
}
:deep(.import-dropzone.dragging) { border-color: var(--accent, #6366f1); background: rgba(99,102,241,.08); }
:deep(.import-dropzone.has-file) { border-color: rgba(34,197,94,.3); background: rgba(34,197,94,.04); }
:deep(.dropzone-icon) { color: rgba(255,255,255,.2); margin-bottom: 12px; }
:deep(.dropzone-icon--selected) { color: rgba(34,197,94,.7); margin-bottom: 8px; }
:deep(.dropzone-text) { font-size: 1rem; margin: 0 0 4px; color: rgba(255,255,255,.6); }
:deep(.dropzone-hint) { font-size: .8rem; color: rgba(255,255,255,.3); margin: 0 0 10px; }
:deep(.dropzone-formats) { font-size: .72rem; color: rgba(255,255,255,.2); margin-top: 12px; }
:deep(.dropzone-filename) { font-weight: 600; font-size: .95rem; margin: 0 0 4px; }
:deep(.dropzone-filesize) { font-size: .8rem; color: rgba(255,255,255,.4); margin: 0 0 10px; }

:deep(.import-info-banner) {
  display: flex; align-items: center; gap: 10px;
  padding: 10px 14px; border-radius: 8px;
  background: #eff6ff; border: 1px solid #bfdbfe;
  margin-bottom: 8px;
}
:deep(.import-info-banner p) { margin: 0; font-size: .85rem; }

/* ── Mapping (shared) ── */
:deep(.import-options-row) { display: flex; gap: 20px; margin-bottom: 16px; }
:deep(.import-toggle) {
  display: flex; align-items: center; gap: 8px; font-size: .82rem;
  color: #4b5563; cursor: pointer;
}
:deep(.import-toggle input) { accent-color: var(--accent, #6366f1); }

:deep(.import-preview-wrap) { margin-bottom: 20px; }
:deep(.import-preview-title) { font-size: .82rem; color: #6b7280; margin: 0 0 8px; }
:deep(.import-preview-table-wrap) {
  overflow-x: auto; border-radius: 8px;
  border: 1px solid #e5e7eb;
}
:deep(.import-preview-table) { width: 100%; border-collapse: collapse; font-size: .78rem; }
:deep(.import-preview-table th) {
  background: #f9fafb; padding: 8px 12px; text-align: left;
  font-weight: 600; white-space: nowrap; color: #374151;
  border-bottom: 1px solid #e5e7eb;
}
:deep(.import-preview-table td) {
  padding: 6px 12px; white-space: nowrap; color: #6b7280;
  border-bottom: 1px solid #f3f4f6; max-width: 200px;
  overflow: hidden; text-overflow: ellipsis;
}

:deep(.import-mapping) { margin-bottom: 16px; }
:deep(.import-mapping-title) { font-size: .85rem; font-weight: 600; margin: 0 0 10px; color: #374151; }
:deep(.import-mapping-row) {
  display: flex; align-items: center; gap: 10px;
  padding: 6px 0; border-bottom: 1px solid #f3f4f6;
}
:deep(.mapping-source) {
  flex: 1; font-size: .82rem; font-weight: 500;
  padding: 6px 10px; border-radius: 6px;
  background: #f9fafb; color: #374151;
}
:deep(.mapping-arrow) { color: #9ca3af; flex-shrink: 0; }
:deep(.mapping-target) { flex: 1.3; }

:deep(.import-field) { margin-bottom: 14px; }
:deep(.import-field label) { display: block; font-size: .8rem; color: #4b5563; margin-bottom: 6px; }
:deep(.import-input), :deep(.import-select) {
  width: 100%; padding: 8px 12px; border-radius: 8px; font-size: .85rem;
  background: #ffffff; border: 1px solid #d1d5db;
  color: inherit; outline: none; transition: border-color .2s;
}
:deep(.import-input:focus), :deep(.import-select:focus) { border-color: var(--accent, #6366f1); }
:deep(.import-select option) { background: #ffffff; }

/* ── Warning ── */
:deep(.import-warning), .import-warning {
  display: flex; align-items: center; gap: 8px;
  padding: 10px 14px; border-radius: 8px; margin: 12px 0;
  background: rgba(245,158,11,.08); border: 1px solid rgba(245,158,11,.2);
  color: #f59e0b; font-size: .82rem;
}

/* ── Result ── */
.import-result-hero { text-align: center; padding: 20px 0 16px; }
.import-result-hero h3 { margin: 0; font-size: 1.2rem; }

/* Animated SVG checkmark */
.result-check-anim { width: 64px; height: 64px; margin: 0 auto 12px; }
.checkmark-svg { width: 64px; height: 64px; border-radius: 50%; display: block; }
.checkmark-circle {
  stroke-dasharray: 166; stroke-dashoffset: 166;
  stroke-width: 2; stroke-miterlimit: 10;
  stroke: #22c55e; fill: none;
  animation: stroke .6s cubic-bezier(.65,0,.45,1) forwards;
}
.checkmark-check {
  stroke: #22c55e; stroke-dasharray: 48; stroke-dashoffset: 48;
  stroke-width: 3; stroke-linecap: round; stroke-linejoin: round;
  animation: stroke .4s cubic-bezier(.65,0,.45,1) .4s forwards;
}
@keyframes stroke {
  100% { stroke-dashoffset: 0; }
}

.import-result-stats {
  display: grid; grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));
  gap: 10px; margin: 16px 0;
}
.stat-item {
  padding: 14px; border-radius: 12px; text-align: center;
  background: rgba(255,255,255,.04); border: 1px solid rgba(255,255,255,.06);
  transition: transform .2s;
}
.stat-item:hover { transform: translateY(-2px); }
.stat-item.stat--success { border-color: rgba(34,197,94,.2); background: rgba(34,197,94,.06); }
.stat-item.stat--warn { border-color: rgba(245,158,11,.2); background: rgba(245,158,11,.06); }
.stat-label { display: block; font-size: .72rem; color: rgba(255,255,255,.4); margin-bottom: 4px; }
.stat-value { display: block; font-size: 1.4rem; font-weight: 700; }

.import-distribution { margin: 16px 0; }
.dist-title { font-size: .82rem; font-weight: 600; color: rgba(255,255,255,.6); margin: 0 0 8px; }
.dist-row { display: flex; align-items: center; gap: 10px; margin-bottom: 6px; }
.dist-status { font-size: .78rem; width: 80px; color: rgba(255,255,255,.6); }
.dist-bar-wrap { flex: 1; height: 8px; border-radius: 4px; background: rgba(255,255,255,.05); overflow: hidden; }
.dist-bar {
  height: 100%; border-radius: 4px;
  background: linear-gradient(90deg, #6366f1, #818cf8);
  transition: width .6s cubic-bezier(.22,1,.36,1);
}
.dist-count { font-size: .78rem; width: 28px; text-align: right; color: rgba(255,255,255,.5); }

/* ── Actions ── */
:deep(.import-actions), .import-actions {
  display: flex; justify-content: space-between; align-items: center;
  padding-top: 16px; border-top: 1px solid #eef2f7;
  margin-top: 12px;
}

/* ── Shared buttons ── */
:deep(.btn), .btn {
  display: inline-flex; align-items: center; gap: 6px;
  padding: 8px 18px; border-radius: 8px; font-size: .85rem;
  font-weight: 500; border: none; cursor: pointer; transition: all .2s;
}
:deep(.btn--primary), .btn--primary { background: var(--accent, #6366f1); color: #fff; }
:deep(.btn--primary:hover), .btn--primary:hover { filter: brightness(1.15); }
:deep(.btn--primary:disabled), .btn--primary:disabled { opacity: .5; cursor: not-allowed; }
:deep(.btn--ghost), .btn--ghost {
  background: transparent; color: #4b5563;
  border: 1px solid #d1d5db;
}
:deep(.btn--ghost:hover), .btn--ghost:hover { background: #f9fafb; }
:deep(.btn--sm), .btn--sm { padding: 6px 14px; font-size: .8rem; }
.btn--danger { color: #ef4444; border-color: rgba(239,68,68,.2); }
.btn--danger:hover { background: rgba(239,68,68,.08); }

.import-skipped-rows {
  margin: 16px 0;
  background: rgba(239, 68, 68, 0.08);
  border: 1px solid rgba(239, 68, 68, 0.2);
  border-radius: 8px;
  padding: 12px;
}
.import-skipped-rows details summary {
  cursor: pointer;
  font-size: 0.85rem;
  font-weight: 600;
  color: #ef4444;
  outline: none;
}
.skipped-list {
  margin: 10px 0 0 0;
  padding-left: 20px;
  font-size: 0.8rem;
  color: rgba(255, 255, 255, 0.7);
}
.skipped-list li {
  margin-bottom: 4px;
}

.document-preview__hero,
.document-success {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 14px;
  border: 1px solid #dbeafe;
  border-radius: 10px;
  background: #eff6ff;
  color: #1f2937;
  margin-bottom: 16px;
}

.document-preview__hero svg,
.document-success svg {
  color: #2563eb;
  flex-shrink: 0;
}

.document-preview__hero span {
  display: block;
  color: #2563eb;
  font-size: 12px;
  font-weight: 700;
  margin-bottom: 2px;
}

.document-preview__hero h3 {
  margin: 0;
  font-size: 18px;
}

.document-preview__stats {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
  margin: 14px 0;
}

.document-preview__stats div {
  padding: 14px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: #fff;
}

.document-preview__stats span {
  display: block;
  color: #6b7280;
  font-size: 12px;
  margin-bottom: 4px;
}

.document-preview__stats strong {
  color: #111827;
}

.document-description {
  padding: 12px 14px;
  border-left: 3px solid #93c5fd;
  background: #f8fafc;
  color: #4b5563;
  font-size: 14px;
  margin: 12px 0;
}

.document-preview__blocks {
  margin: 16px 0;
}

.document-preview__blocks p {
  margin: 0 0 8px;
  color: #374151;
  font-weight: 700;
  font-size: 13px;
}

.document-preview__blocks ul {
  margin: 0;
  padding: 0;
  list-style: none;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  overflow: hidden;
}

.document-preview__blocks li {
  padding: 9px 12px;
  border-bottom: 1px solid #f3f4f6;
  color: #4b5563;
  font-size: 13px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.document-preview__blocks li:last-child {
  border-bottom: 0;
}

.document-success p {
  margin: 4px 0 0;
  color: #6b7280;
}

.confirm-stat__value--text {
  font-size: 1rem;
}

@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
</style>
