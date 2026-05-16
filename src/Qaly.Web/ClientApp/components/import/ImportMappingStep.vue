<script setup lang="ts">
import { ArrowLeft, ArrowRight, AlertTriangle } from 'lucide-vue-next'
import { computed } from 'vue'

const props = defineProps<{
  parseResult: any
  mappings: { columnIndex: number; targetField: string }[]
  firstRowIsHeader: boolean
  skipDuplicates: boolean
  selectedSheet: string | null
  assignToMeIfEmpty: boolean
  defaultPriority: string | null
  enableAiCategorization: boolean
}>()

const emit = defineEmits<{
  'update:mappings': [mappings: { columnIndex: number; targetField: string }[]]
  'update:firstRowIsHeader': [val: boolean]
  'update:skipDuplicates': [val: boolean]
  'update:selectedSheet': [val: string | null]
  'update:assignToMeIfEmpty': [val: boolean]
  'update:defaultPriority': [val: string | null]
  'update:enableAiCategorization': [val: boolean]
  back: []
  next: []
}>()

const targetFields = [
  { value: 'Title', label: '📝 Tiêu đề (Title)', required: true },
  { value: 'Description', label: '📋 Mô tả' },
  { value: 'Status', label: '📊 Trạng thái (Cột Kanban)' },
  { value: 'Priority', label: '🔥 Độ ưu tiên' },
  { value: 'DueDate', label: '📅 Hạn chót' },
  { value: 'EstimatedHours', label: '⏱️ Giờ ước tính' },
  { value: 'Labels', label: '🏷️ Nhãn (Labels)' },
  { value: 'Assignee', label: '👤 Người thực hiện' },
  { value: 'Skip', label: '⏭️ Bỏ qua' },
]

const hasTitleMapping = computed(() =>
  props.mappings.some(m => m.targetField === 'Title')
)

function updateMappingField(index: number, targetField: string) {
  const updated = [...props.mappings]
  updated[index] = { ...updated[index], targetField }
  emit('update:mappings', updated)
}
</script>

<template>
  <div class="import-step">
    <!-- Sheet selector -->
    <div v-if="parseResult.sheetNames?.length > 1" class="import-field">
      <label>Chọn Sheet</label>
      <select
        :value="selectedSheet"
        class="import-select"
        @change="emit('update:selectedSheet', ($event.target as HTMLSelectElement).value)"
      >
        <option v-for="name in parseResult.sheetNames" :key="name" :value="name">{{ name }}</option>
      </select>
    </div>

    <!-- Options row -->
    <div class="import-options-row">
      <label class="import-toggle">
        <input
          type="checkbox"
          :checked="firstRowIsHeader"
          @change="emit('update:firstRowIsHeader', ($event.target as HTMLInputElement).checked)"
        />
        <span>Dòng đầu là header</span>
      </label>
      <label class="import-toggle">
        <input
          type="checkbox"
          :checked="skipDuplicates"
          @change="emit('update:skipDuplicates', ($event.target as HTMLInputElement).checked)"
        />
        <span>Bỏ qua task trùng tên</span>
      </label>
    </div>

    <!-- Default Values Settings -->
    <div class="import-settings-box">
      <p class="import-settings-title">Cài đặt Mặc định (Nếu dữ liệu trống)</p>
      
      <div class="import-options-row">
        <label class="import-toggle">
          <input
            type="checkbox"
            :checked="assignToMeIfEmpty"
            @change="emit('update:assignToMeIfEmpty', ($event.target as HTMLInputElement).checked)"
          />
          <span>Tự động giao cho tôi (Assignee)</span>
        </label>
      </div>

      <div class="import-field">
        <label>Độ ưu tiên mặc định (Priority)</label>
        <select
          :value="defaultPriority || ''"
          class="import-select"
          @change="emit('update:defaultPriority', ($event.target as HTMLSelectElement).value || null)"
        >
          <option value="">-- Bỏ qua (hoặc dùng Medium) --</option>
          <option value="Low">Low</option>
          <option value="Medium">Medium</option>
          <option value="High">High</option>
          <option value="Critical">Critical</option>
        </select>
      </div>

      <!-- AI Option (Phase 2 preview) -->
      <div class="import-options-row" style="margin-top: 10px;">
        <label class="import-toggle ai-toggle">
          <input
            type="checkbox"
            :checked="enableAiCategorization"
            @change="emit('update:enableAiCategorization', ($event.target as HTMLInputElement).checked)"
          />
          <span>✨ Dùng AI để phân loại Kanban & Labels (Task trống sẽ được AI đọc nội dung)</span>
        </label>
      </div>
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
        <select
          :value="m.targetField"
          class="import-select mapping-target"
          @change="updateMappingField(i, ($event.target as HTMLSelectElement).value)"
        >
          <option v-for="f in targetFields" :key="f.value" :value="f.value">{{ f.label }}</option>
        </select>
      </div>
    </div>

    <div v-if="!hasTitleMapping" class="import-warning">
      <AlertTriangle :size="16" />
      <span>Cần ít nhất 1 cột map vào "Tiêu đề (Title)"</span>
    </div>

    <div class="import-actions">
      <button class="btn btn--ghost" type="button" @click="emit('back')"><ArrowLeft :size="16" /> Quay lại</button>
      <button class="btn btn--primary" :disabled="!hasTitleMapping" @click="emit('next')">
        Tiếp tục <ArrowRight :size="16" />
      </button>
    </div>
  </div>
</template>

<style scoped>
.import-settings-box {
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.06);
  border-radius: 12px;
  padding: 16px;
  margin-bottom: 20px;
}
.import-settings-title {
  font-size: 0.85rem;
  font-weight: 600;
  color: rgba(255, 255, 255, 0.7);
  margin: 0 0 12px 0;
}
.ai-toggle {
  color: #a78bfa !important;
  font-weight: 500;
}
</style>
