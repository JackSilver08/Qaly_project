import { ref, type Ref } from 'vue'
import type { DashboardTask, KanbanMoveResultDto, TaskItemDto } from '../types'
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
  const newTaskIsPrivate = ref(false)
  const newTaskIsPinned = ref(false)
  const newTaskContributesToProgress = ref(true)
  const selectedTaskIds = ref(new Set<string>())

  function clearTaskForm() {
    newTaskTitle.value = ''
    newTaskDescription.value = ''
    newTaskPriority.value = 'Medium'
    newTaskAssigneeId.value = ''
    newTaskDueDate.value = ''
    newTaskIsPrivate.value = false
    newTaskIsPinned.value = false
    newTaskContributesToProgress.value = true
  }

  function toggleTaskSelection(taskId: string) {
    if (selectedTaskIds.value.has(taskId)) {
      selectedTaskIds.value.delete(taskId)
    } else {
      selectedTaskIds.value.add(taskId)
    }
  }

  async function batchDeleteTasks() {
    if (selectedTaskIds.value.size === 0) return
    if (!confirm(`Xóa ${selectedTaskIds.value.size} nhiệm vụ đã chọn?`)) return
    try {
      await apiCommand('/api/tasks/batch-delete', {
        method: 'POST',
        body: JSON.stringify({ ids: Array.from(selectedTaskIds.value) }),
      })
      selectedTaskIds.value.clear()
      await loadDashboard()
      showSuccess('Đã xóa thành công')
    } catch (e) {
      showError(errorMessage(e, 'Lỗi khi xóa hàng loạt'))
    }
  }

  async function batchUpdateTaskStatus(status: string) {
    if (selectedTaskIds.value.size === 0) return
    try {
      await apiCommand('/api/tasks/batch-status', {
        method: 'POST',
        body: JSON.stringify({
          ids: Array.from(selectedTaskIds.value),
          status,
        }),
      })
      selectedTaskIds.value.clear()
      await loadDashboard()
      showSuccess(`Đã chuyển ${selectedTaskIds.value.size} sang ${displayStatus(status)}`)
    } catch (e) {
      showError(errorMessage(e, 'Lỗi khi cập nhật hàng loạt'))
    }
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
          assigneeIds: newTaskAssigneeId.value ? [newTaskAssigneeId.value] : [],
          isPrivate: newTaskIsPrivate.value,
          isPinned: newTaskIsPinned.value,
          contributesToProgress: newTaskContributesToProgress.value,
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

  async function moveTaskOnKanban(
    projectId: string,
    task: DashboardTask,
    status: string,
    beforeTaskId: string | null,
    afterTaskId: string | null,
  ) {
    try {
      await apiResult<KanbanMoveResultDto>(`/api/tasks/project/${projectId}/kanban/move`, {
        method: 'PATCH',
        body: JSON.stringify({
          taskId: task.id,
          fromStatus: task.status,
          toStatus: status,
          beforeTaskId,
          afterTaskId: beforeTaskId ? null : afterTaskId,
          rowVersion: task.rowVersion || null,
        }),
      })

      await loadDashboard()
      selectedTaskId.value = task.id
      showSuccess(`Đã cập nhật vị trí nhiệm vụ trong ${displayStatus(status)}`)
    } catch (error) {
      showError(errorMessage(error, 'Không thể cập nhật vị trí nhiệm vụ'))
    }
  }

  function beginEditTask(task: DashboardTask) {
    taskBeingEdited.value = task
    newTaskTitle.value = task.title
    newTaskDescription.value = ''
    newTaskPriority.value = task.priority
    newTaskAssigneeId.value = ''
    newTaskDueDate.value = task.dueDate ? new Date(task.dueDate).toISOString().split('T')[0] : ''
    newTaskIsPrivate.value = task.isPrivate
    newTaskIsPinned.value = task.isPinned
    newTaskContributesToProgress.value = task.contributesToProgress
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
          estimatedHours: null,
          actualHours: null,
          assigneeId: newTaskAssigneeId.value || null,
          assigneeIds: newTaskAssigneeId.value ? [newTaskAssigneeId.value] : [],
          isPrivate: newTaskIsPrivate.value,
          isPinned: newTaskIsPinned.value,
          contributesToProgress: newTaskContributesToProgress.value,
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
    newTaskIsPrivate,
    newTaskIsPinned,
    newTaskContributesToProgress,
    selectedTaskIds,
    toggleTaskSelection,
    batchDeleteTasks,
    batchUpdateTaskStatus,
    createTask,
    moveTask,
    moveTaskOnKanban,
    beginEditTask,
    saveTaskEdit,
    deleteTask,
  }
}
