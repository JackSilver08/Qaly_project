<script setup lang="ts">
import { ref } from 'vue'
import { Archive, CheckCircle2, Download, FileSpreadsheet, FileText, Upload } from 'lucide-vue-next'
import { showError } from '../../composables/use-toast'

const props = defineProps<{
  projectId?: string
  projectName?: string
  file: File | null
  newProjectName: string
  isLoading: boolean
  importSessions?: any[]
  isLoadingSessions?: boolean
}>()

const emit = defineEmits<{
  'update:file': [file: File | null]
  'update:newProjectName': [name: string]
  cancel: []
  next: []
}>()

const isDragging = ref(false)
const activeTab = ref<'discover' | 'completed'>('discover')

const isNewProject = !props.projectId
const acceptedTypes = '.csv,.xlsx,.tsv,.dsv,.txt,.psv,.json,.md,.markdown,.html,.htm'
const tableExtensions = ['csv', 'xlsx', 'tsv', 'dsv', 'psv', 'json']
const phaseOneDocumentExtensions = ['md', 'markdown', 'txt', 'html', 'htm']
const roadmapExtensions = ['pdf', 'docx', 'epub', 'zip']
const supportedExtensions = [...tableExtensions, ...phaseOneDocumentExtensions]

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
  if (roadmapExtensions.includes(ext || '')) {
    showError('Dinh dang nay nam trong lo trinh, nhung hien tai chua co parser nen chua the import.')
    return
  }
  if (!supportedExtensions.includes(ext || '')) {
    showError('Dinh dang nay chua duoc ho tro.')
    return
  }
  if (f.size > 5 * 1024 * 1024) {
    showError('Phase dau dang gioi han 5MB de xu ly an toan.')
    return
  }
  if (!props.projectId && !tableExtensions.includes(ext || '')) {
    showError('Hay vao mot du an cu the de import document thanh Wiki page.')
    return
  }
  emit('update:file', f)
}

function formatFileSize(bytes: number) {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / 1048576).toFixed(1) + ' MB'
}

function extensionOf(name: string) {
  return name.split('.').pop()?.toLowerCase() || ''
}

function fileKindLabel(name: string) {
  const ext = extensionOf(name)
  if (tableExtensions.includes(ext)) return 'Task table import'
  if (phaseOneDocumentExtensions.includes(ext)) return 'Wiki page import'
  if (roadmapExtensions.includes(ext)) return 'Roadmap importer'
  return 'Unsupported file'
}
</script>

<template>
  <div class="import-step import-discover">
    <div class="import-intro">
      <h3>Import</h3>
      <p>Import data from other apps and files into QALY</p>
    </div>

    <div class="import-tabs" role="tablist" aria-label="Import views">
      <button type="button" :class="{ active: activeTab === 'discover' }" @click="activeTab = 'discover'">Discover</button>
      <button type="button" :class="{ active: activeTab === 'completed' }" @click="activeTab = 'completed'">Completed</button>
    </div>

    <template v-if="activeTab === 'discover'">
      <div class="import-section-heading">
        <h4>Import your content</h4>
        <p>Supported now: CSV, Excel, JSON, TXT, Markdown, and HTML. PDF, DOCX, EPUB, and ZIP stay in the roadmap until their parsers are ready.</p>
      </div>

      <div class="template-actions" aria-label="Download import templates">
        <a class="template-link" href="/api/import/templates/tasks.xlsx" download>
          <Download :size="15" />
          Excel template
        </a>
        <a class="template-link" href="/api/import/templates/tasks.csv" download>
          <Download :size="15" />
          CSV template
        </a>
      </div>

      <div
        :class="['import-dropzone notion-dropzone', { dragging: isDragging, 'has-file': !!file }]"
        @dragover="onDragOver"
        @dragleave="onDragLeave"
        @drop="onDrop"
      >
        <template v-if="!file">
          <Upload :size="34" class="dropzone-icon" />
          <p class="dropzone-text">Import your content to QALY</p>
          <p class="dropzone-hint">
            Drag and drop CSV, Excel, JSON, text, markdown, or HTML files, or
            <label class="choose-link">
              choose a file
              <input type="file" :accept="acceptedTypes" hidden @change="onFileInput" />
            </label>
          </p>
          <p class="dropzone-formats">Available now: CSV, Excel, JSON, TXT, Markdown, and HTML. Roadmap: PDF, DOCX, EPUB, and ZIP after we add parsers.</p>
        </template>
        <template v-else>
          <FileSpreadsheet v-if="tableExtensions.includes(extensionOf(file.name))" :size="34" class="dropzone-icon--selected" />
          <FileText v-else :size="34" class="dropzone-icon--selected" />
          <p class="dropzone-filename">{{ file.name }}</p>
          <p class="dropzone-filesize">{{ fileKindLabel(file.name) }} - {{ formatFileSize(file.size) }}</p>
          <button class="btn btn--ghost btn--sm" type="button" @click="emit('update:file', null)">Choose another file</button>
        </template>
      </div>

      <div class="import-type-section">
        <h4>File-based imports</h4>
        <p>Import CSV, Excel, JSON, TXT, Markdown, and HTML now. PDF, DOCX, EPUB, and ZIP stay in the roadmap until their parsers are ready.</p>
        <div class="import-type-grid">
          <article>
            <FileText :size="18" />
            <strong>Documents now</strong>
            <span>Markdown, text, and HTML convert to Wiki pages today.</span>
          </article>
          <article>
            <FileSpreadsheet :size="18" />
            <strong>Tables now</strong>
            <span>CSV, Excel, TSV, PSV, and JSON import to tasks today.</span>
          </article>
          <article>
            <Archive :size="18" />
            <strong>Roadmap</strong>
            <span>PDF, DOCX, EPUB, and ZIP will come later, after parsing support is added.</span>
          </article>
        </div>
      </div>

      <div v-if="isNewProject" class="import-field">
        <label>New project name for table imports</label>
        <input
          :value="newProjectName"
          type="text"
          placeholder="Enter project name..."
          class="import-input"
          @input="emit('update:newProjectName', ($event.target as HTMLInputElement).value)"
        />
      </div>
      <div v-else class="import-info-banner">
        <CheckCircle2 :size="16" />
        <p>Target project: <strong>{{ projectName }}</strong></p>
      </div>
    </template>

    <div v-else-if="isLoadingSessions" class="completed-empty">
      <CheckCircle2 :size="28" />
      <strong>Đang tải lịch sử import...</strong>
      <p>QALY đang kiểm tra các phiên import gần đây của dự án này.</p>
    </div>

    <div v-else-if="projectId && importSessions?.length" class="completed-imports">
      <article v-for="session in importSessions" :key="session.id" class="completed-import">
        <div>
          <strong>{{ session.fileName }}</strong>
          <span>{{ new Date(session.createdAt).toLocaleString() }}</span>
        </div>
        <div class="completed-import__stats">
          <span class="ok">{{ session.importedCount }} nhập</span>
          <span v-if="session.skippedCount > 0" class="warn">{{ session.skippedCount }} không nhập</span>
          <span v-if="session.isUndone" class="muted">Đã hoàn tác</span>
          <span v-else-if="session.canUndo" class="muted">Còn hoàn tác</span>
        </div>
      </article>
    </div>

    <div v-else class="completed-empty">
      <CheckCircle2 :size="28" />
      <strong>No completed imports in this panel yet</strong>
      <p>{{ projectId ? 'Dự án này chưa có phiên import task-table nào.' : 'Lịch sử import chỉ hiển thị khi bạn import trong một dự án cụ thể.' }}</p>
    </div>

    <div class="import-actions">
      <button class="btn btn--ghost" type="button" @click="emit('cancel')">Cancel</button>
      <button class="btn btn--primary" :disabled="!file || isLoading" @click="emit('next')">
        <template v-if="isLoading">Reading...</template>
        <template v-else>Continue</template>
      </button>
    </div>
  </div>
</template>

<style scoped>
.import-discover {
  color: #111827;
}

.import-intro h3 {
  margin: 0 0 10px;
  font-size: 30px;
  line-height: 1.1;
  font-weight: 700;
}

.import-intro p {
  margin: 0;
  color: #374151;
  font-size: 15px;
}

.import-tabs {
  display: flex;
  gap: 8px;
  margin: 28px 0 34px;
}

.import-tabs button {
  border: 0;
  border-radius: 999px;
  padding: 10px 15px;
  background: transparent;
  color: #6b7280;
  font-size: 15px;
  font-weight: 500;
  cursor: pointer;
}

.import-tabs button.active {
  background: #f3f4f6;
  color: #111827;
  font-weight: 700;
}

.import-section-heading h4,
.import-type-section h4 {
  margin: 0 0 8px;
  font-size: 16px;
  font-weight: 700;
}

.import-section-heading p,
.import-type-section p {
  margin: 0;
  color: #6b7280;
  font-size: 14px;
}

.notion-dropzone {
  min-height: 238px;
  margin: 24px 0 34px;
  border: 1.5px dashed #2f80ed;
  border-radius: 10px;
  background: #f4faff;
  display: flex;
  align-items: center;
  justify-content: center;
  text-align: center;
}

.notion-dropzone.dragging {
  background: #eaf4ff;
  border-color: #0f62fe;
}

.template-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 14px;
}

.template-link {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 34px;
  padding: 7px 11px;
  border: 1px solid #dbeafe;
  border-radius: 8px;
  background: #eff6ff;
  color: #1d4ed8;
  font-size: 13px;
  font-weight: 700;
  text-decoration: none;
}

.template-link:hover {
  background: #dbeafe;
}

.dropzone-icon {
  color: #93c5fd;
  margin-bottom: 24px;
}

.dropzone-icon--selected {
  color: #2563eb;
  margin-bottom: 12px;
}

.dropzone-text {
  margin: 0 0 18px;
  font-size: 16px;
  font-weight: 700;
  color: #111827;
}

.dropzone-hint {
  margin: 0 0 20px;
  color: #6b7280;
  font-size: 14px;
}

.choose-link {
  color: #2563eb;
  cursor: pointer;
}

.dropzone-formats,
.dropzone-filesize {
  margin: 0;
  color: #8b949e;
  font-size: 13px;
}

.dropzone-filename {
  margin: 0 0 6px;
  color: #111827;
  font-weight: 700;
}

.import-type-section {
  margin-bottom: 22px;
}

.import-type-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 10px;
  margin-top: 14px;
}

.import-type-grid article {
  min-height: 96px;
  padding: 14px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: #fff;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.import-type-grid svg {
  color: #2563eb;
}

.import-type-grid strong {
  color: #111827;
  font-size: 14px;
}

.import-type-grid span {
  color: #6b7280;
  font-size: 12px;
  line-height: 1.35;
}

.completed-empty {
  min-height: 300px;
  display: grid;
  place-items: center;
  align-content: center;
  gap: 10px;
  text-align: center;
  color: #6b7280;
}

.completed-empty strong {
  color: #111827;
}

.completed-empty p {
  max-width: 480px;
  margin: 0;
  font-size: 14px;
}

.completed-imports {
  display: grid;
  gap: 10px;
  margin: 8px 0 22px;
}

.completed-import {
  display: flex;
  justify-content: space-between;
  gap: 16px;
  padding: 13px 14px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: #fff;
}

.completed-import strong {
  display: block;
  color: #111827;
  font-size: 14px;
}

.completed-import span {
  color: #6b7280;
  font-size: 12px;
}

.completed-import__stats {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  align-content: center;
  gap: 6px;
}

.completed-import__stats span {
  padding: 3px 8px;
  border-radius: 999px;
  background: #f3f4f6;
  font-weight: 700;
}

.completed-import__stats .ok {
  color: #047857;
  background: #ecfdf5;
}

.completed-import__stats .warn {
  color: #b45309;
  background: #fffbeb;
}

.completed-import__stats .muted {
  color: #6b7280;
}

@media (max-width: 720px) {
  .import-type-grid {
    grid-template-columns: 1fr;
  }
}
</style>
