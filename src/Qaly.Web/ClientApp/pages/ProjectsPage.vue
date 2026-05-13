<script setup lang="ts">
import { ref } from 'vue'
import { FileSpreadsheet } from 'lucide-vue-next'
import ProjectList from '../components/ProjectList.vue'
import ProjectToolbar from '../components/ProjectToolbar.vue'
import ImportModal from '../components/import/ImportModal.vue'
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
} = useDashboardContext()

const showImportModal = ref(false)

function onImported() {
  showImportModal.value = false
  loadDashboard()
}
</script>

<template>
  <div class="dashboard-scroll dashboard-scroll--embedded no-scrollbar">
    <div class="dashboard-main project-home-main no-scrollbar">
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

        <ProjectList
          :projects="activeProjectCards"
          :active-project-id="selectedProject?.id ?? null"
          @view="selectProject"
          @edit="beginEditProject"
          @delete="deleteProject"
        />
      </section>
    </div>

    <ImportModal
      v-if="showImportModal"
      @close="showImportModal = false"
      @imported="onImported"
    />
  </div>
</template>

<style scoped>
.btn-import {
  display: inline-flex; align-items: center; gap: 6px;
  padding: 6px 14px; border-radius: 8px; font-size: .8rem; font-weight: 500;
  background: rgba(99,102,241,.12); color: #818cf8;
  border: 1px solid rgba(99,102,241,.2); cursor: pointer;
  transition: all .2s;
}
.btn-import:hover { background: rgba(99,102,241,.2); }
</style>
