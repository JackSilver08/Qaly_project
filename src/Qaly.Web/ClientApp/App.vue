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
} from "lucide-vue-next";
import AppShell from "./components/AppShell.vue";
import WelcomeOverlay from "./components/WelcomeOverlay.vue";
import { dashboardContextKey } from "./composables/dashboard-context";
import { showError, showInfo, showSuccess } from "./composables/use-toast";
import { useDashboard } from "./composables/use-dashboard-state";
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
  WikiPageDto,
  TimeEntryDto,
} from "./types";

const {
  dashboard,
  currentUser,
  users,
  isLoading,
  usingFallback,
  projects,
  team,
  summaryCards,
  loadDashboard,
  loadMe,
  loadUsers,
} = useDashboard();

const activeProjectId = ref<string | null>(null);
const selectedTaskId = ref<string | null>(null);

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
} = useProjectActions(projects, activeProjectId, loadDashboard);

const {
  createTaskOpen,
  taskBeingEdited,
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
} = useTaskActions(selectedTaskId, loadDashboard);

const navigation: ShellNavItem[] = [
  { label: "Tổng quan", to: "/dashboard", icon: LayoutDashboard },
  { label: "Dự án", to: "/projects", icon: FolderKanban },
  { label: "Nhiệm vụ", to: "/tasks", icon: ClipboardList },
  { label: "Nhóm", to: "/teams", icon: Users },
  { label: "Phân tích", to: "/analytics", icon: BarChart3 },
];

const statusColumns = ["Todo", "InProgress", "OnHold", "InReview", "Done"];
const priorities = ["Low", "Medium", "High", "Critical"];

const notifications = ref<NotificationDto[]>([]);
const comments = ref<CommentDto[]>([]);
const attachments = ref<AttachmentDto[]>([]);
const wikiPages = ref<WikiPageDto[]>([]);
const timeEntries = ref<TimeEntryDto[]>([]);
const activeTimer = ref<TimeEntryDto | null>(null);

const notificationsOpen = ref(false);
const globalSearchOpen = ref(false);
const globalSearchQuery = ref("");
const globalSearchInput = ref<HTMLInputElement | null>(null);
const taskSearchQuery = ref("");
const taskBeingQuickEditedId = ref<string | null>(null);
const activeTaskMenu = ref<string | null>(null);

function toggleTaskMenu(taskId: string) {
  activeTaskMenu.value = activeTaskMenu.value === taskId ? null : taskId;
}
const searchQuery = ref("");
const projectFilter = ref<"all" | "active" | "planned" | "at-risk">("all");
const projectSort = ref<"recent" | "risk" | "progress" | "name">("recent");
const activeProjectTab = ref("stats");

const tabs = [
  { id: "stats", label: "Thống kê" },
  { id: "tasks", label: "Nhiệm vụ" },
  { id: "gantt", label: "Sprint & Timeline" },
  { id: "members", label: "Thành viên" },
  { id: "wiki", label: "Wiki" },
  { id: "webhooks", label: "Webhooks" },
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
  if (activeProjectId.value) {
    const active = projects.value.find(
      (project) => project.id === activeProjectId.value,
    );
    if (active) return active;
  }
  return filteredProjects.value[0] ?? projects.value[0] ?? null;
});

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

const isProjectAdmin = computed(() => {
  const project = selectedProject.value;
  const user = currentUser.value;
  if (!project || !user) return false;
  const userRole = String(user.role || "").toLowerCase();
  if (userRole === "admin" || user.email === "admin@qaly.dev") return true;
  const userId = String(user.id || "").toLowerCase();
  if (project.ownerId?.toLowerCase() === userId) return true;
  const member = project.members?.find(
    (m) => String(m.userId || "").toLowerCase() === userId,
  );
  return member
    ? ["owner", "manager"].includes(String(member.role || "").toLowerCase())
    : false;
});

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
  const tasks = project.tasks;
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

    if (["project-detail", "projects", "dashboard"].includes(String(route.name ?? ""))) {
      selectedTaskId.value = null;
    }
  },
  { immediate: true },
);

watch(
  selectedTask,
  (task) => {
    selectedTaskId.value = task?.id ?? null;
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

onMounted(async () => {
  document.addEventListener("keydown", handleDocumentSearchShortcut);
  await Promise.all([
    loadMe(),
    loadDashboard(),
    loadUsers(),
    loadNotifications(),
  ]);
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
  try {
    const entries = await apiJson<TimeEntryDto[]>(
      `/api/tasks/${taskId}/time-entries`,
    );
    timeEntries.value = entries;
    activeTimer.value = entries.find((e) => e.endedAt === null) ?? null;
  } catch (e) {
    timeEntries.value = [];
    activeTimer.value = null;
  }
}

async function markTaskViewed(projectId: string, taskId: string) {
  try {
    await apiCommand(`/api/projects/${projectId}/tasks/${taskId}/viewed`, {
      method: "POST",
    });
  } catch (e) {
    console.warn("Khong the ghi nhan task da xem.", e);
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
  globalSearchOpen.value = true;
  notificationsOpen.value = false;
  void nextTick(() => globalSearchInput.value?.focus());
}

function closeGlobalSearch() {
  globalSearchOpen.value = false;
}

function goToProjectFromSearch(projectId: string, tab = "stats") {
  activeProjectId.value = projectId;
  activeProjectTab.value = tab;
  selectedTaskId.value = null;
  closeGlobalSearch();
  void router.push(`/projects/${projectId}`);
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
  closeGlobalSearch();
  if (!selectedProject.value && projects.value[0]) {
    activeProjectId.value = projects.value[0].id;
  }
  activeProjectTab.value = "tasks";
  createTaskOpen.value = true;
  if (selectedProject.value?.id) {
    void router.push(`/projects/${selectedProject.value.id}`);
  }
}

function handleGlobalSearchKeydown(event: KeyboardEvent) {
  if (event.key === "Escape") {
    closeGlobalSearch();
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
  activeProjectTab.value = "tasks";
  if (selectedProject.value?.id)
    void router.push(`/projects/${selectedProject.value.id}/tasks/${id}`);
}

function openTask(pId: string, tId: string) {
  activeProjectId.value = pId;
  selectedTaskId.value = tId;
  activeProjectTab.value = "tasks";
  void router.push(`/projects/${pId}/tasks/${tId}`);
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
  if (!confirm("Xóa?")) return;
  try {
    await apiCommand(`/api/comments/${id}`, { method: "DELETE" });
    if (selectedTask.value) await loadComments(selectedTask.value.id);
    await loadDashboard();
    showSuccess("Thành công");
  } catch (e) {
    showError(errorMessage(e, "Lỗi"));
  }
}

async function addMember(uId: string) {
  if (!selectedProject.value) return;
  try {
    await apiCommand(`/api/projects/${selectedProject.value.id}/members`, {
      method: "POST",
      body: JSON.stringify({ userId: uId, role: "Member" }),
    });
    await loadDashboard();
    showSuccess("Thành công");
  } catch (e) {
    showError(errorMessage(e, "Lỗi"));
  }
}

async function removeMember(uId: string) {
  if (!selectedProject.value || !confirm("Xóa?")) return;
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
    if (selectedTask.value) await loadAttachments(selectedTask.value.id);
    showSuccess("Thành công");
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
  void router.push('/analytics')
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

function nextStatuses(status: string) {
  const allowedTransitions: Record<string, string[]> = {
    Todo: ["InProgress", "OnHold"],
    InProgress: ["InReview", "OnHold", "Done"],
    InReview: ["InProgress", "Done", "OnHold"],
    OnHold: ["Todo", "InProgress"],
    Done: ["InReview"],
  };

  return (allowedTransitions[status] ?? []).filter((item) =>
    statusColumns.includes(item),
  );
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
    title: n.type,
    message: n.message,
    tone,
    createdAt: n.createdAt,
  };
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
  isProjectAdmin,
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
    :user-name="currentUser?.fullName || currentUser?.email || 'Qaly user'"
    :user-initials="
      initials(currentUser?.fullName || currentUser?.email || 'QU')
    "
    @notifications="notificationsOpen = !notificationsOpen"
    @assistant="openChatWithPrompt()"
    @search="openGlobalSearch"
    @logout="logout"
  >
    <RouterView />

    <Teleport to="body">
      <div
        v-if="globalSearchOpen"
        class="global-search-backdrop"
        @click.self="closeGlobalSearch"
        @keydown="handleGlobalSearchKeydown"
      >
        <section class="global-search-panel" role="dialog" aria-modal="true" aria-label="Tìm kiếm">
          <div class="global-search-input">
            <Search :size="22" />
            <input
              ref="globalSearchInput"
              v-model="globalSearchQuery"
              type="search"
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
            <button type="button" @click="createTaskFromSearch">
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
      class="notification-popover glass-card home-notification-popover"
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
      >
        <div class="notice__top">
          <strong>{{ notification.title }}</strong>
          <button
            type="button"
            aria-label="Đóng thông báo"
            @click="dismissNotification(notification.id)"
          >
            <X :size="14" />
          </button>
        </div>
        <p>{{ notification.message }}</p>
        <span>{{ formatTime(notification.createdAt) }}</span>
      </article>
    </div>

    <WelcomeOverlay />
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
  backdrop-filter: blur(8px);
}

.global-search-panel {
  width: min(760px, 100%);
  max-height: min(760px, calc(100vh - 120px));
  overflow: hidden;
  display: grid;
  grid-template-rows: auto auto minmax(0, 1fr);
  border: 1px solid rgba(203, 213, 225, 0.92);
  border-radius: 18px;
  background: #ffffff;
  box-shadow: 0 30px 80px rgba(15, 23, 42, 0.22);
}

.global-search-input {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 12px;
  padding: 16px 18px;
  border-bottom: 1px solid #e2e8f0;
  color: #64748b;
}

.global-search-input input {
  width: 100%;
  border: 0;
  outline: 0;
  color: #0f172a;
  background: transparent;
  font-size: 18px;
  font-weight: 700;
}

.global-search-input button {
  width: 34px;
  height: 34px;
  display: grid;
  place-items: center;
  border: 1px solid #cbd5e1;
  border-radius: 10px;
  color: #475569;
  background: #f8fafc;
  cursor: pointer;
}

.global-search-shortcuts {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px 18px;
  border-bottom: 1px solid #e2e8f0;
  background: #f8fafc;
}

.global-search-shortcuts button {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  border: 1px solid #bfdbfe;
  border-radius: 10px;
  padding: 8px 12px;
  color: #0f52ba;
  background: #eff6ff;
  font-weight: 800;
  cursor: pointer;
}

.global-search-shortcuts span {
  margin-left: auto;
  color: #64748b;
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
  color: #475569;
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
  border-radius: 14px;
  padding: 12px;
  background: transparent;
  text-align: left;
  cursor: pointer;
}

.global-search-item:hover,
.global-search-item:focus-visible {
  border-color: #bfdbfe;
  background: #f8fafc;
  outline: none;
}

.global-search-item__icon {
  width: 42px;
  height: 42px;
  display: grid;
  place-items: center;
  border-radius: 12px;
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
  color: #0f172a;
  font-size: 14px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.global-search-item__body small {
  overflow: hidden;
  color: #64748b;
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
  color: #dc2626;
  background: #fee2e2;
}

.global-search-empty {
  padding: 34px 18px;
  border: 1px dashed #cbd5e1;
  border-radius: 14px;
  color: #64748b;
  background: #f8fafc;
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
