<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import {
  AlertTriangle,
  ArrowUpRight,
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
import PageStatePanel from '../components/PageStatePanel.vue'
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
const undoBannerData = ref<{
  importSessionId: string
  importedCount: number
  failedCount: number
  duplicateSkippedCount: number
  createdAt: string
} | null>(null)
const createSourceMode = ref<'members' | 'group'>('members')
const selectedMemberIds = ref<string[]>([])
const selectedGroupId = ref('')
const autoCreateOutsourceMap = ref(false)
const availableGroups = ref<
  { id: string; name: string; description: string | null; memberCount: number }[]
>([])

async function onPlannerCreated(newProjectId: string) {
  await loadDashboard()
  selectProject(newProjectId)
}

const activeProjectCount = computed(
  () => activeProjectCards.value.filter((p: any) => p.status === 'Active').length,
)
const plannedProjectCount = computed(
  () => activeProjectCards.value.filter((p: any) => p.status === 'Planned').length,
)
const archivedProjectCount = computed(
  () => projects.value.filter((p: any) => p.status === 'Archived').length,
)
const activeUsers = computed<UserDto[]>(() =>
  (users.value ?? []).filter((user: UserDto) => user.isActive),
)

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
    return `${featuredProject.value.name} đang dẫn đầu theo tiến độ và đã sẵn sàng để bạn drill-down theo nhiệm vụ, thành viên và rủi ro.`
  }

  return 'Tạo, lọc, sắp xếp và theo dõi dự án trong một giao diện rõ ràng hơn, sang hơn và có cấu trúc tốt hơn.'
})

onMounted(() => {
  void loadGroups()
})

async function loadGroups() {
  try {
    const result = await apiResult<
      PagedResult<{
        id: string
        name: string
        description: string | null
        memberCount: number
      }>
    >('/api/groups?pageSize=100')
    availableGroups.value = result.items
  } catch {
    availableGroups.value = []
  }
}

async function createProjectWithSelection() {
  const name = projectName.value.trim()
  if (!name) return

  try {
    const createdOutsourceMap = autoCreateOutsourceMap.value

    if (createSourceMode.value === 'group' && selectedGroupId.value) {
      const result = await apiResult<any>(
        `/api/groups/${selectedGroupId.value}/create-project`,
        {
          method: 'POST',
          body: JSON.stringify({
            name,
            code: null,
            description: projectDescription.value.trim() || null,
            startDate: null,
            endDate: projectEndDate.value
              ? new Date(projectEndDate.value).toISOString()
              : null,
          }),
        },
      )

      createProjectOpen.value = false
      selectedGroupId.value = ''
      selectedMemberIds.value = []
      projectName.value = ''
      projectDescription.value = ''
      projectEndDate.value = ''
      await loadDashboard()

      const projectId = result.project?.id ?? result.project?.Id
      if (projectId) selectProject(projectId)

      showSuccess(
        `Tạo project từ nhóm thành công (${result.membersAdded ?? 0} thành viên)`,
      )
      return
    }

    const project = await apiResult<ProjectDto>('/api/projects', {
      method: 'POST',
      body: JSON.stringify({
        name,
        description: projectDescription.value.trim() || null,
        startDate: null,
        endDate: projectEndDate.value
          ? new Date(projectEndDate.value).toISOString()
          : null,
      }),
    })

    for (const userId of selectedMemberIds.value) {
      await apiCommand(`/api/projects/${project.id}/members`, {
        method: 'POST',
        body: JSON.stringify({ userId, role: 'Member' }),
      })
    }

    if (createdOutsourceMap && project.id) {
      try {
        const now = new Date()
        const end = projectEndDate.value
          ? new Date(projectEndDate.value)
          : new Date(now.getTime() + 60 * 24 * 60 * 60 * 1000)
        const totalDays = Math.max(
          30,
          Math.ceil((end.getTime() - now.getTime()) / (1000 * 3600 * 24)),
        )
        const stepDays = Math.floor(totalDays / 5)

        const addDays = (d: Date, days: number) => {
          const res = new Date(d)
          res.setDate(res.getDate() + days)
          return res.toISOString()
        }

        const outsourcePhases = [
          {
            name: 'Mốc 1: Khảo sát & Khởi tạo yêu cầu',
            start: now.toISOString(),
            end: addDays(now, stepDays),
            goal: 'Thống nhất yêu cầu chi tiết của khách hàng và chốt phạm vi dự án.',
          },
          {
            name: 'Mốc 2: Thiết kế Prototype UI/UX & Architecture',
            start: addDays(now, stepDays + 1),
            end: addDays(now, stepDays * 2),
            goal: 'Chốt wireframe Figma và thiết kế database API.',
          },
          {
            name: 'Mốc 3: Phát triển Core Modules & Backend Services',
            start: addDays(now, stepDays * 2 + 1),
            end: addDays(now, stepDays * 3),
            goal: 'Lập trình các tính năng cốt lõi phía backend.',
          },
          {
            name: 'Mốc 4: Tích hợp Giao diện & AI Services',
            start: addDays(now, stepDays * 3 + 1),
            end: addDays(now, stepDays * 4),
            goal: 'Hoàn thiện frontend và các dịch vụ bên ngoài.',
          },
          {
            name: 'Mốc 5: Kiểm thử UAT & Demo Khách hàng',
            start: addDays(now, stepDays * 4 + 1),
            end: addDays(now, totalDays - 5),
            goal: 'UAT với khách hàng và nghiệm thu tính năng.',
          },
          {
            name: 'Mốc 6: Bàn giao, Deploy Go-Live & Đào tạo',
            start: addDays(now, totalDays - 4),
            end: end.toISOString(),
            goal: 'Triển khai production và bàn giao hoàn tất.',
          },
        ]

        for (const phase of outsourcePhases) {
          await apiCommand(`/api/projects/${project.id}/sprints`, {
            method: 'POST',
            body: JSON.stringify({
              name: phase.name,
              startDate: phase.start,
              endDate: phase.end,
              goal: phase.goal,
            }),
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
    showSuccess(
      `Tạo dự án "${project.name}" thành công${
        createdOutsourceMap ? ' kèm sơ đồ mốc Outsource 6 bước' : ''
      }`,
    )
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
    const res = await fetch(
      `/api/import/sessions/${undoBannerData.value.importSessionId}`,
      { method: 'DELETE' },
    )
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
  <div class="projects-page-shell">
    <div class="projects-page-shell__glow projects-page-shell__glow--one" />
    <div class="projects-page-shell__glow projects-page-shell__glow--two" />

    <div class="dashboard-scroll dashboard-scroll--embedded no-scrollbar projects-page-shell__content">
      <div class="dashboard-main project-home-main no-scrollbar projects-page-main">
        <section class="projects-overview glass-card projects-overview--enterprise">
          <div class="projects-overview__header">
            <div class="projects-overview__copy">
              <div class="projects-overview__eyebrow">
                <FolderKanban :size="15" />
                <span>Vận hành / Dự án</span>
              </div>

              <div class="projects-overview__title">
                <h1>Bảng điều khiển dự án</h1>
                <p>{{ projects.length }} dự án · {{ activeProjectCount }} đang chạy · {{ activeUsers.length }} thành viên hoạt động</p>
              </div>
            </div>

            <button class="btn-hero btn-hero--primary projects-overview__cta" type="button" @click="openCreateProject">
              <FolderKanban :size="16" />
              Tạo dự án mới
            </button>
          </div>

          <div class="projects-overview__body">
            <div class="projects-overview__stats">
              <article class="projects-overview__stat-card">
                <div class="projects-overview__stat-icon">
                  <FolderKanban :size="14" />
                </div>
                <strong>{{ projects.length }}</strong>
                <span>Tổng dự án</span>
              </article>
              <article class="projects-overview__stat-card">
                <div class="projects-overview__stat-icon">
                  <TrendingUp :size="14" />
                </div>
                <strong>{{ activeProjectCount }}</strong>
                <span>Đang chạy</span>
              </article>
              <article class="projects-overview__stat-card">
                <div class="projects-overview__stat-icon">
                  <Sparkles :size="14" />
                </div>
                <strong>{{ plannedProjectCount }}</strong>
                <span>Lên kế hoạch</span>
              </article>
              <article class="projects-overview__stat-card">
                <div class="projects-overview__stat-icon">
                  <CheckCircle2 :size="14" />
                </div>
                <strong>{{ archivedProjectCount }}</strong>
                <span>Lưu trữ</span>
              </article>
              <article class="projects-overview__stat-card">
                <div class="projects-overview__stat-icon">
                  <Users :size="14" />
                </div>
                <strong>{{ activeUsers.length }}</strong>
                <span>Thành viên</span>
              </article>
            </div>

            <article class="projects-overview__feature">
              <div class="projects-overview__feature-header">
                <span>Dự án nổi bật</span>
                <ArrowUpRight :size="16" />
              </div>

              <template v-if="featuredProject">
                <strong>{{ featuredProject.name }}</strong>
                <p>{{ featuredProject.progressPercentage }}% hoàn thành · {{ featuredProject.memberInitials.length }} thành viên · {{ featuredProject.completedTaskCount }}/{{ featuredProject.taskCount }} nhiệm vụ</p>

                <div class="projects-overview__feature-meta">
                  <span>
                    <TrendingUp :size="14" />
                    Sprint đang chạy
                  </span>
                  <span v-if="featuredProject.overdueTaskCount > 0" class="projects-overview__feature-risk">
                    <AlertTriangle :size="14" />
                    {{ featuredProject.overdueTaskCount }} task quá hạn
                  </span>
                </div>
              </template>

              <template v-else>
                <strong>Chưa có dự án nổi bật</strong>
                <p>Hệ thống sẽ tự gắn dự án có tiến độ tốt nhất ngay khi có dữ liệu.</p>
              </template>
            </article>
          </div>
        </section>

        <section class="projects-hero glass-card projects-hero--spotlight">
          <div class="projects-hero__left">
            <div class="projects-hero__eyebrow">
              <Sparkles :size="15" />
              <span>Workspace projects</span>
            </div>

            <div class="projects-hero__title">
              <h1>Không gian dự án rõ ràng hơn, nổi bật hơn</h1>
              <p>{{ heroSubtitle }}</p>
            </div>

            <div class="projects-hero__actions">
              <button class="btn-hero btn-hero--primary" type="button" @click="openCreateProject">
                <FolderKanban :size="16" />
                Tạo dự án mới
              </button>
              <button class="btn-hero" type="button" @click="showAiPlanner = true">
                <Sparkles :size="16" />
                Lên kế hoạch AI
              </button>
              <button class="btn-hero" type="button" @click="showImportModal = true">
                <FileUp :size="16" />
                Nhập dữ liệu
              </button>
            </div>
          </div>

          <div class="projects-hero__center" aria-hidden="true">
            <div class="project-orbit">
              <div class="project-orbit__ring project-orbit__ring--outer" />
              <div class="project-orbit__ring project-orbit__ring--inner" />
              <div class="project-orbit__shine" />

              <div class="project-orbit__core">
                <div class="project-orbit__core-icon">
                  <FolderKanban :size="36" stroke-width="2.2" />
                </div>
                <div class="project-orbit__core-caption">
                  <strong>{{ featuredProject?.name ?? 'Project Hub' }}</strong>
                </div>
              </div>

              <div class="project-orbit__chip project-orbit__chip--top">
                <CheckCircle2 :size="14" />
                <span>
                  {{ featuredProject ? `${featuredProject.progressPercentage}% sẵn sàng` : 'Sẵn sàng để khởi tạo' }}
                </span>
              </div>
              <div class="project-orbit__chip project-orbit__chip--bottom-left">
                <Users :size="14" />
                <span>{{ activeUsers.length }} thành viên</span>
              </div>
              <div class="project-orbit__chip project-orbit__chip--bottom-right">
                <TrendingUp :size="14" />
                <span>{{ featuredProject ? 'Sprint đang chạy' : 'Roadmap sắp mở' }}</span>
              </div>
            </div>
          </div>

          <aside class="projects-hero__right">
            <article class="projects-spotlight projects-spotlight--project">
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
                  <span>
                    <CheckCircle2 :size="14" />
                    {{ featuredProject.completedTaskCount }}/{{ featuredProject.taskCount }} nhiệm vụ
                  </span>
                  <span
                    v-if="featuredProject.overdueTaskCount > 0"
                    class="projects-spotlight__risk"
                  >
                    <AlertTriangle :size="14" />
                    {{ featuredProject.overdueTaskCount }} task quá hạn
                  </span>
                </div>
              </template>

              <template v-else>
                <strong>Chưa có dự án nào</strong>
                <p>
                  Tạo dự án đầu tiên để bắt đầu theo dõi tiến độ, nhiệm vụ và thành viên.
                </p>
              </template>
            </article>

            <div class="projects-hero__summary">
              <article class="projects-hero__summary-card">
                <strong>{{ activeProjectCount }}</strong>
                <span>Đang chạy</span>
              </article>
              <article class="projects-hero__summary-card">
                <strong>{{ plannedProjectCount }}</strong>
                <span>Lên kế hoạch</span>
              </article>
              <article class="projects-hero__summary-card">
                <strong>{{ archivedProjectCount }}</strong>
                <span>Lưu trữ</span>
              </article>
              <article class="projects-hero__summary-card">
                <strong>{{ activeUsers.length }}</strong>
                <span>Thành viên</span>
              </article>
            </div>
          </aside>
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
              <button class="btn-toolbar" type="button" @click="showImportModal = true">
                <FileUp :size="15" />
                Nhập
              </button>
              <button class="btn-toolbar btn-toolbar--accent" type="button" @click="showAiPlanner = true">
                <Sparkles :size="15" />
                Lên kế hoạch AI
              </button>
            </template>
          </ProjectToolbar>

          <div class="project-workspace__hint">
            <span><CheckCircle2 :size="14" /> Lưới cho quét nhanh, danh sách cho rà soát kỹ hơn.</span>
          </div>

          <PageStatePanel
            v-if="activeProjectCards.length === 0"
            variant="empty"
            title="Chưa có dự án đang hoạt động"
            message="Hãy tạo dự án đầu tiên hoặc nhập dữ liệu để bắt đầu theo dõi tiến độ, nhiệm vụ và thành viên."
            :skeleton-rows="3"
          >
            <template #actions>
              <button class="btn-hero btn-hero--primary" type="button" @click="openCreateProject">
                <FolderKanban :size="16" />
                Tạo dự án mới
              </button>
            </template>
          </PageStatePanel>

          <template v-else>
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
          </template>
        </section>
      </div>
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
      <div
        v-if="createProjectOpen"
        class="project-modal-backdrop"
        @click.self="createProjectOpen = false"
      >
        <div class="project-modal glass-card">
          <div class="project-modal-header">
            <div class="project-modal-title">
              <FolderKanban :size="20" />
              <h2>Tạo dự án mới</h2>
            </div>
            <button class="icon-button" @click="createProjectOpen = false">
              <X :size="18" />
            </button>
          </div>

          <form class="project-modal-body" @submit.prevent="createProjectWithSelection">
            <div class="form-group">
              <label>Tên dự án</label>
              <input
                v-model="projectName"
                type="text"
                placeholder="Nhập tên dự án..."
                required
                class="modal-input"
              />
            </div>

            <div class="form-group">
              <label>Mô tả ngắn</label>
                <textarea
                  v-model="projectDescription"
                  placeholder="Nhập mô tả dự án (không bắt buộc)..."
                  rows="3"
                  class="modal-input"
                ></textarea>
            </div>

            <div class="form-group">
              <label>Ngày kết thúc dự kiến</label>
              <input v-model="projectEndDate" type="date" class="modal-input" />
            </div>

            <div class="form-group">
              <label>Thêm thành viên</label>
              <div class="project-source-toggle">
                <button
                  type="button"
                  :class="{ active: createSourceMode === 'members' }"
                  @click="createSourceMode = 'members'"
                >
                  Chọn từng người
                </button>
                <button
                  type="button"
                  :class="{ active: createSourceMode === 'group' }"
                  @click="createSourceMode = 'group'"
                >
                  Chọn nhóm
                </button>
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

            <div class="form-group project-modal__option">
              <label class="project-modal__checkbox">
                <input v-model="autoCreateOutsourceMap" type="checkbox" />
                <span>Khởi tạo sơ đồ mốc Outsource 6 bước</span>
              </label>
              <p v-if="autoCreateOutsourceMap" class="project-modal__help">
                Tự động sinh 6 mốc tiến độ Outsource từ khảo sát đến go-live.
              </p>
            </div>

            <div class="project-modal-actions">
              <button class="btn btn--ghost" type="button" @click="createProjectOpen = false">
                Hủy
              </button>
              <button class="btn btn--primary" type="submit" :disabled="!projectName.trim()">
                Tạo dự án
              </button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>

    <Teleport to="body">
      <div
        v-if="projectBeingEditedId"
        class="project-modal-backdrop"
        @click.self="projectBeingEditedId = null"
      >
        <div class="project-modal glass-card">
          <div class="project-modal-header">
            <div class="project-modal-title">
              <Edit3 :size="20" />
              <h2>Chỉnh sửa dự án</h2>
            </div>
            <button class="icon-button" @click="projectBeingEditedId = null">
              <X :size="18" />
            </button>
          </div>

          <form class="project-modal-body" @submit.prevent="saveProjectEdit">
            <div class="form-group">
              <label>Tên dự án</label>
              <input
                v-model="editProjectName"
                type="text"
                placeholder="Nhập tên dự án..."
                required
                class="modal-input"
              />
            </div>

            <div class="form-group">
              <label>Mô tả ngắn</label>
                <textarea
                  v-model="editProjectDescription"
                  placeholder="Nhập mô tả dự án (không bắt buộc)..."
                  rows="3"
                  class="modal-input"
                ></textarea>
            </div>

            <div class="project-modal-actions">
              <button class="btn btn--ghost" type="button" @click="projectBeingEditedId = null">
                Hủy
              </button>
              <button class="btn btn--primary" type="submit" :disabled="!editProjectName.trim()">
                Lưu thay đổi
              </button>
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

.projects-page-shell__content {
  position: relative;
  z-index: 1;
}

.projects-page-shell__glow {
  position: fixed;
  z-index: 0;
  border-radius: 999px;
  pointer-events: none;
  filter: blur(24px);
}

.projects-page-shell__glow--one {
  top: 32px;
  right: 42px;
  width: 180px;
  height: 180px;
  background: radial-gradient(circle, rgba(37, 99, 235, 0.07) 0%, rgba(37, 99, 235, 0) 72%);
}

.projects-page-shell__glow--two {
  bottom: 24px;
  left: 38%;
  width: 240px;
  height: 240px;
  background: radial-gradient(circle, rgba(16, 185, 129, 0.05) 0%, rgba(16, 185, 129, 0) 76%);
}

.projects-page-main {
  gap: 12px;
  padding: 14px 16px 16px;
}

.projects-hero {
  display: grid;
  grid-template-columns: minmax(0, 1.45fr) minmax(290px, 0.85fr);
  gap: 14px;
  padding: 18px;
  overflow: hidden;
  border-color: rgba(226, 232, 240, 0.96);
  background:
    radial-gradient(circle at top left, rgba(37, 99, 235, 0.06), transparent 36%),
    linear-gradient(180deg, rgba(255, 255, 255, 0.995), rgba(249, 251, 255, 0.98));
  box-shadow:
    0 12px 28px rgba(15, 23, 42, 0.05),
    inset 0 1px 0 rgba(255, 255, 255, 0.92);
}

.projects-hero__content {
  display: grid;
  align-content: start;
  gap: 14px;
}

.projects-hero__eyebrow {
  width: fit-content;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 7px 12px;
  border: 1px solid rgba(191, 219, 254, 0.68);
  border-radius: 999px;
  background: rgba(248, 250, 252, 0.98);
  color: #1d4ed8;
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

.projects-hero__title {
  display: grid;
  gap: 10px;
}

.projects-hero h1 {
  max-width: 680px;
  font-size: clamp(28px, 3.2vw, 42px);
  line-height: 1.02;
  letter-spacing: -0.045em;
  color: #0f172a;
}

.projects-hero p {
  max-width: 640px;
  color: #64748b;
  font-size: 14px;
  line-height: 1.6;
}

.projects-hero__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.btn-hero,
.btn-toolbar {
  min-height: 40px;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  border: 1px solid rgba(191, 219, 254, 0.82);
  border-radius: 12px;
  padding: 0 14px;
  color: #1d4ed8;
  background: #eff6ff;
  font-size: 13px;
  font-weight: 800;
  transition:
    transform 180ms ease,
    border-color 180ms ease,
    background 180ms ease,
    box-shadow 180ms ease;
}

.btn-hero:hover,
.btn-toolbar:hover {
  transform: translateY(-1px);
  border-color: rgba(96, 165, 250, 0.72);
  background: #dbeafe;
}

.btn-hero--primary,
.btn-toolbar--accent {
  color: #ffffff;
  border-color: rgba(37, 99, 235, 0.82);
  background: linear-gradient(135deg, #2563eb, #1e40af);
  box-shadow: 0 12px 20px rgba(37, 99, 235, 0.18);
}

.btn-hero--primary:hover,
.btn-toolbar--accent:hover {
  background: linear-gradient(135deg, #1d4ed8, #1e3a8a);
}

.projects-hero__sidebar {
  display: grid;
  align-content: start;
  gap: 10px;
}

.projects-spotlight {
  display: grid;
  gap: 8px;
  padding: 14px;
  border-radius: 18px;
  border: 1px solid rgba(226, 232, 240, 0.95);
  background: linear-gradient(180deg, rgba(255, 255, 255, 1), rgba(250, 252, 255, 0.98));
  box-shadow:
    0 8px 18px rgba(15, 23, 42, 0.04),
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
  font-size: 16px;
  line-height: 1.15;
}

.projects-spotlight p {
  color: var(--muted);
  line-height: 1.55;
  font-size: 12px;
}

.projects-spotlight__metrics {
  display: grid;
  gap: 4px;
}

.projects-spotlight__metrics span {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: var(--text);
  font-size: 11px;
  font-weight: 700;
}

.projects-spotlight__risk {
  color: #b91c1c !important;
}

.projects-hero__summary {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
}

.projects-hero__summary span {
  display: grid;
  gap: 6px;
  padding: 12px;
  border: 1px solid rgba(226, 232, 240, 0.95);
  border-radius: 16px;
  background: rgba(255, 255, 255, 0.88);
  color: #64748b;
  font-size: 12px;
  font-weight: 700;
}

.projects-hero__summary strong {
  color: #0f172a;
  font-size: 20px;
  line-height: 1;
  font-weight: 900;
}

.project-workspace--modern {
  display: grid;
  gap: 12px;
  padding: 14px;
  border-color: rgba(226, 232, 240, 0.92);
  box-shadow:
    0 8px 18px rgba(15, 23, 42, 0.03),
    0 0 0 1px rgba(255, 255, 255, 0.86) inset;
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
  display: flex;
  justify-content: space-between;
  align-items: end;
  gap: 12px;
  padding-top: 0;
}

.project-workspace__header span {
  display: inline-block;
  margin-bottom: 6px;
  color: #2563eb;
  font-size: 11px;
  font-weight: 900;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.project-workspace__header h2 {
  color: #0f172a;
  letter-spacing: -0.03em;
}

.project-workspace__header p {
  color: #64748b;
  font-weight: 700;
}

.project-workspace__hint {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.project-workspace__hint span {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: #64748b;
  font-size: 12px;
  font-weight: 600;
}

.project-modal-backdrop {
  position: fixed;
  inset: 0;
  z-index: 9999;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 18px;
  background: rgba(15, 23, 42, 0.42);
  backdrop-filter: blur(10px);
  -webkit-backdrop-filter: blur(10px);
}

.project-modal {
  width: min(480px, 94vw);
  max-height: 88vh;
  overflow-y: auto;
  border-radius: 20px;
  padding: 0;
  background: #ffffff;
  box-shadow:
    0 18px 50px rgba(15, 23, 42, 0.18),
    0 0 0 1px rgba(15, 23, 42, 0.04);
}

.project-modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 22px 24px 18px;
  border-bottom: 1px solid rgba(226, 232, 240, 0.95);
}

.project-modal-title {
  display: flex;
  align-items: center;
  gap: 12px;
  color: #0f172a;
}

.project-modal-title h2 {
  font-size: 1.15rem;
  font-weight: 800;
  letter-spacing: -0.02em;
}

.icon-button {
  display: grid;
  place-items: center;
  width: 38px;
  height: 38px;
  border: 1px solid rgba(226, 232, 240, 0.95);
  border-radius: 999px;
  color: #64748b;
  background: #f8fafc;
  transition: transform 160ms ease, background 160ms ease, color 160ms ease;
}

.icon-button:hover {
  transform: translateY(-1px);
  color: #0f172a;
  background: #eef2ff;
}

.project-modal-body {
  padding: 22px 24px 24px;
}

.form-group {
  margin-bottom: 20px;
}

.form-group label {
  display: block;
  margin-bottom: 8px;
  color: #0f172a;
  font-size: 0.875rem;
  font-weight: 700;
}

.modal-input {
  width: 100%;
  padding: 12px 14px;
  border: 1px solid rgba(203, 213, 225, 0.95);
  border-radius: 12px;
  color: #0f172a;
  background: #f8fafc;
  outline: none;
  transition: border-color 160ms ease, box-shadow 160ms ease, background 160ms ease;
}

.modal-input:hover {
  background: #ffffff;
  border-color: rgba(148, 163, 184, 0.85);
}

.modal-input:focus {
  background: #ffffff;
  border-color: rgba(37, 99, 235, 0.9);
  box-shadow: 0 0 0 4px rgba(37, 99, 235, 0.12);
}

textarea.modal-input {
  resize: vertical;
  min-height: 90px;
  line-height: 1.55;
}

.project-source-toggle {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
}

.project-source-toggle button {
  min-height: 40px;
  border: 1px solid rgba(203, 213, 225, 0.95);
  border-radius: 12px;
  color: #64748b;
  background: #f8fafc;
  font-weight: 700;
}

.project-source-toggle button.active {
  border-color: rgba(37, 99, 235, 0.72);
  color: #1d4ed8;
  background: #eff6ff;
}

.project-member-picker {
  display: grid;
  gap: 8px;
  max-height: 200px;
  overflow: auto;
  padding: 8px;
  border: 1px solid rgba(203, 213, 225, 0.95);
  border-radius: 14px;
  background: #f8fafc;
}

.project-member-picker label {
  display: flex;
  align-items: center;
  gap: 10px;
  margin: 0;
  padding: 9px 10px;
  border-radius: 10px;
  background: #ffffff;
  color: #0f172a;
}

.project-member-picker span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.project-modal__option {
  margin-top: 8px;
}

.project-modal__checkbox {
  display: flex;
  align-items: center;
  gap: 10px;
  margin: 0;
  cursor: pointer;
}

.project-modal__help {
  margin-top: 8px;
  color: #64748b;
  font-size: 12px;
  line-height: 1.5;
}

.project-modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  padding-top: 4px;
}

.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  min-height: 42px;
  padding: 0 16px;
  border-radius: 12px;
  font-size: 0.95rem;
  font-weight: 700;
  transition: transform 160ms ease, background 160ms ease, border-color 160ms ease, box-shadow 160ms ease;
}

.btn--primary {
  color: #ffffff;
  border: 1px solid rgba(37, 99, 235, 0.82);
  background: linear-gradient(135deg, #2563eb, #1e40af);
  box-shadow: 0 12px 20px rgba(37, 99, 235, 0.18);
}

.btn--primary:hover:not(:disabled) {
  transform: translateY(-1px);
  background: linear-gradient(135deg, #1d4ed8, #1e3a8a);
}

.btn--primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  box-shadow: none;
}

.btn--ghost {
  color: #475569;
  border: 1px solid rgba(203, 213, 225, 0.95);
  background: #ffffff;
}

.btn--ghost:hover {
  transform: translateY(-1px);
  color: #0f172a;
  background: #f8fafc;
}

@media (max-width: 1180px) {
  .projects-hero {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 840px) {
  .projects-hero__summary {
    grid-template-columns: 1fr;
  }

  .project-workspace__header {
    align-items: start;
    flex-direction: column;
  }
}

@media (max-width: 640px) {
  .projects-page-main {
    padding: 12px;
  }

  .projects-hero,
  .project-workspace--modern {
    padding: 14px;
  }

  .projects-hero__actions {
    flex-direction: column;
  }

  .btn-hero,
  .btn-toolbar {
    width: 100%;
    justify-content: center;
  }

  .project-modal-body,
  .project-modal-header {
    padding-left: 16px;
    padding-right: 16px;
  }
}
</style>

<style scoped>
.projects-page-shell {
  background:
    radial-gradient(circle at 12% 0%, rgba(31, 128, 255, 0.06), transparent 22%),
    radial-gradient(circle at 86% 4%, rgba(16, 185, 129, 0.035), transparent 18%),
    linear-gradient(180deg, #f8fbff 0%, #ffffff 54%, #f8fafc 100%) !important;
}

.projects-hero {
  border-color: rgba(191, 219, 254, 0.7) !important;
  background:
    radial-gradient(circle at 84% 12%, rgba(31, 128, 255, 0.06), transparent 28%),
    linear-gradient(180deg, rgba(255, 255, 255, 0.99), rgba(246, 249, 255, 0.97)) !important;
  box-shadow: 0 16px 36px rgba(15, 23, 42, 0.05) !important;
}

.projects-hero::before {
  background: linear-gradient(90deg, #2563eb, #38bdf8, #16a34a) !important;
}

.projects-hero__eyebrow {
  color: var(--primary) !important;
  border-color: rgba(191, 219, 254, 0.72) !important;
  background: rgba(239, 246, 255, 0.86) !important;
}

.projects-hero__title h1,
.projects-spotlight strong,
.project-workspace__header h2,
.project-modal-title h2 {
  color: var(--text-strong) !important;
}

.projects-hero__title p,
.projects-spotlight p,
.project-workspace__header p,
.projects-hero__summary span,
.project-workspace__hint {
  color: var(--muted) !important;
}

.btn-hero,
.btn-toolbar {
  color: #475569 !important;
  border-color: rgba(193, 211, 232, 0.9) !important;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(243, 248, 255, 0.98)) !important;
  box-shadow: 0 8px 18px rgba(15, 23, 42, 0.06) !important;
}

.btn-hero:hover,
.btn-toolbar:hover {
  color: #1d4ed8 !important;
  border-color: rgba(96, 165, 250, 0.72) !important;
  background: #dbeafe !important;
}

.btn-hero--primary {
  color: #ffffff !important;
  border-color: rgba(37, 99, 235, 0.82) !important;
  background: linear-gradient(135deg, #2563eb, #1d4ed8) !important;
  box-shadow: 0 12px 20px rgba(37, 99, 235, 0.18) !important;
}

.btn-hero--primary:hover {
  color: #ffffff !important;
  background: linear-gradient(135deg, #1d4ed8, #1e40af) !important;
}

.projects-spotlight,
.project-workspace--modern,
.project-modal {
  border-color: rgba(223, 231, 242, 0.86) !important;
  background:
    radial-gradient(circle at top right, rgba(31, 128, 255, 0.05), transparent 28%),
    linear-gradient(180deg, rgba(255, 255, 255, 0.99), rgba(247, 250, 255, 0.95)) !important;
  box-shadow: 0 12px 28px rgba(15, 23, 42, 0.05) !important;
}

.projects-spotlight::before {
  background: linear-gradient(90deg, #2563eb, #38bdf8) !important;
}

.projects-spotlight__header,
.project-workspace__header span,
.project-option-card__meta {
  color: var(--primary) !important;
}

.projects-hero__summary span,
.project-workspace__hint,
.project-source-toggle button,
.project-option-card,
.create-source-grid__card {
  border-color: rgba(223, 231, 242, 0.9) !important;
  background: rgba(255, 255, 255, 0.98) !important;
}

.project-source-toggle button.is-active {
  color: #1d4ed8 !important;
  border-color: rgba(37, 99, 235, 0.34) !important;
  background: #eff6ff !important;
}

.project-option-card.is-selected,
.create-source-grid__card.is-selected {
  border-color: rgba(37, 99, 235, 0.34) !important;
  box-shadow: 0 14px 26px rgba(37, 99, 235, 0.1) !important;
}

.project-option-card:hover,
.create-source-grid__card:hover {
  border-color: rgba(96, 165, 250, 0.82) !important;
  box-shadow: 0 12px 24px rgba(15, 23, 42, 0.07) !important;
}

.modal-input:focus {
  border-color: rgba(59, 130, 246, 0.85) !important;
  box-shadow: 0 0 0 4px rgba(59, 130, 246, 0.12) !important;
}

.icon-button:hover {
  color: #1d4ed8 !important;
  border-color: rgba(191, 219, 254, 0.95) !important;
  background: #eff6ff !important;
}

.form-primary {
  color: #ffffff !important;
  border: 0 !important;
  background: linear-gradient(135deg, #2563eb, #1d4ed8) !important;
  box-shadow: 0 12px 22px rgba(37, 99, 235, 0.18) !important;
}

.form-primary:hover {
  color: #ffffff !important;
  background: linear-gradient(135deg, #1d4ed8, #1e40af) !important;
  box-shadow: 0 16px 28px rgba(37, 99, 235, 0.22) !important;
}

.project-modal-backdrop {
  background: rgba(15, 23, 42, 0.38) !important;
  backdrop-filter: blur(10px) !important;
}

@media (max-width: 1180px) {
  .projects-hero {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 720px) {
  .projects-page-shell__content {
    padding: 14px;
  }

  .projects-hero,
  .project-workspace--modern {
    padding: 18px;
    border-radius: 20px;
  }

  .projects-hero__summary {
    grid-template-columns: 1fr;
  }

  .projects-hero__actions {
    flex-direction: column;
    align-items: stretch;
  }

  .btn-hero,
  .btn-toolbar {
    width: 100%;
  }
}
</style>

<style scoped>
.projects-page-shell {
  background:
    radial-gradient(circle at 10% 0%, rgba(31, 128, 255, 0.08), transparent 24%),
    radial-gradient(circle at 88% 8%, rgba(45, 212, 191, 0.06), transparent 22%),
    linear-gradient(180deg, #f8fbff 0%, #ffffff 52%, #f5f7fb 100%);
}

.projects-page-shell__content {
  padding: 28px;
}

.projects-page-shell__glow {
  filter: blur(22px);
  opacity: 0.4;
}

.projects-page-shell__glow--one {
  top: -72px;
  left: -48px;
  width: 260px;
  height: 260px;
  background: radial-gradient(circle, rgba(59, 130, 246, 0.18), transparent 68%);
}

.projects-page-shell__glow--two {
  top: 76px;
  right: 12px;
  width: 220px;
  height: 220px;
  background: radial-gradient(circle, rgba(16, 185, 129, 0.12), transparent 70%);
}

.projects-page-main {
  gap: 20px;
}

.projects-hero--spotlight {
  position: relative;
  overflow: hidden;
  display: grid;
  grid-template-columns: minmax(0, 1.1fr) minmax(260px, 0.72fr) minmax(320px, 0.96fr);
  align-items: center;
  gap: 18px;
  padding: 24px;
  border: 1px solid rgba(191, 219, 254, 0.8);
  border-radius: 30px;
  background:
    radial-gradient(circle at 10% 18%, rgba(59, 130, 246, 0.08), transparent 22%),
    radial-gradient(circle at 84% 20%, rgba(45, 212, 191, 0.06), transparent 18%),
    linear-gradient(180deg, rgba(255, 255, 255, 0.99), rgba(247, 250, 255, 0.96));
  box-shadow:
    0 26px 64px rgba(15, 23, 42, 0.08),
    inset 0 1px 0 rgba(255, 255, 255, 0.95);
}

.projects-hero--spotlight::before {
  content: '';
  position: absolute;
  inset: 0 auto auto 0;
  width: 100%;
  height: 4px;
  background: linear-gradient(90deg, #2563eb, #38bdf8, #22c55e);
}

.projects-hero__left,
.projects-hero__center,
.projects-hero__right {
  position: relative;
  z-index: 1;
}

.projects-hero__left {
  display: grid;
  gap: 20px;
  align-self: start;
}

.projects-hero__title h1 {
  max-width: 11ch;
  color: #0f172a;
  font-size: clamp(32px, 4vw, 56px);
  line-height: 0.96;
  letter-spacing: -0.05em;
}

.projects-hero__title p {
  color: #64748b;
  line-height: 1.72;
  font-size: 16px;
}

.projects-hero__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.btn-hero,
.btn-toolbar {
  min-height: 42px;
  gap: 8px;
  padding: 0 14px;
  border: 1px solid rgba(191, 219, 254, 0.9);
  border-radius: 12px;
  color: #1d4ed8;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(243, 248, 255, 0.98));
  font-size: 13px;
  font-weight: 800;
  box-shadow:
    0 10px 24px rgba(15, 23, 42, 0.06),
    inset 0 1px 0 rgba(255, 255, 255, 0.92);
  transition:
    transform 180ms ease,
    border-color 180ms ease,
    background 180ms ease,
    box-shadow 180ms ease;
}

.btn-hero:hover,
.btn-toolbar:hover {
  transform: translateY(-1px);
  border-color: rgba(96, 165, 250, 0.72);
  background: #dbeafe;
}

.btn-hero--primary,
.btn-toolbar--accent {
  color: #ffffff;
  border-color: rgba(37, 99, 235, 0.82);
  background: linear-gradient(135deg, #2563eb, #1e40af);
  box-shadow: 0 12px 20px rgba(37, 99, 235, 0.18);
}

.btn-hero--primary:hover,
.btn-toolbar--accent:hover {
  color: #ffffff;
  background: linear-gradient(135deg, #1d4ed8, #1e3a8a);
}

.projects-hero__center {
  display: grid;
  place-items: center;
  min-height: 320px;
}

.project-orbit {
  position: relative;
  width: min(100%, 360px);
  aspect-ratio: 1;
  display: grid;
  place-items: center;
}

.project-orbit__ring {
  position: absolute;
  inset: 8%;
  border-radius: 50%;
  border: 1px solid rgba(37, 99, 235, 0.14);
  box-shadow: inset 0 0 0 10px rgba(59, 130, 246, 0.022);
}

.project-orbit__ring--inner {
  inset: 24%;
  border-color: rgba(59, 130, 246, 0.1);
  background: radial-gradient(circle, rgba(255, 255, 255, 0.92), transparent 72%);
}

.project-orbit__shine {
  content: '';
  position: absolute;
  inset: 16% 18% 16% 18%;
  border-radius: 50%;
  background:
    radial-gradient(circle at 34% 30%, rgba(59, 130, 246, 0.1), transparent 24%),
    radial-gradient(circle at 68% 72%, rgba(34, 197, 94, 0.08), transparent 22%);
  pointer-events: none;
}

.project-orbit__core {
  position: relative;
  display: grid;
  justify-items: center;
  gap: 8px;
  width: 180px;
  height: 180px;
  padding: 18px;
  border-radius: 999px;
  background:
    radial-gradient(circle at 30% 30%, rgba(255, 255, 255, 0.99), rgba(239, 246, 255, 0.98) 62%, rgba(219, 234, 254, 0.92));
  box-shadow:
    0 26px 52px rgba(37, 99, 235, 0.16),
    inset 0 1px 0 rgba(255, 255, 255, 0.96);
  transform: translateY(4px);
}

.project-orbit__core::before {
  content: '';
  position: absolute;
  inset: 12px;
  border-radius: inherit;
  border: 1px solid rgba(191, 219, 254, 0.68);
}

.project-orbit__core-icon {
  z-index: 1;
  display: grid;
  place-items: center;
  width: 72px;
  height: 72px;
  border-radius: 26px;
  color: #ffffff;
  background: linear-gradient(135deg, #2563eb, #38bdf8);
  box-shadow:
    0 20px 38px rgba(37, 99, 235, 0.24),
    inset 0 1px 0 rgba(255, 255, 255, 0.24);
}

.project-orbit__core-caption {
  z-index: 1;
  display: grid;
  gap: 0;
  justify-items: center;
  text-align: center;
}

.project-orbit__core-caption strong {
  color: #0f172a;
  font-size: 16px;
  line-height: 1.12;
  font-weight: 900;
}

.project-orbit__chip {
  position: absolute;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  min-height: 38px;
  padding: 0 12px;
  border: 1px solid rgba(191, 219, 254, 0.9);
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.96);
  backdrop-filter: blur(14px);
  box-shadow:
    0 14px 28px rgba(15, 23, 42, 0.08),
    inset 0 1px 0 rgba(255, 255, 255, 0.96);
  color: #0f172a;
  font-size: 12px;
  font-weight: 800;
  white-space: nowrap;
}

.project-orbit__chip--top {
  top: 5%;
  left: 50%;
  transform: translateX(-50%);
}

.project-orbit__chip--bottom-left {
  left: 10px;
  bottom: 12px;
  transform: none;
}

.project-orbit__chip--bottom-right {
  right: 10px;
  bottom: 12px;
  transform: none;
}

.projects-hero__right {
  display: grid;
  gap: 10px;
  align-content: start;
}

.projects-spotlight--project {
  display: grid;
  gap: 10px;
  padding: 16px;
  border-radius: 22px;
  border: 1px solid rgba(223, 231, 242, 0.92);
  background:
    radial-gradient(circle at top right, rgba(37, 99, 235, 0.06), transparent 26%),
    linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(247, 250, 255, 0.95));
  box-shadow:
    0 18px 40px rgba(15, 23, 42, 0.08),
    inset 0 1px 0 rgba(255, 255, 255, 0.95);
}

.projects-spotlight--project::before {
  content: '';
  position: absolute;
  inset: 0 auto auto 0;
  width: 100%;
  height: 5px;
  border-radius: inherit;
  background: linear-gradient(90deg, #2563eb, #38bdf8);
}

.projects-spotlight__header {
  color: #1d4ed8;
  letter-spacing: 0.08em;
}

.projects-spotlight strong {
  font-size: 18px;
  line-height: 1.2;
}

.projects-spotlight p {
  color: #475569;
  line-height: 1.65;
}

.projects-spotlight__metrics {
  display: grid;
  gap: 8px;
}

.projects-spotlight__metrics span {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: #334155;
  font-size: 12px;
  font-weight: 700;
}

.projects-spotlight__risk {
  color: #b91c1c !important;
}

.projects-hero__summary {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
}

.projects-hero__summary-card {
  display: grid;
  gap: 6px;
  padding: 12px 14px;
  border: 1px solid rgba(223, 231, 242, 0.92);
  border-radius: 18px;
  background: rgba(255, 255, 255, 0.96);
  box-shadow:
    0 10px 24px rgba(15, 23, 42, 0.06),
    inset 0 1px 0 rgba(255, 255, 255, 0.92);
}

.projects-hero__summary-card strong {
  color: #0f172a;
  font-size: 20px;
  line-height: 1;
  font-weight: 900;
}

.projects-hero__summary-card span {
  color: #64748b;
  font-size: 12px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.project-workspace--modern {
  display: grid;
  gap: 12px;
  padding: 14px;
  border-color: rgba(226, 232, 240, 0.92);
  box-shadow:
    0 8px 18px rgba(15, 23, 42, 0.03),
    0 0 0 1px rgba(255, 255, 255, 0.86) inset;
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
  display: flex;
  justify-content: space-between;
  align-items: end;
  gap: 12px;
  padding-top: 0;
}

.project-workspace__header span {
  display: inline-block;
  margin-bottom: 6px;
  color: #2563eb;
  font-size: 11px;
  font-weight: 900;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.project-workspace__header h2 {
  color: #0f172a;
  letter-spacing: -0.03em;
}

.project-workspace__header p {
  color: #64748b;
  font-weight: 700;
}

.project-workspace__hint {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.project-workspace__hint span {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: #64748b;
  font-size: 12px;
  font-weight: 600;
}

.project-modal-backdrop {
  background: rgba(15, 23, 42, 0.38) !important;
  backdrop-filter: blur(10px) !important;
}

.project-modal,
.project-workspace--modern,
.projects-spotlight--project {
  border-radius: 22px;
}

.project-source-toggle button.is-active {
  border-color: rgba(37, 99, 235, 0.34);
  color: #1d4ed8;
  background: #eff6ff;
}

.modal-input:focus {
  border-color: rgba(59, 130, 246, 0.85);
  box-shadow: 0 0 0 4px rgba(59, 130, 246, 0.12);
}

.icon-button:hover {
  color: #1d4ed8;
  border-color: rgba(191, 219, 254, 0.95);
  background: #eff6ff;
}

.project-option-card:hover {
  border-color: rgba(96, 165, 250, 0.82);
  box-shadow: 0 12px 24px rgba(15, 23, 42, 0.07);
}

.projects-hero {
  display: none !important;
}

.projects-overview--enterprise {
  position: relative;
  overflow: hidden;
  display: grid;
  gap: 18px;
  padding: 22px 24px 24px;
  border: 1px solid rgba(191, 219, 254, 0.72);
  background:
    radial-gradient(circle at top left, rgba(59, 130, 246, 0.05), transparent 28%),
    radial-gradient(circle at top right, rgba(34, 197, 94, 0.04), transparent 28%),
    linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(248, 251, 255, 0.96));
  box-shadow:
    0 24px 60px rgba(15, 23, 42, 0.08),
    inset 0 1px 0 rgba(255, 255, 255, 0.95);
}

.projects-overview--enterprise::before {
  content: '';
  position: absolute;
  inset: 0 auto auto 0;
  width: 100%;
  height: 2px;
  background: linear-gradient(90deg, #2563eb, #38bdf8, #22c55e);
}

.projects-overview__header {
  position: relative;
  z-index: 1;
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: 18px;
}

.projects-overview__copy {
  display: grid;
  gap: 14px;
}

.projects-overview__eyebrow {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  width: fit-content;
  padding: 8px 14px;
  border: 1px solid rgba(191, 219, 254, 0.86);
  border-radius: 999px;
  background: rgba(239, 246, 255, 0.82);
  color: #2563eb;
  font-size: 11px;
  font-weight: 900;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.projects-overview__title {
  display: grid;
  gap: 8px;
}

.projects-overview__title h1 {
  color: #0f172a;
  font-size: clamp(2rem, 2.6vw, 3rem);
  line-height: 0.98;
  font-weight: 900;
  letter-spacing: -0.04em;
}

.projects-overview__title p {
  max-width: 70ch;
  color: #64748b;
  font-size: 14px;
  line-height: 1.6;
  font-weight: 600;
}

.projects-overview__cta {
  flex: none;
  box-shadow: 0 16px 28px rgba(37, 99, 235, 0.18);
}

.projects-overview__body {
  position: relative;
  z-index: 1;
  display: grid;
  grid-template-columns: minmax(0, 1.55fr) minmax(260px, 0.7fr);
  gap: 14px;
  align-items: stretch;
}

.projects-overview__stats {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 12px;
}

.projects-overview__stat-card {
  display: grid;
  gap: 10px;
  align-content: start;
  min-height: 122px;
  padding: 14px;
  border: 1px solid rgba(223, 231, 242, 0.92);
  border-radius: 18px;
  background: rgba(255, 255, 255, 0.96);
  box-shadow:
    0 12px 28px rgba(15, 23, 42, 0.05),
    inset 0 1px 0 rgba(255, 255, 255, 0.94);
}

.projects-overview__stat-icon {
  display: grid;
  place-items: center;
  width: 30px;
  height: 30px;
  border-radius: 10px;
  color: #2563eb;
  background: #eff6ff;
}

.projects-overview__stat-card strong {
  color: #0f172a;
  font-size: 28px;
  line-height: 1;
  font-weight: 900;
  letter-spacing: -0.04em;
}

.projects-overview__stat-card span {
  color: #64748b;
  font-size: 12px;
  font-weight: 800;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.projects-overview__feature {
  display: grid;
  gap: 10px;
  align-content: start;
  padding: 16px;
  border: 1px solid rgba(223, 231, 242, 0.92);
  border-radius: 20px;
  background:
    radial-gradient(circle at top right, rgba(37, 99, 235, 0.06), transparent 30%),
    linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(247, 250, 255, 0.95));
  box-shadow:
    0 14px 34px rgba(15, 23, 42, 0.06),
    inset 0 1px 0 rgba(255, 255, 255, 0.95);
}

.projects-overview__feature-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  color: #2563eb;
  font-size: 11px;
  font-weight: 900;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.projects-overview__feature strong {
  color: #0f172a;
  font-size: 18px;
  line-height: 1.25;
  font-weight: 900;
}

.projects-overview__feature p {
  color: #475569;
  font-size: 13px;
  line-height: 1.55;
  font-weight: 600;
}

.projects-overview__feature-meta {
  display: grid;
  gap: 8px;
}

.projects-overview__feature-meta span {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: #334155;
  font-size: 12px;
  font-weight: 700;
}

.projects-overview__feature-risk {
  color: #b91c1c !important;
}

:global(:root[data-theme='dark'] .projects-page-shell) {
  background: var(--surface-canvas);
}

:global(:root[data-theme='dark'] .projects-page-shell__glow) {
  display: none;
}

:global(:root[data-theme='dark'] .projects-overview--enterprise),
:global(:root[data-theme='dark'] .project-workspace--modern),
:global(:root[data-theme='dark'] .projects-overview__stat-card),
:global(:root[data-theme='dark'] .projects-overview__feature) {
  border-color: var(--border);
  background: var(--surface);
  color: var(--text-primary);
  box-shadow: var(--qaly-shadow-sm);
}

:global(:root[data-theme='dark'] .projects-overview__title h1),
:global(:root[data-theme='dark'] .projects-overview__stat-card strong),
:global(:root[data-theme='dark'] .projects-overview__feature strong),
:global(:root[data-theme='dark'] .project-workspace__header h2) {
  color: var(--text-primary);
}

:global(:root[data-theme='dark'] .projects-overview__title p),
:global(:root[data-theme='dark'] .projects-overview__stat-card span),
:global(:root[data-theme='dark'] .projects-overview__feature p),
:global(:root[data-theme='dark'] .projects-overview__feature-meta span),
:global(:root[data-theme='dark'] .project-workspace__header p),
:global(:root[data-theme='dark'] .project-workspace__hint span) {
  color: var(--text-secondary);
}

:global(:root[data-theme='dark'] .projects-overview__eyebrow),
:global(:root[data-theme='dark'] .projects-overview__stat-icon),
:global(:root[data-theme='dark'] .project-source-toggle button.is-active),
:global(:root[data-theme='dark'] .icon-button:hover) {
  border-color: rgba(96, 165, 250, 0.34);
  background: var(--primary-soft);
  color: var(--primary-strong);
}

:global(:root[data-theme='dark'] .btn-hero:not(.btn-hero--primary)),
:global(:root[data-theme='dark'] .btn-toolbar:not(.btn-toolbar--accent)) {
  border-color: var(--border-strong);
  background: var(--surface-muted);
  color: var(--text-primary);
  box-shadow: var(--qaly-shadow-sm);
}

:global(:root[data-theme='dark'] .project-modal) {
  border-color: var(--border) !important;
  background: var(--surface) !important;
  color: var(--text-primary) !important;
  box-shadow: var(--qaly-shadow-md) !important;
}

:global(:root[data-theme='dark'] .project-modal-header) {
  border-color: var(--border) !important;
}

:global(:root[data-theme='dark'] .project-modal-title),
:global(:root[data-theme='dark'] .project-modal-title h2),
:global(:root[data-theme='dark'] .project-modal .form-group label),
:global(:root[data-theme='dark'] .project-member-picker label) {
  color: var(--text-primary) !important;
}

:global(:root[data-theme='dark'] .project-modal .icon-button),
:global(:root[data-theme='dark'] .project-modal .modal-input),
:global(:root[data-theme='dark'] .project-modal .project-source-toggle button),
:global(:root[data-theme='dark'] .project-modal .project-member-picker),
:global(:root[data-theme='dark'] .project-modal .project-member-picker label),
:global(:root[data-theme='dark'] .project-modal .project-option-card),
:global(:root[data-theme='dark'] .project-modal .create-source-grid__card) {
  border-color: var(--border-strong) !important;
  background: var(--surface-muted) !important;
  color: var(--text-primary) !important;
}

:global(:root[data-theme='dark'] .project-modal .modal-input:hover),
:global(:root[data-theme='dark'] .project-modal .modal-input:focus) {
  border-color: var(--primary) !important;
  background: var(--surface-hover) !important;
}

@media (max-width: 1180px) {
  .projects-hero--spotlight {
    grid-template-columns: 1fr;
  }

  .projects-hero__center {
    min-height: 280px;
  }
}

@media (max-width: 840px) {
  .projects-hero__summary {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 720px) {
  .projects-page-shell__content {
    padding: 14px;
  }

  .projects-hero--spotlight,
  .project-workspace--modern {
    padding: 16px;
    border-radius: 22px;
  }

  .projects-hero__actions {
    flex-direction: column;
    align-items: stretch;
  }

  .btn-hero,
  .btn-toolbar {
    width: 100%;
    justify-content: center;
  }

  .projects-hero__center {
    min-height: 240px;
  }
}
</style>
