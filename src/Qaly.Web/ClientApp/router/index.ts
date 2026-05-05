import { createRouter, createWebHistory } from 'vue-router'
import ArchivedProjectsPage from '../pages/ArchivedProjectsPage.vue'
import DashboardPage from '../pages/DashboardPage.vue'
import ProjectDetailPage from '../pages/ProjectDetailPage.vue'
import ProjectsPage from '../pages/ProjectsPage.vue'
import TasksPage from '../pages/TasksPage.vue'
import TeamsPage from '../pages/TeamsPage.vue'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/dashboard' },
    { path: '/dashboard', name: 'dashboard', component: DashboardPage },
    { path: '/projects', name: 'projects', component: ProjectsPage },
    { path: '/projects/archived', name: 'projects-archived', component: ArchivedProjectsPage },
    { path: '/projects/:projectId', name: 'project-detail', component: ProjectDetailPage },
    { path: '/projects/:projectId/tasks/:taskId', name: 'project-task', component: ProjectDetailPage },
    { path: '/tasks', name: 'tasks', component: TasksPage },
    { path: '/teams', name: 'teams', component: TeamsPage },
    { path: '/:pathMatch(.*)*', redirect: '/dashboard' },
  ],
  scrollBehavior() {
    return { top: 0 }
  },
})
