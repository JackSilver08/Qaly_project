<script setup lang="ts">
import ProjectList from '../components/ProjectList.vue'
import { useDashboardContext } from '../composables/dashboard-context'

const {
  archivedProjectCards,
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
            <span>Archived</span>
            <h2>Dự án đã lưu trữ</h2>
          </div>
          <p>{{ archivedProjectCards.length }} dự án read-only</p>
        </div>

        <ProjectList
          :projects="archivedProjectCards"
          :active-project-id="selectedProject?.id ?? null"
          read-only
          @view="selectProject"
        />
      </section>
    </div>
  </div>
</template>
