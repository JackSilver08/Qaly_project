<script setup lang="ts">
import { ArrowLeft, ArrowRight, AlertTriangle } from 'lucide-vue-next'
import { computed } from 'vue'

const props = defineProps<{
  parseResult: any
  mappings: { columnIndex: number; targetField: string }[]
  firstRowIsHeader: boolean
  skipDuplicates: boolean
  selectedSheet: string | null
  projectMembers?: { userId: string; fullName: string; email?: string }[]
  defaultAssigneeId: string | null
  assignToMeIfEmpty: boolean
  defaultPriority: string | null
  defaultStatus: string | null
  enableAiCategorization: boolean
}>()

const emit = defineEmits<{
  'update:mappings': [mappings: { columnIndex: number; targetField: string }[]]
  'update:firstRowIsHeader': [val: boolean]
  'update:skipDuplicates': [val: boolean]
  'update:selectedSheet': [val: string | null]
  'update:defaultAssigneeId': [val: string | null]
  'update:assignToMeIfEmpty': [val: boolean]
  'update:defaultPriority': [val: string | null]
  'update:defaultStatus': [val: string | null]
  'update:enableAiCategorization': [val: boolean]
  back: []
  next: []
}>()

const targetFields = [
  { value: 'Title', label: 'Tiêu đề (Title)', required: true },
  { value: 'Description', label: 'Mô tả' },
  { value: 'Status', label: 'Trạng thái (Cột Kanban)' },
  { value: 'Priority', label: 'Độ ưu tiên' },
  { value: 'DueDate', label: 'Hạn chót' },
  { value: 'EstimatedHours', label: 'Giờ ước tính' },
  { value: 'Labels', label: 'Nhãn (Labels)' },
  { value: 'Assignee', label: 'Người thực hiện' },
  { value: 'Skip', label: 'Bỏ qua' },
]

const hasTitleMapping = computed(() =>
  props.mappings.some(m => m.targetField === 'Title')
)

const duplicateMappedFields = computed(() => {
  const counts = new Map<string, number>()
  for (const mapping of props.mappings) {
    if (mapping.targetField === 'Skip') continue
    counts.set(mapping.targetField, (counts.get(mapping.targetField) || 0) + 1)
  }
  return [...counts.entries()].filter(([, count]) => count > 1).map(([field]) => field)
})

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

      <div v-if="projectMembers?.length" class="import-field">
        <label>Người phụ trách mặc định</label>
        <select
          :value="defaultAssigneeId || ''"
          class="import-select"
          @change="emit('update:defaultAssigneeId', ($event.target as HTMLSelectElement).value || null)"
        >
          <option value="">Không chọn</option>
          <option v-for="member in projectMembers" :key="member.userId" :value="member.userId">
            {{ member.fullName }}{{ member.email ? ` (${member.email})` : '' }}
          </option>
        </select>
      </div>

      <div class="import-field">
        <label>Cột Kanban mặc định (trạng thái)</label>
        <select
          :value="defaultStatus || ''"
          class="import-select"
          @change="emit('update:defaultStatus', ($event.target as HTMLSelectElement).value || null)"
        >
          <option value="">Chưa làm</option>
          <option value="Todo">Chưa làm</option>
          <option value="InProgress">Đang thực hiện</option>
          <option value="OnHold">Tạm dừng</option>
          <option value="InReview">Đang xem xét</option>
          <option value="Done">Hoàn thành</option>
          <option value="Cancelled">Đã hủy</option>
        </select>
      </div>

      <div class="import-field">
        <label>Mức ưu tiên mặc định</label>
        <select
          :value="defaultPriority || ''"
          class="import-select"
          @change="emit('update:defaultPriority', ($event.target as HTMLSelectElement).value || null)"
        >
          <option value="">-- Bỏ qua (hoặc dùng Trung bình) --</option>
          <option value="Low">Thấp</option>
          <option value="Medium">Trung bình</option>
          <option value="High">Cao</option>
          <option value="Critical">Khẩn cấp</option>
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
          <span>Dùng AI để phân loại Kanban và nhãn (nhiệm vụ còn trống sẽ được AI đọc nội dung)</span>
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

    <div v-if="duplicateMappedFields.length" class="import-warning">
      <AlertTriangle :size="16" />
      <span>Mỗi field chỉ được map một lần: {{ duplicateMappedFields.join(', ') }}</span>
    </div>

    <div class="import-actions">
      <button class="btn btn--ghost" type="button" @click="emit('back')"><ArrowLeft :size="16" /> Quay lại</button>
      <button class="btn btn--primary" :disabled="!hasTitleMapping || duplicateMappedFields.length > 0" @click="emit('next')">
        Tiếp tục <ArrowRight :size="16" />
      </button>
    </div>
  </div>
</template>

<style scoped>
.import-settings-box {
  background: #f9fafb;
  border: 1px solid #e5e7eb;
  border-radius: var(--qaly-radius-lg);
  padding: 16px;
  margin-bottom: 20px;
}
.import-settings-title {
  font-size: 0.85rem;
  font-weight: 600;
  color: #374151;
  margin: 0 0 12px 0;
}
.ai-toggle {
  color: #7dd3fc !important;
  font-weight: 500;
}
</style>
