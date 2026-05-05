<script setup lang="ts">
import TaskList from '../components/TaskList.vue'
import { useDashboardContext } from '../composables/dashboard-context'

const {
  assignedTaskCards,
  openTask,
} = useDashboardContext()
</script>

<template>
  <div class="dashboard-scroll dashboard-scroll--embedded no-scrollbar">
    <div class="dashboard-main project-home-main no-scrollbar">
      <section class="project-workspace glass-card">
        <div class="project-workspace__header">
          <div>
            <span>Tasks</span>
            <h2>Nhiệm vụ của tôi</h2>
          </div>
          <p>{{ assignedTaskCards.length }} nhiệm vụ được giao</p>
        </div>

        <TaskList :tasks="assignedTaskCards" @view="openTask" />
      </section>
    </div>
  </div>
</template>
