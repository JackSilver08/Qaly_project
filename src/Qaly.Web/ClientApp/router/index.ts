import { createRouter, createWebHistory } from "vue-router";

const ArchivedProjectsPage = () => import("../pages/ArchivedProjectsPage.vue");
const DashboardPage = () => import("../pages/DashboardPage.vue");
const ProfilePage = () => import("../pages/ProfilePage.vue");
const SettingsPage = () => import("../pages/SettingsPage.vue");
const ProjectDetailPage = () => import("../pages/ProjectDetailPage.vue");
const ProjectsPage = () => import("../pages/ProjectsPage.vue");
const TasksPage = () => import("../pages/TasksPage.vue");
const TeamsPage = () => import("../pages/TeamsPage.vue");
const AnalyticsPage = () => import("../pages/AnalyticsPage.vue");
const RouteErrorPage = () => import("../pages/RouteErrorPage.vue");
const AdminUsersPage = () => import("../pages/AdminUsersPage.vue");
const OrganizationUsersPage = () => import("../pages/OrganizationUsersPage.vue");
const ModeratorAssignmentsPage = () => import("../pages/ModeratorAssignmentsPage.vue");
const OrganizationsPage = () => import("../pages/OrganizationsPage.vue");

const guidPattern =
    "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";

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
            path: `/projects/:projectId(${guidPattern})`,
            name: "project-detail",
            component: ProjectDetailPage,
        },
        {
            path: `/projects/:projectId(${guidPattern})/tasks/:taskId(${guidPattern})`,
            name: "project-task",
            component: ProjectDetailPage,
        },
        {
            path: `/projects/:projectId(${guidPattern})/wiki/:wikiId(${guidPattern})`,
            name: "project-wiki-detail",
            component: () => import("../pages/WikiDetailPage.vue"),
        },
        {
            path: "/projects/:projectId/tasks/:taskId",
            name: "project-task-format-error",
            component: RouteErrorPage,
            props: {
                title: "URL nhiệm vụ không hợp lệ",
                message: "Task URL sai định dạng hoặc đã bị thay đổi.",
            },
        },
        {
            path: "/projects/:projectId",
            name: "project-format-error",
            component: RouteErrorPage,
            props: {
                title: "URL dự án không hợp lệ",
                message: "Project URL sai định dạng hoặc đã bị thay đổi.",
            },
        },
        { path: "/tasks", name: "tasks", component: TasksPage },
        { path: "/teams", name: "teams", component: TeamsPage },
        { path: "/analytics", name: "analytics", component: AnalyticsPage },
        { path: "/admin/users", name: "admin-users", component: AdminUsersPage },
        { path: "/organizations/users", name: "organization-users", component: OrganizationUsersPage },
        { path: "/organizations", name: "organizations", component: OrganizationsPage },
        { path: "/admin/moderators", name: "admin-moderators", component: ModeratorAssignmentsPage },
        {
            path: "/groups",
            name: "groups",
            component: () => import("../pages/TeamsPage.vue"),
        },
        {
            path: `/groups/:groupId(${guidPattern})`,
            name: "group-detail",
            component: () => import("../pages/TeamsPage.vue"),
        },
        {
            path: `/groups/:groupId(${guidPattern})/meeting`,
            name: "group-meeting",
            component: () => import("../pages/GroupMeetingPage.vue"),
        },
        {
            path: `/groups/:groupId(${guidPattern})/polls`,
            name: "group-polls",
            component: () => import("../pages/GroupPollPage.vue"),
        },
        {
            path: `/groups/:groupId(${guidPattern})/polls/:pollId(${guidPattern})`,
            name: "group-poll-detail",
            component: () => import("../pages/GroupPollPage.vue"),
        },
        {
            path: "/groups/:groupId/:rest(.*)*",
            name: "group-format-error",
            component: RouteErrorPage,
            props: {
                title: "URL nhóm không hợp lệ",
                message: "Group link sai định dạng hoặc đã hết hiệu lực.",
            },
        },
        { path: "/settings", name: "settings", component: SettingsPage },
        {
            path: "/:pathMatch(.*)*",
            name: "route-not-found",
            component: RouteErrorPage,
            props: {
                title: "Không tìm thấy trang",
                message: "Đường dẫn không tồn tại hoặc đã bị thay đổi.",
            },
        },
    ],
    scrollBehavior() {
        return { top: 0 };
    },
});
