<script setup lang="ts">
import { ref, computed } from 'vue'
import { X, FileSpreadsheet } from 'lucide-vue-next'
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

const stepLabels = ['Tải file', 'Ghép cột', 'Xác nhận', 'Kết quả']

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
            <FileSpreadsheet :size="22" />
            <h2>Nhập CSV / Excel</h2>
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

        <!-- Step 2: Mapping -->
        <ImportMappingStep
          v-if="step === 2 && parseResult"
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
          @update:first-row-is-header="firstRowIsHeader = $event"
          @update:skip-duplicates="skipDuplicates = $event"
          @update:selected-sheet="selectedSheet = $event"
          @update:default-assignee-id="defaultAssigneeId = $event"
          @update:assign-to-me-if-empty="assignToMeIfEmpty = $event"
          @update:default-priority="defaultPriority = $event"
          @update:enable-ai-categorization="enableAiCategorization = $event"
          @back="step = 1"
          @next="goToConfirm"
        />

        <!-- Step 3: Confirm (NEW — preview BEFORE import) -->
        <ImportConfirmStep
          v-if="step === 3 && parseResult"
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
            <button class="btn btn--ghost btn--danger" @click="undoImport" :disabled="isLoading">
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
  background: rgba(0,0,0,.55); backdrop-filter: blur(6px);
  display: flex; align-items: center; justify-content: center;
  animation: fadeIn .2s ease;
}
.import-modal {
  --accent: #1f80ff;
  width: min(700px, 94vw); max-height: 88vh; overflow-y: auto;
  border-radius: 18px; padding: 0;
  background: var(--glass-bg, rgba(30,30,45,.94));
  border: 1px solid rgba(255,255,255,.08);
  box-shadow: 0 24px 80px rgba(0,0,0,.5), 0 0 0 1px rgba(255,255,255,.04) inset;
}
.import-header {
  display: flex; align-items: center; justify-content: space-between;
  padding: 20px 24px; border-bottom: 1px solid rgba(255,255,255,.06);
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
  height: 3px; background: rgba(255,255,255,.06);
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
  background: rgba(255,255,255,.06); color: rgba(255,255,255,.25);
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
  font-size: .7rem; color: rgba(255,255,255,.25);
  text-align: center; width: 34px;
  transition: color .3s;
}
.stepper-labels span.active { color: rgba(255,255,255,.65); }

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
  background: rgba(99,102,241,.08); border: 1px solid rgba(99,102,241,.15);
  margin-bottom: 8px;
}
:deep(.import-info-banner p) { margin: 0; font-size: .85rem; }

/* ── Mapping (shared) ── */
:deep(.import-options-row) { display: flex; gap: 20px; margin-bottom: 16px; }
:deep(.import-toggle) {
  display: flex; align-items: center; gap: 8px; font-size: .82rem;
  color: rgba(255,255,255,.6); cursor: pointer;
}
:deep(.import-toggle input) { accent-color: var(--accent, #6366f1); }

:deep(.import-preview-wrap) { margin-bottom: 20px; }
:deep(.import-preview-title) { font-size: .82rem; color: rgba(255,255,255,.5); margin: 0 0 8px; }
:deep(.import-preview-table-wrap) {
  overflow-x: auto; border-radius: 8px;
  border: 1px solid rgba(255,255,255,.06);
}
:deep(.import-preview-table) { width: 100%; border-collapse: collapse; font-size: .78rem; }
:deep(.import-preview-table th) {
  background: rgba(255,255,255,.04); padding: 8px 12px; text-align: left;
  font-weight: 600; white-space: nowrap; color: rgba(255,255,255,.7);
  border-bottom: 1px solid rgba(255,255,255,.06);
}
:deep(.import-preview-table td) {
  padding: 6px 12px; white-space: nowrap; color: rgba(255,255,255,.5);
  border-bottom: 1px solid rgba(255,255,255,.03); max-width: 200px;
  overflow: hidden; text-overflow: ellipsis;
}

:deep(.import-mapping) { margin-bottom: 16px; }
:deep(.import-mapping-title) { font-size: .85rem; font-weight: 600; margin: 0 0 10px; color: rgba(255,255,255,.7); }
:deep(.import-mapping-row) {
  display: flex; align-items: center; gap: 10px;
  padding: 6px 0; border-bottom: 1px solid rgba(255,255,255,.03);
}
:deep(.mapping-source) {
  flex: 1; font-size: .82rem; font-weight: 500;
  padding: 6px 10px; border-radius: 6px;
  background: rgba(255,255,255,.04); color: rgba(255,255,255,.7);
}
:deep(.mapping-arrow) { color: rgba(255,255,255,.2); flex-shrink: 0; }
:deep(.mapping-target) { flex: 1.3; }

:deep(.import-field) { margin-bottom: 14px; }
:deep(.import-field label) { display: block; font-size: .8rem; color: rgba(255,255,255,.5); margin-bottom: 6px; }
:deep(.import-input), :deep(.import-select) {
  width: 100%; padding: 8px 12px; border-radius: 8px; font-size: .85rem;
  background: rgba(255,255,255,.05); border: 1px solid rgba(255,255,255,.1);
  color: inherit; outline: none; transition: border-color .2s;
}
:deep(.import-input:focus), :deep(.import-select:focus) { border-color: var(--accent, #6366f1); }
:deep(.import-select option) { background: #1e1e2d; }

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
  padding-top: 16px; border-top: 1px solid rgba(255,255,255,.06);
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
  background: transparent; color: rgba(255,255,255,.6);
  border: 1px solid rgba(255,255,255,.1);
}
:deep(.btn--ghost:hover), .btn--ghost:hover { background: rgba(255,255,255,.05); }
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

@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
</style>
