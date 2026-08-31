import { ref } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useTaskActions } from '@/composables/use-task-actions'

const mocks = vi.hoisted(() => ({
  apiCommand: vi.fn(),
  apiResult: vi.fn(),
  confirmDialog: vi.fn(),
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))

vi.mock('@/utils/api-client', () => ({
  apiCommand: mocks.apiCommand,
  apiResult: mocks.apiResult,
  errorMessage: (_error: unknown, fallback: string) => fallback,
}))
vi.mock('@/composables/use-confirm-dialog', () => ({ confirmDialog: mocks.confirmDialog }))
vi.mock('@/composables/use-toast', () => ({ showError: mocks.showError, showSuccess: mocks.showSuccess }))

function task(overrides: Record<string, unknown> = {}) {
  return {
    id: 'task-1', title: 'Task one', description: 'Description', status: 'Todo', priority: 'Medium',
    dueDate: null, estimatedHours: 8, actualHours: 2, isPrivate: false, isRestricted: false,
    isPinned: false, contributesToProgress: true, upvoteCount: 0, downvoteCount: 0,
    projectId: 'project-1', projectName: 'Project', assigneeId: null, assigneeName: null,
    assignees: [], labels: [{ id: 'label-1', name: 'Backend', color: '#000' }], reporterId: 'user-1',
    reporterName: 'Reporter', commentCount: 0, attachmentCount: 0, aiPrioritySuggestion: null,
    createdAt: '2026-08-01T00:00:00Z', sortOrder: 1, rowVersion: 'rv-1', number: 1,
    key: 'QALY-1', parentTaskId: null, subtaskCount: 0, completedSubtaskCount: 0,
    subtaskProgressPercentage: 0, sprintId: 'sprint-1',
    ...overrides,
  }
}

function setup(openTaskRoute?: (projectId: string, taskId: string) => void) {
  const selectedTaskId = ref<string | null>(null)
  const loadDashboard = vi.fn().mockResolvedValue(undefined)
  const actions = useTaskActions(selectedTaskId, loadDashboard, () => 'project-1', openTaskRoute)
  return { actions, selectedTaskId, loadDashboard }
}

beforeEach(() => {
  vi.clearAllMocks()
  mocks.confirmDialog.mockResolvedValue(true)
  mocks.apiCommand.mockResolvedValue(undefined)
})

describe('useTaskActions', () => {
  it('toggles task selection without duplicating ids', () => {
    const { actions } = setup()
    actions.toggleTaskSelection('task-1')
    actions.toggleTaskSelection('task-1')
    expect([...actions.selectedTaskIds.value]).toEqual([])
  })

  it('does not batch-delete an empty selection', async () => {
    const { actions } = setup()
    await actions.batchDeleteTasks()
    expect(mocks.confirmDialog).not.toHaveBeenCalled()
    expect(mocks.apiCommand).not.toHaveBeenCalled()
  })

  it('keeps batch selection when delete confirmation is cancelled', async () => {
    mocks.confirmDialog.mockResolvedValue(false)
    const { actions } = setup()
    actions.toggleTaskSelection('task-1')
    await actions.batchDeleteTasks()
    expect([...actions.selectedTaskIds.value]).toEqual(['task-1'])
  })

  it('clears selection only after a successful batch delete', async () => {
    const { actions, loadDashboard } = setup()
    actions.toggleTaskSelection('task-1')
    await actions.batchDeleteTasks()
    expect(mocks.apiCommand).toHaveBeenCalledWith('/api/tasks/batch-delete', expect.objectContaining({ method: 'POST' }))
    expect(actions.selectedTaskIds.value.size).toBe(0)
    expect(loadDashboard).toHaveBeenCalledOnce()
  })

  it('preserves selection when batch delete fails', async () => {
    mocks.apiCommand.mockRejectedValue(new Error('failed'))
    const { actions } = setup()
    actions.toggleTaskSelection('task-1')
    await actions.batchDeleteTasks()
    expect([...actions.selectedTaskIds.value]).toEqual(['task-1'])
    expect(mocks.showError).toHaveBeenCalled()
  })

  it('uses the single-task status endpoint for one selected task', async () => {
    const { actions } = setup()
    actions.toggleTaskSelection('task-1')
    await actions.batchUpdateTaskStatus('Done')
    expect(mocks.apiCommand).toHaveBeenCalledWith('/api/tasks/task-1/status', expect.objectContaining({ method: 'PATCH' }))
  })

  it('uses the batch status endpoint for multiple selected tasks', async () => {
    const { actions } = setup()
    actions.toggleTaskSelection('task-1')
    actions.toggleTaskSelection('task-2')
    await actions.batchUpdateTaskStatus('InReview')
    const request = mocks.apiCommand.mock.calls[0]
    expect(request[0]).toBe('/api/tasks/batch-status')
    expect(JSON.parse(request[1].body)).toEqual({ ids: ['task-1', 'task-2'], status: 'InReview' })
  })

  it('does not create a task without project or title', async () => {
    const { actions } = setup()
    await actions.createTask('project-1')
    actions.newTaskTitle.value = 'Task'
    await actions.createTask('')
    expect(mocks.apiResult).not.toHaveBeenCalled()
  })

  it('creates, reads back, then clears the form', async () => {
    const canonical = task({ title: 'New task', aiPrioritySuggestion: 'Ưu tiên High' })
    mocks.apiResult.mockResolvedValueOnce(canonical).mockResolvedValueOnce(canonical)
    const { actions, loadDashboard } = setup()
    actions.newTaskTitle.value = '  New task  '
    actions.newTaskDescription.value = ' Details '
    actions.newTaskAssigneeId.value = 'user-2'
    actions.newTaskDueDate.value = '2026-09-01'
    await actions.createTask('project-1')
    const payload = JSON.parse(mocks.apiResult.mock.calls[0][1].body)
    expect(payload).toMatchObject({ title: 'New task', description: 'Details', assigneeId: 'user-2', assigneeIds: ['user-2'] })
    expect(mocks.apiResult.mock.calls[1][0]).toBe('/api/tasks/task-1')
    expect(actions.newTaskTitle.value).toBe('')
    expect(loadDashboard).toHaveBeenCalledOnce()
  })

  it('keeps the create form when canonical read-back disagrees', async () => {
    mocks.apiResult.mockResolvedValueOnce(task({ title: 'New task' })).mockResolvedValueOnce(task({ title: 'Other task' }))
    const { actions } = setup()
    actions.newTaskTitle.value = 'New task'
    await actions.createTask('project-1')
    expect(actions.newTaskTitle.value).toBe('New task')
    expect(mocks.showError).toHaveBeenCalled()
  })

  it('prevents duplicate create submissions while one is pending', async () => {
    let resolveCreate!: (value: unknown) => void
    mocks.apiResult.mockImplementationOnce(() => new Promise(resolve => { resolveCreate = resolve }))
    const { actions } = setup()
    actions.newTaskTitle.value = 'New task'
    const first = actions.createTask('project-1')
    const second = actions.createTask('project-1')
    expect(mocks.apiResult).toHaveBeenCalledTimes(1)
    resolveCreate(task({ title: 'New task' }))
    mocks.apiResult.mockResolvedValueOnce(task({ title: 'New task' }))
    await Promise.all([first, second])
  })

  it('opens a task route after a successful move when a route callback exists', async () => {
    const open = vi.fn()
    const { actions } = setup(open)
    await actions.moveTask(task() as never, 'InProgress')
    expect(open).toHaveBeenCalledWith('project-1', 'task-1')
  })

  it('falls back to selectedTaskId after a successful move', async () => {
    const { actions, selectedTaskId } = setup()
    await actions.moveTask(task() as never, 'InProgress')
    expect(selectedTaskId.value).toBe('task-1')
  })

  it('sends rowVersion and gives beforeTask precedence in Kanban moves', async () => {
    mocks.apiResult.mockResolvedValue({ task: task(), board: { projectId: 'project-1', columns: [] } })
    const { actions } = setup()
    await actions.moveTaskOnKanban('project-1', task() as never, 'InReview', 'before-1', 'after-1')
    const payload = JSON.parse(mocks.apiResult.mock.calls[0][1].body)
    expect(payload).toMatchObject({ taskId: 'task-1', fromStatus: 'Todo', toStatus: 'InReview', beforeTaskId: 'before-1', afterTaskId: null, rowVersion: 'rv-1' })
  })

  it('returns null and does not change selection when a Kanban move fails', async () => {
    mocks.apiResult.mockRejectedValue(new Error('conflict'))
    const { actions, selectedTaskId } = setup()
    const result = await actions.moveTaskOnKanban('project-1', task() as never, 'Done', null, null)
    expect(result).toBeNull()
    expect(selectedTaskId.value).toBeNull()
  })

  it('populates and cancels the edit form without mutating data', () => {
    const { actions } = setup()
    actions.beginEditTask(task({ dueDate: '2026-09-02T00:00:00Z', assigneeId: 'user-2' }) as never)
    expect(actions.newTaskTitle.value).toBe('Task one')
    expect(actions.newTaskDueDate.value).toBe('2026-09-02')
    actions.cancelTaskForm()
    expect(actions.taskBeingEdited.value).toBeNull()
    expect(actions.createTaskOpen.value).toBe(false)
  })

  it('preserves hidden canonical fields when saving an edit', async () => {
    const current = task()
    const updated = task({ title: 'Renamed' })
    mocks.apiResult.mockResolvedValueOnce(current).mockResolvedValueOnce(updated).mockResolvedValueOnce(updated)
    const { actions, loadDashboard } = setup()
    actions.beginEditTask(task() as never)
    actions.newTaskTitle.value = 'Renamed'
    await actions.saveTaskEdit()
    const payload = JSON.parse(mocks.apiResult.mock.calls[1][1].body)
    expect(payload).toMatchObject({ estimatedHours: 8, actualHours: 2, labelIds: ['label-1'], sprintId: 'sprint-1', rowVersion: 'rv-1' })
    expect(loadDashboard).toHaveBeenCalledOnce()
  })

  it('keeps edit state when the server changes protected fields', async () => {
    const current = task()
    mocks.apiResult.mockResolvedValueOnce(current).mockResolvedValueOnce(task()).mockResolvedValueOnce(task({ estimatedHours: 3 }))
    const { actions } = setup()
    actions.beginEditTask(task() as never)
    await actions.saveTaskEdit()
    expect(actions.taskBeingEdited.value).not.toBeNull()
    expect(mocks.showError).toHaveBeenCalled()
  })

  it('does not delete when confirmation is cancelled', async () => {
    mocks.confirmDialog.mockResolvedValue(false)
    const { actions } = setup()
    await actions.deleteTask('task-1')
    expect(mocks.apiCommand).not.toHaveBeenCalled()
  })
})
