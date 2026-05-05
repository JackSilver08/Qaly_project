<script setup lang="ts">
import ProjectList from '../components/ProjectList.vue'
import ProjectToolbar from '../components/ProjectToolbar.vue'
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
} = useDashboardContext()
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
        />

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
  </div>
</template>
