<script setup lang="ts">
import { Plus } from 'lucide-vue-next'
import DashboardSummaryCards from '../components/DashboardSummaryCards.vue'
import ProjectGrid from '../components/ProjectGrid.vue'
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
      
      <!-- Welcome Banner -->
      <header class="dashboard-welcome-banner">
        <div>
          <h1>Chào buổi sáng, Minh!</h1>
          <p>Dưới đây là tóm tắt hoạt động của Workspace ngày hôm nay.</p>
        </div>
        <button class="primary-button" type="button" @click="openCreateProject">
          <Plus :size="18" />
          <span>Tạo Dự Án Mới</span>
        </button>
      </header>

      <DashboardSummaryCards id="overview" :cards="summaryCards" />

      <section id="projects" class="project-workspace">
        <div class="project-workspace__header">
          <div>
            <h2>Danh mục Dự án</h2>
          </div>
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

        <ProjectGrid
          :projects="projectCards"
          :active-project-id="selectedProject?.id ?? null"
          @view="selectProject"
          @edit="beginEditProject"
          @delete="deleteProject"
        />
      </section>

      <!-- Performance Banner -->
      <section class="performance-banner">
        <div class="performance-banner__content">
          <span class="performance-banner__label">HIỆU SUẤT HỆ THỐNG</span>
          <h2>Phân tích Tổng quan Quý 2</h2>
          <p>Workspace của bạn đang hoạt động với hiệu suất vượt trội hơn 24% so với quý trước. Hầu hết các dự án trọng điểm đang đi đúng lộ trình đề ra.</p>
          <div class="performance-banner__stats">
            <div>
              <strong>94%</strong>
              <span>Hoàn thành đúng hạn</span>
            </div>
            <div>
              <strong>08</strong>
              <span>Nhiệm vụ mới tuần này</span>
            </div>
          </div>
        </div>
        <div class="performance-banner__visual">
          <!-- Abstract representation of the dark UI graphic in the mockup -->
          <div class="abstract-ui">
            <div class="abstract-ui-header"></div>
            <div class="abstract-ui-body">
              <div class="abstract-ui-card"></div>
              <div class="abstract-ui-card"></div>
            </div>
          </div>
        </div>
      </section>
    </div>
  </div>
</template>
