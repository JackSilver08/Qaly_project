<script setup lang="ts">
import { computed, ref } from 'vue'
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
const hasCompletedImports = computed(() => !!props.projectId && (props.importSessions?.length ?? 0) > 0)
const acceptedTypes = '.csv,.xlsx,.tsv,.dsv,.txt,.psv,.json,.md,.markdown,.html,.htm,.docx,.zip'
const tableExtensions = ['csv', 'xlsx', 'tsv', 'dsv', 'psv', 'json']
const phaseOneDocumentExtensions = ['md', 'markdown', 'txt', 'html', 'htm', 'docx', 'zip']
const roadmapExtensions = ['pdf', 'epub']
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
    showError('Định dạng này nằm trong lộ trình, nhưng hiện chưa có bộ phân tích nên chưa thể nhập.')
    return
  }
  if (!supportedExtensions.includes(ext || '')) {
    showError('Định dạng này chưa được hỗ trợ.')
    return
  }
  if (f.size > 5 * 1024 * 1024) {
    showError('Giai đoạn đầu giới hạn tệp ở mức 5 MB để xử lý an toàn.')
    return
  }
  if (ext === 'zip' && !props.projectId) {
    showError('Hãy mở một dự án cụ thể để nhập gói ZIP thành nhiều trang Wiki.')
    return
  }
  if (!props.projectId && !tableExtensions.includes(ext || '')) {
    showError('Hãy mở một dự án cụ thể để nhập tài liệu hoặc gói ZIP thành trang Wiki.')
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
  if (tableExtensions.includes(ext)) return 'Nhập bảng nhiệm vụ'
  if (ext === 'zip') return 'Nhập gói ZIP'
  if (phaseOneDocumentExtensions.includes(ext)) return 'Nhập trang Wiki'
  if (roadmapExtensions.includes(ext)) return 'Định dạng trong lộ trình'
  return 'Tệp không được hỗ trợ'
}
</script>

<template>
  <div class="import-step import-discover">
    <div class="import-intro">
      <h3>Nhập dữ liệu</h3>
      <p>Nhập dữ liệu từ ứng dụng và tệp khác vào QALY</p>
    </div>

    <div class="import-tabs" role="tablist" aria-label="Các chế độ nhập dữ liệu">
      <button type="button" role="tab" :aria-selected="activeTab === 'discover'" :class="{ active: activeTab === 'discover' }" @click="activeTab = 'discover'">Khám phá</button>
      <button type="button" role="tab" :aria-selected="activeTab === 'completed'" :class="{ active: activeTab === 'completed' }" @click="activeTab = 'completed'">Đã hoàn tất</button>
    </div>

    <template v-if="activeTab === 'discover'">
      <div class="import-section-heading">
        <h4>Nhập nội dung của bạn</h4>
        <p>Hiện hỗ trợ CSV, Excel, JSON, TXT, Markdown, HTML, DOCX và gói ZIP. PDF và EPUB sẽ được bổ sung khi bộ phân tích sẵn sàng.</p>
      </div>

      <div class="template-actions" aria-label="Tải mẫu nhập dữ liệu">
        <a class="template-link" href="/api/import/templates/tasks.xlsx" download>
          <Download :size="15" />
          Mẫu Excel
        </a>
        <a class="template-link" href="/api/import/templates/tasks.csv" download>
          <Download :size="15" />
          Mẫu CSV
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
          <p class="dropzone-text">Nhập nội dung vào QALY</p>
          <p class="dropzone-hint">
            Kéo thả tệp CSV, Excel, JSON, văn bản, Markdown hoặc HTML, hoặc
            <label class="choose-link">
              chọn tệp
              <input type="file" :accept="acceptedTypes" hidden @change="onFileInput" />
            </label>
          </p>
          <p class="dropzone-formats">Hiện hỗ trợ CSV, Excel, JSON, TXT, Markdown, HTML, DOCX và gói ZIP. PDF và EPUB đang nằm trong lộ trình.</p>
        </template>
        <template v-else>
          <FileSpreadsheet v-if="tableExtensions.includes(extensionOf(file.name))" :size="34" class="dropzone-icon--selected" />
          <Archive v-else-if="extensionOf(file.name) === 'zip'" :size="34" class="dropzone-icon--selected" />
          <FileText v-else :size="34" class="dropzone-icon--selected" />
          <p class="dropzone-filename">{{ file.name }}</p>
          <p class="dropzone-filesize">{{ fileKindLabel(file.name) }} - {{ formatFileSize(file.size) }}</p>
          <button class="btn btn--ghost btn--sm" type="button" @click="emit('update:file', null)">Chọn tệp khác</button>
        </template>
      </div>

      <div class="import-type-section">
        <h4>Nhập dữ liệu từ tệp</h4>
        <p>Có thể nhập CSV, Excel, JSON, TXT, Markdown, HTML, DOCX và ZIP ngay lúc này. PDF và EPUB đang nằm trong lộ trình.</p>
        <div class="import-type-grid">
          <article>
            <FileText :size="18" />
            <strong>Tài liệu</strong>
            <span>Markdown, văn bản, HTML và DOCX được chuyển thành trang Wiki.</span>
          </article>
          <article>
            <FileSpreadsheet :size="18" />
            <strong>Bảng dữ liệu</strong>
            <span>CSV, Excel, TSV, PSV và JSON được nhập thành nhiệm vụ.</span>
          </article>
          <article>
            <Archive :size="18" />
            <strong>Gói ZIP</strong>
            <span>Có thể xem trước các tệp con được hỗ trợ và tạo nhiều trang Wiki.</span>
          </article>
        </div>
      </div>

      <div v-if="isNewProject" class="import-field">
        <label>Tên dự án mới khi nhập bảng dữ liệu</label>
        <input
          :value="newProjectName"
          type="text"
          aria-label="Tên dự án mới khi nhập dữ liệu"
          placeholder="Nhập tên dự án..."
          class="import-input"
          @input="emit('update:newProjectName', ($event.target as HTMLInputElement).value)"
        />
      </div>
      <div v-else class="import-info-banner">
        <CheckCircle2 :size="16" />
        <p>Dự án đích: <strong>{{ projectName }}</strong></p>
      </div>
    </template>

    <div v-else-if="isLoadingSessions" class="completed-empty">
      <CheckCircle2 :size="28" />
      <strong>Đang tải lịch sử import...</strong>
      <p>QALY đang kiểm tra các phiên import gần đây của dự án này.</p>
    </div>

    <div v-else-if="hasCompletedImports" class="completed-imports">
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
      <strong>Chưa có lần nhập dữ liệu nào hoàn tất</strong>
      <p>{{ projectId ? 'Dự án này chưa có phiên import task-table nào.' : 'Lịch sử import chỉ hiển thị khi bạn import trong một dự án cụ thể.' }}</p>
    </div>

    <div class="import-actions">
      <button class="btn btn--ghost" type="button" @click="emit('cancel')">Hủy</button>
      <button type="button" class="btn btn--primary" :disabled="!file || isLoading" @click="emit('next')">
        <template v-if="isLoading">Đang đọc...</template>
        <template v-else>Tiếp tục</template>
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
  border-radius: var(--qaly-radius-lg);
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
  border-radius: var(--qaly-radius-lg);
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
  border-radius: var(--qaly-radius-lg);
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
  border-radius: var(--qaly-radius-lg);
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
