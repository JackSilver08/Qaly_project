<script setup lang="ts">
import { ref, computed } from 'vue'
import { FileSpreadsheet, FolderKanban, CheckCircle2, AlertTriangle } from 'lucide-vue-next'
import ProjectList from '../components/ProjectList.vue'
import ProjectGrid from '../components/ProjectGrid.vue'
import ProjectToolbar from '../components/ProjectToolbar.vue'
import ImportModal from '../components/import/ImportModal.vue'
import ImportUndoBanner from '../components/import/ImportUndoBanner.vue'
import { useDashboardContext } from '../composables/dashboard-context'

const {
  activeProjectCards,
  beginEditProject,
  createProject,
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
} = useDashboardContext()

const isGridView = ref(true)
const showImportModal = ref(false)
const undoBannerData = ref<{ importSessionId: string; importedCount: number; createdAt: string } | null>(null)

const totalProjectsCount = computed(() => projects.value.filter((p: any) => p.status !== 'Archived').length)
const onTrackCount = computed(() => projects.value.filter((p: any) => p.status !== 'Archived' && p.overdueTaskCount === 0).length)
const atRiskCount = computed(() => projects.value.filter((p: any) => p.status !== 'Archived' && p.overdueTaskCount > 0).length)

function onImported(result: any) {
  showImportModal.value = false
  if (result) {
    undoBannerData.value = {
      importSessionId: result.importSessionId,
      importedCount: result.importedCount,
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
              <FileSpreadsheet :size="15" /> Import CSV
            </button>
          </template>
        </ProjectToolbar>

        <form v-if="createProjectOpen" class="project-inline-form project-inline-form--stacked" @submit.prevent="createProject">
          <input v-model="projectName" type="text" placeholder="Tên dự án" />
          <input v-model="projectDescription" type="text" placeholder="Mô tả ngắn" />
          <input v-model="projectEndDate" type="date" />
          <button class="primary-button primary-button--compact" type="submit" :disabled="!projectName.trim()">
            Tạo
          </button>
          <button class="text-button" type="button" @click="createProjectOpen = false">Hủy</button>
        </form>

        <form v-if="projectBeingEditedId" class="project-inline-form project-inline-form--stacked" @submit.prevent="saveProjectEdit">
          <input v-model="editProjectName" type="text" aria-label="Tên dự án" />
          <input v-model="editProjectDescription" type="text" aria-label="Mô tả dự án" />
          <button class="primary-button primary-button--compact" type="submit" :disabled="!editProjectName.trim()">
            Lưu
          </button>
          <button class="text-button" type="button" @click="projectBeingEditedId = null">Hủy</button>
        </form>

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
      :created-at="undoBannerData.createdAt"
      @undo="handleUndoFromBanner"
      @dismiss="undoBannerData = null"
    />
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
  background: rgba(255, 255, 255, 0.08);
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
</style>
