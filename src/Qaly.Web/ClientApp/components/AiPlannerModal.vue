<script setup lang="ts">
import { ref } from 'vue'
import {
  Sparkles,
  X,
  Plus,
  Trash2,
  Calendar,
  CheckCircle2,
} from 'lucide-vue-next'
import { apiResult } from '../utils/api-client'
import { showError, showSuccess } from '../composables/use-toast'

interface TaskPlanItem {
  title: string
  description: string
  priority: string
  dueDate: string
}

interface GeneratedPlan {
  isNewProject: boolean
  projectName: string | null
  projectDescription: string | null
  tasks: {
    title: string
    description: string | null
    priority: string
    dueDateOffsetDays: number
  }[]
}

const props = defineProps<{
  projectId: string | null
}>()

const emit = defineEmits<{
  close: []
  created: [projectId: string]
}>()

const step = ref<'input' | 'review'>('input')
const promptText = ref('')
const isLoading = ref(false)

// Review state
const isNewProject = ref(true)
const projectName = ref('')
const projectDescription = ref('')
const tasks = ref<TaskPlanItem[]>([])

const suggestions = [
  'Tạo dự án Xây dựng Web bán hàng quần áo, đầy đủ các task frontend, backend và UI/UX',
  'Lập kế hoạch phát triển tính năng đăng ký/đăng nhập bằng Google/Facebook',
  'Lên danh sách công việc kiểm thử (test cases) cho chức năng giỏ hàng và thanh toán',
  'Thiết lập quy trình CI/CD cho dự án và deploy lên AWS/Azure'
]

function useSuggestion(s: string) {
  promptText.value = s
}

async function handleGeneratePlan() {
  const prompt = promptText.value.trim()
  if (!prompt) return

  isLoading.value = true
  try {
    const plan = await apiResult<GeneratedPlan>('/api/ai/generate-plan', {
      method: 'POST',
      body: JSON.stringify({
        userPrompt: prompt,
        projectId: props.projectId
      })
    })

    isNewProject.value = plan.isNewProject
    projectName.value = plan.projectName || ''
    projectDescription.value = plan.projectDescription || ''
    
    // Map offset days to actual calendar dates
    tasks.value = (plan.tasks || []).map(t => {
      const offset = t.dueDateOffsetDays || 7
      const date = new Date()
      date.setDate(date.getDate() + offset)
      const dateStr = date.toISOString().split('T')[0]
      return {
        title: t.title,
        description: t.description || '',
        priority: t.priority || 'Medium',
        dueDate: dateStr
      }
    })

    step.value = 'review'
  } catch (error: any) {
    showError(error?.message || 'Không thể tạo kế hoạch bằng AI. Vui lòng thử lại.')
  } finally {
    isLoading.value = false
  }
}

function addTask() {
  const date = new Date()
  date.setDate(date.getDate() + 7)
  tasks.value.push({
    title: 'Công việc mới',
    description: '',
    priority: 'Medium',
    dueDate: date.toISOString().split('T')[0]
  })
}

function removeTask(index: number) {
  tasks.value.splice(index, 1)
}

async function handleCreatePlan() {
  if (isNewProject.value && !projectName.value.trim()) {
    showError('Tên dự án là bắt buộc.')
    return
  }
  if (tasks.value.length === 0) {
    showError('Vui lòng thêm ít nhất một công việc.')
    return
  }

  isLoading.value = true
  try {
    const formattedTasks = tasks.value.map(t => ({
      title: t.title.trim(),
      description: t.description.trim() || null,
      priority: t.priority,
      dueDate: t.dueDate ? new Date(t.dueDate).toISOString() : null
    }))

    const result = await apiResult<{ projectId: string; taskCount: number }>('/api/ai/create-plan', {
      method: 'POST',
      body: JSON.stringify({
        isNewProject: isNewProject.value,
        projectName: isNewProject.value ? projectName.value.trim() : null,
        projectDescription: isNewProject.value ? projectDescription.value.trim() : null,
        projectId: props.projectId,
        tasks: formattedTasks
      })
    })

    showSuccess(
      isNewProject.value
        ? `Đã tạo thành công dự án mới với ${result.taskCount} công việc!`
        : `Đã thêm thành công ${result.taskCount} công việc vào dự án!`
    )
    
    emit('created', result.projectId)
    emit('close')
  } catch (error: any) {
    showError(error?.message || 'Không thể tạo dự án/công việc. Vui lòng kiểm tra lại.')
  } finally {
    isLoading.value = false
  }
}
</script>

<template>
  <div class="ai-planner-backdrop" @click.self="emit('close')">
    <div class="ai-planner-modal glass-card">
      
      <!-- Modal Header -->
      <div class="ai-planner-header">
        <div class="ai-planner-title">
          <div class="ai-sparkle-icon">
            <Sparkles :size="20" />
          </div>
          <div>
            <h2>Lập kế hoạch thông minh bằng AI</h2>
            <p v-if="props.projectId">Lập kế hoạch công việc cho dự án hiện tại</p>
            <p v-else>Khởi tạo dự án mới và sơ đồ công việc tự động</p>
          </div>
        </div>
        <button class="icon-button" @click="emit('close')">
          <X :size="18" />
        </button>
      </div>

      <!-- Loading Overlay -->
      <div v-if="isLoading" class="ai-loading-overlay">
        <div class="ai-spinner-container">
          <div class="ai-pulse-glow"></div>
          <Sparkles class="ai-spinning-sparkle" :size="32" />
        </div>
        <h3>AI đang tính toán và xây dựng kế hoạch...</h3>
        <p>Quá trình này có thể mất từ 5-10 giây</p>
      </div>

      <!-- Step 1: Input Prompts -->
      <div v-else-if="step === 'input'" class="ai-planner-body">
        <div class="form-group">
          <label class="input-section-label">Mô tả mục tiêu của bạn bằng ngôn ngữ tự nhiên</label>
          <textarea
            v-model="promptText"
            placeholder="Ví dụ: Lên kế hoạch xây dựng module đăng nhập, đăng ký sử dụng mạng xã hội Google và Facebook cho dự án hiện tại, bao gồm cả tài liệu hướng dẫn và test cases..."
            rows="6"
            class="modal-input prompt-textarea"
            required
          ></textarea>
        </div>

        <div class="suggestions-container">
          <label class="suggestions-label">Gợi ý nhanh:</label>
          <div class="suggestions-list">
            <button
              v-for="(s, index) in suggestions"
              :key="index"
              type="button"
              class="suggestion-item"
              @click="useSuggestion(s)"
            >
              {{ s }}
            </button>
          </div>
        </div>

        <div class="ai-planner-actions">
          <button class="btn btn--ghost" type="button" @click="emit('close')">Hủy</button>
          <button
            class="btn btn--ai-primary"
            type="button"
            :disabled="!promptText.trim()"
            @click="handleGeneratePlan"
          >
            <Sparkles :size="16" />
            <span>Lên kế hoạch với AI</span>
          </button>
        </div>
      </div>

      <!-- Step 2: Review & Edit Plan -->
      <div v-else-if="step === 'review'" class="ai-planner-body has-scroll">
        <div class="review-intro">
          <CheckCircle2 class="success-icon" :size="18" />
          <span>AI đã lập xong kế hoạch! Bạn có thể chỉnh sửa các thông tin bên dưới trước khi tạo chính thức.</span>
        </div>

        <!-- Project info (if new project) -->
        <div v-if="isNewProject" class="project-info-review">
          <h3 class="section-title">Thông tin Dự án</h3>
          <div class="form-group">
            <label>Tên dự án</label>
            <input v-model="projectName" type="text" class="modal-input" required />
          </div>
          <div class="form-group">
            <label>Mô tả dự án</label>
            <textarea v-model="projectDescription" class="modal-input" rows="2"></textarea>
          </div>
        </div>

        <!-- Tasks list review -->
        <div class="tasks-review-section">
          <div class="tasks-review-header">
            <h3 class="section-title">Danh sách công việc đề xuất ({{ tasks.length }})</h3>
            <button class="btn-add-task-inline" type="button" @click="addTask">
              <Plus :size="14" /> Thêm việc
            </button>
          </div>

          <div class="tasks-review-list">
            <div v-for="(task, index) in tasks" :key="index" class="task-review-card">
              <div class="task-review-card-header">
                <input
                  v-model="task.title"
                  type="text"
                  class="task-title-input"
                  placeholder="Tên công việc..."
                  required
                />
                <button
                  type="button"
                  class="task-delete-btn"
                  title="Xóa công việc này"
                  @click="removeTask(index)"
                >
                  <Trash2 :size="15" />
                </button>
              </div>
              
              <div class="task-review-card-body">
                <textarea
                  v-model="task.description"
                  class="task-desc-input"
                  placeholder="Mô tả công việc (không bắt buộc)..."
                  rows="2"
                ></textarea>
                
                <div class="task-meta-grid">
                  <div class="meta-item">
                    <label>Độ ưu tiên</label>
                    <select v-model="task.priority" class="meta-select">
                      <option value="Low">Low</option>
                      <option value="Medium">Medium</option>
                      <option value="High">High</option>
                      <option value="Critical">Critical</option>
                    </select>
                  </div>
                  <div class="meta-item">
                    <label>Hạn chót</label>
                    <div class="date-input-container">
                      <Calendar :size="13" />
                      <input v-model="task.dueDate" type="date" class="meta-date-input" />
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="ai-planner-actions">
          <button class="btn btn--ghost" type="button" @click="step = 'input'">Quay lại</button>
          <button class="btn btn--ai-success" type="button" @click="handleCreatePlan">
            <CheckCircle2 :size="16" />
            <span>Chấp nhận & Tạo thực tế</span>
          </button>
        </div>
      </div>

    </div>
  </div>
</template>

<style scoped>
.ai-planner-backdrop {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(10, 10, 18, 0.65);
  backdrop-filter: blur(8px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 9999;
  padding: 1.5rem;
}

.ai-planner-modal {
  width: 100%;
  max-width: 680px;
  background: rgba(18, 18, 30, 0.85);
  border: 1px solid rgba(139, 92, 246, 0.25);
  border-radius: 16px;
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.5), inset 0 1px 0 rgba(255, 255, 255, 0.05);
  display: flex;
  flex-direction: column;
  max-height: 85vh;
  overflow: hidden;
  color: #e2e8f0;
}

.ai-planner-header {
  padding: 1.25rem 1.5rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.ai-planner-title {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.ai-sparkle-icon {
  background: linear-gradient(135deg, #8b5cf6 0%, #d946ef 100%);
  color: white;
  padding: 0.5rem;
  border-radius: 10px;
  box-shadow: 0 0 15px rgba(139, 92, 246, 0.4);
  display: flex;
  align-items: center;
  justify-content: center;
}

.ai-planner-title h2 {
  font-size: 1.15rem;
  font-weight: 600;
  margin: 0;
  background: linear-gradient(90deg, #f1f5f9 0%, #c084fc 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
}

.ai-planner-title p {
  font-size: 0.8rem;
  color: #94a3b8;
  margin: 2px 0 0 0;
}

.icon-button {
  background: transparent;
  border: none;
  color: #94a3b8;
  cursor: pointer;
  padding: 0.35rem;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.icon-button:hover {
  background: rgba(255, 255, 255, 0.08);
  color: #f1f5f9;
}

.ai-planner-body {
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
  overflow: hidden;
}

.ai-planner-body.has-scroll {
  overflow-y: auto;
  flex: 1;
}

/* Custom scrollbar for webkit */
.ai-planner-body.has-scroll::-webkit-scrollbar {
  width: 6px;
}
.ai-planner-body.has-scroll::-webkit-scrollbar-track {
  background: rgba(0, 0, 0, 0.1);
}
.ai-planner-body.has-scroll::-webkit-scrollbar-thumb {
  background: rgba(139, 92, 246, 0.3);
  border-radius: 3px;
}
.ai-planner-body.has-scroll::-webkit-scrollbar-thumb:hover {
  background: rgba(139, 92, 246, 0.5);
}

.input-section-label {
  display: block;
  font-size: 0.9rem;
  font-weight: 500;
  margin-bottom: 0.5rem;
  color: #cbd5e1;
}

.prompt-textarea {
  resize: none;
  font-size: 0.95rem;
  line-height: 1.5;
  border: 1px solid rgba(255, 255, 255, 0.08);
  background: rgba(0, 0, 0, 0.2);
  border-radius: 10px;
  color: #f1f5f9;
  transition: all 0.2s ease;
}

.prompt-textarea:focus {
  border-color: rgba(139, 92, 246, 0.6);
  box-shadow: 0 0 10px rgba(139, 92, 246, 0.15);
  outline: none;
}

.suggestions-container {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.suggestions-label {
  font-size: 0.8rem;
  color: #94a3b8;
  font-weight: 500;
}

.suggestions-list {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.suggestion-item {
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.05);
  border-radius: 8px;
  padding: 0.5rem 0.75rem;
  font-size: 0.8rem;
  text-align: left;
  color: #cbd5e1;
  cursor: pointer;
  transition: all 0.2s ease;
}

.suggestion-item:hover {
  background: rgba(139, 92, 246, 0.08);
  border-color: rgba(139, 92, 246, 0.2);
  color: #c084fc;
}

.ai-planner-actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 0.75rem;
  margin-top: 0.5rem;
}

.btn {
  padding: 0.55rem 1.25rem;
  border-radius: 8px;
  font-size: 0.9rem;
  font-weight: 500;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  transition: all 0.2s ease;
}

.btn--ghost {
  background: transparent;
  border: 1px solid rgba(255, 255, 255, 0.1);
  color: #cbd5e1;
}

.btn--ghost:hover {
  background: rgba(255, 255, 255, 0.05);
}

.btn--ai-primary {
  background: linear-gradient(135deg, #8b5cf6 0%, #a78bfa 100%);
  border: none;
  color: white;
  box-shadow: 0 4px 12px rgba(139, 92, 246, 0.3);
}

.btn--ai-primary:hover:not(:disabled) {
  opacity: 0.95;
  box-shadow: 0 4px 16px rgba(139, 92, 246, 0.45);
}

.btn--ai-primary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  box-shadow: none;
}

.btn--ai-success {
  background: linear-gradient(135deg, #10b981 0%, #059669 100%);
  border: none;
  color: white;
  box-shadow: 0 4px 12px rgba(16, 185, 129, 0.3);
}

.btn--ai-success:hover {
  opacity: 0.95;
  box-shadow: 0 4px 16px rgba(16, 185, 129, 0.45);
}

/* Loading Overlay Styles */
.ai-loading-overlay {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 4rem 2rem;
  text-align: center;
}

.ai-spinner-container {
  position: relative;
  width: 80px;
  height: 80px;
  margin-bottom: 1.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
}

.ai-pulse-glow {
  position: absolute;
  width: 100%;
  height: 100%;
  border-radius: 50%;
  background: rgba(139, 92, 246, 0.2);
  animation: ai-pulse 2s infinite ease-in-out;
}

.ai-spinning-sparkle {
  color: #c084fc;
  animation: ai-spin 3s infinite linear;
  filter: drop-shadow(0 0 8px rgba(168, 85, 247, 0.5));
}

.ai-loading-overlay h3 {
  font-size: 1.1rem;
  font-weight: 500;
  margin: 0 0 0.5rem 0;
  color: #f1f5f9;
}

.ai-loading-overlay p {
  font-size: 0.8rem;
  color: #64748b;
  margin: 0;
}

@keyframes ai-pulse {
  0% {
    transform: scale(0.8);
    opacity: 0.2;
  }
  50% {
    transform: scale(1.2);
    opacity: 0.6;
    box-shadow: 0 0 30px rgba(139, 92, 246, 0.4);
  }
  100% {
    transform: scale(0.8);
    opacity: 0.2;
  }
}

@keyframes ai-spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

/* Review Step Styles */
.review-intro {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  background: rgba(16, 185, 129, 0.1);
  border: 1px solid rgba(16, 185, 129, 0.2);
  border-radius: 8px;
  padding: 0.75rem 1rem;
  font-size: 0.85rem;
  color: #34d399;
}

.success-icon {
  flex-shrink: 0;
}

.section-title {
  font-size: 0.95rem;
  font-weight: 600;
  color: #f1f5f9;
  margin: 0;
  border-left: 3px solid #8b5cf6;
  padding-left: 0.5rem;
}

.project-info-review {
  background: rgba(255, 255, 255, 0.02);
  border: 1px solid rgba(255, 255, 255, 0.05);
  border-radius: 10px;
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.tasks-review-section {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.tasks-review-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.btn-add-task-inline {
  background: rgba(139, 92, 246, 0.15);
  border: 1px solid rgba(139, 92, 246, 0.25);
  color: #c084fc;
  font-size: 0.8rem;
  font-weight: 500;
  padding: 0.35rem 0.75rem;
  border-radius: 6px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 0.35rem;
  transition: all 0.2s ease;
}

.btn-add-task-inline:hover {
  background: rgba(139, 92, 246, 0.25);
  color: #e9d5ff;
}

.tasks-review-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  max-height: 40vh;
  overflow-y: auto;
  padding-right: 0.25rem;
}

.task-review-card {
  background: rgba(255, 255, 255, 0.02);
  border: 1px solid rgba(255, 255, 255, 0.06);
  border-radius: 8px;
  padding: 0.85rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  transition: border-color 0.2s ease;
}

.task-review-card:hover {
  border-color: rgba(139, 92, 246, 0.2);
}

.task-review-card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.task-title-input {
  background: transparent;
  border: none;
  border-bottom: 1px solid transparent;
  color: #f1f5f9;
  font-size: 0.9rem;
  font-weight: 600;
  padding: 0.15rem 0;
  width: 100%;
  transition: border-color 0.2s ease;
}

.task-title-input:focus {
  outline: none;
  border-color: rgba(139, 92, 246, 0.5);
}

.task-delete-btn {
  background: transparent;
  border: none;
  color: #ef4444;
  opacity: 0.6;
  cursor: pointer;
  padding: 0.25rem;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.task-delete-btn:hover {
  opacity: 1;
  background: rgba(239, 68, 68, 0.1);
}

.task-desc-input {
  background: rgba(0, 0, 0, 0.15);
  border: 1px solid rgba(255, 255, 255, 0.04);
  border-radius: 6px;
  padding: 0.4rem 0.6rem;
  color: #cbd5e1;
  font-size: 0.8rem;
  resize: none;
  width: 100%;
}

.task-desc-input:focus {
  outline: none;
  border-color: rgba(139, 92, 246, 0.3);
}

.task-meta-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.75rem;
}

.meta-item {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.meta-item label {
  font-size: 0.75rem;
  color: #94a3b8;
}

.meta-select {
  background: rgba(0, 0, 0, 0.2);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  padding: 0.3rem 0.5rem;
  color: #cbd5e1;
  font-size: 0.8rem;
}

.date-input-container {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  background: rgba(0, 0, 0, 0.2);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  padding: 0.3rem 0.5rem;
  color: #cbd5e1;
}

.meta-date-input {
  background: transparent;
  border: none;
  color: #cbd5e1;
  font-size: 0.8rem;
  width: 100%;
}

.meta-date-input:focus {
  outline: none;
}
</style>
