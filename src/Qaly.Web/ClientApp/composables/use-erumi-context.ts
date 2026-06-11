import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useDashboardContext } from './dashboard-context'

export function useErumiContext() {
  const route = useRoute()
  const dashboard = useDashboardContext()

  const projectId = computed<string | null>(() => {
    // 1. From route params
    if (route.params.projectId && typeof route.params.projectId === 'string') {
      return route.params.projectId
    }
    // 2. From dashboard activeProjectId
    if (dashboard.activeProjectId?.value) {
      return dashboard.activeProjectId.value
    }
    // 3. From dashboard selectedProject
    if (dashboard.selectedProject?.value?.id) {
      return dashboard.selectedProject.value.id
    }
    return null
  })

  const taskId = computed<string | null>(() => {
    // 1. From route params
    if (route.params.taskId && typeof route.params.taskId === 'string') {
      return route.params.taskId
    }
    // 2. From dashboard selectedTaskId
    if (dashboard.selectedTaskId?.value) {
      return dashboard.selectedTaskId.value
    }
    // 3. From dashboard selectedTask
    if (dashboard.selectedTask?.value?.id) {
      return dashboard.selectedTask.value.id
    }
    return null
  })

  const routeName = computed(() => route.name)
  const routePath = computed(() => route.path)

  return {
    projectId,
    taskId,
    routeName,
    routePath
  }
}
