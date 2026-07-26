<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  ArrowUpRight,
  AlertTriangle,
  CheckCircle2,
  Edit3,
  FileUp,
  FolderKanban,
  Sparkles,
  TrendingUp,
  Users,
  X,
} from 'lucide-vue-next'
import ProjectList from '../components/ProjectList.vue'
import ProjectGrid from '../components/ProjectGrid.vue'
import ProjectToolbar from '../components/ProjectToolbar.vue'
import ImportModal from '../components/import/ImportModal.vue'
import ImportUndoBanner from '../components/import/ImportUndoBanner.vue'
import AiPlannerModal from '../components/AiPlannerModal.vue'
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
const showAiPlanner = ref(false)
const undoBannerData = ref<{ importSessionId: string; importedCount: number; failedCount: number; duplicateSkippedCount: number; createdAt: string } | null>(null)
const createSourceMode = ref<'members' | 'group'>('members')
const selectedMemberIds = ref<string[]>([])
const selectedGroupId = ref('')
const autoCreateOutsourceMap = ref(false)
const availableGroups = ref<{ id: string; name: string; description: string | null; memberCount: number }[]>([])

async function onPlannerCreated(newProjectId: string) {
  await loadDashboard()
  selectProject(newProjectId)
}


const activeProjectCount = computed(() => activeProjectCards.value.filter((p: any) => p.status === 'Active').length)
const plannedProjectCount = computed(() => activeProjectCards.value.filter((p: any) => p.status === 'Planned').length)
const archivedProjectCount = computed(() => projects.value.filter((p: any) => p.status === 'Archived').length)
const activeUsers = computed<UserDto[]>(() => (users.value ?? []).filter((user: UserDto) => user.isActive))

const featuredProject = computed(() => {
  const ranked = [...activeProjectCards.value].sort((a: any, b: any) => {
    if (b.progressPercentage !== a.progressPercentage) {
      return b.progressPercentage - a.progressPercentage
    }
    if (a.overdueTaskCount !== b.overdueTaskCount) {
      return a.overdueTaskCount - b.overdueTaskCount
    }
    return a.name.localeCompare(b.name)
  })

  return ranked[0] ?? null
})

const heroSubtitle = computed(() => {
  if (featuredProject.value) {
    return `${featuredProject.value.name} đang dẫn đầu bảng theo dõi với tiến độ nổi bật và khả năng điều hướng nhanh hơn.`
  }

  return 'Tạo, lọc, sắp xếp và theo dõi dự án trong một giao diện dashboard sáng hơn, rõ hơn và chuyên nghiệp hơn.'
})

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

    if (autoCreateOutsourceMap.value && project.id) {
      try {
        const now = new Date()
        const end = projectEndDate.value ? new Date(projectEndDate.value) : new Date(now.getTime() + 60 * 24 * 60 * 60 * 1000)
        const totalDays = Math.max(30, Math.ceil((end.getTime() - now.getTime()) / (1000 * 3600 * 24)))
        const stepDays = Math.floor(totalDays / 5)

        const addDays = (d: Date, days: number) => {
          const res = new Date(d)
          res.setDate(res.getDate() + days)
          return res.toISOString()
        }

        const outsourcePhases = [
          { name: 'Mốc 1: Khảo sát & Khởi tạo Yêu cầu (Scope Alignment)', start: now.toISOString(), end: addDays(now, stepDays), goal: 'Thống nhất yêu cầu chi tiết của khách hàng & chốt Scope dự án.' },
          { name: 'Mốc 2: Thiết kế Prototype UI/UX & Architecture', start: addDays(now, stepDays + 1), end: addDays(now, stepDays * 2), goal: 'Chốt Wireframe Figma & Thiết kế Database API.' },
          { name: 'Mốc 3: Phát triển Core Modules & Backend Services', start: addDays(now, stepDays * 2 + 1), end: addDays(now, stepDays * 3), goal: 'Lập trình các tính năng cốt lõi backend.' },
          { name: 'Mốc 4: Tích hợp Giao diện & AI Services', start: addDays(now, stepDays * 3 + 1), end: addDays(now, stepDays * 4), goal: 'Hoàn thiện Frontend & các dịch vụ bên ngoài.' },
          { name: 'Mốc 5: Kiểm thử UAT & Demo Khách hàng', start: addDays(now, stepDays * 4 + 1), end: addDays(now, totalDays - 5), goal: 'UAT với khách hàng & nghiệm thu tính năng.' },
          { name: 'Mốc 6: Bàn giao, Deploy Go-Live & Đào tạo', start: addDays(now, totalDays - 4), end: end.toISOString(), goal: 'Triển khai Server Production & bàn giao hoàn tất.' }
        ]

        for (const phase of outsourcePhases) {
          await apiCommand(`/api/projects/${project.id}/sprints`, {
            method: 'POST',
            body: JSON.stringify({ name: phase.name, startDate: phase.start, endDate: phase.end, goal: phase.goal })
          })
        }
      } catch {
        // ignore sprint creation errors
      }
    }

    createProjectOpen.value = false
    selectedMemberIds.value = []
    projectName.value = ''
    projectDescription.value = ''
    projectEndDate.value = ''
    autoCreateOutsourceMap.value = false
    await loadDashboard()
    selectProject(project.id)
    showSuccess(`Tạo dự án "${project.name}" thành công${autoCreateOutsourceMap.value ? ' kèm Sơ đồ demo Outsource 6 Mốc' : ''}`)
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
  } catch {
    // ignore
  }
}
</script>

<template>
  <div class="dashboard-scroll dashboard-scroll--embedded no-scrollbar projects-page-shell">
    <div class="dashboard-main project-home-main no-scrollbar projects-page-main">
      <section class="projects-hero glass-card">
        <div class="projects-hero__content">
          <div class="projects-hero__eyebrow">
            <Sparkles :size="15" />
            <span>Workspace projects</span>
          </div>

          <h1>Không gian dự án hiện đại, gọn và dễ điều hướng</h1>
          <p>{{ heroSubtitle }}</p>
        </div>

        <div class="projects-hero__sidebar">
          <article class="projects-spotlight">
            <div class="projects-spotlight__header">
              <span>Dự án nổi bật</span>
              <ArrowUpRight :size="16" />
            </div>

            <template v-if="featuredProject">
              <strong>{{ featuredProject.name }}</strong>
              <p>{{ featuredProject.description }}</p>

              <div class="projects-spotlight__metrics">
                <span>
                  <TrendingUp :size="14" />
                  {{ featuredProject.progressPercentage }}% hoàn thành
                </span>
                <span>
                  <Users :size="14" />
                  {{ featuredProject.memberInitials.length }} thành viên
                </span>
                <span v-if="featuredProject.overdueTaskCount > 0" class="projects-spotlight__risk">
                  <AlertTriangle :size="14" />
                  {{ featuredProject.overdueTaskCount }} task trễ hạn
                </span>
              </div>
            </template>

            <template v-else>
              <strong>Chưa có dự án nào</strong>
              <p>Tạo dự án đầu tiên để bắt đầu theo dõi tiến độ, nhiệm vụ và thành viên.</p>
            </template>
          </article>
          <div class="projects-hero__summary projects-hero__summary--compact">
            <span><strong>{{ activeProjectCount }}</strong> đang chạy</span>
            <span><strong>{{ plannedProjectCount }}</strong> lên kế hoạch</span>
            <span><strong>{{ archivedProjectCount }}</strong> lưu trữ</span>
            <span><strong>{{ activeUsers.length }}</strong> thành viên</span>
          </div>
        </div>
      </section>

      <section class="project-workspace glass-card project-workspace--modern">
        <div class="project-workspace__header">
          <div>
            <span>Dự án</span>
            <h2>Danh sách làm việc</h2>
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
            <button class="btn-import" type="button" @click="showImportModal = true">
              <FileUp :size="15" /> Nhập
            </button>
            <button class="btn-ai-plan" type="button" @click="showAiPlanner = true">
              <Sparkles :size="15" /> Lên kế hoạch AI
            </button>
          </template>
        </ProjectToolbar>

        <div class="project-workspace__hint">
          <span><CheckCircle2 :size="14" /> Lưới cho thao tác nhanh, danh sách cho rà soát kỹ hơn.</span>
        </div>

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

    <AiPlannerModal
      v-if="showAiPlanner"
      :project-id="null"
      @close="showAiPlanner = false"
      @created="onPlannerCreated"
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
            <div class="form-group border-t border-line pt-3 mt-3">
              <label class="d-flex align-items-center gap-2 cursor-pointer" style="display: flex; align-items: center; gap: 8px; cursor: pointer;">
                <input v-model="autoCreateOutsourceMap" type="checkbox" />
                <span>🚩 Khởi tạo Sơ đồ Mốc Outsource chuẩn 6 bước (Tùy chọn)</span>
              </label>
              <p v-if="autoCreateOutsourceMap" class="text-xs text-muted mt-1" style="font-size: 12px; color: var(--muted); margin-top: 4px;">
                Tự động sinh 6 mốc tiến độ Outsource (Requirement ➔ UI/UX ➔ Core Backend ➔ Frontend/AI ➔ Client UAT ➔ Go-Live).
              </p>
            </div>

            <div class="project-modal-actions">
              <button class="btn btn--ghost" type="button" @click="createProjectOpen = false">Hủy</button>
              <button class="btn btn--primary" type="submit" :disabled="!projectName.trim()">Tạo dự án</button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>

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
.projects-page-shell {
  position: relative;
  isolation: isolate;
}

.projects-page-shell::before,
.projects-page-shell::after {
  content: '';
  position: fixed;
  pointer-events: none;
  z-index: -1;
  filter: blur(18px);
}

.projects-page-shell::before {
  top: 32px;
  right: 42px;
  width: 180px;
  height: 180px;
  border-radius: 50%;
  background: radial-gradient(circle, rgba(37, 99, 235, 0.025) 0%, rgba(37, 99, 235, 0) 74%);
}

.projects-page-shell::after {
  bottom: 24px;
  left: 44%;
  width: 220px;
  height: 220px;
  border-radius: 50%;
  background: radial-gradient(circle, rgba(16, 185, 129, 0.02) 0%, rgba(16, 185, 129, 0) 76%);
}

.projects-page-main {
  gap: 8px;
}

.projects-hero {
  position: relative;
  display: grid;
  grid-template-columns: minmax(0, 1.48fr) minmax(260px, 0.88fr);
  gap: 12px;
  padding: 16px;
  overflow: hidden;
  border-color: rgba(226, 232, 240, 0.96);
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.995), rgba(249, 251, 255, 0.98));
  box-shadow:
    0 12px 26px rgba(15, 23, 42, 0.04),
    inset 0 1px 0 rgba(255, 255, 255, 0.94);
}

.projects-hero::before {
  content: '';
  position: absolute;
  inset: 0;
  background: linear-gradient(90deg, rgba(239, 246, 255, 0.22), transparent 30%, transparent 70%, rgba(239, 246, 255, 0.16));
  pointer-events: none;
}

.projects-hero__content,
.projects-hero__sidebar {
  position: relative;
  z-index: 1;
}

.projects-hero__content {
  display: grid;
  gap: 10px;
}

.projects-hero__eyebrow {
  width: fit-content;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 7px 12px;
  border: 1px solid rgba(191, 219, 254, 0.66);
  border-radius: 999px;
  background: rgba(248, 250, 252, 0.98);
  color: #1d4ed8;
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

.projects-hero h1 {
  max-width: 640px;
  font-size: clamp(24px, 2.9vw, 36px);
  line-height: 1.02;
  letter-spacing: -0.04em;
  color: #0f172a;
}

.projects-hero p {
  max-width: 610px;
  color: #64748b;
  font-size: 13px;
  line-height: 1.55;
}

.projects-hero__summary {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.projects-hero__summary span {
  display: inline-flex;
  align-items: baseline;
  gap: 8px;
  min-height: 32px;
  padding: 0;
  color: #64748b;
  font-size: 12px;
  font-weight: 700;
}

.projects-hero__summary strong {
  color: #0f172a;
  font-size: 17px;
  line-height: 1;
  font-weight: 900;
}

.projects-hero__summary--compact {
  gap: 10px 14px;
}

.projects-hero__summary--compact span {
  padding: 6px 0;
}

.projects-hero__summary--compact span + span {
  position: relative;
  padding-left: 14px;
}

.projects-hero__summary--compact span + span::before {
  content: '';
  position: absolute;
  left: 0;
  top: 50%;
  width: 1px;
  height: 16px;
  background: rgba(148, 163, 184, 0.24);
  transform: translateY(-50%);
}

.projects-hero__sidebar {
  display: grid;
  gap: 6px;
  align-content: start;
}

.projects-spotlight {
  display: grid;
  gap: 7px;
  padding: 12px;
  border-radius: 16px;
  border: 1px solid rgba(226, 232, 240, 0.95);
  background: linear-gradient(180deg, rgba(255, 255, 255, 1), rgba(250, 252, 255, 0.98));
  box-shadow:
    0 8px 18px rgba(15, 23, 42, 0.035),
    inset 0 1px 0 rgba(255, 255, 255, 0.9);
}

.projects-spotlight__header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  color: #2563eb;
  font-size: 12px;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.08em;
}

.projects-spotlight strong {
  color: var(--text-strong);
  font-size: 15px;
  line-height: 1.12;
}

.projects-spotlight p {
  color: var(--muted);
  line-height: 1.5;
  font-size: 12px;
}

.projects-spotlight__metrics {
  display: grid;
  gap: 3px;
}

.projects-spotlight__metrics span {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: var(--text);
  font-size: 10px;
  font-weight: 700;
}

.projects-spotlight__risk {
  color: #b91c1c !important;
}

.projects-hero__mini-strip {
  display: none;
}

.project-workspace--modern {
  display: grid;
  gap: 10px;
  padding: 14px;
  border-color: rgba(226, 232, 240, 0.92);
  box-shadow:
    0 8px 18px rgba(15, 23, 42, 0.03),
    0 0 0 1px rgba(255, 255, 255, 0.86) inset;
}

.project-workspace__hint {
  display: flex;
  flex-wrap: wrap;
  gap: 5px;
  padding: 0;
}

.project-workspace__hint span {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 0;
  color: #64748b;
  font-size: 12px;
  font-weight: 600;
}

.btn-import {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 7px 12px;
  border: 1px solid rgba(191, 219, 254, 0.82);
  border-radius: 11px;
  background: linear-gradient(135deg, #2563eb, #1e40af);
  color: #ffffff;
  font-size: 0.78rem;
  font-weight: 700;
  cursor: pointer;
  transition: transform 220ms ease, border-color 220ms ease, background 220ms ease, box-shadow 220ms ease, color 220ms ease;
  box-shadow: 0 12px 20px rgba(37, 99, 235, 0.18);
}

.btn-import:hover {
  transform: translateY(-1px);
  border-color: rgba(96, 165, 250, 0.7);
  background: linear-gradient(135deg, #1d4ed8, #1e3a8a);
  box-shadow: 0 18px 32px rgba(37, 99, 235, 0.18);
}

.project-workspace {
  position: relative;
  overflow: hidden;
}

.project-workspace::before {
  content: '';
  position: absolute;
  inset: 0 auto auto 0;
  width: 100%;
  height: 4px;
  background: linear-gradient(90deg, rgba(37, 99, 235, 0.94), rgba(56, 189, 248, 0.9), rgba(99, 102, 241, 0.94));
}

.project-workspace__header {
  padding-top: 0;
}

.project-workspace__header h2 {
  letter-spacing: -0.03em;
}

.project-workspace__header p {
  font-weight: 700;
}

.projects-hero__stat,
.projects-hero__metric,
.projects-hero__mini-strip span,
.projects-spotlight {
  transition: transform 180ms ease, box-shadow 180ms ease, border-color 180ms ease;
}

.projects-hero__metric:hover,
.projects-hero__mini-strip span:hover,
.projects-spotlight:hover {
  transform: translateY(-2px);
}

@media (max-width: 1180px) {
  .projects-hero {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 840px) {
  .projects-hero__summary--compact {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 640px) {
  .projects-page-main {
    padding: 14px;
  }

  .projects-hero,
  .project-workspace--modern {
    padding: 14px;
  }

  .projects-hero__stats,
  .projects-hero__mini-strip {
    width: 100%;
  }

  .projects-hero__mini-strip {
    grid-template-columns: 1fr;
  }
}

.project-modal-backdrop {
  position: fixed;
  inset: 0;
  z-index: 9999;
  background: rgba(15, 23, 42, 0.4);
  backdrop-filter: blur(8px);
  -webkit-backdrop-filter: blur(8px);
  display: flex;
  align-items: center;
  justify-content: center;
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
    0 10px 40px -10px rgba(0, 0, 0, 0.1),
    0 0 0 1px rgba(0, 0, 0, 0.05);
  transform-origin: center;
  animation: modalScaleIn 0.4s cubic-bezier(0.16, 1, 0.3, 1);
}

.project-modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 24px 28px 20px;
  border-bottom: 1px solid var(--border-color);
}

.project-modal-title {
  display: flex;
  align-items: center;
  gap: 12px;
  color: var(--text-main);
}

.project-modal-title h2 {
  font-size: 1.25rem;
  font-weight: 700;
  margin: 0;
  letter-spacing: -0.01em;
}

.icon-button {
  background: transparent;
  border: none;
  color: var(--text-muted);
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  justify-content: center;
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

.modal-input::placeholder {
  color: #94a3b8;
}

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
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  padding-top: 12px;
}

.btn {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 10px 24px;
  border-radius: 10px;
  font-size: 0.95rem;
  font-weight: 600;
  border: none;
  cursor: pointer;
  transition: all 0.2s;
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
  opacity: 0.6;
  cursor: not-allowed;
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
