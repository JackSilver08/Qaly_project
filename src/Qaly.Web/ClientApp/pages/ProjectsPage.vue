<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { FileUp, FolderKanban, CheckCircle2, AlertTriangle, X, Edit3 } from 'lucide-vue-next'
import ProjectList from '../components/ProjectList.vue'
import ProjectGrid from '../components/ProjectGrid.vue'
import ProjectToolbar from '../components/ProjectToolbar.vue'
import ImportModal from '../components/import/ImportModal.vue'
import ImportUndoBanner from '../components/import/ImportUndoBanner.vue'
import { useDashboardContext } from '../composables/dashboard-context'
import { apiCommand, apiResult, errorMessage } from '../utils/api-client'
import { showError, showSuccess } from '../composables/use-toast'
import type { PagedResult, ProjectDto, UserDto } from '../types'

const {
  activeProjectCards,
  beginEditProject,
  createProjectOpen,
  deleteProject,
  editProjectDescription,
  editProjectName,
  openCreateProject,
  projectBeingEditedId,
  projectDescription,
  projectEndDate,
  projectFilter,
  projectName,
  projectSort,
  saveProjectEdit,
  searchQuery,
  selectProject,
  selectedProject,
  loadDashboard,
  projects,
  users,
} = useDashboardContext()

const isGridView = ref(true)
const showImportModal = ref(false)
const undoBannerData = ref<{ importSessionId: string; importedCount: number; failedCount: number; duplicateSkippedCount: number; createdAt: string } | null>(null)
const createSourceMode = ref<'members' | 'group'>('members')
const selectedMemberIds = ref<string[]>([])
const selectedGroupId = ref('')
const availableGroups = ref<{ id: string; name: string; description: string | null; memberCount: number }[]>([])

const totalProjectsCount = computed(() => projects.value.filter((p: any) => p.status !== 'Archived').length)
const onTrackCount = computed(() => projects.value.filter((p: any) => p.status !== 'Archived' && p.overdueTaskCount === 0).length)
const atRiskCount = computed(() => projects.value.filter((p: any) => p.status !== 'Archived' && p.overdueTaskCount > 0).length)
const activeUsers = computed<UserDto[]>(() => (users.value ?? []).filter((user: UserDto) => user.isActive))

onMounted(() => {
  void loadGroups()
})

async function loadGroups() {
  try {
    const result = await apiResult<PagedResult<{ id: string; name: string; description: string | null; memberCount: number }>>('/api/groups?pageSize=100')
    availableGroups.value = result.items
  } catch {
    availableGroups.value = []
  }
}

async function createProjectWithSelection() {
  const name = projectName.value.trim()
  if (!name) return

  try {
    if (createSourceMode.value === 'group' && selectedGroupId.value) {
      const result = await apiResult<any>(`/api/groups/${selectedGroupId.value}/create-project`, {
        method: 'POST',
        body: JSON.stringify({
          name,
          code: null,
          description: projectDescription.value.trim() || null,
          startDate: null,
          endDate: projectEndDate.value ? new Date(projectEndDate.value).toISOString() : null,
        }),
      })
      createProjectOpen.value = false
      selectedGroupId.value = ''
      selectedMemberIds.value = []
      projectName.value = ''
      projectDescription.value = ''
      projectEndDate.value = ''
      await loadDashboard()
      const projectId = result.project?.id ?? result.project?.Id
      if (projectId) selectProject(projectId)
      showSuccess(`Tạo project từ nhóm thành công (${result.membersAdded ?? 0} thành viên)`)
      return
    }

    const project = await apiResult<ProjectDto>('/api/projects', {
      method: 'POST',
      body: JSON.stringify({
        name,
        description: projectDescription.value.trim() || null,
        startDate: null,
        endDate: projectEndDate.value ? new Date(projectEndDate.value).toISOString() : null,
      }),
    })

    for (const userId of selectedMemberIds.value) {
      await apiCommand(`/api/projects/${project.id}/members`, {
        method: 'POST',
        body: JSON.stringify({ userId, role: 'Member' }),
      })
    }

    createProjectOpen.value = false
    selectedMemberIds.value = []
    projectName.value = ''
    projectDescription.value = ''
    projectEndDate.value = ''
    await loadDashboard()
    selectProject(project.id)
    showSuccess(`Tạo dự án "${project.name}" thành công`)
  } catch (error) {
    showError(errorMessage(error, 'Không thể tạo dự án'))
  }
}

function onImported(result: any) {
  showImportModal.value = false
  if (result?.importSessionId) {
    undoBannerData.value = {
      importSessionId: result.importSessionId,
      importedCount: result.importedCount,
      failedCount: result.failedCount ?? 0,
      duplicateSkippedCount: result.duplicateSkippedCount ?? 0,
      createdAt: new Date().toISOString(),
    }
  }
  loadDashboard()
}

async function handleUndoFromBanner() {
  if (!undoBannerData.value) return
  try {
    const res = await fetch(`/api/import/sessions/${undoBannerData.value.importSessionId}`, { method: 'DELETE' })
    const data = await res.json()
    if (data.isSuccess) {
      undoBannerData.value = null
      loadDashboard()
    }
  } catch { /* ignore */ }
}
</script>

<template>
  <div class="dashboard-scroll dashboard-scroll--embedded no-scrollbar">
    <div class="dashboard-main project-home-main no-scrollbar">
      
      <!-- Stats Header -->
      <section class="summary-card-grid" aria-label="Tổng quan nhanh dự án" style="margin-bottom: 16px;">
        <article class="summary-card glass-card">
          <div style="display: flex; justify-content: space-between; align-items: center; width: 100%;">
            <span style="font-size: 11px; font-weight: 800; color: var(--muted); letter-spacing: 0.5px;">TỔNG DỰ ÁN</span>
            <div style="width: 34px; height: 34px; border-radius: 10px; display: grid; place-items: center; background: var(--blue-100); color: var(--primary);">
              <FolderKanban :size="16" />
            </div>
          </div>
          <div style="margin-top: 8px;">
            <strong style="font-size: 28px; font-weight: 800; color: var(--text);">{{ totalProjectsCount }}</strong>
            <p style="font-size: 11px; color: var(--muted); margin-top: 2px; margin-bottom: 0;">Dự án hoạt động trong Workspace</p>
          </div>
        </article>

        <article class="summary-card glass-card">
          <div style="display: flex; justify-content: space-between; align-items: center; width: 100%;">
            <span style="font-size: 11px; font-weight: 800; color: var(--muted); letter-spacing: 0.5px;">ĐÚNG TIẾN ĐỘ</span>
            <div style="width: 34px; height: 34px; border-radius: 10px; display: grid; place-items: center; background: var(--mint-100); color: #047857; position: relative;">
              <CheckCircle2 :size="16" />
              <span style="position: absolute; width: 6px; height: 6px; border-radius: 50%; background: #10b981; top: 6px; right: 6px; display: inline-block; animation: pulse 2s infinite;"></span>
            </div>
          </div>
          <div style="margin-top: 8px;">
            <strong style="font-size: 28px; font-weight: 800; color: var(--text);">{{ onTrackCount }}</strong>
            <p style="font-size: 11px; color: var(--muted); margin-top: 2px; margin-bottom: 0;">Dự án không có việc trễ hạn</p>
          </div>
        </article>

        <article class="summary-card glass-card">
          <div style="display: flex; justify-content: space-between; align-items: center; width: 100%;">
            <span style="font-size: 11px; font-weight: 800; color: var(--muted); letter-spacing: 0.5px;">CÓ RỦI RO / CHẬM</span>
            <div style="width: 34px; height: 34px; border-radius: 10px; display: grid; place-items: center; background: #fee2e2; color: #ef4444;">
              <AlertTriangle :size="16" />
            </div>
          </div>
          <div style="margin-top: 8px;">
            <strong style="font-size: 28px; font-weight: 800; color: #ef4444;">{{ atRiskCount }}</strong>
            <p style="font-size: 11px; color: var(--muted); margin-top: 2px; margin-bottom: 0;">Dự án có đầu việc bị trễ hạn</p>
          </div>
        </article>
      </section>

      <section class="project-workspace glass-card">
        <div class="project-workspace__header">
          <div>
            <span>Projects</span>
            <h2>Dự án</h2>
          </div>
          <p>{{ activeProjectCards.length }} dự án đang hiển thị</p>
        </div>

        <ProjectToolbar
          v-model:search="searchQuery"
          v-model:sort="projectSort"
          v-model:filter="projectFilter"
          v-model:is-grid-view="isGridView"
          :project-count="activeProjectCards.length"
          @create="openCreateProject"
        >
          <template #actions>
            <button class="btn-import" @click="showImportModal = true">
              <FileUp :size="15" /> Nhập
            </button>
          </template>
        </ProjectToolbar>



        <ProjectGrid
          v-if="isGridView"
          :projects="activeProjectCards"
          :active-project-id="selectedProject?.id ?? null"
          @view="selectProject"
          @edit="beginEditProject"
          @delete="deleteProject"
          @create="openCreateProject"
        />
        <ProjectList
          v-else
          :projects="activeProjectCards"
          :active-project-id="selectedProject?.id ?? null"
          @view="selectProject"
          @edit="beginEditProject"
          @delete="deleteProject"
          @create="openCreateProject"
        />
      </section>
    </div>

    <ImportModal
      v-if="showImportModal"
      @close="showImportModal = false"
      @imported="onImported"
    />

    <ImportUndoBanner
      v-if="undoBannerData"
      :import-session-id="undoBannerData.importSessionId"
      :imported-count="undoBannerData.importedCount"
      :failed-count="undoBannerData.failedCount"
      :duplicate-skipped-count="undoBannerData.duplicateSkippedCount"
      :created-at="undoBannerData.createdAt"
      @undo="handleUndoFromBanner"
      @dismiss="undoBannerData = null"
    />

    <!-- Create Project Modal -->
    <Teleport to="body">
      <div v-if="createProjectOpen" class="project-modal-backdrop" @click.self="createProjectOpen = false">
        <div class="project-modal glass-card">
          <div class="project-modal-header">
            <div class="project-modal-title">
              <FolderKanban :size="20" />
              <h2>Tạo dự án mới</h2>
            </div>
            <button class="icon-button" @click="createProjectOpen = false"><X :size="18" /></button>
          </div>
          <form class="project-modal-body" @submit.prevent="createProjectWithSelection">
            <div class="form-group">
              <label>Tên dự án</label>
              <input v-model="projectName" type="text" placeholder="Nhập tên dự án..." required class="modal-input" />
            </div>
            <div class="form-group">
              <label>Mô tả ngắn</label>
              <textarea v-model="projectDescription" placeholder="Nhập mô tả dự án (không bắt buộc)..." rows="3" class="modal-input"></textarea>
            </div>
            <div class="form-group">
              <label>Ngày kết thúc dự kiến</label>
              <input v-model="projectEndDate" type="date" class="modal-input" />
            </div>
            <div class="form-group">
              <label>Thêm thành viên</label>
              <div class="project-source-toggle">
                <button type="button" :class="{ active: createSourceMode === 'members' }" @click="createSourceMode = 'members'">Chọn từng người</button>
                <button type="button" :class="{ active: createSourceMode === 'group' }" @click="createSourceMode = 'group'">Chọn nhóm</button>
              </div>
            </div>
            <div v-if="createSourceMode === 'members'" class="form-group">
              <label>Tài khoản trong workspace</label>
              <div class="project-member-picker">
                <label v-for="user in activeUsers" :key="user.id">
                  <input v-model="selectedMemberIds" type="checkbox" :value="user.id" />
                  <span>{{ user.fullName }} · {{ user.email }}</span>
                </label>
              </div>
            </div>
            <div v-else class="form-group">
              <label>Nhóm nguồn</label>
              <select v-model="selectedGroupId" class="modal-input">
                <option value="">Chọn nhóm</option>
                <option v-for="group in availableGroups" :key="group.id" :value="group.id">
                  {{ group.name }} · {{ group.memberCount }} thành viên
                </option>
              </select>
            </div>
            
            <div class="project-modal-actions">
              <button class="btn btn--ghost" type="button" @click="createProjectOpen = false">Hủy</button>
              <button class="btn btn--primary" type="submit" :disabled="!projectName.trim()">Tạo dự án</button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>

    <!-- Edit Project Modal -->
    <Teleport to="body">
      <div v-if="projectBeingEditedId" class="project-modal-backdrop" @click.self="projectBeingEditedId = null">
        <div class="project-modal glass-card">
          <div class="project-modal-header">
            <div class="project-modal-title">
              <Edit3 :size="20" />
              <h2>Chỉnh sửa dự án</h2>
            </div>
            <button class="icon-button" @click="projectBeingEditedId = null"><X :size="18" /></button>
          </div>
          <form class="project-modal-body" @submit.prevent="saveProjectEdit">
            <div class="form-group">
              <label>Tên dự án</label>
              <input v-model="editProjectName" type="text" placeholder="Nhập tên dự án..." required class="modal-input" />
            </div>
            <div class="form-group">
              <label>Mô tả ngắn</label>
              <textarea v-model="editProjectDescription" placeholder="Nhập mô tả dự án (không bắt buộc)..." rows="3" class="modal-input"></textarea>
            </div>
            
            <div class="project-modal-actions">
              <button class="btn btn--ghost" type="button" @click="projectBeingEditedId = null">Hủy</button>
              <button class="btn btn--primary" type="submit" :disabled="!editProjectName.trim()">Lưu thay đổi</button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.btn-import {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 7px 14px;
  border: 1px solid rgba(184, 219, 255, 0.34);
  border-radius: 10px;
  background: rgb(15, 0, 130);
  color: #d9e9ff;
  font-size: 0.8rem;
  font-weight: 700;
  cursor: pointer;
  transition: transform 220ms ease, border-color 220ms ease, background 220ms ease, box-shadow 220ms ease, color 220ms ease;
}
.btn-import:hover {
  transform: translateY(-1px);
  border-color: rgba(117, 182, 255, 0.62);
  background: rgba(31, 128, 255, 0.2);
  box-shadow: 0 14px 28px rgba(15, 76, 255, 0.24);
}

/* Modal Styles */
.project-modal-backdrop {
  position: fixed; inset: 0; z-index: 9999;
  background: rgba(15, 23, 42, 0.4);
  backdrop-filter: blur(8px);
  -webkit-backdrop-filter: blur(8px);
  display: flex; align-items: center; justify-content: center;
  animation: fadeIn 0.3s cubic-bezier(0.16, 1, 0.3, 1);
}

.project-modal {
  --accent: #2563eb;
  --accent-hover: #1d4ed8;
  --text-main: #0f172a;
  --text-muted: #64748b;
  --border-color: #e2e8f0;
  
  width: min(480px, 94vw);
  max-height: 88vh;
  overflow-y: auto;
  border-radius: 20px;
  padding: 0;
  background: #ffffff;
  box-shadow: 
    0 10px 40px -10px rgba(0,0,0,0.1), 
    0 0 0 1px rgba(0,0,0,0.05);
  transform-origin: center;
  animation: modalScaleIn 0.4s cubic-bezier(0.16, 1, 0.3, 1);
}

.project-modal-header {
  display: flex; align-items: center; justify-content: space-between;
  padding: 24px 28px 20px;
  border-bottom: 1px solid var(--border-color);
}

.project-modal-title { 
  display: flex; align-items: center; gap: 12px;
  color: var(--text-main);
}
.project-modal-title h2 { 
  font-size: 1.25rem; font-weight: 700; margin: 0;
  letter-spacing: -0.01em;
}

.icon-button { 
  background: transparent; 
  border: none; 
  color: var(--text-muted); 
  cursor: pointer; 
  transition: all 0.2s ease; 
  display: flex; align-items: center; justify-content: center; 
  padding: 8px; 
  border-radius: 50%; 
}
.icon-button:hover { 
  background: #f1f5f9; 
  color: var(--text-main); 
}

.project-modal-body {
  padding: 24px 28px 28px;
}

.form-group {
  margin-bottom: 24px;
}
.form-group label {
  display: block; 
  font-size: 0.875rem; 
  font-weight: 600; 
  color: var(--text-main); 
  margin-bottom: 8px;
}

.modal-input {
  width: 100%; 
  padding: 12px 16px; 
  border-radius: 12px; 
  font-size: 0.95rem;
  background: #f8fafc; 
  border: 1px solid var(--border-color);
  color: var(--text-main); 
  outline: none; 
  transition: all 0.2s ease;
  box-sizing: border-box;
}
.modal-input:hover {
  background: #ffffff;
  border-color: #cbd5e1;
}
.modal-input:focus { 
  background: #ffffff;
  border-color: var(--accent); 
  box-shadow: 0 0 0 4px rgba(37, 99, 235, 0.1); 
}
.modal-input::placeholder { color: #94a3b8; }

textarea.modal-input { 
  resize: vertical; 
  min-height: 90px;
  line-height: 1.5;
}

.project-source-toggle {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
}

.project-source-toggle button {
  min-height: 38px;
  border: 1px solid var(--border-color);
  border-radius: 10px;
  background: #f8fafc;
  color: var(--text-muted);
  font-weight: 700;
  cursor: pointer;
}

.project-source-toggle button.active {
  border-color: var(--accent);
  background: #eff6ff;
  color: var(--accent);
}

.project-member-picker {
  max-height: 180px;
  overflow: auto;
  display: grid;
  gap: 8px;
  padding: 8px;
  border: 1px solid var(--border-color);
  border-radius: 12px;
  background: #f8fafc;
}

.project-member-picker label {
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 0;
  padding: 8px;
  border-radius: 8px;
  background: #ffffff;
  color: var(--text-main);
}

.project-member-picker span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.project-modal-actions {
  display: flex; justify-content: flex-end; gap: 12px;
  padding-top: 12px;
}

.btn {
  display: inline-flex; align-items: center; gap: 8px;
  padding: 10px 24px; border-radius: 10px; font-size: 0.95rem;
  font-weight: 600; border: none; cursor: pointer; transition: all 0.2s;
}
.btn--primary { 
  background: var(--accent); 
  color: #ffffff; 
  box-shadow: 0 2px 8px -2px rgba(37, 99, 235, 0.4);
}
.btn--primary:hover:not(:disabled) { 
  background: var(--accent-hover); 
  transform: translateY(-1px);
  box-shadow: 0 4px 12px -2px rgba(37, 99, 235, 0.5);
}
.btn--primary:active:not(:disabled) {
  transform: translateY(0);
}
.btn--primary:disabled { 
  opacity: 0.6; cursor: not-allowed; 
  background: #94a3b8;
  box-shadow: none;
}

.btn--ghost {
  background: transparent; 
  color: var(--text-muted);
  border: 1px solid var(--border-color);
}
.btn--ghost:hover { 
  background: #f8fafc; 
  color: var(--text-main); 
  border-color: #cbd5e1;
}

@keyframes fadeIn { 
  from { opacity: 0; backdrop-filter: blur(0px); } 
  to { opacity: 1; backdrop-filter: blur(8px); } 
}
@keyframes modalScaleIn {
  from { opacity: 0; transform: scale(0.96) translateY(10px); }
  to { opacity: 1; transform: scale(1) translateY(0); }
}
</style>
