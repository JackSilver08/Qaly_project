<script setup lang="ts">
import DashboardSummaryCards from '../components/DashboardSummaryCards.vue'
import ProjectList from '../components/ProjectList.vue'
import ProjectToolbar from '../components/ProjectToolbar.vue'
import { useDashboardContext } from '../composables/dashboard-context'

const {
  beginEditProject,
  createProject,
  createProjectOpen,
  deleteProject,
  editProjectDescription,
  editProjectName,
  filteredProjects,
  openCreateProject,
  projectBeingEditedId,
  projectCards,
  projectDescription,
  projectEndDate,
  projectFilter,
  projectName,
  projects,
  projectSort,
  saveProjectEdit,
  searchQuery,
  selectProject,
  selectedProject,
  summaryCards,
} = useDashboardContext()
</script>

<template>
  <div class="dashboard-scroll dashboard-scroll--embedded no-scrollbar">
    <div class="dashboard-main project-home-main no-scrollbar">
      <DashboardSummaryCards id="overview" :cards="summaryCards" />

      <section id="projects" class="project-workspace glass-card">
        <div class="project-workspace__header">
          <div>
            <span>Projects</span>
            <h2>Project portfolio</h2>
          </div>
          <p>{{ filteredProjects.length }} of {{ projects.length }} projects</p>
        </div>

        <ProjectToolbar
          v-model:search="searchQuery"
          v-model:sort="projectSort"
          v-model:filter="projectFilter"
          :project-count="filteredProjects.length"
          @create="openCreateProject"
        />

        <form v-if="createProjectOpen" class="project-inline-form project-inline-form--stacked" @submit.prevent="createProject">
          <input v-model="projectName" type="text" placeholder="Project name" />
          <input v-model="projectDescription" type="text" placeholder="Short description" />
          <input v-model="projectEndDate" type="date" />
          <button class="primary-button primary-button--compact" type="submit" :disabled="!projectName.trim()">
            Create
          </button>
          <button class="text-button" type="button" @click="createProjectOpen = false">Cancel</button>
        </form>

        <form v-if="projectBeingEditedId" class="project-inline-form project-inline-form--stacked" @submit.prevent="saveProjectEdit">
          <input v-model="editProjectName" type="text" aria-label="Project name" />
          <input v-model="editProjectDescription" type="text" aria-label="Project description" />
          <button class="primary-button primary-button--compact" type="submit" :disabled="!editProjectName.trim()">
            Save
          </button>
          <button class="text-button" type="button" @click="projectBeingEditedId = null">Cancel</button>
        </form>

        <ProjectList
          :projects="projectCards"
          :active-project-id="selectedProject?.id ?? null"
          @view="selectProject"
          @edit="beginEditProject"
          @delete="deleteProject"
        />
      </section>
    </div>
  </div>
</template>
