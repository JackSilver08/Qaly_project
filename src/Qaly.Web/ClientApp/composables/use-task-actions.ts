import { ref, type Ref } from 'vue'
import type { DashboardTask, TaskItemDto } from '../types'
import { apiCommand, apiResult, errorMessage } from '../utils/api-client'
import { displayStatus } from '../utils/formatters'
import { showError, showSuccess } from './use-toast'

export function useTaskActions(
  selectedTaskId: Ref<string | null>,
  loadDashboard: () => Promise<void>,
) {
  const createTaskOpen = ref(false)
  const taskBeingEdited = ref<DashboardTask | null>(null)
  const newTaskTitle = ref('')
  const newTaskDescription = ref('')
  const newTaskPriority = ref('Medium')
  const newTaskAssigneeId = ref('')
  const newTaskDueDate = ref('')

  function clearTaskForm() {
    newTaskTitle.value = ''
    newTaskDescription.value = ''
    newTaskPriority.value = 'Medium'
    newTaskAssigneeId.value = ''
    newTaskDueDate.value = ''
  }

  async function createTask(projectId: string) {
    const title = newTaskTitle.value.trim()
    if (!projectId || !title) return

    if (taskBeingEdited.value) {
      await saveTaskEdit()
      return
    }

    try {
      const task = await apiResult<{ aiPrioritySuggestion: string | null }>('/api/tasks', {
        method: 'POST',
        body: JSON.stringify({
          title,
          description: newTaskDescription.value.trim() || null,
          priority: newTaskPriority.value,
          dueDate: newTaskDueDate.value ? new Date(newTaskDueDate.value).toISOString() : null,
          estimatedHours: null,
          projectId,
          assigneeId: newTaskAssigneeId.value || null,
          isPrivate: false,
        }),
      })

      clearTaskForm()
      createTaskOpen.value = false
      await loadDashboard()
      showSuccess(task.aiPrioritySuggestion ? `Thêm nhiệm vụ thành công. ${task.aiPrioritySuggestion}` : 'Thêm nhiệm vụ thành công')
    } catch (error) {
      showError(errorMessage(error, 'Không thể thêm nhiệm vụ'))
    }
  }

  async function moveTask(task: DashboardTask, status: string) {
    try {
      await apiCommand(`/api/tasks/${task.id}/status`, {
        method: 'PATCH',
        body: JSON.stringify({ status }),
      })

      await loadDashboard()
      selectedTaskId.value = task.id
      showSuccess(`Đã chuyển nhiệm vụ sang ${displayStatus(status)}`)
    } catch (error) {
      showError(errorMessage(error, 'Không thể cập nhật trạng thái nhiệm vụ'))
    }
  }

  function beginEditTask(task: DashboardTask) {
    taskBeingEdited.value = task
    newTaskTitle.value = task.title
    newTaskDescription.value = ''
    newTaskPriority.value = task.priority
    newTaskAssigneeId.value = ''
    newTaskDueDate.value = task.dueDate ? new Date(task.dueDate).toISOString().split('T')[0] : ''
    createTaskOpen.value = true
  }

  async function saveTaskEdit() {
    if (!taskBeingEdited.value) return

    try {
      await apiResult<TaskItemDto>(`/api/tasks/${taskBeingEdited.value.id}`, {
        method: 'PUT',
        body: JSON.stringify({
          title: newTaskTitle.value.trim(),
          description: newTaskDescription.value.trim() || null,
          status: taskBeingEdited.value.status,
          priority: newTaskPriority.value,
          dueDate: newTaskDueDate.value ? new Date(newTaskDueDate.value).toISOString() : null,
          assigneeId: newTaskAssigneeId.value || null,
        }),
      })

      clearTaskForm()
      createTaskOpen.value = false
      taskBeingEdited.value = null
      await loadDashboard()
      showSuccess('Cập nhật nhiệm vụ thành công')
    } catch (error) {
      showError(errorMessage(error, 'Không thể cập nhật nhiệm vụ'))
    }
  }

  async function deleteTask(taskId: string) {
    if (!confirm('Bạn có chắc chắn muốn xóa task này?')) return

    try {
      await apiCommand(`/api/tasks/${taskId}`, { method: 'DELETE' })
      await loadDashboard()
      showSuccess('Xóa nhiệm vụ thành công')
    } catch (error) {
      showError(errorMessage(error, 'Không thể xóa nhiệm vụ'))
    }
  }

  return {
    createTaskOpen,
    taskBeingEdited,
    newTaskTitle,
    newTaskDescription,
    newTaskPriority,
    newTaskAssigneeId,
    newTaskDueDate,
    createTask,
    moveTask,
    beginEditTask,
    saveTaskEdit,
    deleteTask,
  }
}
