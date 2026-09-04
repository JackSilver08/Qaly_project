import { createRouter, createWebHistory } from "vue-router";
import { usePermissions } from "../composables/use-permissions";

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
const OrganizationOverviewPage = () => import("../pages/OrganizationOverviewPage.vue");

const guidPattern =
    "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";

export const router = createRouter({
    history: createWebHistory(),
    routes: [
        { path: "/", redirect: "/dashboard" },
        { path: "/dashboard", name: "dashboard", component: DashboardPage, meta: { moduleKey: "Dashboard" } },
        { path: "/profile", name: "profile", component: ProfilePage, meta: { moduleKey: "Settings" } },
        { path: "/projects", name: "projects", component: ProjectsPage, meta: { moduleKey: "Projects" } },
        {
            path: "/projects/archived",
            name: "projects-archived",
            component: ArchivedProjectsPage,
            meta: { moduleKey: "Projects" },
        },
        {
            path: `/projects/:projectId(${guidPattern})`,
            name: "project-detail",
            component: ProjectDetailPage,
            meta: { moduleKey: "Projects" },
        },
        {
            path: `/projects/:projectId(${guidPattern})/tasks/:taskId(${guidPattern})`,
            name: "project-task",
            component: ProjectDetailPage,
            meta: { moduleKey: "Tasks" },
        },
        {
            path: `/projects/:projectId(${guidPattern})/wiki/:wikiId(${guidPattern})`,
            name: "project-wiki-detail",
            component: () => import("../pages/WikiDetailPage.vue"),
            meta: { moduleKey: "Wiki" },
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
        { path: "/tasks", name: "tasks", component: TasksPage, meta: { moduleKey: "Tasks" } },
        { path: "/teams", name: "teams", component: TeamsPage, meta: { moduleKey: "WorkGroups" } },
        { path: "/analytics", name: "analytics", component: AnalyticsPage, meta: { moduleKey: "Analytics" } },
        { path: "/admin/users", name: "admin-users", component: AdminUsersPage, meta: { moduleKey: "UserManagement" } },
        { path: "/organizations/users", name: "organization-users", component: OrganizationUsersPage, meta: { moduleKey: "OrganizationMembers" } },
        { path: "/organizations", name: "organizations", component: OrganizationsPage, meta: { moduleKey: "OrganizationManagement" } },
        { path: `/organizations/:organizationId(${guidPattern})`, name: "organization-overview", component: OrganizationOverviewPage, meta: { moduleKey: "OrganizationManagement" } },
        { path: "/admin/moderators", name: "admin-moderators", component: ModeratorAssignmentsPage, meta: { moduleKey: "ModeratorAssignments" } },
        {
            path: "/groups",
            name: "groups",
            component: () => import("../pages/TeamsPage.vue"),
            meta: { moduleKey: "WorkGroups" },
        },
        {
            path: `/groups/:groupId(${guidPattern})`,
            name: "group-detail",
            component: () => import("../pages/TeamsPage.vue"),
            meta: { moduleKey: "WorkGroups" },
        },
        {
            path: `/groups/:groupId(${guidPattern})/meeting`,
            name: "group-meeting",
            component: () => import("../pages/GroupMeetingPage.vue"),
            meta: { moduleKey: "WorkGroups" },
        },
        {
            path: `/groups/:groupId(${guidPattern})/polls`,
            name: "group-polls",
            component: () => import("../pages/GroupPollPage.vue"),
            meta: { moduleKey: "WorkGroups" },
        },
        {
            path: `/groups/:groupId(${guidPattern})/polls/:pollId(${guidPattern})`,
            name: "group-poll-detail",
            component: () => import("../pages/GroupPollPage.vue"),
            meta: { moduleKey: "WorkGroups" },
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
        { path: "/settings", name: "settings", component: SettingsPage, meta: { moduleKey: "Settings" } },
        {
            path: "/access-denied",
            name: "access-denied",
            component: RouteErrorPage,
            props: {
                title: "Bạn không có quyền mở trang này",
                message: "Qaly đã kiểm tra quyền hiệu lực từ máy chủ. Hãy quay lại trang được cấp quyền hoặc liên hệ quản trị viên.",
            },
        },
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

router.beforeEach(async to => {
    if (to.name === "access-denied") return true;
    const moduleKey = typeof to.meta.moduleKey === "string" ? to.meta.moduleKey : null;
    if (!moduleKey) return true;

    const permissions = usePermissions();
    if (permissions.permissionLoadState.value !== "loaded") {
        await permissions.loadSystemPermissions();
    }

    return permissions.canAccessModule(moduleKey)
        ? true
        : { name: "access-denied", query: { from: to.fullPath } };
});
