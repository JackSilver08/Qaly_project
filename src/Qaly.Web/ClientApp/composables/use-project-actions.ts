import { ref, type Ref } from 'vue'
import { useRouter } from 'vue-router'
import type { DashboardProject, ProjectDto, DashboardResponse } from '../types'
import { apiCommand, apiResult, errorMessage } from '../utils/api-client'
import { showError, showSuccess } from './use-toast'
import { confirmDialog } from './use-confirm-dialog'

export function useProjectActions(
  projects: Ref<DashboardProject[]>,
  activeProjectId: Ref<string | null>,
  loadDashboard: () => Promise<void>,
) {
  const router = useRouter()
  const createProjectOpen = ref(false)
  const projectBeingEditedId = ref<string | null>(null)
  const projectName = ref('')
  const projectDescription = ref('')
  const projectEndDate = ref('')
  const editProjectName = ref('')
  const editProjectDescription = ref('')

  function openCreateProject() {
    createProjectOpen.value = true
    projectBeingEditedId.value = null
    void router.push('/projects')
  }

  function clearProjectForm() {
    projectName.value = ''
    projectDescription.value = ''
    projectEndDate.value = ''
  }

  async function createProject() {
    const name = projectName.value.trim()
    if (!name) return

    try {
      const project = await apiResult<ProjectDto>('/api/projects', {
        method: 'POST',
        body: JSON.stringify({
          name,
          description: projectDescription.value.trim() || null,
          startDate: null,
          endDate: projectEndDate.value ? new Date(projectEndDate.value).toISOString() : null,
        }),
      })

      clearProjectForm()
      createProjectOpen.value = false
      await loadDashboard()
      activeProjectId.value = project.id
      void router.push(`/projects/${project.id}`)
      showSuccess(`Thêm dự án "${project.name}" thành công`)
    } catch (error) {
      showError(errorMessage(error, 'Không thể thêm dự án'))
    }
  }

  function beginEditProject(projectId: string) {
    const project = projects.value.find((item) => item.id === projectId)
    if (!project) return

    activeProjectId.value = projectId
    createProjectOpen.value = false
    projectBeingEditedId.value = projectId
    editProjectName.value = project.name
    editProjectDescription.value = project.description ?? ''
  }

  async function saveProjectEdit() {
    const project = projects.value.find((item) => item.id === projectBeingEditedId.value)
    const name = editProjectName.value.trim()

    if (!project || !name) return

    try {
      await apiResult<ProjectDto>(`/api/projects/${project.id}`, {
        method: 'PUT',
        body: JSON.stringify({
          name,
          description: editProjectDescription.value.trim() || null,
          status: project.status,
          startDate: null,
          endDate: project.endDate,
        }),
      })

      projectBeingEditedId.value = null
      await loadDashboard()
      activeProjectId.value = project.id
      showSuccess(`Cập nhật dự án "${name}" thành công`)
    } catch (error) {
      showError(errorMessage(error, 'Không thể cập nhật dự án'))
    }
  }

  async function deleteProject(projectId: string) {
    const project = projects.value.find((item) => item.id === projectId)
    if (!project) return
    if (!await confirmDialog({ tone:'danger', title:'Xóa dự án?', subject:project.name, message:'Dự án sẽ được chuyển vào thùng rác.', confirmLabel:'Xóa dự án' })) return

    try {
      await apiCommand(`/api/projects/${projectId}`, { method: 'DELETE' })
      await loadDashboard()
      showSuccess(`Xóa dự án "${project.name}" thành công`)
    } catch (error) {
      showError(errorMessage(error, 'Không thể xóa dự án'))
    }
  }

  function selectProject(projectId: string) {
    activeProjectId.value = projectId
    void router.push(`/projects/${projectId}`)
  }

  async function archiveProject(projectId: string) {
    const project = projects.value.find((item) => item.id === projectId)
    if (!project) return
    if (!await confirmDialog({ tone:'warning', title:'Lưu trữ dự án?', subject:project.name, message:'Dự án sẽ chuyển sang chế độ chỉ đọc và có thể khôi phục sau.', confirmLabel:'Lưu trữ' })) return

    try {
      await apiResult<ProjectDto>(`/api/projects/${projectId}`, {
        method: 'PUT',
        body: JSON.stringify({
          name: project.name,
          description: project.description,
          status: 'Archived',
          startDate: null,
          endDate: project.endDate,
        }),
      })
      await loadDashboard()
      showSuccess(`Đã lưu trữ dự án "${project.name}"`)
    } catch (error) {
      showError(errorMessage(error, 'Không thể lưu trữ dự án'))
    }
  }

  async function restoreProject(projectId: string) {
    const project = projects.value.find((item) => item.id === projectId)
    if (!project) return
    if (!await confirmDialog({ title:'Khôi phục dự án?', subject:project.name, message:'Dự án sẽ hoạt động trở lại.', confirmLabel:'Khôi phục' })) return

    try {
      await apiResult<ProjectDto>(`/api/projects/${projectId}`, {
        method: 'PUT',
        body: JSON.stringify({
          name: project.name,
          description: project.description,
          status: 'Active',
          startDate: null,
          endDate: project.endDate,
        }),
      })
      await loadDashboard()
      showSuccess(`Đã khôi phục dự án "${project.name}"`)
    } catch (error) {
      showError(errorMessage(error, 'Không thể khôi phục dự án'))
    }
  }

  return {
    createProjectOpen,
    projectBeingEditedId,
    projectName,
    projectDescription,
    projectEndDate,
    editProjectName,
    editProjectDescription,
    openCreateProject,
    createProject,
    beginEditProject,
    saveProjectEdit,
    deleteProject,
    selectProject,
    archiveProject,
    restoreProject,
  }
}

