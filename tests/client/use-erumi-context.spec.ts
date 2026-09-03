import { describe, it, expect, beforeEach, vi } from 'vitest'
import { ref } from 'vue'

const mockRoute = {
  params: {} as Record<string, any>,
  name: 'ProjectDetail',
  path: '/projects/p-123'
}

vi.mock('vue-router', () => ({
  useRoute: () => mockRoute
}))

const mockDashboardContext = {
  activeProjectId: ref<string | null>(null),
  selectedProject: ref<{ id: string; name: string } | null>(null),
  selectedTaskId: ref<string | null>(null),
  selectedTask: ref<{ id: string; title: string } | null>(null)
}

vi.mock('@/composables/dashboard-context', () => ({
  useDashboardContext: () => mockDashboardContext
}))

import { useErumiContext } from '@/composables/use-erumi-context'

describe('useErumiContext', () => {
  beforeEach(() => {
    mockRoute.params = {}
    mockRoute.name = 'ProjectDetail'
    mockRoute.path = '/projects/p-123'
    mockDashboardContext.activeProjectId.value = null
    mockDashboardContext.selectedProject.value = null
    mockDashboardContext.selectedTaskId.value = null
    mockDashboardContext.selectedTask.value = null
  })

  it('returns null for projectId and taskId when route params and dashboard are empty', () => {
    const { projectId, taskId, routeName, routePath } = useErumiContext()

    expect(projectId.value).toBeNull()
    expect(taskId.value).toBeNull()
    expect(routeName.value).toBe('ProjectDetail')
    expect(routePath.value).toBe('/projects/p-123')
  })

  it('resolves projectId from route.params.projectId with highest priority', () => {
    mockRoute.params = { projectId: 'route-proj-01' }
    mockDashboardContext.activeProjectId.value = 'active-proj-02'
    mockDashboardContext.selectedProject.value = { id: 'selected-proj-03', name: 'Proj 3' }

    const { projectId } = useErumiContext()
    expect(projectId.value).toBe('route-proj-01')
  })

  it('falls back to activeProjectId when route params lack projectId', () => {
    mockRoute.params = {}
    mockDashboardContext.activeProjectId.value = 'active-proj-02'
    mockDashboardContext.selectedProject.value = { id: 'selected-proj-03', name: 'Proj 3' }

    const { projectId } = useErumiContext()
    expect(projectId.value).toBe('active-proj-02')
  })

  it('falls back to selectedProject.id when route params and activeProjectId are absent', () => {
    mockRoute.params = {}
    mockDashboardContext.activeProjectId.value = null
    mockDashboardContext.selectedProject.value = { id: 'selected-proj-03', name: 'Proj 3' }

    const { projectId } = useErumiContext()
    expect(projectId.value).toBe('selected-proj-03')
  })

  it('resolves taskId from route.params.taskId with highest priority', () => {
    mockRoute.params = { taskId: 'route-task-01' }
    mockDashboardContext.selectedTaskId.value = 'active-task-02'
    mockDashboardContext.selectedTask.value = { id: 'selected-task-03', title: 'Task 3' }

    const { taskId } = useErumiContext()
    expect(taskId.value).toBe('route-task-01')
  })

  it('falls back to selectedTaskId when route params lack taskId', () => {
    mockRoute.params = {}
    mockDashboardContext.selectedTaskId.value = 'active-task-02'
    mockDashboardContext.selectedTask.value = { id: 'selected-task-03', title: 'Task 3' }

    const { taskId } = useErumiContext()
    expect(taskId.value).toBe('active-task-02')
  })

  it('falls back to selectedTask.id when route params and selectedTaskId are absent', () => {
    mockRoute.params = {}
    mockDashboardContext.selectedTaskId.value = null
    mockDashboardContext.selectedTask.value = { id: 'selected-task-03', title: 'Task 3' }

    const { taskId } = useErumiContext()
    expect(taskId.value).toBe('selected-task-03')
  })

  it('reactively updates when dashboard context changes dynamically', () => {
    const { projectId, taskId } = useErumiContext()

    expect(projectId.value).toBeNull()
    expect(taskId.value).toBeNull()

    mockDashboardContext.selectedProject.value = { id: 'p-dyn-99', name: 'Dynamic Project' }
    mockDashboardContext.selectedTask.value = { id: 't-dyn-88', title: 'Dynamic Task' }

    expect(projectId.value).toBe('p-dyn-99')
    expect(taskId.value).toBe('t-dyn-88')
  })

  it('handles non-string route parameters gracefully without crashing', () => {
    mockRoute.params = { projectId: ['array-val'] as any, taskId: 123 as any }
    mockDashboardContext.selectedProject.value = { id: 'fallback-p', name: 'Fallback' }
    mockDashboardContext.selectedTask.value = { id: 'fallback-t', title: 'Fallback' }

    const { projectId, taskId } = useErumiContext()
    expect(projectId.value).toBe('fallback-p')
    expect(taskId.value).toBe('fallback-t')
  })
})
