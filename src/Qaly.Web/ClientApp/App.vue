<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, provide, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { HubConnectionBuilder } from "@microsoft/signalr";
import {
  ClipboardList,
  FolderKanban,
  LayoutDashboard,
  Plus,
  Search,
  Users,
  X,
  BarChart3,
  Settings,
  ShieldCheck,
  Building2,
} from "lucide-vue-next";
import AppShell from "./components/AppShell.vue";
import ConfirmDialogHost from "./components/ConfirmDialogHost.vue";
import ProjectActivityTab from "./components/ProjectActivityTab.vue";
import ProjectWorkloadTab from "./components/ProjectWorkloadTab.vue";
import WelcomeOverlay from "./components/WelcomeOverlay.vue";
import FloatingChatbot from "./components/chat/FloatingChatbot.vue";
import { dashboardContextKey } from "./composables/dashboard-context";
import { showError, showInfo, showSuccess } from "./composables/use-toast";
import { confirmDialog } from "./composables/use-confirm-dialog";
import { useDashboard } from "./composables/use-dashboard-state";
import { usePermissions } from "./composables/use-permissions";
import { useProjectActions } from "./composables/use-project-actions";
import { useTaskActions } from "./composables/use-task-actions";
import {
  apiCommand,
  apiJson,
  apiResult,
  errorMessage,
} from "./utils/api-client";

import {
  displayRole,
  displayStatus,
  formatDate,
  formatFileSize,
  formatTime,
  initials,
  isTaskOverdue,
  statusTone,
} from "./utils/formatters";
import { fallbackProjectPermissions } from "./utils/project-roles";
import { taskStatusColumns } from "./utils/task-workspace";
import { taskNextStatuses } from "./utils/task-transitions";
import type {
  ProjectCardModel,
  SummaryCardModel,
  TaskListItemModel,
} from "./components/dashboard-models";
import type { ShellNavItem } from "./components/shell-models";
import type {
  AttachmentDto,
  CommentDto,
  DashboardNotification,
  DashboardProject,
  DashboardTask,
  NotificationDto,
  ProjectPermissionsDto,
  WikiPageDto,
  TimeEntryDto,
} from "./types";

const {
  dashboard,
  currentUser,
  currentUserLoaded,
  users,
  isLoading,
  usingFallback,
  loadError,
  projects,
  team,
  summaryCards,
  loadDashboard,
  loadMe,
  loadUsers,
} = useDashboard();

const { canAccessModule, loadSystemPermissions, permissionLoadState } = usePermissions();

const refreshDashboard = async () => {
  await loadDashboard();
};

const activeProjectStorageKey = "qaly-active-project-id";

function readActiveProjectId() {
  if (typeof window === "undefined") return null;
  try {
    return window.sessionStorage.getItem(activeProjectStorageKey);
  } catch {
    return null;
  }
}

const activeProjectId = ref<string | null>(readActiveProjectId());
const selectedProjectSnapshot = ref<DashboardProject | null>(null);
const selectedTaskId = ref<string | null>(null);

watch(
  activeProjectId,
  (projectId) => {
    if (typeof window === "undefined") return;
    try {
      if (projectId) window.sessionStorage.setItem(activeProjectStorageKey, projectId);
      else window.sessionStorage.removeItem(activeProjectStorageKey);
    } catch {
      // Storage can be unavailable in hardened browser contexts; in-memory selection still works.
    }
  },
  { flush: "sync" },
);

const {
  createProjectOpen,
  projectName,
  projectDescription,
  projectEndDate,
  editProjectName,
  editProjectDescription,
  projectBeingEditedId,
  openCreateProject,
  createProject,
  beginEditProject,
  saveProjectEdit,
  deleteProject,
  selectProject: baseSelectProject,
  archiveProject,
  restoreProject,
} = useProjectActions(projects, activeProjectId, refreshDashboard);

const {
  createTaskOpen,
  taskBeingEdited,
  cancelTaskForm,
  newTaskTitle,
  newTaskDescription,
  newTaskPriority,
  newTaskAssigneeId,
  newTaskDueDate,
  newTaskIsPrivate,
  newTaskIsPinned,
  newTaskContributesToProgress,
  selectedTaskIds,
  toggleTaskSelection,
  batchDeleteTasks,
  batchUpdateTaskStatus,
  createTask: baseCreateTask,
  moveTask,
  moveTaskOnKanban,
  beginEditTask,
  saveTaskEdit,
  deleteTask,
} = useTaskActions(
  selectedTaskId,
  refreshDashboard,
  () => selectedProject.value?.id ?? null,
  openTask,
);

const navigation = computed<ShellNavItem[]>(() => {
  const candidates: Array<ShellNavItem & { moduleKey: string }> = [
    { moduleKey: "OrganizationMembers", label: "Thành viên tổ chức", to: "/organizations/users", icon: Building2 },
    { moduleKey: "Dashboard", label: "Tổng quan", to: "/dashboard", icon: LayoutDashboard },
    { moduleKey: "Projects", label: "Dự án", to: "/projects", icon: FolderKanban },
    { moduleKey: "Tasks", label: "Nhiệm vụ", to: "/tasks", icon: ClipboardList },
    { moduleKey: "WorkGroups", label: "Nhóm", to: "/teams", icon: Users },
    { moduleKey: "Analytics", label: "Phân tích", to: "/analytics", icon: BarChart3 },
    { moduleKey: "OrganizationManagement", label: "Quản lý tổ chức", to: "/organizations", icon: Building2 },
    { moduleKey: "ModeratorAssignments", label: "Ủy quyền Moderator", to: "/admin/moderators", icon: ShieldCheck },
    { moduleKey: "UserManagement", label: "Quản lý người dùng", to: "/admin/users", icon: ShieldCheck },
  ];
  return candidates
    .filter(item => canAccessModule(item.moduleKey))
    .map(({ moduleKey: _moduleKey, ...item }) => item);
});

const statusColumns = computed(() => taskStatusColumns(selectedProject.value));
const priorities = ["Low", "Medium", "High", "Critical"];

const notifications = ref<NotificationDto[]>([]);
const comments = ref<CommentDto[]>([]);
const attachments = ref<AttachmentDto[]>([]);
const wikiPages = ref<WikiPageDto[]>([]);
const timeEntries = ref<TimeEntryDto[]>([]);
const timeEntriesError = ref("");
const activeTimer = ref<TimeEntryDto | null>(null);

const notificationsOpen = ref(false);
const globalSearchOpen = ref(false);
const globalSearchQuery = ref("");
const globalSearchInput = ref<HTMLInputElement | null>(null);
const globalSearchPanel = ref<HTMLElement | null>(null);
let globalSearchPreviousFocus: HTMLElement | null = null;
const taskSearchQuery = ref("");
const taskBeingQuickEditedId = ref<string | null>(null);
const activeTaskMenu = ref<string | null>(null);

interface AiAssistantOpenRequest {
  id: number;
  view: "chat";
  prompt: string;
  projectId: string | null;
}

const aiAssistantOpenRequest = ref<AiAssistantOpenRequest | null>(null);
let aiAssistantOpenRequestId = 0;

function toggleTaskMenu(taskId: string) {
  activeTaskMenu.value = activeTaskMenu.value === taskId ? null : taskId;
}
const searchQuery = ref("");
const projectFilter = ref<"all" | "active" | "planned" | "at-risk">("all");
const projectSort = ref<"recent" | "risk" | "progress" | "name">("recent");
const activeProjectTab = ref("stats");

const tabs = [
  { id: "stats", label: "Thống kê" },
  { id: "roadmap", label: "Lộ Trình Dự Án" },
  { id: "tasks", label: "Nhiệm vụ" },
  { id: "capacity", label: "Phân công & Capacity" },
  { id: "activity", label: "Hoạt động" },
  { id: "members", label: "Thành viên" },
  { id: "wiki", label: "Wiki" },
  { id: "github", label: "GitHub" },
  { id: "webhooks", label: "Webhook" },
];

const newComment = ref("");

let notificationConnectionStarted = false;
const router = useRouter();
const route = useRoute();

const filteredProjects = computed(() => {
  const query = searchQuery.value.trim().toLowerCase();
  return projects.value
    .filter((project) => {
      if (
        projectFilter.value === "active" &&
        !["Active", "InProgress"].includes(project.status)
      )
        return false;
      if (projectFilter.value === "planned" && project.status !== "Planned")
        return false;
      if (projectFilter.value === "at-risk" && project.overdueTaskCount === 0)
        return false;
      if (!query) return true;
      const members = Array.isArray(project.members) ? project.members : [];
      return [
        project.name,
        project.description,
        project.ownerName,
        ...members.map((member) => member.fullName),
      ]
        .join(" ")
        .toLowerCase()
        .includes(query);
    })
    .sort((left, right) => {
      if (projectSort.value === "name")
        return left.name.localeCompare(right.name);
      if (projectSort.value === "progress")
        return right.progressPercentage - left.progressPercentage;
      if (projectSort.value === "risk")
        return right.overdueTaskCount - left.overdueTaskCount;
      return (
        new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime()
      );
    });
});

function toProjectCard(project: DashboardProject): ProjectCardModel {
  return {
    id: project.id,
    name: project.name,
    description: project.description || "Chưa có mô tả.",
    status: project.status,
    statusLabel: displayStatus(project.status),
    statusTone: statusTone(project.status),
    ownerId: project.ownerId,
    ownerName: project.ownerName,
    dueDateLabel: project.endDate
      ? `Hạn ${formatDate(project.endDate)}`
      : "Chưa đặt hạn",
    completedTaskCount: project.completedTaskCount,
    taskCount: project.taskCount,
    overdueTaskCount: project.overdueTaskCount,
    progressPercentage: project.progressPercentage,
    memberInitials: (project.members || [])
      .slice(0, 4)
      .map((m) => initials(m.fullName)),
    code: project.code,
    memberCount: project.memberCount,
    endDate: project.endDate,
    archivedAt: project.archivedAt,
  };
}

const projectCards = computed<ProjectCardModel[]>(() =>
  filteredProjects.value.map(toProjectCard),
);
const activeProjectCards = computed<ProjectCardModel[]>(() =>
  filteredProjects.value
    .filter((p) => p.status !== "Archived")
    .map(toProjectCard),
);
const archivedProjectCards = computed<ProjectCardModel[]>(() =>
  filteredProjects.value
    .filter((p) => p.status === "Archived")
    .map(toProjectCard),
);

const assignedTaskCards = computed<TaskListItemModel[]>(() =>
  projects.value.flatMap((project) =>
    (Array.isArray(project.tasks) ? project.tasks : [])
      .filter(
        (task) =>
          !currentUser.value ||
          task.assigneeName === currentUser.value.fullName,
      )
      .map((task) => ({
        id: task.id,
        projectId: project.id,
        title: task.title,
        projectName: project.name,
        assignedAtLabel: formatDate(project.createdAt),
        priority: task.priority,
        dueDateLabel: formatDate(task.dueDate),
        reporterName: task.reporterName,
        reporterInitials: initials(task.reporterName),
        statusLabel: displayStatus(task.status),
        isOverdue: isTaskOverdue(task),
      })),
  ),
);

const selectedProject = computed(() => {
  const routeProjectId = route.params.projectId;
  if (
    ["project-detail", "project-task"].includes(String(route.name ?? "")) &&
    typeof routeProjectId === "string"
  ) {
    return projects.value.find((project) => project.id === routeProjectId)
      ?? (selectedProjectSnapshot.value?.id === routeProjectId ? selectedProjectSnapshot.value : null);
  }

  if (activeProjectId.value) {
    const active = projects.value.find(
      (project) => project.id === activeProjectId.value,
    );
    if (active) return active;
    if (selectedProjectSnapshot.value?.id === activeProjectId.value) {
      return selectedProjectSnapshot.value;
    }
  }
  return filteredProjects.value[0] ?? projects.value[0] ?? null;
});

watch(selectedProject, (project) => {
  if (project) selectedProjectSnapshot.value = project;
});

const aiActionProjectOptions = computed(() =>
  projects.value.map((project) => ({
    id: project.id,
    name: project.name,
    code: project.code,
    status: project.status,
    members: (project.members || []).map((member) => ({
      userId: member.userId,
      fullName: member.fullName,
    })),
  })),
);

const selectedProjectTasks = computed(() => selectedProject.value?.tasks ?? []);
const selectedTask = computed(() => {
  if (!selectedTaskId.value) return null;
  if (!selectedProjectTasks.value.length) return null;
  return (
    selectedProjectTasks.value.find(
      (task) => task.id === selectedTaskId.value,
    ) ?? null
  );
});

/**
 * What the signed-in user may do in the selected project.
 *
 * The server resolves this and returns it on the project payload, so the UI does not re-derive
 * permissions from the role string. The fallback below only covers a payload from an older server
 * that does not send `permissions` yet.
 */
const projectPermissions = computed<ProjectPermissionsDto | null>(() => {
  const project = selectedProject.value;
  const user = currentUser.value;
  if (!project || !user) return null;
  if (project.permissions) return project.permissions;

  const userId = String(user.id || "").toLowerCase();
  const member = project.members?.find(
    (m) => String(m.userId || "").toLowerCase() === userId,
  );
  return fallbackProjectPermissions({
    role: member?.role ?? null,
    isOwner: project.ownerId?.toLowerCase() === userId,
    isSystemAdmin: String(user.role || "").toLowerCase() === "admin",
  });
});

const taskCreationProject = computed(() => {
  if (selectedProject.value?.permissions?.canCreateTask) return selectedProject.value;
  return projects.value.find((project) => project.permissions?.canCreateTask) ?? null;
});

const isProjectAdmin = computed(() => projectPermissions.value?.canManageProject ?? false);

const selectedProjectMembers = computed(() => {
  const project = selectedProject.value;
  if (!project) return [];
  const members = Array.isArray(project.members) ? project.members : [];
  return members.map((member) => ({
    id: member.userId,
    fullName: member.fullName,
    role: member.role,
    email: member.email,
    initials: initials(member.fullName),
    canViewProjectTimeline: member.canViewProjectTimeline,
    canViewTaskRisk: member.canViewTaskRisk,
    canNudgeAssignee: member.canNudgeAssignee,
    canViewUnseenTaskSignal: member.canViewUnseenTaskSignal,
  }));
});

const normalizedGlobalSearchQuery = computed(() =>
  globalSearchQuery.value.trim().toLowerCase(),
);

const globalProjectResults = computed(() => {
  const query = normalizedGlobalSearchQuery.value;
  return projects.value
    .filter((project) => {
      if (!query) return true;
      const members = Array.isArray(project.members) ? project.members : [];
      return [
        project.name,
        project.code,
        project.description,
        project.ownerName,
        project.status,
        ...members.map((member) => member.fullName),
      ]
        .filter(Boolean)
        .join(" ")
        .toLowerCase()
        .includes(query);
    })
    .slice(0, 5);
});

const globalTaskResults = computed(() => {
  const query = normalizedGlobalSearchQuery.value;
  return projects.value
    .flatMap((project) =>
      (Array.isArray(project.tasks) ? project.tasks : []).map((task) => ({
        ...task,
        projectId: project.id,
        projectName: project.name,
      })),
    )
    .filter((task) => {
      if (!query) return true;
      return [
        task.title,
        task.status,
        task.priority,
        task.assigneeName,
        task.reporterName,
        task.projectName,
      ]
        .filter(Boolean)
        .join(" ")
        .toLowerCase()
        .includes(query);
    })
    .sort((left, right) => Number(isTaskOverdue(right)) - Number(isTaskOverdue(left)))
    .slice(0, 6);
});

const globalMemberResults = computed(() => {
  const query = normalizedGlobalSearchQuery.value;
  const members = new Map<string, { id: string; fullName: string; email: string; role: string; projectNames: string[] }>();
  for (const project of projects.value) {
    for (const member of project.members ?? []) {
      const existing =
        members.get(member.userId) ??
        {
          id: member.userId,
          fullName: member.fullName,
          email: member.email,
          role: member.role,
          projectNames: [],
        };
      existing.projectNames.push(project.name);
      members.set(member.userId, existing);
    }
  }

  return [...members.values()]
    .filter((member) => {
      if (!query) return true;
      return [member.fullName, member.email, member.role, ...member.projectNames]
        .join(" ")
        .toLowerCase()
        .includes(query);
    })
    .slice(0, 5);
});

const globalSearchHasResults = computed(
  () =>
    globalProjectResults.value.length > 0 ||
    globalTaskResults.value.length > 0 ||
    globalMemberResults.value.length > 0,
);

const selectedProjectStats = computed(() => {
  const project = selectedProject.value;
  if (!project)
    return {
      total: 0,
      todo: 0,
      inProgress: 0,
      inReview: 0,
      done: 0,
      overdue: 0,
      completionRate: 0,
    };
  const tasks = project.tasks.filter(
    (task) =>
      task.contributesToProgress &&
      String(task.status || "").toLowerCase() !== "cancelled",
  );
  const total = tasks.length;
  const done = tasks.filter((t) => t.status === "Done").length;
  return {
    total,
    todo: tasks.filter((t) => t.status === "Todo").length,
    inProgress: tasks.filter((t) => t.status === "InProgress").length,
    inReview: tasks.filter((t) => t.status === "InReview").length,
    done,
    overdue: tasks.filter((t) => isTaskOverdue(t)).length,
    completionRate: total > 0 ? Math.round((done / total) * 100) : 0,
  };
});

const notificationItems = computed<DashboardNotification[]>(() => {
  const realtime = notifications.value.map(toDashboardNotification);
  const dashboardItems = dashboard.value.notifications;
  const seen = new Set<string>();
  return [...realtime, ...dashboardItems]
    .filter((item) => {
      if (seen.has(item.id)) return false;
      seen.add(item.id);
      return true;
    })
    .sort(
      (left, right) =>
        new Date(right.createdAt).getTime() -
        new Date(left.createdAt).getTime(),
    );
});

const notificationCount = computed(
  () =>
    notifications.value.filter((n) => !n.isRead).length +
    dashboard.value.notifications.filter((n) => n.tone !== "info").length,
);

watch(
  filteredProjects,
  (items) => {
    if (!["dashboard", "projects"].includes(String(route.name ?? ""))) return;
    if (!items.some((p) => p.id === activeProjectId.value)) {
      activeProjectId.value = items[0]?.id ?? projects.value[0]?.id ?? null;
    }
  },
  { immediate: true },
);

watch(
  () => route.params.projectId,
  (id) => {
    if (typeof id === "string") activeProjectId.value = id;
  },
  { immediate: true },
);

watch(
  () => route.params.taskId,
  (id) => {
    if (typeof id === "string") {
      selectedTaskId.value = id;
      activeProjectTab.value = "tasks";
      return;
    }

    selectedTaskId.value = null;
  },
  { immediate: true },
);

watch(
  selectedTask,
  (task) => {
    if (task) {
      selectedTaskId.value = task.id;
    } else if (typeof route.params.taskId !== "string") {
      selectedTaskId.value = null;
    }

    if (task && !usingFallback.value) {
      if (selectedProject.value?.id)
        void markTaskViewed(selectedProject.value.id, task.id);
      void loadComments(task.id);
      void loadAttachments(task.id);
      void loadTimeEntries(task.id);
    } else {
      comments.value = [];
      attachments.value = [];
      timeEntries.value = [];
    }
  },
  { immediate: true },
);

watch(
  () => [activeProjectId.value, activeProjectTab.value],
  ([id, tab]) => {
    if (id && tab === "wiki" && !usingFallback.value)
      void loadWikiPages(String(id));
  },
  { immediate: true },
);

async function handleAiActionCompleted(projectId: string) {
  await loadDashboard();
  if (route.params.projectId === projectId) {
    activeProjectId.value = projectId;
  }
}

onMounted(async () => {
  document.addEventListener("keydown", handleDocumentSearchShortcut);
  await Promise.all([
    loadMe(),
    loadDashboard(),
    loadUsers(),
    loadNotifications(),
  ]);
  if (permissionLoadState.value !== "loaded") {
    await loadSystemPermissions();
  }
  await connectNotifications();
});

onBeforeUnmount(() => {
  document.removeEventListener("keydown", handleDocumentSearchShortcut);
});

async function loadNotifications() {
  try {
    notifications.value =
      await apiResult<NotificationDto[]>("/api/notifications");
  } catch (e) {
    console.warn(e);
  }
}

async function loadComments(taskId: string) {
  try {
    comments.value = await apiResult<CommentDto[]>(
      `/api/comments/task/${taskId}`,
    );
  } catch (e) {
    comments.value = [];
  }
}

async function loadAttachments(taskId: string) {
  try {
    attachments.value = await apiResult<AttachmentDto[]>(
      `/api/attachments/task/${taskId}`,
    );
  } catch (e) {
    attachments.value = [];
  }
}

async function loadTimeEntries(taskId: string) {
  timeEntriesError.value = "";
  try {
    const entries = await apiResult<TimeEntryDto[]>(
      `/api/tasks/${taskId}/time-entries`,
    );
    timeEntries.value = entries;
    activeTimer.value = entries.find((e) => e.endedAt === null) ?? null;
  } catch (e) {
    timeEntries.value = [];
    activeTimer.value = null;
    timeEntriesError.value = errorMessage(
      e,
      "Không thể tải dữ liệu thời gian của nhiệm vụ.",
    );
  }
}

async function markTaskViewed(projectId: string, taskId: string) {
  try {
    await apiCommand(`/api/projects/${projectId}/tasks/${taskId}/viewed`, {
      method: "POST",
    });
  } catch (e) {
    console.warn("Không thể ghi nhận nhiệm vụ đã xem.", e);
  }
}

async function startTimer(taskId: string) {
  try {
    activeTimer.value = await apiJson<TimeEntryDto>(
      `/api/tasks/${taskId}/time-entries`,
      { method: "POST" },
    );
    await loadTimeEntries(taskId);
    showSuccess("Đã bắt đầu ghi thời gian");
  } catch (e) {
    showError(errorMessage(e, "Không thể bắt đầu"));
  }
}

async function stopTimer(entryId: string) {
  try {
    await apiJson<TimeEntryDto>(`/api/time-entries/${entryId}/stop`, {
      method: "PATCH",
    });
    activeTimer.value = null;
    if (selectedTaskId.value) await loadTimeEntries(selectedTaskId.value);
    showSuccess("Đã dừng ghi thời gian");
  } catch (e) {
    showError(errorMessage(e, "Không thể dừng"));
  }
}

async function addManualTimeEntry(
  taskId: string,
  minutes: number,
  note: string,
) {
  if (!taskId || minutes <= 0) return false;
  try {
    await apiJson<TimeEntryDto>(`/api/tasks/${taskId}/time-entries/manual`, {
      method: "POST",
      body: JSON.stringify({
        taskId,
        startedAt: new Date().toISOString(),
        manualMinutes: minutes,
        note: note.trim() || null,
      }),
    });
    await loadTimeEntries(taskId);
    await loadDashboard();
    showSuccess("Thành công");
    return true;
  } catch (e) {
    showError(errorMessage(e, "Lỗi"));
    return false;
  }
}

async function connectNotifications() {
  if (notificationConnectionStarted) return;
  notificationConnectionStarted = true;
  const connection = new HubConnectionBuilder()
    .withUrl("/hubs/notification")
    .withAutomaticReconnect()
    .build();
  connection.on("notificationReceived", (n: NotificationDto) => {
    notifications.value = [
      n,
      ...notifications.value.filter((item) => item.id !== n.id),
    ];
    showInfo(n.message);
    void loadDashboard();
  });
  try {
    await connection.start();
  } catch (e) {
    console.warn(e);
  }
}

function selectProject(id: string) {
  baseSelectProject(id);
  selectedTaskId.value = null;
  activeProjectTab.value = "stats";
}

function openGlobalSearch() {
  globalSearchPreviousFocus = document.activeElement as HTMLElement | null;
  globalSearchOpen.value = true;
  notificationsOpen.value = false;
  void nextTick(() => globalSearchInput.value?.focus());
}

function closeGlobalSearch() {
  globalSearchOpen.value = false;
  void nextTick(() => globalSearchPreviousFocus?.focus());
}

function goToProjectFromSearch(projectId: string, tab = "stats") {
  activeProjectId.value = projectId;
  activeProjectTab.value = tab;
  selectedTaskId.value = null;
  closeGlobalSearch();
  void router.push({
    name: "project-detail",
    params: { projectId },
    query: tab === "stats" ? undefined : { tab },
  });
}

function goToTaskFromSearch(projectId: string, taskId: string) {
  closeGlobalSearch();
  openTask(projectId, taskId);
}

function goToMemberFromSearch(memberId: string) {
  const project = projects.value.find((item) =>
    item.members?.some((member) => member.userId === memberId),
  );
  if (project) {
    goToProjectFromSearch(project.id, "members");
  }
}

function createProjectFromSearch() {
  closeGlobalSearch();
  openCreateProject();
  void router.push("/projects");
}

function createTaskFromSearch() {
  const project = taskCreationProject.value;
  if (!project) return;
  closeGlobalSearch();
  activeProjectId.value = project.id;
  activeProjectTab.value = "tasks";
  createTaskOpen.value = true;
  void router.push({
    name: "project-detail",
    params: { projectId: project.id },
    query: { tab: "tasks" },
  });
}

function handleGlobalSearchKeydown(event: KeyboardEvent) {
  if (event.key === "Escape") {
    event.preventDefault();
    closeGlobalSearch();
    return;
  }
  if (event.key !== "Tab" || !globalSearchPanel.value) return;
  const focusable = Array.from(globalSearchPanel.value.querySelectorAll<HTMLElement>(
    'button:not([disabled]), input:not([disabled]), [href], [tabindex]:not([tabindex="-1"])',
  ));
  if (!focusable.length) return;
  const first = focusable[0];
  const last = focusable[focusable.length - 1];
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault();
    last.focus();
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault();
    first.focus();
  }
}

function handleDocumentSearchShortcut(event: KeyboardEvent) {
  const target = event.target as HTMLElement | null;
  const isTyping =
    target instanceof HTMLInputElement ||
    target instanceof HTMLTextAreaElement ||
    target?.isContentEditable;
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
    event.preventDefault();
    openGlobalSearch();
    return;
  }
  if (!isTyping && event.key === "/") {
    event.preventDefault();
    openGlobalSearch();
  }
}

function closeProjectDetails() {
  void router.push("/projects");
}
function selectTaskInProject(id: string) {
  selectedTaskId.value = id;
  const projectId = selectedProject.value?.id;
  if (!projectId) return;

  void router.push({
    name: "project-task",
    params: { projectId, taskId: id },
  });
}

function openTask(pId: string, tId: string) {
  activeProjectId.value = pId;
  selectedTaskId.value = tId;
  void router.push({
    name: "project-task",
    params: { projectId: pId, taskId: tId },
  });
}

async function createTask() {
  if (selectedProject.value) await baseCreateTask(selectedProject.value.id);
}

async function quickEditTaskTitle(taskId: string, title: string) {
  const t = title.trim();
  if (!taskId || !t) return false;
  try {
    const current = await apiResult<any>(`/api/tasks/${taskId}`);
    await apiResult<any>(`/api/tasks/${taskId}`, {
      method: "PUT",
      body: JSON.stringify({ ...current, title: t }),
    });
    await loadDashboard();
    selectedTaskId.value = taskId;
    showSuccess("Thành công");
    return true;
  } catch (e) {
    showError(errorMessage(e, "Lỗi"));
    return false;
  }
}

async function submitComment() {
  const t = selectedTask.value;
  const c = newComment.value.trim();
  if (!t || !c) return;
  try {
    await apiResult<any>("/api/comments", {
      method: "POST",
      body: JSON.stringify({ taskItemId: t.id, content: c }),
    });
    newComment.value = "";
    await loadComments(t.id);
    await loadDashboard();
    showSuccess("Thành công");
  } catch (e) {
    showError(errorMessage(e, "Lỗi"));
  }
}

async function deleteComment(id: string) {
  if (!await confirmDialog({ tone: "danger", title: "Xóa bình luận?", message: "Bình luận sẽ bị xóa khỏi nhiệm vụ.", confirmLabel: "Xóa bình luận" })) return;
  try {
    await apiCommand(`/api/comments/${id}`, { method: "DELETE" });
    if (selectedTask.value) await loadComments(selectedTask.value.id);
    await loadDashboard();
    showSuccess("Thành công");
  } catch (e) {
    showError(errorMessage(e, "Lỗi"));
  }
}

async function addMember(uId: string, role = "Member") {
  if (!selectedProject.value) return;
  try {
    await apiCommand(`/api/projects/${selectedProject.value.id}/members`, {
      method: "POST",
      body: JSON.stringify({ userId: uId, role }),
    });
    await loadDashboard();
    showSuccess("Thành công");
  } catch (e) {
    showError(errorMessage(e, "Lỗi"));
  }
}

async function removeMember(uId: string) {
  if (!selectedProject.value || !await confirmDialog({ tone: "danger", title: "Xóa thành viên khỏi dự án?", message: "Thành viên sẽ mất quyền truy cập dự án này.", confirmLabel: "Xóa thành viên" })) return;
  try {
    await apiCommand(
      `/api/projects/${selectedProject.value.id}/members/${uId}`,
      { method: "DELETE" },
    );
    await loadDashboard();
    showSuccess("Thành công");
  } catch (e) {
    showError(errorMessage(e, "Lỗi"));
  }
}

async function updateMemberRole(uId: string, role: string) {
  if (!selectedProject.value) return;
  try {
    await apiCommand(`/api/projects/${selectedProject.value.id}/members`, {
      method: "POST",
      body: JSON.stringify({ userId: uId, role }),
    });
    await loadDashboard();
    showSuccess("Thành công");
  } catch (e) {
    showError(errorMessage(e, "Lỗi"));
  }
}

async function updateMemberPermissions(
  uId: string,
  permissions: Record<string, boolean>,
) {
  if (!selectedProject.value) return;
  try {
    await apiCommand(
      `/api/projects/${selectedProject.value.id}/members/${uId}/permissions`,
      {
        method: "PATCH",
        body: JSON.stringify(permissions),
      },
    );
    await loadDashboard();
    showSuccess("Đã cập nhật quyền theo dõi timeline");
  } catch (e) {
    showError(errorMessage(e, "Không thể cập nhật quyền timeline"));
  }
}

async function loadWikiPages(id: string) {
  try {
    wikiPages.value = await apiResult<WikiPageDto[]>(
      `/api/projects/${id}/wiki`,
    );
  } catch (e) {
    wikiPages.value = [];
  }
}

async function createWikiPage(
  title: string,
  content: string = "",
  visibility = "internal",
) {
  if (!selectedProject.value || !title) return;
  try {
    await apiResult<any>(`/api/projects/${selectedProject.value.id}/wiki`, {
      method: "POST",
      body: JSON.stringify({ title, content, visibility }),
    });
    await loadWikiPages(selectedProject.value.id);
    showSuccess("Thành công");
    return true;
  } catch (e) {
    showError(errorMessage(e, "Lỗi"));
  }
  return false;
}

async function updateWikiPage(
  id: string,
  title: string,
  content: string,
  visibility = "internal",
) {
  if (!selectedProject.value || !title) return;
  try {
    await apiCommand(`/api/projects/${selectedProject.value.id}/wiki/${id}`, {
      method: "PUT",
      body: JSON.stringify({ title, content, visibility }),
    });
    await loadWikiPages(selectedProject.value.id);
    showSuccess("Thành công");
    return true;
  } catch (e) {
    showError(errorMessage(e, "Lỗi"));
  }
  return false;
}

async function deleteWikiPage(id: string) {
  if (!selectedProject.value) return;
  try {
    await apiCommand(`/api/projects/${selectedProject.value.id}/wiki/${id}`, {
      method: "DELETE",
    });
    await loadWikiPages(selectedProject.value.id);
    showSuccess("Thành công");
  } catch (e) {
    showError(errorMessage(e, "Lỗi"));
  }
}

async function uploadAttachment(e: Event) {
  const t = selectedTask.value;
  const f = (e.target as HTMLInputElement).files?.[0];
  if (!t || !f) return;
  const data = new FormData();
  data.append("file", f);
  try {
    await apiResult<any>(`/api/attachments/task/${t.id}`, {
      method: "POST",
      body: data,
    });
    await loadAttachments(t.id);
    await loadDashboard();
    showSuccess("Thành công");
  } catch (err) {
    showError(errorMessage(err, "Lỗi"));
  }
}

async function deleteAttachment(a: any) {
  try {
    await apiCommand(`/api/attachments/${a.id}`, { method: "DELETE" });
    if (selectedTask.value) await loadAttachments(selectedTask.value.id);
    await loadDashboard();
    showSuccess("Thành công");
  } catch (err) {
    showError(errorMessage(err, "Lỗi"));
  }
}

async function markAsEvidence(attachmentId: string, isEvidence: boolean) {
  try {
    await apiResult(`/api/attachments/${attachmentId}/evidence`, {
      method: "PATCH",
      body: JSON.stringify({ isEvidence }),
    });
    if (selectedTask.value) await loadAttachments(selectedTask.value.id);
    showSuccess("Thành công");
  } catch (err) {
    showError(errorMessage(err, "Lỗi"));
  }
}

async function reviewEvidence(attachmentId: string, approve: boolean, reviewNote: string) {
  try {
    await apiResult(`/api/attachments/${attachmentId}/evidence/review`, {
      method: "POST",
      body: JSON.stringify({ approve, reviewNote }),
    });
    await loadDashboard();
    if (selectedTask.value) await loadAttachments(selectedTask.value.id);
    showSuccess(approve
      ? "Đã duyệt minh chứng và tự động hoàn thành nhiệm vụ."
      : "Đã trả minh chứng để cập nhật lại.");
  } catch (err) {
    showError(errorMessage(err, "Lỗi"));
  }
}

async function dismissNotification(id: string) {
  notifications.value = notifications.value.filter((n) => n.id !== id);
  dashboard.value.notifications = dashboard.value.notifications.filter(
    (n) => n.id !== id,
  );
  if (isGuid(id))
    try {
      await apiCommand(`/api/notifications/${id}/read`, { method: "PATCH" });
    } catch (e) {
      console.warn(e);
    }
}

async function openNotification(notification: DashboardNotification) {
  await dismissNotification(notification.id);
  notificationsOpen.value = false;
  if (notification.targetUrl) await router.push(notification.targetUrl);
}

async function clearActionableNotifications() {
  notifications.value = [];
  dashboard.value.notifications = dashboard.value.notifications.filter(
    (n) => n.tone === "info",
  );
  notificationsOpen.value = false;
  try {
    await apiCommand("/api/notifications/read-all", { method: "PATCH" });
    showSuccess("Thành công");
  } catch (e) {
    console.warn(e);
  }
}

function openChatWithPrompt(prompt?: string) {
  const normalizedPrompt = prompt?.trim()
  const routeProjectId = typeof route.params.projectId === 'string' ? route.params.projectId : null
  aiAssistantOpenRequest.value = {
    id: ++aiAssistantOpenRequestId,
    view: 'chat',
    prompt: normalizedPrompt || '',
    projectId: routeProjectId,
  }
}

async function logout() {
  try {
    await apiCommand("/api/auth/logout", { method: "POST" });
  } finally {
    window.location.href = "/Account/Login";
  }
}

function tasksByStatus(status: string) {
  const query = taskSearchQuery.value.trim().toLowerCase();
  const tasks = Array.isArray(selectedProjectTasks.value)
    ? selectedProjectTasks.value
    : [];
  return tasks
    .filter(
      (t) =>
        t.status === status && (!query || t.title.toLowerCase().includes(query)),
    )
    .sort(
      (left, right) =>
        (left.sortOrder ?? 0) - (right.sortOrder ?? 0) ||
        left.title.localeCompare(right.title),
    );
}

function nextStatuses(task: { id: string }) {
  const project = projects.value.find(p => p.tasks.some(t => t.id === task.id));
  const canonicalTask = project?.tasks.find(t => t.id === task.id);
  if (!project || !canonicalTask) return [];
  return taskNextStatuses(canonicalTask, project, project.permissions, currentUser.value?.id);
}

function toDashboardNotification(n: NotificationDto): DashboardNotification {
  const tone =
    n.type === "DueDateReminder"
      ? "critical"
      : n.tone === "info" || n.tone === "warning"
        ? n.tone
        : n.tone === "success"
          ? "info"
          : n.type === "Info"
            ? "info"
            : "warning";
  return {
    id: n.id,
    title: notificationTitle(n.type),
    message: n.message,
    tone,
    createdAt: n.createdAt,
    targetUrl: n.targetUrl,
  };
}

function notificationTitle(type: string) {
  const titles: Record<string, string> = {
    GroupMeetingStarted: "Cuộc họp nhóm",
    GroupInvitationReceived: "Lời mời nhóm",
    TaskAssigned: "Nhiệm vụ mới",
    Mentioned: "Bạn được nhắc đến",
    GroupMention: "Bạn được nhắc trong nhóm",
    CommentAdded: "Bình luận mới",
    TaskStatusChanged: "Cập nhật nhiệm vụ",
    ReviewCompleted: "Duyệt minh chứng",
  };

  return titles[type] ?? type;
}

function isGuid(v: string) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(v);
}

provide(dashboardContextKey, {
  activeProjectCards,
  activeProjectId,
  activeProjectTab,
  activeTaskMenu,
  addManualTimeEntry,
  addMember,
  archivedProjectCards,
  archiveProject,
  restoreProject,
  assignedTaskCards,
  attachments,
  beginEditProject,
  beginEditTask,
  clearActionableNotifications,
  closeProjectDetails,
  comments,
  createProject,
  createProjectOpen,
  createTask,
  createTaskOpen,
  taskBeingEdited,
  cancelTaskForm,
  currentUser,
  deleteAttachment,
  deleteComment,
  deleteProject,
  deleteTask,
  displayRole,
  displayStatus,
  editProjectDescription,
  editProjectName,
  filteredProjects,
  formatDate,
  formatFileSize,
  formatTime,
  isLoading,
  loadError,
  isProjectAdmin,
  projectPermissions,
  isTaskOverdue,
  logout,
  moveTask,
  moveTaskOnKanban,
  newComment,
  newTaskAssigneeId,
  newTaskDescription,
  newTaskDueDate,
  newTaskIsPrivate,
  newTaskIsPinned,
  newTaskContributesToProgress,
  newTaskPriority,
  newTaskTitle,
  nextStatuses,
  openChatWithPrompt,
  openCreateProject,
  openTask,
  priorities,
  markAsEvidence,
  reviewEvidence,
  projectBeingEditedId,
  projectCards,
  projectDescription,
  projectEndDate,
  projectFilter,
  projectName,
  projectSort,
  projects,
  quickEditTaskTitle,
  removeMember,
  saveProjectEdit,
  searchQuery,
  selectProject,
  selectedProject,
  selectedProjectMembers,
  selectedProjectStats,
  selectedTask,
  selectedTaskId,
  selectTaskInProject,
  statusColumns,
  statusTone,
  submitComment,
  summaryCards,
  tabs,
  tasksByStatus,
  team,
  toggleTaskMenu,
  updateMemberRole,
  updateMemberPermissions,
  uploadAttachment,
  users,
  wikiPages,
  loadWikiPages,
  createWikiPage,
  updateWikiPage,
  deleteWikiPage,
  taskSearchQuery,
  taskBeingQuickEditedId,
  timeEntries,
  timeEntriesError,
  activeTimer,
  startTimer,
  stopTimer,
  loadTimeEntries,
  loadDashboard,
});
</script>

<template>
  <AppShell
    :nav-items="navigation"
    :notification-count="notificationCount"
    :notifications-open="notificationsOpen"
    :user-name="currentUser?.fullName || currentUser?.email || 'Qaly user'"
    :user-initials="
      initials(currentUser?.fullName || currentUser?.email || 'QU')
    "
    :user-role="currentUser?.role ?? null"
    :user-avatar-url="currentUser?.avatarUrl ?? null"
    :user-loading="!currentUserLoaded"
    :can-access-archived-projects="canAccessModule('Projects')"
    :can-access-settings="canAccessModule('Settings')"
    :can-start-simulation="String(currentUser?.role || '').toLowerCase() === 'admin'"
    :simulation-users="users.map(user => ({ id: user.id, fullName: user.fullName, role: user.systemRole || 'Member' }))"
    @notifications="notificationsOpen = !notificationsOpen"
    @assistant="openChatWithPrompt()"
    @search="openGlobalSearch"
    @logout="logout"
  >
    <ConfirmDialogHost />
    <RouterView />

    <Teleport to="body">
      <div
        v-if="globalSearchOpen"
        class="global-search-backdrop"
        @click.self="closeGlobalSearch"
        @keydown="handleGlobalSearchKeydown"
      >
        <section ref="globalSearchPanel" class="global-search-panel" role="dialog" aria-modal="true" aria-label="Tìm kiếm">
          <div class="global-search-input">
            <Search :size="22" />
            <input
              ref="globalSearchInput"
              v-model="globalSearchQuery"
              type="search"
              aria-label="Tìm dự án, nhiệm vụ hoặc thành viên"
              placeholder="Tìm dự án, nhiệm vụ, thành viên..."
            />
            <button type="button" aria-label="Đóng tìm kiếm" @click="closeGlobalSearch">
              <X :size="18" />
            </button>
          </div>

          <div class="global-search-shortcuts">
            <button type="button" @click="createProjectFromSearch">
              <Plus :size="15" />
              Tạo dự án
            </button>
            <button v-if="taskCreationProject" type="button" @click="createTaskFromSearch">
              <Plus :size="15" />
              Tạo nhiệm vụ
            </button>
            <span>Ctrl K hoặc / để mở nhanh</span>
          </div>

          <div class="global-search-results">
            <div v-if="!globalSearchHasResults" class="global-search-empty">
              Không tìm thấy kết quả phù hợp.
            </div>

            <section v-if="globalProjectResults.length" class="global-search-group">
              <h3><FolderKanban :size="16" /> Dự án</h3>
              <button
                v-for="project in globalProjectResults"
                :key="`project-${project.id}`"
                type="button"
                class="global-search-item"
                @click="goToProjectFromSearch(project.id)"
              >
                <span class="global-search-item__icon">{{ initials(project.name) }}</span>
                <span class="global-search-item__body">
                  <strong>{{ project.name }}</strong>
                  <small>{{ project.ownerName }} · {{ project.taskCount }} nhiệm vụ · {{ project.progressPercentage }}%</small>
                </span>
                <span class="global-search-chip">{{ displayStatus(project.status) }}</span>
              </button>
            </section>

            <section v-if="globalTaskResults.length" class="global-search-group">
              <h3><ClipboardList :size="16" /> Nhiệm vụ</h3>
              <button
                v-for="task in globalTaskResults"
                :key="`task-${task.id}`"
                type="button"
                class="global-search-item"
                @click="goToTaskFromSearch(task.projectId, task.id)"
              >
                <span class="global-search-item__icon global-search-item__icon--task">{{ task.priority.charAt(0) }}</span>
                <span class="global-search-item__body">
                  <strong>{{ task.title }}</strong>
                  <small>{{ task.projectName }} · {{ displayStatus(task.status) }} · {{ task.assigneeName || 'Chưa giao' }}</small>
                </span>
                <span class="global-search-chip" :class="{ 'is-danger': isTaskOverdue(task) }">
                  {{ isTaskOverdue(task) ? 'Quá hạn' : formatDate(task.dueDate) }}
                </span>
              </button>
            </section>

            <section v-if="globalMemberResults.length" class="global-search-group">
              <h3><Users :size="16" /> Thành viên</h3>
              <button
                v-for="member in globalMemberResults"
                :key="`member-${member.id}`"
                type="button"
                class="global-search-item"
                @click="goToMemberFromSearch(member.id)"
              >
                <span class="global-search-item__icon global-search-item__icon--member">{{ initials(member.fullName) }}</span>
                <span class="global-search-item__body">
                  <strong>{{ member.fullName }}</strong>
                  <small>{{ member.email }} · {{ member.projectNames.slice(0, 2).join(', ') }}</small>
                </span>
                <span class="global-search-chip">{{ member.role }}</span>
              </button>
            </section>
          </div>
        </section>
      </div>
    </Teleport>

    <div
      v-if="notificationsOpen"
      id="notification-panel"
      class="notification-popover glass-card home-notification-popover"
      role="region"
      aria-label="Danh sách thông báo"
    >
      <div class="panel-heading">
        <div>
          <span>Thông báo</span>
          <h2>Tín hiệu hiện tại</h2>
        </div>
        <div class="popover-actions">
          <button
            class="text-button"
            type="button"
            @click="clearActionableNotifications"
          >
            Đánh dấu đã đọc
          </button>
          <button
            class="icon-button icon-button--small"
            type="button"
            aria-label="Đóng danh sách thông báo"
            @click="notificationsOpen = false"
          >
            <X :size="16" />
          </button>
        </div>
      </div>
      <article
        v-for="notification in notificationItems.slice(0, 6)"
        :key="notification.id"
        :class="`notice notice--${notification.tone}`"
        :role="notification.targetUrl ? 'link' : undefined"
        :tabindex="notification.targetUrl ? 0 : undefined"
        @click="notification.targetUrl && openNotification(notification)"
        @keydown.enter="notification.targetUrl && openNotification(notification)"
        @keydown.space.prevent="notification.targetUrl && openNotification(notification)"
      >
        <div class="notice__top">
          <strong>{{ notification.title }}</strong>
          <button
            type="button"
            aria-label="Đóng thông báo"
            @click.stop="dismissNotification(notification.id)"
          >
            <X :size="14" />
          </button>
        </div>
        <p>{{ notification.message }}</p>
        <span>{{ formatTime(notification.createdAt) }}</span>
      </article>
    </div>

    <WelcomeOverlay />
    <template #overlays>
      <FloatingChatbot
        :project-id="typeof route.params.projectId === 'string' ? route.params.projectId : null"
        :projects="aiActionProjectOptions"
        :open-request="aiAssistantOpenRequest"
        :conversation-runtime-enabled="route.name !== 'analytics'"
        @completed="handleAiActionCompleted"
      />
    </template>
  </AppShell>
</template>

<style scoped>
.global-search-backdrop {
  position: fixed;
  inset: 0;
  z-index: 80;
  display: flex;
  justify-content: center;
  align-items: flex-start;
  padding: 88px 20px 24px;
  background: rgba(15, 23, 42, 0.28);
  backdrop-filter: none;
}

.global-search-panel {
  width: min(760px, 100%);
  max-height: min(760px, calc(100vh - 120px));
  overflow: hidden;
  display: grid;
  grid-template-rows: auto auto minmax(0, 1fr);
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
  background: var(--panel);
  color: var(--text);
  box-shadow: var(--shadow-card);
}

.global-search-input {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 12px;
  padding: 16px 18px;
  border-bottom: 1px solid var(--line);
  color: var(--muted);
}

.global-search-input input {
  width: 100%;
  border: 0;
  outline: 0;
  color: var(--text-strong);
  background: transparent;
  font-size: 18px;
  font-weight: 700;
}

.global-search-input button {
  width: 34px;
  height: 34px;
  display: grid;
  place-items: center;
  border: 1px solid var(--border-strong);
  border-radius: var(--qaly-radius-lg);
  color: var(--text);
  background: var(--panel-soft);
  cursor: pointer;
}

.global-search-shortcuts {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px 18px;
  border-bottom: 1px solid var(--line);
  background: var(--panel-soft);
}

.global-search-shortcuts button {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  border: 1px solid #bfdbfe;
  border-radius: var(--qaly-radius-lg);
  padding: 8px 12px;
  color: #0f52ba;
  background: #eff6ff;
  font-weight: 800;
  cursor: pointer;
}

.global-search-shortcuts span {
  margin-left: auto;
  color: var(--muted);
  font-size: 12px;
  font-weight: 700;
}

.global-search-results {
  overflow-y: auto;
  padding: 14px;
  display: grid;
  gap: 14px;
}

.global-search-group {
  display: grid;
  gap: 8px;
}

.global-search-group h3 {
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 4px 4px 2px;
  color: var(--muted);
  font-size: 12px;
  font-weight: 900;
  text-transform: uppercase;
}

.global-search-item {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 12px;
  width: 100%;
  border: 1px solid transparent;
  border-radius: var(--qaly-radius-lg);
  padding: 12px;
  background: transparent;
  text-align: left;
  cursor: pointer;
}

.global-search-item:hover,
.global-search-item:focus-visible {
  border-color: #bfdbfe;
  background: var(--surface-hover);
  outline: none;
}

.global-search-item__icon {
  width: 42px;
  height: 42px;
  display: grid;
  place-items: center;
  border-radius: var(--qaly-radius-lg);
  color: #ffffff;
  background: #0f52ba;
  font-size: 12px;
  font-weight: 900;
}

.global-search-item__icon--task {
  background: #0891b2;
}

.global-search-item__icon--member {
  background: #4f46e5;
}

.global-search-item__body {
  min-width: 0;
  display: grid;
  gap: 3px;
}

.global-search-item__body strong {
  overflow: hidden;
  color: var(--text-strong);
  font-size: 14px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.global-search-item__body small {
  overflow: hidden;
  color: var(--muted);
  font-size: 12px;
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.global-search-chip {
  border-radius: 999px;
  padding: 6px 9px;
  color: #1d4ed8;
  background: #eff6ff;
  font-size: 11px;
  font-weight: 900;
  white-space: nowrap;
}

.global-search-chip.is-danger {
  color: #b91c1c;
  background: #fee2e2;
}

.global-search-empty {
  padding: 34px 18px;
  border: 1px dashed var(--border-strong);
  border-radius: var(--qaly-radius-lg);
  color: var(--muted);
  background: var(--panel-soft);
  text-align: center;
  font-weight: 700;
}

@media (max-width: 720px) {
  .global-search-backdrop {
    padding: 72px 12px 16px;
  }

  .global-search-shortcuts {
    flex-wrap: wrap;
  }

  .global-search-shortcuts span {
    width: 100%;
    margin-left: 0;
  }

  .global-search-item {
    grid-template-columns: auto minmax(0, 1fr);
  }

  .global-search-chip {
    grid-column: 2;
    justify-self: flex-start;
  }
}
</style>
