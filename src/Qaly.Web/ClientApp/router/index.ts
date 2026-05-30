import { createRouter, createWebHistory } from "vue-router";

const ArchivedProjectsPage = () => import("../pages/ArchivedProjectsPage.vue");
const DashboardPage = () => import("../pages/DashboardPage.vue");
const ProfilePage = () => import("../pages/ProfilePage.vue");
const ProjectDetailPage = () => import("../pages/ProjectDetailPage.vue");
const ProjectsPage = () => import("../pages/ProjectsPage.vue");
const TasksPage = () => import("../pages/TasksPage.vue");
const TeamsPage = () => import("../pages/TeamsPage.vue");
const AnalyticsPage = () => import("../pages/AnalyticsPage.vue");

export const router = createRouter({
    history: createWebHistory(),
    routes: [
        { path: "/", redirect: "/dashboard" },
        { path: "/dashboard", name: "dashboard", component: DashboardPage },
        { path: "/profile", name: "profile", component: ProfilePage },
        { path: "/projects", name: "projects", component: ProjectsPage },
        {
            path: "/projects/archived",
            name: "projects-archived",
            component: ArchivedProjectsPage,
        },
        {
            path: "/projects/:projectId",
            name: "project-detail",
            component: ProjectDetailPage,
        },
        {
            path: "/projects/:projectId/tasks/:taskId",
            name: "project-task",
            component: ProjectDetailPage,
        },
        {
            path: "/projects/:projectId/wiki/:wikiId",
            name: "project-wiki-detail",
            component: () => import("../pages/WikiDetailPage.vue"),
        },
        { path: "/tasks", name: "tasks", component: TasksPage },
        { path: "/teams", name: "teams", component: TeamsPage },
        { path: "/analytics", name: "analytics", component: AnalyticsPage },
        {
            path: "/groups",
            name: "groups",
            component: () => import("../pages/TeamsPage.vue"),
        },
        {
            path: "/groups/:groupId/meeting",
            name: "group-meeting",
            component: () => import("../pages/GroupMeetingPage.vue"),
        },
        {
            path: "/groups/:groupId/polls",
            name: "group-polls",
            component: () => import("../pages/GroupPollPage.vue"),
        },
        {
            path: "/groups/:groupId/polls/:pollId",
            name: "group-poll-detail",
            component: () => import("../pages/GroupPollPage.vue"),
        },
        { path: "/settings", name: "settings", component: ProfilePage },
        { path: "/:pathMatch(.*)*", redirect: "/dashboard" },
    ],
    scrollBehavior() {
        return { top: 0 };
    },
});
