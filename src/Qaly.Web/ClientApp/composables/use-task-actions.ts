import { ref, type Ref } from 'vue'
import type { DashboardTask, KanbanMoveResultDto, TaskItemDto } from '../types'
import { apiCommand, apiResult, errorMessage } from '../utils/api-client'
import { displayStatus } from '../utils/formatters'
import { showError, showSuccess } from './use-toast'
import { confirmDialog } from './use-confirm-dialog'

export function useTaskActions(
  selectedTaskId: Ref<string | null>,
  loadDashboard: () => Promise<void>,
  getActiveProjectId: () => string | null,
  openTaskRoute?: (projectId: string, taskId: string) => void,
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
  const isTaskMutationPending = ref(false)
  const isBatchMutationPending = ref(false)

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

  function cancelTaskForm() {
    clearTaskForm()
    taskBeingEdited.value = null
    createTaskOpen.value = false
  }

  function toggleTaskSelection(taskId: string) {
    if (selectedTaskIds.value.has(taskId)) {
      selectedTaskIds.value.delete(taskId)
    } else {
      selectedTaskIds.value.add(taskId)
    }
  }

  async function batchDeleteTasks() {
    if (selectedTaskIds.value.size === 0 || isBatchMutationPending.value) return
    if (!await confirmDialog({ tone:'danger', title:`Xóa ${selectedTaskIds.value.size} nhiệm vụ?`, message:'Các nhiệm vụ đã chọn sẽ bị xóa khỏi dự án.', confirmLabel:'Xóa nhiệm vụ' })) return
    isBatchMutationPending.value = true
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
    } finally {
      isBatchMutationPending.value = false
    }
  }

  async function batchUpdateTaskStatus(status: string) {
    if (selectedTaskIds.value.size === 0 || isBatchMutationPending.value) return
    const ids = Array.from(selectedTaskIds.value)
    isBatchMutationPending.value = true
    try {
      if (ids.length === 1) {
        await apiCommand(`/api/tasks/${ids[0]}/status`, {
          method: 'PATCH',
          body: JSON.stringify({ status }),
        })
      } else {
        await apiCommand('/api/tasks/batch-status', {
          method: 'POST',
          body: JSON.stringify({
            ids,
            status,
          }),
        })
      }

      selectedTaskIds.value.clear()
      await loadDashboard()
      showSuccess(`Đã chuyển ${ids.length} sang ${displayStatus(status)}`)
    } catch (e) {
      showError(errorMessage(e, 'Lỗi khi cập nhật hàng loạt'))
    } finally {
      isBatchMutationPending.value = false
    }
  }

  async function createTask(projectId: string) {
    const title = newTaskTitle.value.trim()
    if (!projectId || !title || isTaskMutationPending.value) return

    if (taskBeingEdited.value) {
      await saveTaskEdit()
      return
    }

    isTaskMutationPending.value = true
    try {
      const task = await apiResult<TaskItemDto>('/api/tasks', {
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
      const canonical = await apiResult<TaskItemDto>(`/api/tasks/${task.id}`)
      if (canonical.projectId !== projectId || canonical.title !== title) {
        throw new Error('Máy chủ chưa xác nhận đúng nhiệm vụ vừa tạo. Vui lòng tải lại trước khi thử lại.')
      }

      clearTaskForm()
      createTaskOpen.value = false
      await loadDashboard()
      showSuccess(canonical.aiPrioritySuggestion ? `Thêm nhiệm vụ thành công. ${canonical.aiPrioritySuggestion}` : 'Thêm nhiệm vụ thành công')
    } catch (error) {
      showError(errorMessage(error, 'Không thể thêm nhiệm vụ'))
    } finally {
      isTaskMutationPending.value = false
    }
  }

  async function moveTask(task: DashboardTask, status: string) {
    try {
      await apiCommand(`/api/tasks/${task.id}/status`, {
        method: 'PATCH',
        body: JSON.stringify({ status }),
      })

      await loadDashboard()
      const projectId = getActiveProjectId()
      if (projectId && openTaskRoute) {
        openTaskRoute(projectId, task.id)
      } else {
        selectedTaskId.value = task.id
      }
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
      const result = await apiResult<KanbanMoveResultDto>(`/api/tasks/project/${projectId}/kanban/move`, {
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
      if (openTaskRoute) {
        openTaskRoute(projectId, task.id)
      } else {
        selectedTaskId.value = task.id
      }
      showSuccess(`Đã cập nhật vị trí nhiệm vụ trong ${displayStatus(status)}`)
      return result
    } catch (error) {
      showError(errorMessage(error, 'Không thể cập nhật vị trí nhiệm vụ'))
      return null
    }
  }

  function beginEditTask(task: DashboardTask) {
    taskBeingEdited.value = task
    newTaskTitle.value = task.title
    newTaskDescription.value = task.description ?? ''
    newTaskPriority.value = task.priority
    newTaskAssigneeId.value = task.assigneeId ?? ''
    newTaskDueDate.value = task.dueDate ? new Date(task.dueDate).toISOString().split('T')[0] : ''
    newTaskIsPrivate.value = task.isPrivate
    newTaskIsPinned.value = task.isPinned
    newTaskContributesToProgress.value = task.contributesToProgress
    createTaskOpen.value = true
  }

  async function saveTaskEdit() {
    if (!taskBeingEdited.value || isTaskMutationPending.value) return

    isTaskMutationPending.value = true
    try {
      // The edit sheet only exposes a subset of Task fields. Merge against a
      // fresh canonical row so reassigning from Kanban cannot silently clear
      // estimate, logged hours, labels or Sprint.
      const current = await apiResult<TaskItemDto>(`/api/tasks/${taskBeingEdited.value.id}`)
      const requestedAssigneeId = newTaskAssigneeId.value || null
      const assigneeIds = requestedAssigneeId === current.assigneeId
        ? current.assignees.map(assignee => assignee.userId)
        : requestedAssigneeId ? [requestedAssigneeId] : []
      await apiResult<TaskItemDto>(`/api/tasks/${taskBeingEdited.value.id}`, {
        method: 'PUT',
        body: JSON.stringify({
          title: newTaskTitle.value.trim(),
          description: newTaskDescription.value.trim() || null,
          status: current.status,
          priority: newTaskPriority.value,
          dueDate: newTaskDueDate.value ? new Date(newTaskDueDate.value).toISOString() : null,
          estimatedHours: current.estimatedHours,
          actualHours: current.actualHours,
          assigneeId: requestedAssigneeId,
          assigneeIds,
          labelIds: current.labels.map(label => label.id),
          sprintId: current.sprintId,
          isPrivate: newTaskIsPrivate.value,
          isPinned: newTaskIsPinned.value,
          contributesToProgress: newTaskContributesToProgress.value,
          rowVersion: current.rowVersion,
        }),
      })
      const updated = await apiResult<TaskItemDto>(`/api/tasks/${taskBeingEdited.value.id}`)

      const sameLabels = updated.labels.map(label => label.id).sort().join(',') ===
        current.labels.map(label => label.id).sort().join(',')
      if (updated.assigneeId !== requestedAssigneeId ||
          updated.estimatedHours !== current.estimatedHours ||
          updated.actualHours !== current.actualHours ||
          updated.sprintId !== current.sprintId || !sameLabels) {
        throw new Error('Máy chủ chưa xác nhận đầy đủ thay đổi hoặc đã làm lệch dữ liệu Task; vui lòng tải lại trước khi thử lại.')
      }

      clearTaskForm()
      createTaskOpen.value = false
      taskBeingEdited.value = null
      await loadDashboard()
      showSuccess('Cập nhật nhiệm vụ thành công')
    } catch (error) {
      showError(errorMessage(error, 'Không thể cập nhật nhiệm vụ'))
    } finally {
      isTaskMutationPending.value = false
    }
  }

  async function deleteTask(taskId: string) {
    if (!await confirmDialog({ tone:'danger', title:'Xóa nhiệm vụ?', message:'Nhiệm vụ này sẽ bị xóa khỏi dự án.', confirmLabel:'Xóa nhiệm vụ' })) return

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
    cancelTaskForm,
    newTaskTitle,
    newTaskDescription,
    newTaskPriority,
    newTaskAssigneeId,
    newTaskDueDate,
    newTaskIsPrivate,
    newTaskIsPinned,
    newTaskContributesToProgress,
    selectedTaskIds,
    isTaskMutationPending,
    isBatchMutationPending,
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
