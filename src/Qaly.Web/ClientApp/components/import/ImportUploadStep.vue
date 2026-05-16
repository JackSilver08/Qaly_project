<script setup lang="ts">
import { Upload, FileSpreadsheet } from 'lucide-vue-next'

const props = defineProps<{
  projectId?: string
  projectName?: string
  file: File | null
  newProjectName: string
  isLoading: boolean
}>()

const emit = defineEmits<{
  'update:file': [file: File | null]
  'update:newProjectName': [name: string]
  cancel: []
  next: []
}>()

const isDragging = ref(false)

import { ref } from 'vue'

const isNewProject = !props.projectId
const acceptedTypes = '.csv,.xlsx,.tsv'

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
  if (!['csv', 'xlsx', 'tsv'].includes(ext || '')) return
  if (f.size > 5 * 1024 * 1024) return
  emit('update:file', f)
}

function formatFileSize(bytes: number) {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / 1048576).toFixed(1) + ' MB'
}
</script>

<template>
  <div class="import-step">
    <div v-if="isNewProject" class="import-field">
      <label>Tên dự án mới</label>
      <input
        :value="newProjectName"
        type="text"
        placeholder="Nhập tên dự án..."
        class="import-input"
        @input="emit('update:newProjectName', ($event.target as HTMLInputElement).value)"
      />
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
        <button class="btn btn--ghost btn--sm" type="button" @click="emit('update:file', null)">Chọn file khác</button>
      </template>
    </div>

    <div class="import-actions">
      <button class="btn btn--ghost" type="button" @click="emit('cancel')">Hủy</button>
      <button class="btn btn--primary" :disabled="!file || isLoading" @click="emit('next')">
        <template v-if="isLoading">Đang đọc...</template>
        <template v-else>Tiếp tục →</template>
      </button>
    </div>
  </div>
</template>
