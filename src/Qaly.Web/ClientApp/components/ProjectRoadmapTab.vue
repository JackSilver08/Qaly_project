<script setup lang="ts">
import {
  ref,
  computed,
  onMounted,
  onBeforeUnmount,
  watch,
  nextTick,
} from "vue";
import {
  Compass,
  CheckCircle2,
  Clock,
  Plus,
  Sparkles,
  CalendarDays,
  Calendar,
  Edit3,
  Trash2,
  FolderKanban,
  Navigation,
  AlertTriangle,
  Layers,
  Flag,
  X,
  Check,
  Eye,
  Link as LinkIcon,
  UserCheck,
  ShieldCheck,
  User,
  Shield,
  RefreshCw,
  ChevronRight,
  PlayCircle,
  Lock,
  ListTodo,
  CheckSquare,
  Users,
  Award,
  Zap,
  LayoutGrid,
  Search,
  Filter,
  ArrowRight,
  TrendingUp,
  Target,
} from "lucide-vue-next";
import { apiResult, apiCommand } from "../utils/api-client";
import { showError, showSuccess } from "../composables/use-toast";
import { useDashboardContext } from "../composables/dashboard-context";
import type { SprintDto, DashboardTask, GanttTaskDto } from "../types";
import ProjectProgressAiCard from "./ProjectProgressAiCard.vue";
import { useRoute, useRouter } from "vue-router";

type RoadmapTask = DashboardTask & {
  startDate?: string | null;
  endDate?: string | null;
  progress?: number | null;
};

const props = defineProps<{
  projectId: string;
  projectName?: string;
  canGenerateAi: boolean;
}>();

const {
  selectedProject,
  activeProjectTab,
  isProjectAdmin,
  loadDashboard,
  selectTaskInProject,
} = useDashboardContext();
const route = useRoute();
const router = useRouter();

// Sprint list & loading state
const sprints = ref<SprintDto[]>([]);
const isLoading = ref(false);
const selectedSprintId = ref<string | null>(null);

// Roadmap and Timeline View mode
const viewMode = ref<"journey" | "timeline">("journey");

// View modes: Executive Client View vs Management View
const isClientViewMode = ref(false);

// Node Filter & Search
const milestoneFilter = ref<"all" | "active" | "completed" | "overdue">("all");
const milestoneSearchQuery = ref("");

// Modals state
const showCreateModal = ref(false);
const showEditModal = ref(false);
const showPresetModal = ref(false);
const showTaskAssignModal = ref(false);
const showQuickCreateTaskModal = ref(false);
const confirmation = ref<{
  type: "complete" | "delete";
  sprint: SprintDto;
} | null>(null);

// Form states for milestone creation/editing
const milestoneName = ref("");
const milestoneStartDate = ref("");
const milestoneEndDate = ref("");
const milestoneGoal = ref("");
const milestoneStatus = ref("Planning");
const editingSprintId = ref<string | null>(null);

// Form states for Quick Task Creation in milestone
const quickTaskTitle = ref("");
const quickTaskPriority = ref("Medium");
const quickTaskAssigneeId = ref("");
const quickTaskDueDate = ref("");

// Selected tasks IDs for Task Assignment Modal
const selectedTaskIdsForSprint = ref<string[]>([]);
const isSavingTaskAssignment = ref(false);
const isGeneratingPreset = ref(false);
const milestoneTitleInput = ref<HTMLInputElement | null>(null);
const quickTaskTitleInput = ref<HTMLInputElement | null>(null);
const activeModalRoot = ref<HTMLElement | null>(null);
const confirmationModalRoot = ref<HTMLElement | null>(null);

// Task status filter inside milestone detail
const milestoneTaskSearch = ref("");
const milestoneTaskStatusFilter = ref<string>("all");

const roadmapTasks = computed<RoadmapTask[]>(() =>
  (selectedProject.value?.tasks || []) as RoadmapTask[],
);
const ganttTasks = ref<GanttTaskDto[]>([]);

type TimelineTask = RoadmapTask | GanttTaskDto;

function taskStartDate(task: TimelineTask) {
  return task.startDate ?? ("dueDate" in task ? task.dueDate : null);
}

function taskEndDate(task: TimelineTask) {
  return (
    task.endDate ??
    ("dueDate" in task ? task.dueDate : null) ??
    task.startDate
  );
}

const timelineTasks = computed<TimelineTask[]>(() => {
  const ganttWithDates = ganttTasks.value.filter(
    (task) => task.startDate || task.endDate,
  );
  const source: TimelineTask[] = ganttWithDates.length
    ? ganttWithDates
    : roadmapTasks.value;
  return source
    .filter((task) => taskStartDate(task) || taskEndDate(task))
    .slice()
    .sort((a, b) => {
      const aDate =
        toValidTimestamp(taskStartDate(a)) ??
        toValidTimestamp(taskEndDate(a)) ??
        0;
      const bDate =
        toValidTimestamp(taskStartDate(b)) ??
        toValidTimestamp(taskEndDate(b)) ??
        0;
      return aDate - bDate || a.title.localeCompare(b.title);
    });
});

function toValidTimestamp(value: string | null | undefined) {
  if (!value) return null;
  const time = new Date(value).getTime();
  return Number.isFinite(time) ? time : null;
}

const timelineBounds = computed(() => {
  const points = timelineTasks.value
    .flatMap((task) => [
      toValidTimestamp(taskStartDate(task)),
      toValidTimestamp(taskEndDate(task)),
    ])
    .filter((time): time is number => time !== null);

  if (!points.length) {
    const now = Date.now();
    return { min: new Date(now), max: new Date(now + 7 * 24 * 60 * 60 * 1000) };
  }

  const min = Math.min(...points);
  const max = Math.max(...points);
  return {
    min: new Date(min),
    max: new Date(max >= min ? max : min + 7 * 24 * 60 * 60 * 1000),
  };
});

const minDate = computed(() => timelineBounds.value.min);
const maxDate = computed(() => timelineBounds.value.max);
const totalDays = computed(() => {
  const DAY_MS = 24 * 60 * 60 * 1000;
  const spanDays =
    Math.ceil((maxDate.value.getTime() - minDate.value.getTime()) / DAY_MS) + 3;
  return Math.max(1, Math.min(366, spanDays));
});

const timelineDays = computed(() => {
  const DAY_MS = 24 * 60 * 60 * 1000;
  return Array.from({ length: totalDays.value }, (_, index) => {
    const date = new Date(minDate.value.getTime() + index * DAY_MS);
    return {
      key: date.toISOString(),
      day: date.getDate(),
      weekday: new Intl.DateTimeFormat("vi-VN", { weekday: "short" }).format(
        date,
      ),
      month:
        date.getDate() === 1 || index === 0
          ? new Intl.DateTimeFormat("vi-VN", { month: "short" }).format(date)
          : "",
      isWeekend: date.getDay() === 0 || date.getDay() === 6,
      isToday: new Date().toDateString() === date.toDateString(),
    };
  });
});

const activeModalKind = computed(() => {
  if (showCreateModal.value || showEditModal.value) return "milestone";
  if (showTaskAssignModal.value) return "taskAssign";
  if (showQuickCreateTaskModal.value) return "quickTask";
  return null;
});

const modalFocusableSelector = [
  'button:not([disabled])',
  'input:not([disabled]):not([type="hidden"])',
  "select:not([disabled])",
  "textarea:not([disabled])",
  '[tabindex]:not([tabindex="-1"])',
].join(",");

function focusActiveModal() {
  const root = confirmation.value
    ? confirmationModalRoot.value
    : activeModalRoot.value;
  if (!root) return;

  const preferred = root.querySelector<HTMLElement>(
    '[data-modal-initial-focus="true"], [autofocus]',
  );
  const firstFocusable =
    preferred || root.querySelector<HTMLElement>(modalFocusableSelector) || root;

  firstFocusable.focus?.();
}

function closeMilestoneModal() {
  showCreateModal.value = false;
  showEditModal.value = false;
  resetMilestoneForm();
}

function closeTaskAssignModal() {
  showTaskAssignModal.value = false;
}

function closeQuickCreateTaskModal() {
  showQuickCreateTaskModal.value = false;
}

function closeActiveModal() {
  if (showCreateModal.value || showEditModal.value) {
    closeMilestoneModal();
    return;
  }
  if (showTaskAssignModal.value) {
    closeTaskAssignModal();
    return;
  }
  if (showQuickCreateTaskModal.value) {
    closeQuickCreateTaskModal();
    return;
  }
  confirmation.value = null;
}

function handleModalKeydown(event: KeyboardEvent) {
  if (!activeModalKind.value && !confirmation.value) return;

  if (event.key === "Escape") {
    event.preventDefault();
    closeActiveModal();
    return;
  }

  if (event.key !== "Tab") return;

  const root = confirmation.value
    ? confirmationModalRoot.value
    : activeModalRoot.value;
  if (!root) return;

  const focusables = Array.from(
    root.querySelectorAll<HTMLElement>(modalFocusableSelector),
  ).filter((el) => !el.hasAttribute("disabled") && el.offsetParent !== null);

  if (focusables.length === 0) {
    event.preventDefault();
    root.focus?.();
    return;
  }

  const currentIndex = focusables.indexOf(document.activeElement as HTMLElement);
  const lastIndex = focusables.length - 1;

  if (event.shiftKey) {
    if (currentIndex <= 0) {
      event.preventDefault();
      focusables[lastIndex].focus();
    }
    return;
  }

  if (currentIndex === -1 || currentIndex === lastIndex) {
    event.preventDefault();
    focusables[0].focus();
  }
}

watch(
  activeModalKind,
  async (kind) => {
    if (!kind) return;
    await nextTick();
    focusActiveModal();
  },
  { flush: "post" },
);

watch(
  confirmation,
  async (value) => {
    if (!value) return;
    await nextTick();
    const focusTarget =
      confirmationModalRoot.value?.querySelector<HTMLElement>("button") ?? null;
    focusTarget?.focus();
  },
  { flush: "post" },
);

function getTaskStyle(task: TimelineTask) {
  const startTime =
    toValidTimestamp(taskStartDate(task)) ??
    toValidTimestamp(taskEndDate(task));
  const endTime = toValidTimestamp(taskEndDate(task)) ?? startTime;
  if (startTime === null || endTime === null) return { display: "none" };

  const normalizedEnd = Math.max(startTime, endTime);
  const DAY_MS = 24 * 60 * 60 * 1000;
  const left = Math.floor((startTime - minDate.value.getTime()) / DAY_MS) + 1;
  const width = Math.ceil((normalizedEnd - startTime) / DAY_MS) + 1;
  const startColumn = Math.max(1, left);
  const span = Math.max(1, Math.min(width, totalDays.value - startColumn + 1));
  return { gridColumn: `${startColumn} / span ${span}` };
}

function taskTone(task: TimelineTask) {
  const now = Date.now();
  const end = toValidTimestamp(taskEndDate(task));
  if (end !== null && task.status !== "Done" && now > end) return "is-overdue";
  if (
    task.status === "Todo" &&
    taskStartDate(task) &&
    toValidTimestamp(taskStartDate(task))! > now
  )
    return "is-stale";
  if (
    end !== null &&
    task.status !== "Done" &&
    end - now <= 3 * 24 * 60 * 60 * 1000
  )
    return "is-due";
  return "is-normal";
}

onMounted(() => {
  loadSprints();
  window.addEventListener("keydown", handleModalKeydown);
});

onBeforeUnmount(() => {
  window.removeEventListener("keydown", handleModalKeydown);
});

watch(
  () => props.projectId,
  () => {
    selectedSprintId.value = null;
    loadSprints();
  },
);

watch(selectedSprintId, (sprintId) => {
  if (!sprintId) return;
  const hash = `#milestone-${sprintId}`;
  if (route.hash !== hash) void router.replace({ hash });
});

async function loadSprints() {
  isLoading.value = true;
  try {
    const [result, ganttResult] = await Promise.all([
      apiResult<SprintDto[]>(`/api/projects/${props.projectId}/sprints`),
      apiResult<GanttTaskDto[]>(`/api/tasks/project/${props.projectId}/gantt`).catch(
        () => [] as GanttTaskDto[],
      ),
    ]);
    ganttTasks.value = ganttResult || [];
    // Sort sprints chronologically by StartDate
    sprints.value = (result || []).sort(
      (a, b) =>
        new Date(a.startDate).getTime() - new Date(b.startDate).getTime(),
    );

    const requestedSprintId = route.hash.startsWith("#milestone-")
      ? route.hash.slice("#milestone-".length)
      : null;
    if (
      sprints.value.length > 0 &&
      (!selectedSprintId.value ||
        !sprints.value.some((s) => s.id === selectedSprintId.value))
    ) {
      const current =
        sprints.value.find((s) => s.id === requestedSprintId) ||
        sprints.value.find((s) => isCurrentMilestone(s)) ||
        sprints.value[0];
      selectedSprintId.value = current.id;
    }
  } catch (error) {
    sprints.value = [];
    ganttTasks.value = [];
    showError("Không thể tải lộ trình dự án.");
  } finally {
    isLoading.value = false;
  }
}

// Milestone state calculation helpers
function isCompletedMilestone(sprint: SprintDto): boolean {
  if (sprint.status === "Completed") return true;
  if (sprint.taskCount > 0 && sprint.completedTaskCount === sprint.taskCount)
    return true;
  if (sprint.progress >= 100) return true;
  return false;
}

function isCurrentMilestone(sprint: SprintDto): boolean {
  if (isCompletedMilestone(sprint)) return false;
  if (sprint.status === "Active") return true;

  const now = new Date().getTime();
  const start = new Date(sprint.startDate).getTime();
  const end = new Date(sprint.endDate).getTime();

  if (now >= start && now <= end) return true;

  const uncompleted = sprints.value.filter((s) => !isCompletedMilestone(s));
  return uncompleted.length > 0 && uncompleted[0].id === sprint.id;
}

function isOverdueMilestone(sprint: SprintDto): boolean {
  if (isCompletedMilestone(sprint)) return false;
  const now = new Date().getTime();
  const end = new Date(sprint.endDate).getTime();
  return now > end && sprint.progress < 100;
}

const activeMilestone = computed(() => {
  return sprints.value.find((s) => s.id === selectedSprintId.value) || null;
});

const currentPositionMilestone = computed(() => {
  return sprints.value.find((s) => isCurrentMilestone(s)) || null;
});

// Filtered milestones list
const filteredSprints = computed(() => {
  let list = sprints.value;
  if (milestoneFilter.value === "active") {
    list = list.filter((s) => isCurrentMilestone(s));
  } else if (milestoneFilter.value === "completed") {
    list = list.filter((s) => isCompletedMilestone(s));
  } else if (milestoneFilter.value === "overdue") {
    list = list.filter((s) => isOverdueMilestone(s));
  }

  if (milestoneSearchQuery.value.trim()) {
    const q = milestoneSearchQuery.value.trim().toLowerCase();
    list = list.filter(
      (s) =>
        s.name.toLowerCase().includes(q) ||
        (s.goal && s.goal.toLowerCase().includes(q)),
    );
  }
  return list;
});

// Overall project health summary
const overallProgress = computed(() => {
  if (sprints.value.length === 0)
    return selectedProject.value?.progressPercentage || 0;
  const totalTasks = sprints.value.reduce((sum, s) => sum + s.taskCount, 0);
  const completedTasks = sprints.value.reduce(
    (sum, s) => sum + s.completedTaskCount,
    0,
  );
  if (totalTasks > 0) return Math.round((completedTasks / totalTasks) * 100);

  const completedMilestones = sprints.value.filter((s) =>
    isCompletedMilestone(s),
  ).length;
  return Math.round((completedMilestones / sprints.value.length) * 100);
});

const projectHealthStatus = computed(() => {
  if (sprints.value.length === 0)
    return { label: "Chưa có mốc", tone: "muted" };
  const overdueCount = sprints.value.filter((s) =>
    isOverdueMilestone(s),
  ).length;
  if (overdueCount > 0)
    return { label: `Có ${overdueCount} mốc trễ hạn`, tone: "danger" };
  const allCompleted = sprints.value.every((s) => isCompletedMilestone(s));
  if (allCompleted)
    return { label: "Đã hoàn thành toàn bộ mốc", tone: "success" };
  return { label: "Đang theo đúng tiến độ", tone: "primary" };
});

// Milestone tasks
const allProjectTasks = computed<DashboardTask[]>(() => {
  return selectedProject.value?.tasks || [];
});

const milestoneTasks = computed<DashboardTask[]>(() => {
  if (!selectedSprintId.value || !selectedProject.value?.tasks) return [];
  return selectedProject.value.tasks.filter(
    (t: DashboardTask) => t.sprintId === selectedSprintId.value,
  );
});

const filteredMilestoneTasks = computed<DashboardTask[]>(() => {
  let list = milestoneTasks.value;
  if (milestoneTaskStatusFilter.value !== "all") {
    list = list.filter(
      (t) =>
        t.status.toLowerCase() ===
        milestoneTaskStatusFilter.value.toLowerCase(),
    );
  }
  if (milestoneTaskSearch.value.trim()) {
    const q = milestoneTaskSearch.value.trim().toLowerCase();
    list = list.filter(
      (t) =>
        t.title.toLowerCase().includes(q) ||
        (t.key && t.key.toLowerCase().includes(q)),
    );
  }
  return list;
});

// Assigned members breakdown in active milestone
const milestoneAssignedMembers = computed(() => {
  if (!activeMilestone.value || milestoneTasks.value.length === 0) return [];
  const memberMap = new Map<
    string,
    { userId: string; name: string; taskCount: number; completedCount: number }
  >();

  for (const t of milestoneTasks.value) {
    if (t.assigneeId && t.assigneeName) {
      const existing = memberMap.get(t.assigneeId) || {
        userId: t.assigneeId,
        name: t.assigneeName,
        taskCount: 0,
        completedCount: 0,
      };
      existing.taskCount++;
      if (t.status === "Done") existing.completedCount++;
      memberMap.set(t.assigneeId, existing);
    }
  }
  return Array.from(memberMap.values());
});

const trackFillPercentage = computed(() => {
  if (sprints.value.length <= 1) return overallProgress.value;
  const currentIndex = sprints.value.findIndex((s) => isCurrentMilestone(s));
  if (currentIndex === -1) {
    const allDone = sprints.value.every((s) => isCompletedMilestone(s));
    return allDone ? 100 : 0;
  }
  return Math.round(((currentIndex + 0.5) / sprints.value.length) * 100);
});

// Action: Jump to Kanban Board for selected milestone
function jumpToKanban(sprintId?: string) {
  const targetId = sprintId || selectedSprintId.value;
  if (!targetId) return;
  activeProjectTab.value = "tasks";
}

// Action: Quick change milestone status
async function quickChangeMilestoneStatus(
  sprint: SprintDto,
  newStatus: string,
) {
  try {
    await apiCommand(`/api/sprints/${sprint.id}`, {
      method: "PATCH",
      body: JSON.stringify({
        name: sprint.name,
        startDate: sprint.startDate,
        endDate: sprint.endDate,
        status: newStatus,
        goal: sprint.goal,
      }),
    });
    await loadSprints();
    showSuccess(
      `Đã chuyển mốc "${sprint.name}" sang trạng thái "${newStatus}".`,
    );
  } catch (error) {
    showError("Không thể cập nhật trạng thái mốc.");
  }
}

// Action: Sign-off / Complete Milestone
function markMilestoneCompleted(sprint: SprintDto) {
  confirmation.value = { type: "complete", sprint };
}

// Action: Generate Roadmap Preset (Scrum, Outsource, Waterfall)
async function handleGeneratePreset(
  presetType: "scrum" | "outsource" | "waterfall",
) {
  isGeneratingPreset.value = true;
  try {
    await apiCommand(`/api/projects/${props.projectId}/sprints/presets`, {
      method: "POST",
      body: JSON.stringify({ presetType }),
    });
    showPresetModal.value = false;
    await loadSprints();
    showSuccess(
      `Đã tự động khởi tạo Mẫu Lộ trình (${presetType.toUpperCase()}) thành công!`,
    );
  } catch (error) {
    showError("Không thể khởi tạo mẫu mốc tiến độ.");
  } finally {
    isGeneratingPreset.value = false;
  }
}

// Action: Create manual milestone
async function handleCreateMilestone() {
  if (
    !milestoneName.value.trim() ||
    !milestoneStartDate.value ||
    !milestoneEndDate.value
  ) {
    showError("Vui lòng nhập đầy đủ Tên mốc, Ngày bắt đầu và Ngày kết thúc.");
    return;
  }

  try {
    await apiCommand(`/api/projects/${props.projectId}/sprints`, {
      method: "POST",
      body: JSON.stringify({
        name: milestoneName.value.trim(),
        startDate: new Date(milestoneStartDate.value).toISOString(),
        endDate: new Date(milestoneEndDate.value).toISOString(),
        goal: milestoneGoal.value.trim() || null,
      }),
    });

    closeMilestoneModal();
    await loadSprints();
    showSuccess("Đã thêm mốc tiến độ mới.");
  } catch (error) {
    showError("Không thể tạo mốc tiến độ.");
  }
}

// Action: Edit milestone
function openEdit(sprint: SprintDto) {
  editingSprintId.value = sprint.id;
  milestoneName.value = sprint.name;
  milestoneStartDate.value = sprint.startDate.slice(0, 10);
  milestoneEndDate.value = sprint.endDate.slice(0, 10);
  milestoneGoal.value = sprint.goal || "";
  milestoneStatus.value = sprint.status || "Planning";
  showEditModal.value = true;
}

async function handleUpdateMilestone() {
  if (!editingSprintId.value || !milestoneName.value.trim()) return;
  try {
    await apiCommand(`/api/sprints/${editingSprintId.value}`, {
      method: "PATCH",
      body: JSON.stringify({
        name: milestoneName.value.trim(),
        startDate: new Date(milestoneStartDate.value).toISOString(),
        endDate: new Date(milestoneEndDate.value).toISOString(),
        status: milestoneStatus.value,
        goal: milestoneGoal.value.trim() || null,
      }),
    });

    closeMilestoneModal();
    await loadSprints();
    showSuccess("Đã cập nhật thông tin mốc tiến độ.");
  } catch (error) {
    showError("Không thể cập nhật mốc tiến độ.");
  }
}

// Action: Delete milestone
function handleDeleteMilestone(sprintId: string) {
  const sprint = sprints.value.find((item) => item.id === sprintId);
  if (sprint) confirmation.value = { type: "delete", sprint };
}

async function confirmMilestoneAction() {
  const pending = confirmation.value;
  if (!pending) return;
  confirmation.value = null;

  if (pending.type === "complete") {
    await quickChangeMilestoneStatus(pending.sprint, "Completed");
    return;
  }

  try {
    await apiCommand(`/api/sprints/${pending.sprint.id}`, { method: "DELETE" });
    if (selectedSprintId.value === pending.sprint.id)
      selectedSprintId.value = null;
    await loadSprints();
    showSuccess("Đã xóa mốc tiến độ.");
  } catch (error) {
    showError("Không thể xóa mốc tiến độ.");
  }
}

// Action: Open Task Assignment Modal
function openTaskAssignModal() {
  if (!selectedSprintId.value) return;
  selectedTaskIdsForSprint.value = milestoneTasks.value.map((t) => t.id);
  showTaskAssignModal.value = true;
}

function toggleTaskSelection(taskId: string) {
  const index = selectedTaskIdsForSprint.value.indexOf(taskId);
  if (index >= 0) {
    selectedTaskIdsForSprint.value.splice(index, 1);
  } else {
    selectedTaskIdsForSprint.value.push(taskId);
  }
}

async function handleSaveTaskAssignments() {
  if (!selectedSprintId.value) return;
  isSavingTaskAssignment.value = true;
  try {
    await apiCommand(`/api/sprints/${selectedSprintId.value}/tasks`, {
      method: "PUT",
      body: JSON.stringify({ taskIds: selectedTaskIdsForSprint.value }),
    });
    closeTaskAssignModal();
    await loadDashboard();
    await loadSprints();
    showSuccess("Đã cập nhật danh sách công việc thuộc mốc.");
  } catch (error) {
    showError("Không thể cập nhật phân công công việc vào mốc.");
  } finally {
    isSavingTaskAssignment.value = false;
  }
}

// Action: Quick Create Task inside selected milestone
async function handleQuickCreateTask() {
  if (!selectedSprintId.value || !quickTaskTitle.value.trim()) {
    showError("Vui lòng nhập tiêu đề nhiệm vụ.");
    return;
  }

  try {
    await apiCommand("/api/tasks", {
      method: "POST",
      body: JSON.stringify({
        title: quickTaskTitle.value.trim(),
        priority: quickTaskPriority.value,
        projectId: props.projectId,
        assigneeId: quickTaskAssigneeId.value || null,
        dueDate: quickTaskDueDate.value
          ? new Date(quickTaskDueDate.value).toISOString()
          : null,
        sprintId: selectedSprintId.value,
      }),
    });

    closeQuickCreateTaskModal();
    quickTaskTitle.value = "";
    quickTaskPriority.value = "Medium";
    quickTaskAssigneeId.value = "";
    quickTaskDueDate.value = "";

    await loadDashboard();
    await loadSprints();
    showSuccess("Đã tạo nhiệm vụ mới trực tiếp trong mốc này.");
  } catch (error) {
    showError("Không thể tạo nhiệm vụ.");
  }
}

// Action: Inline Update Task Status (For members)
async function updateTaskStatusInline(task: DashboardTask, newStatus: string) {
  if (task.status === newStatus) return;
  try {
    await apiCommand(`/api/tasks/${task.id}/status`, {
      method: "PATCH",
      body: JSON.stringify({
        status: newStatus,
        rowVersion: task.rowVersion,
      }),
    });
    await loadDashboard();
    await loadSprints();
    showSuccess(`Đã chuyển trạng thái task sang "${newStatus}".`);
  } catch (error) {
    showError("Không thể cập nhật trạng thái nhiệm vụ.");
  }
}

function resetMilestoneForm() {
  milestoneName.value = "";
  milestoneStartDate.value = "";
  milestoneEndDate.value = "";
  milestoneGoal.value = "";
  milestoneStatus.value = "Planning";
  editingSprintId.value = null;
}

function formatDateRange(start: string, end: string) {
  if (!start || !end) return "";
  const d1 = new Date(start);
  const d2 = new Date(end);
  return `${d1.getDate()}/${d1.getMonth() + 1} - ${d2.getDate()}/${d2.getMonth() + 1}/${d2.getFullYear()}`;
}

function getDaysRemaining(endDateStr: string): {
  text: string;
  isOverdue: boolean;
} {
  if (!endDateStr) return { text: "", isOverdue: false };
  const now = new Date().getTime();
  const end = new Date(endDateStr).getTime();
  const diffDays = Math.ceil((end - now) / (1000 * 60 * 60 * 24));
  if (diffDays < 0)
    return { text: `Trễ ${Math.abs(diffDays)} ngày`, isOverdue: true };
  if (diffDays === 0) return { text: "Hạn chót hôm nay", isOverdue: false };
  return { text: `Còn ${diffDays} ngày`, isOverdue: false };
}
</script>

<template>
  <div class="project-roadmap-shell">
    <!-- Role & Mode Indicator Banner -->
    <div class="role-mode-bar glass-card">
      <div class="role-badge-box">
        <span v-if="isProjectAdmin" class="role-chip chip-admin">
          <ShieldCheck :size="15" /> Quyền Quản Lý (Project Leader)
        </span>
        <span v-else class="role-chip chip-member">
          <UserCheck :size="15" /> Giao diện Theo dõi Tiến độ (Thành viên &
          Stakeholders)
        </span>
        <span class="mode-text ms-2">
          {{
            isClientViewMode
              ? "👀 Đang ở chế độ xem Khách hàng / Stakeholder"
              : "⚙️ Đang ở chế độ xem Quản trị (Đầy đủ cấu hình & thao tác)"
          }}
        </span>
      </div>

      <div class="view-mode-toggle">
        <button
          type="button"
          class="toggle-btn"
          :class="{ 'is-active': !isClientViewMode }"
          @click="isClientViewMode = false"
        >
          <LayoutGrid :size="14" />
          <span>Quản trị</span>
        </button>
        <button
          type="button"
          class="toggle-btn"
          :class="{ 'is-active': isClientViewMode }"
          @click="isClientViewMode = true"
        >
          <Eye :size="14" />
          <span>Khách hàng</span>
        </button>
      </div>
    </div>

    <!-- Top Executive Overview Header -->
    <div class="roadmap-header glass-card">
      <div class="roadmap-header__title">
        <div class="title-with-icon">
          <div class="icon-glow-box">
            <Compass :size="26" class="text-primary" />
          </div>
          <div>
            <h3>Lộ Trình Dự Án & Mốc Tiến Độ</h3>
            <p class="text-muted text-sm">
              Theo dõi tiến độ nghiệm thu giai đoạn, quản lý mốc bàn giao và
              kiểm soát rủi ro dự án.
            </p>
          </div>
        </div>
      </div>

      <div class="roadmap-header__actions">
        <div class="roadmap-header__view-switch">
          <button
            type="button"
            class="view-mode-pill"
            :class="{ active: viewMode === 'journey' }"
            @click="viewMode = 'journey'"
          >
            <LayoutGrid :size="14" />
            <span>Journey View</span>
          </button>
          <button
            type="button"
            class="view-mode-pill"
            :class="{ active: viewMode === 'timeline' }"
            @click="viewMode = 'timeline'"
          >
            <CalendarDays :size="14" />
            <span>Timeline View</span>
          </button>
        </div>

        <button
          v-if="isProjectAdmin && !isClientViewMode"
          type="button"
          class="primary-button btn-outsource-preset"
          @click="showPresetModal = true"
        >
          <Sparkles :size="16" />
          <span>Khởi Tạo Mẫu Lộ Trình</span>
        </button>

        <button
          v-if="isProjectAdmin && !isClientViewMode"
          type="button"
          class="secondary-button"
          @click="showCreateModal = true"
        >
          <Plus :size="16" />
          <span>Thêm mốc mới</span>
        </button>
      </div>
    </div>

    <!-- Empty State if no milestones exist -->
    <div
      v-if="viewMode === 'journey' && sprints.length === 0 && !isLoading"
      class="empty-roadmap-card glass-card"
    >
      <div class="empty-roadmap-content">
        <Layers :size="52" class="text-primary opacity-60 mb-3" />
        <h4>Chưa có mốc tiến độ nào được thiết lập</h4>
        <p>
          Dự án này chưa có mốc tiến độ nào. Bạn có thể sử dụng các
          <strong>Mẫu Quy trình Chuẩn (Scrum, Outsource, Waterfall)</strong>
          hoặc tự thêm các mốc quan trọng để theo dõi tiến độ bàn giao.
        </p>

        <div class="empty-actions mt-4" v-if="isProjectAdmin">
          <button
            type="button"
            class="primary-button primary-button--lg"
            @click="showPresetModal = true"
          >
            <Sparkles :size="18" />
            <span>Chọn Bộ Mẫu Lộ Trình (Scrum / Outsource / Waterfall)</span>
          </button>

          <button
            type="button"
            class="secondary-button secondary-button--lg ms-3"
            @click="showCreateModal = true"
          >
            <Plus :size="18" />
            <span>Tự nhập mốc thủ công</span>
          </button>
        </div>
        <p v-else class="text-muted text-sm mt-3">
          Vui lòng liên hệ Người quản lý dự án (Project Leader) để thiết lập lộ
          trình dự án.
        </p>
      </div>
    </div>

    <!-- MAIN ROADMAP TIMELINE TRACK -->
    <template v-else>
      <template v-if="viewMode === 'journey'">
        <div class="roadmap-track-card glass-card">
          <!-- Client Executive Executive Bar & Filters -->
          <div class="roadmap-metrics-bar">
          <div class="metric-pill">
            <span class="metric-label">Trạng thái Sức khỏe Dự án</span>
            <span
              :class="`badge-tag tag-${projectHealthStatus.tone}`"
              class="health-tag"
            >
              <Zap :size="13" /> {{ projectHealthStatus.label }}
            </span>
          </div>

          <div class="metric-pill">
            <span class="metric-label">Mốc Tập trung Hiện tại</span>
            <strong class="metric-value text-primary">
              {{
                currentPositionMilestone
                  ? currentPositionMilestone.name
                  : "Đã hoàn thành tất cả mốc"
              }}
            </strong>
          </div>

          <div class="metric-pill">
            <span class="metric-label">Tiến độ Nghiệm thu Tổng thể</span>
            <div class="metric-progress-wrap">
              <div class="mini-progress-rail">
                <div
                  class="mini-progress-fill"
                  :style="{ width: `${overallProgress}%` }"
                ></div>
              </div>
              <strong class="metric-value text-success ms-2"
                >{{ overallProgress }}%</strong
              >
            </div>
          </div>

          <div class="metric-pill">
            <span class="metric-label">Tổng quy mô Mốc</span>
            <strong class="metric-value">{{ sprints.length }} Giai đoạn</strong>
          </div>

          <!-- Filter Pills -->
          <div class="milestone-filter-group">
            <button
              type="button"
              class="filter-pill"
              :class="{ active: milestoneFilter === 'all' }"
              @click="milestoneFilter = 'all'"
            >
              Tất cả ({{ sprints.length }})
            </button>
            <button
              type="button"
              class="filter-pill"
              :class="{ active: milestoneFilter === 'active' }"
              @click="milestoneFilter = 'active'"
            >
              ⚡ Đang chạy
            </button>
            <button
              type="button"
              class="filter-pill"
              :class="{ active: milestoneFilter === 'overdue' }"
              @click="milestoneFilter = 'overdue'"
            >
              ⚠️ Trễ hạn
            </button>
            <button
              type="button"
              class="filter-pill"
              :class="{ active: milestoneFilter === 'completed' }"
              @click="milestoneFilter = 'completed'"
            >
              ✓ Đã xong
            </button>
          </div>
        </div>

        <!-- Horizontal Connected Milestone Journey Track -->
        <div class="roadmap-scroll-wrapper no-scrollbar">
          <div class="roadmap-visual-container">
            <!-- Connecting Line -->
            <div class="connecting-line">
              <div
                class="connecting-line-fill"
                :style="{ width: `${trackFillPercentage}%` }"
              ></div>
            </div>

            <!-- Milestone Nodes -->
            <div class="milestones-nodes-row">
              <div
                v-for="(sprint, index) in filteredSprints"
                :key="sprint.id"
                class="milestone-node"
                :class="{
                  'is-completed': isCompletedMilestone(sprint),
                  'is-current': isCurrentMilestone(sprint),
                  'is-overdue': isOverdueMilestone(sprint),
                  'is-selected': selectedSprintId === sprint.id,
                }"
                @click="selectedSprintId = sprint.id"
              >
                <!-- Node Icon & Badge -->
                <div class="node-circle">
                  <div
                    v-if="isCurrentMilestone(sprint)"
                    class="current-location-flag"
                  >
                    <Navigation :size="12" />
                    <span>VỊ TRÍ HIỆN TẠI</span>
                  </div>

                  <CheckCircle2
                    v-if="isCompletedMilestone(sprint)"
                    :size="24"
                    class="text-success"
                  />
                  <span v-else class="node-number">{{ index + 1 }}</span>
                </div>

                <!-- Milestone Summary Card -->
                <div class="milestone-node-card">
                  <div class="node-status-badge">
                    <span
                      v-if="isCompletedMilestone(sprint)"
                      class="badge-tag tag-success"
                      >✓ Đã nghiệm thu</span
                    >
                    <span
                      v-else-if="isCurrentMilestone(sprint)"
                      class="badge-tag tag-primary"
                      >⚡ Đang làm</span
                    >
                    <span
                      v-else-if="isOverdueMilestone(sprint)"
                      class="badge-tag tag-danger"
                      >⚠️ Trễ hạn</span
                    >
                    <span v-else class="badge-tag tag-muted">📅 Chưa tới</span>
                  </div>

                  <h5 class="node-title">{{ sprint.name }}</h5>
                  <div class="node-dates">
                    <Calendar :size="12" />
                    <span>{{
                      formatDateRange(sprint.startDate, sprint.endDate)
                    }}</span>
                  </div>

                  <div
                    class="node-days-info"
                    :class="{
                      'is-overdue': getDaysRemaining(sprint.endDate).isOverdue,
                    }"
                  >
                    <Clock :size="11" />
                    <span>{{ getDaysRemaining(sprint.endDate).text }}</span>
                  </div>

                  <div class="node-progress-rail">
                    <div
                      class="node-progress-fill"
                      :class="{
                        'bg-success': isCompletedMilestone(sprint),
                        'bg-primary': isCurrentMilestone(sprint),
                      }"
                      :style="{ width: `${sprint.progress}%` }"
                    ></div>
                  </div>
                  <div class="node-task-count">
                    <strong
                      >{{ sprint.completedTaskCount }}/{{
                        sprint.taskCount
                      }}</strong
                    >
                    tasks ({{ sprint.progress }}%)
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
      </template>

      <template v-else>
        <div class="roadmap-track-card glass-card timeline-mode-card">
          <div class="section-header section-header--timeline">
            <div>
              <div class="eyebrow"><CalendarDays :size="14" /> Timeline View</div>
              <h3>Dòng thời gian Roadmap</h3>
              <p>Theo dõi các task và mốc theo định dạng biểu đồ Gantt dựa trên dữ liệu Roadmap hiện tại.</p>
            </div>
            <div class="timeline-legend">
              <span><i class="normal"></i>Bình thường</span>
              <span><i class="due"></i>Sắp hạn</span>
              <span><i class="stale"></i>Chưa bắt đầu</span>
              <span><i class="overdue"></i>Quá hạn</span>
            </div>
          </div>

          <div v-if="timelineTasks.length === 0" class="empty-state">
            <AlertTriangle :size="38" />
            <strong>Không có task có ngày bắt đầu hoặc hạn hoàn thành.</strong>
            <p>Hãy cập nhật ngày bắt đầu/hạn hoàn thành cho nhiệm vụ trong roadmap.</p>
          </div>

          <div v-else class="timeline-frame">
            <div class="timeline-labels">
              <div class="labels-header">Công việc</div>
              <button
                v-for="task in timelineTasks"
                :key="task.id"
                type="button"
                class="timeline-task-label"
                @click="selectTaskInProject(task.id)"
              >
                <span class="task-status-dot" :class="taskTone(task)"></span>
                <span>
                  <strong>{{ task.title }}</strong>
                  <small>{{ task.status }}</small>
                </span>
              </button>
            </div>

            <div class="timeline-scroll no-scrollbar">
              <div class="timeline-grid" :style="{ width: `${totalDays * 54}px` }">
                <div class="day-header" :style="{ gridTemplateColumns: `repeat(${totalDays}, 54px)` }">
                  <div v-for="day in timelineDays" :key="day.key" :class="{ weekend: day.isWeekend, today: day.isToday }">
                    <small>{{ day.month || day.weekday }}</small>
                    <strong>{{ day.day }}</strong>
                  </div>
                </div>
                <div v-for="task in timelineTasks" :key="task.id" class="timeline-row" :style="{ gridTemplateColumns: `repeat(${totalDays}, 54px)` }">
                  <span v-for="day in timelineDays" :key="day.key" class="day-cell" :class="{ weekend: day.isWeekend, today: day.isToday }"></span>
                  <button
                    type="button"
                    class="timeline-bar"
                    :class="taskTone(task)"
                    :style="getTaskStyle(task)"
                    @click="selectTaskInProject(task.id)"
                  >
                    <span class="bar-progress" :style="{ width: `${task.progress ?? 0}%` }"></span>
                    <strong>{{ task.progress ?? 0 }}%</strong>
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      </template>

      <!-- SELECTED MILESTONE DETAIL CONTROL PANEL -->
      <div
        v-if="activeMilestone"
        :id="`milestone-${activeMilestone.id}`"
        class="milestone-detail-panel glass-card"
      >
        <!-- Fast Access Command Toolbar -->
        <div class="fast-access-toolbar mb-4">
          <div class="toolbar-left">
            <span class="toolbar-title"
              >⚡ Thao Tác Nhanh Cho Mốc "{{ activeMilestone.name }}"</span
            >
          </div>

          <div class="toolbar-right">
            <!-- Quick Add Task -->
            <button
              v-if="!isClientViewMode"
              type="button"
              class="toolbar-btn btn-primary-gradient"
              @click="showQuickCreateTaskModal = true"
            >
              <Plus :size="14" />
              <span>+ Tạo Task Mốc Này</span>
            </button>

            <!-- One-click Sign-off / Complete Milestone -->
            <button
              v-if="
                isProjectAdmin &&
                !isClientViewMode &&
                !isCompletedMilestone(activeMilestone)
              "
              type="button"
              class="toolbar-btn btn-success-light"
              @click="markMilestoneCompleted(activeMilestone)"
              title="Đánh dấu nghiệm thu hoàn thành mốc này"
            >
              <CheckCircle2 :size="14" />
              <span>Nghiệm Thu Mốc</span>
            </button>

            <!-- Quick Status Change dropdown -->
            <div
              v-if="isProjectAdmin && !isClientViewMode"
              class="quick-status-dropdown-wrap"
            >
              <select
                class="quick-status-select"
                :value="activeMilestone.status || 'Planning'"
                @change="
                  quickChangeMilestoneStatus(
                    activeMilestone,
                    ($event.target as HTMLSelectElement).value,
                  )
                "
              >
                <option value="Planning">Trạng thái: Planning</option>
                <option value="Active">Trạng thái: Active (⚡)</option>
                <option value="Completed">Trạng thái: Completed (✓)</option>
                <option value="Paused">Trạng thái: Paused</option>
              </select>
            </div>

            <!-- Task Assignment -->
            <button
              v-if="isProjectAdmin && !isClientViewMode"
              type="button"
              class="toolbar-btn btn-outline"
              @click="openTaskAssignModal"
            >
              <LinkIcon :size="14" />
              <span>Gán/Gỡ Task ({{ milestoneTasks.length }})</span>
            </button>

            <!-- Kanban Jump -->
            <button
              type="button"
              class="toolbar-btn btn-kanban"
              @click="jumpToKanban(activeMilestone.id)"
            >
              <FolderKanban :size="14" />
              <span>Mở Bảng Kanban ➔</span>
            </button>
          </div>
        </div>

        <!-- Detail Header -->
        <div class="detail-header">
          <div class="detail-header-left">
            <div class="d-flex align-items-center gap-2 mb-1">
              <span class="badge-tag tag-primary">Mốc Đang Chọn</span>
              <span
                v-if="isCompletedMilestone(activeMilestone)"
                class="badge-tag tag-success"
                >✓ Nghiệm thu xong</span
              >
              <span
                v-else-if="isOverdueMilestone(activeMilestone)"
                class="badge-tag tag-danger"
                >⚠️ Trễ hạn</span
              >
              <span v-else class="badge-tag tag-primary"
                >⚡ Đang triển khai</span
              >
            </div>

            <h4>{{ activeMilestone.name }}</h4>
            <p v-if="activeMilestone.goal" class="detail-goal">
              🎯 <strong>Mục tiêu & Hạng mục nghiệm thu:</strong>
              {{ activeMilestone.goal }}
            </p>

            <div class="detail-dates-info">
              <Clock :size="14" />
              <span
                >Thời gian thực hiện:
                <strong>{{
                  formatDateRange(
                    activeMilestone.startDate,
                    activeMilestone.endDate,
                  )
                }}</strong></span
              >
              <span class="ms-3 badge-tag tag-muted">{{
                getDaysRemaining(activeMilestone.endDate).text
              }}</span>
            </div>
          </div>

          <div class="detail-header-actions">
            <!-- Manager actions -->
            <template v-if="isProjectAdmin && !isClientViewMode">
              <button
                type="button"
                class="btn btn-outline"
                @click="openEdit(activeMilestone)"
                title="Chỉnh sửa tên, deadline, goal"
              >
                <Edit3 :size="15" />
                <span>Sửa mốc</span>
              </button>
              <button
                type="button"
                class="btn btn-danger-ghost"
                @click="handleDeleteMilestone(activeMilestone.id)"
                title="Xóa mốc"
              >
                <Trash2 :size="15" />
              </button>
            </template>
          </div>
        </div>

        <!-- Milestone Key Deliverables Checklist (Calculated from Goal & Tasks) -->
        <div class="milestone-deliverables-box mt-3">
          <div class="deliverables-header">
            <Target :size="16" class="text-primary" />
            <strong>Tiêu Chí & Hạng Mục Nghiệm Thu (Key Deliverables)</strong>
            <span class="deliverable-badge"
              >{{ activeMilestone.completedTaskCount }}/{{
                activeMilestone.taskCount
              }}
              Đã Đạt</span
            >
          </div>

          <div class="deliverables-grid">
            <div
              class="deliverable-item"
              :class="{ 'is-done': activeMilestone.progress >= 100 }"
            >
              <CheckSquare
                v-if="activeMilestone.progress >= 100"
                :size="16"
                class="text-success"
              />
              <Clock v-else :size="16" class="text-muted" />
              <span
                >Hoàn thành 100% nhiệm vụ trong mốc ({{
                  activeMilestone.completedTaskCount
                }}/{{ activeMilestone.taskCount }} tasks)</span
              >
            </div>

            <div
              class="deliverable-item"
              :class="{ 'is-done': !isOverdueMilestone(activeMilestone) }"
            >
              <CheckSquare
                v-if="!isOverdueMilestone(activeMilestone)"
                :size="16"
                class="text-success"
              />
              <AlertTriangle v-else :size="16" class="text-danger" />
              <span
                >Đảm bảo đúng mốc thời hạn deadline:
                {{
                  formatDateRange(
                    activeMilestone.startDate,
                    activeMilestone.endDate,
                  )
                }}</span
              >
            </div>

            <div v-if="activeMilestone.goal" class="deliverable-item is-done">
              <CheckSquare :size="16" class="text-primary" />
              <span>Mục tiêu cam kết: {{ activeMilestone.goal }}</span>
            </div>
          </div>
        </div>

        <!-- AI Milestone Summary & Risk Card -->
        <ProjectProgressAiCard
          class="sprint-ai-summary"
          :project-id="projectId"
          :sprint-id="activeMilestone.id"
          :sprint-name="activeMilestone.name"
          :can-generate="canGenerateAi"
        />

        <!-- Team Workload Distribution in Milestone -->
        <div
          v-if="milestoneAssignedMembers.length > 0"
          class="milestone-members-bar mt-4"
        >
          <span class="section-label mb-2"
            ><Users :size="14" /> Nhân sự phụ trách mốc này ({{
              milestoneAssignedMembers.length
            }}
            thành viên):</span
          >
          <div class="members-chips-row">
            <div
              v-for="m in milestoneAssignedMembers"
              :key="m.userId"
              class="member-chip"
            >
              <User :size="13" />
              <span class="member-name">{{ m.name }}</span>
              <span class="member-task-badge"
                >{{ m.completedCount }}/{{ m.taskCount }} tasks</span
              >
            </div>
          </div>
        </div>

        <!-- Milestone Task List Section -->
        <div class="detail-body mt-4">
          <div class="tasks-section-header mb-3">
            <h5>
              <ListTodo :size="18" class="text-primary me-1" />
              Danh sách công việc thuộc mốc này ({{ milestoneTasks.length }})
            </h5>

            <div class="tasks-filter-tools">
              <div class="search-box-wrap">
                <Search :size="14" class="search-icon" />
                <input
                  v-model="milestoneTaskSearch"
                  type="text"
                  placeholder="Lọc task mốc..."
                  class="task-search-input"
                />
              </div>
              <select
                v-model="milestoneTaskStatusFilter"
                class="task-status-filter"
              >
                <option value="all">Tất cả trạng thái</option>
                <option value="todo">Cần làm (Todo)</option>
                <option value="inprogress">Đang làm (InProgress)</option>
                <option value="inreview">Đánh giá (InReview)</option>
                <option value="done">Hoàn thành (Done)</option>
              </select>
            </div>
          </div>

          <!-- Empty Tasks Box -->
          <div
            v-if="filteredMilestoneTasks.length === 0"
            class="empty-tasks-box"
          >
            <p v-if="milestoneTasks.length === 0">
              Chưa có task nào được gán vào mốc này.
              <span v-if="isProjectAdmin"
                >Bạn có thể bấm <strong>"+ Tạo Task Mốc Này"</strong> hoặc
                <strong>"Gán Nhiệm Vụ"</strong> trên thanh công cụ để bắt
                đầu.</span
              >
            </p>
            <p v-else>Không tìm thấy nhiệm vụ phù hợp với bộ lọc.</p>
          </div>

          <!-- Milestone Task Grid -->
          <div v-else class="milestone-tasks-grid">
            <div
              v-for="task in filteredMilestoneTasks"
              :key="task.id"
              class="milestone-task-item"
              :class="`status-${task.status.toLowerCase()}`"
            >
              <div class="task-item-header">
                <span class="task-number">{{
                  task.key || `#${task.number}`
                }}</span>
                <span
                  :class="`priority priority--${task.priority.toLowerCase()}`"
                  >{{ task.priority }}</span
                >
              </div>

              <div class="task-item-title">{{ task.title }}</div>

              <div class="task-item-footer">
                <span v-if="task.assigneeName" class="task-item-assignee">
                  👤 {{ task.assigneeName }}
                </span>
                <span v-else class="task-item-assignee text-muted">
                  👤 Chưa giao
                </span>

                <!-- Inline Status Selector (Allows Members to Update Task Status) -->
                <select
                  class="task-inline-status-select"
                  :value="task.status"
                  @change="
                    updateTaskStatusInline(
                      task,
                      ($event.target as HTMLSelectElement).value,
                    )
                  "
                >
                  <option value="Todo">Todo</option>
                  <option value="InProgress">InProgress</option>
                  <option value="InReview">InReview</option>
                  <option value="Done">Done</option>
                </select>
              </div>
            </div>
          </div>
        </div>
      </div>
    </template>

    <!-- MODAL: Select Preset Template (Scrum, Outsource, Waterfall) -->
    <Teleport to="body">
      <div
        v-if="showPresetModal"
        class="modal-backdrop"
        @click.self="showPresetModal = false"
      >
        <div class="preset-modal glass-card">
          <div class="modal-header">
            <h4>
              <Sparkles :size="18" class="text-primary me-2" /> Khởi Tạo Mẫu Lộ
              Trình Dự Án
            </h4>
            <button
              type="button"
              class="icon-button"
              @click="showPresetModal = false"
            >
              <X :size="18" />
            </button>
          </div>

          <div class="preset-modal-body">
            <p class="text-muted text-sm mb-4">
              Chọn phương pháp quản trị dự án phù hợp để hệ thống tự động khởi
              tạo lộ trình mốc tiến độ tối ưu:
            </p>

            <div class="preset-options-grid">
              <div
                class="preset-option-card"
                @click="handleGeneratePreset('outsource')"
              >
                <div class="preset-card-header">
                  <Award :size="24" class="text-primary" />
                  <h5>Mẫu Quy Trình Outsource (6 Mốc)</h5>
                </div>
                <p class="preset-desc">
                  Phù hợp dự án phần mềm cho khách hàng. Chia rõ mốc Scope ➔
                  Prototype ➔ Core Dev ➔ AI Integration ➔ UAT ➔ Go-Live.
                </p>
                <button
                  type="button"
                  class="btn btn-outline-primary w-100"
                  :disabled="isGeneratingPreset"
                >
                  Tạo Mẫu Outsource
                </button>
              </div>

              <div
                class="preset-option-card"
                @click="handleGeneratePreset('scrum')"
              >
                <div class="preset-card-header">
                  <RefreshCw :size="24" class="text-success" />
                  <h5>Mẫu Scrum / Agile (Sprint 1...4)</h5>
                </div>
                <p class="preset-desc">
                  Phù hợp quản trị Agile. Chia lộ trình thành các Sprint 2 tuần
                  song song với tiêu chí bàn giao liên tục.
                </p>
                <button
                  type="button"
                  class="btn btn-outline-success w-100"
                  :disabled="isGeneratingPreset"
                >
                  Tạo Mẫu Scrum (4 Sprints)
                </button>
              </div>

              <div
                class="preset-option-card"
                @click="handleGeneratePreset('waterfall')"
              >
                <div class="preset-card-header">
                  <Layers :size="24" class="text-warning" />
                  <h5>Mẫu Waterfall / Truyền thống (4 Pha)</h5>
                </div>
                <p class="preset-desc">
                  Phù hợp dự án yêu cầu quy trình tuyến tính: Khảo sát ➔ Thiết
                  kế ➔ Phát triển ➔ Bàn giao.
                </p>
                <button
                  type="button"
                  class="btn btn-outline-warning w-100"
                  :disabled="isGeneratingPreset"
                >
                  Tạo Mẫu Waterfall
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- MODAL: Create / Edit Milestone -->
    <Teleport to="body">
      <div
        v-if="showCreateModal || showEditModal"
        class="modal-backdrop"
        @click.self="closeMilestoneModal()"
      >
        <div
          ref="activeModalRoot"
          class="roadmap-modal-shell roadmap-modal-shell--compact glass-card"
          role="dialog"
          aria-modal="true"
          :aria-labelledby="showEditModal ? 'milestone-modal-title-edit' : 'milestone-modal-title-create'"
          tabindex="-1"
        >
          <div class="modal-header">
            <h4 :id="showEditModal ? 'milestone-modal-title-edit' : 'milestone-modal-title-create'">
              {{
                showEditModal ? "Chỉnh sửa Mốc Tiến Độ" : "Thêm Mốc Tiến Độ Mới"
              }}
            </h4>
            <button
              type="button"
              class="icon-button"
              @click="closeMilestoneModal()"
              aria-label="Đóng hộp thoại mốc tiến độ"
            >
              <X :size="18" />
            </button>
          </div>

          <form
            class="modal-body"
            @submit.prevent="
              showEditModal ? handleUpdateMilestone() : handleCreateMilestone()
            "
          >
            <div class="form-group">
              <label>Tên mốc / Giai đoạn *</label>
              <input
                ref="milestoneTitleInput"
                v-model="milestoneName"
                type="text"
                placeholder="Ví dụ: Mốc 1: Scope Alignment & Prototype UI..."
                required
                class="modal-input"
                data-modal-initial-focus="true"
              />
            </div>

            <div class="form-row">
              <div class="form-group">
                <label>Ngày bắt đầu *</label>
                <input
                  v-model="milestoneStartDate"
                  type="date"
                  required
                  class="modal-input"
                />
              </div>
              <div class="form-group">
                <label>Ngày kết thúc *</label>
                <input
                  v-model="milestoneEndDate"
                  type="date"
                  required
                  class="modal-input"
                />
              </div>
            </div>

            <div v-if="showEditModal" class="form-group">
              <label>Trạng thái mốc</label>
              <select v-model="milestoneStatus" class="modal-input">
                <option value="Planning">Planning (Lên kế hoạch)</option>
                <option value="Active">Active (Đang thực hiện)</option>
                <option value="Completed">Completed (Hoàn thành)</option>
                <option value="Paused">Paused (Tạm dừng)</option>
              </select>
            </div>

            <div class="form-group">
              <label>Mục tiêu nghiệm thu của mốc (Goal Statement)</label>
              <textarea
                v-model="milestoneGoal"
                rows="3"
                placeholder="Mô tả cụ thể tiêu chí để nghiệm thu hoàn thành mốc này..."
                class="modal-input"
              ></textarea>
            </div>

            <div class="modal-actions">
              <button
                type="button"
                class="btn btn--ghost"
                @click="closeMilestoneModal()"
              >
                Hủy
              </button>
              <button type="submit" class="btn btn--primary">
                {{ showEditModal ? "Cập nhật Mốc" : "Tạo Mốc Mới" }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>

    <!-- MODAL: Assign / Unassign Tasks to Milestone -->
    <Teleport to="body">
      <div
        v-if="showTaskAssignModal"
        class="modal-backdrop"
        @click.self="closeTaskAssignModal()"
      >
        <div
          ref="activeModalRoot"
          class="roadmap-modal-shell roadmap-modal-shell--wide glass-card"
          role="dialog"
          aria-modal="true"
          aria-labelledby="task-assign-modal-title"
          tabindex="-1"
        >
          <div class="modal-header">
            <h4 id="task-assign-modal-title">
              <LinkIcon :size="18" class="text-primary me-2" /> Gán Công Việc
              Vào Mốc: {{ activeMilestone?.name }}
            </h4>
            <button
              type="button"
              class="icon-button"
              @click="closeTaskAssignModal()"
              aria-label="Đóng hộp thoại gán công việc vào mốc"
            >
              <X :size="18" />
            </button>
          </div>

          <div class="modal-body">
            <p class="text-muted text-sm mb-3">
              Tích chọn các công việc trong dự án thuộc về mốc tiến độ này. Tiến
              độ mốc sẽ tự động tính dựa trên các task được gán.
            </p>

            <div v-if="allProjectTasks.length === 0" class="empty-tasks-box">
              <p>
                Dự án chưa có task nào. Hãy tạo task trước trên bảng Kanban.
              </p>
            </div>

            <div v-else class="assign-tasks-list no-scrollbar">
              <label
                v-for="task in allProjectTasks"
                :key="task.id"
                class="assign-task-row"
                :class="{
                  'is-selected': selectedTaskIdsForSprint.includes(task.id),
                }"
              >
                <input
                  type="checkbox"
                  :checked="selectedTaskIdsForSprint.includes(task.id)"
                  @change="toggleTaskSelection(task.id)"
                />
                <div class="task-info">
                  <strong
                    >{{
                      task.key ||
                      (task.number != null ? `#${task.number}` : "TASK")
                    }}
                    — {{ task.title }}</strong
                  >
                  <small class="text-muted ms-2"
                    >({{ task.status }} •
                    {{ task.assigneeName || "Chưa giao" }})</small
                  >
                </div>
              </label>
            </div>

            <div class="modal-actions">
              <button
                type="button"
                class="btn btn--ghost"
                @click="closeTaskAssignModal()"
              >
                Hủy
              </button>
              <button
                type="button"
                class="btn btn--primary"
                :disabled="isSavingTaskAssignment"
                @click="handleSaveTaskAssignments"
              >
                {{
                  isSavingTaskAssignment ? "Đang lưu..." : "Lưu Phân Công Mốc"
                }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- MODAL: Quick Create Task inside Milestone -->
    <Teleport to="body">
      <div
        v-if="showQuickCreateTaskModal"
        class="modal-backdrop"
        @click.self="closeQuickCreateTaskModal()"
      >
        <div
          ref="activeModalRoot"
          class="roadmap-modal-shell roadmap-modal-shell--compact glass-card"
          role="dialog"
          aria-modal="true"
          aria-labelledby="quick-task-modal-title"
          tabindex="-1"
        >
          <div class="modal-header">
            <h4 id="quick-task-modal-title">
              <Plus :size="18" class="text-primary me-2" /> Tạo Nhiệm Vụ Mới
              Thuộc Mốc: {{ activeMilestone?.name }}
            </h4>
            <button
              type="button"
              class="icon-button"
              @click="closeQuickCreateTaskModal()"
              aria-label="Đóng hộp thoại tạo nhiệm vụ mới"
            >
              <X :size="18" />
            </button>
          </div>

          <form class="modal-body" @submit.prevent="handleQuickCreateTask">
            <div class="form-group">
              <label>Tiêu đề nhiệm vụ *</label>
              <input
                ref="quickTaskTitleInput"
                v-model="quickTaskTitle"
                type="text"
                placeholder="Nhập tiêu đề nhiệm vụ mới..."
                required
                class="modal-input"
                data-modal-initial-focus="true"
              />
            </div>

            <div class="form-row">
              <div class="form-group">
                <label>Mức độ ưu tiên</label>
                <select v-model="quickTaskPriority" class="modal-input">
                  <option value="Low">Low</option>
                  <option value="Medium">Medium</option>
                  <option value="High">High</option>
                  <option value="Critical">Critical</option>
                </select>
              </div>

              <div class="form-group">
                <label>Hạn chót</label>
                <input
                  v-model="quickTaskDueDate"
                  type="date"
                  class="modal-input"
                />
              </div>
            </div>

            <div class="form-group">
              <label>Người phụ trách</label>
              <select v-model="quickTaskAssigneeId" class="modal-input">
                <option value="">Chưa giao</option>
                <option
                  v-for="user in selectedProject?.members || []"
                  :key="user.userId"
                  :value="user.userId"
                >
                  {{ user.fullName }}
                </option>
              </select>
            </div>

            <div class="modal-actions">
              <button
                type="button"
                class="btn btn--ghost"
                @click="closeQuickCreateTaskModal()"
              >
                Hủy
              </button>
              <button type="submit" class="btn btn--primary">
                Tạo Task Mốc Này
              </button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>

    <Teleport to="body">
      <div
        v-if="confirmation"
        class="modal-backdrop confirm-backdrop"
        @click.self="confirmation = null"
      >
        <div
          ref="confirmationModalRoot"
          class="confirmation-modal"
          role="alertdialog"
          aria-modal="true"
          tabindex="-1"
        >
          <div class="confirmation-icon" :class="`is-${confirmation.type}`">
            <CheckCircle2 v-if="confirmation.type === 'complete'" :size="26" />
            <AlertTriangle v-else :size="26" />
          </div>
          <div class="confirmation-content">
            <h4>
              {{
                confirmation.type === "complete"
                  ? "Xác nhận nghiệm thu mốc"
                  : "Xóa mốc tiến độ?"
              }}
            </h4>
            <p v-if="confirmation.type === 'complete'">
              Mốc <strong>“{{ confirmation.sprint.name }}”</strong> sẽ được đánh
              dấu hoàn thành.
            </p>
            <p v-else>
              Bạn sắp xóa mốc <strong>“{{ confirmation.sprint.name }}”</strong>.
              Các task liên kết vẫn được giữ lại.
            </p>
          </div>
          <div class="confirmation-actions">
            <button
              type="button"
              class="btn btn--ghost"
              @click="confirmation = null"
            >
              <X :size="16" /> Hủy
            </button>
            <button
              type="button"
              class="btn"
              :class="
                confirmation.type === 'delete' ? 'btn--danger' : 'btn--primary'
              "
              @click="confirmMilestoneAction"
            >
              <Trash2 v-if="confirmation.type === 'delete'" :size="16" />
              <CheckCircle2 v-else :size="16" />
              {{
                confirmation.type === "delete"
                  ? "Xóa mốc"
                  : "Xác nhận nghiệm thu"
              }}
            </button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.project-roadmap-shell {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

/* Role & Mode Banner */
.role-mode-bar {
  padding: 12px 20px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-radius: var(--radius-shell);
  background: var(--panel);
  border: 1px solid var(--line);
}

.role-badge-box {
  display: flex;
  align-items: center;
  gap: 8px;
}

.role-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  font-weight: 700;
  padding: 4px 10px;
  border-radius: 20px;
}

.chip-admin {
  background: rgba(37, 99, 235, 0.15);
  color: var(--qaly-primary);
  border: 1px solid rgba(37, 99, 235, 0.3);
}

.chip-member {
  background: rgba(22, 163, 74, 0.15);
  color: var(--qaly-success);
  border: 1px solid rgba(22, 163, 74, 0.3);
}

.mode-text {
  font-size: 12px;
  color: var(--muted);
}

.view-mode-toggle {
  display: flex;
  background: var(--bg);
  padding: 3px;
  border-radius: 8px;
  border: 1px solid var(--line);
}

.toggle-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 12px;
  border: none;
  background: transparent;
  color: var(--muted);
  font-size: 12px;
  font-weight: 600;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.toggle-btn.is-active {
  background: var(--panel);
  color: var(--text);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
}

/* Header */
.roadmap-header {
  padding: 24px 32px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-radius: var(--radius-shell);
  background: var(--panel);
  border: 1px solid var(--line);
}

.title-with-icon {
  display: flex;
  align-items: center;
  gap: 16px;
}

.icon-glow-box {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  background: rgba(37, 99, 235, 0.1);
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px solid rgba(37, 99, 235, 0.2);
}

.roadmap-header h3 {
  font-size: 18px;
  font-weight: 700;
  margin-bottom: 4px;
}

.roadmap-header__actions {
  display: flex;
  gap: 12px;
}

.roadmap-header__view-switch {
  display: flex;
  gap: 6px;
  background: var(--bg);
  border: 1px solid var(--line);
  border-radius: 10px;
  padding: 4px;
}

.view-mode-pill {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  border: none;
  border-radius: 8px;
  background: transparent;
  color: var(--muted);
  font-weight: 700;
  cursor: pointer;
}

.view-mode-pill.active {
  background: var(--panel);
  color: var(--text);
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.08);
}

.timeline-mode-card .section-header--timeline {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 20px;
  padding-bottom: 18px;
  border-bottom: 1px solid var(--line);
}

.timeline-legend {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  color: var(--muted);
  font-size: 12px;
}

.timeline-legend span {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.timeline-legend i {
  width: 10px;
  height: 10px;
  display: inline-block;
  border-radius: 50%;
}

.timeline-legend .normal { background: #3b82f6; }
.timeline-legend .due { background: #f59e0b; }
.timeline-legend .stale { background: #ea580c; }
.timeline-legend .overdue { background: #dc2626; }

.timeline-frame {
  display: grid;
  grid-template-columns: 250px minmax(0, 1fr);
  overflow: hidden;
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
}

.timeline-labels {
  border-right: 1px solid var(--line);
  background: var(--panel);
}

.labels-header {
  padding: 16px;
  font-size: 12px;
  font-weight: 700;
  text-transform: uppercase;
  color: var(--muted);
}

.timeline-task-label {
  width: 100%;
  display: flex;
  align-items: center;
  gap: 10px;
  border: none;
  border-bottom: 1px solid var(--line-light);
  padding: 14px 16px;
  background: transparent;
  text-align: left;
  color: var(--text-strong);
  cursor: pointer;
}

.timeline-task-label:hover {
  background: var(--bg-soft);
}

.timeline-task-label strong {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.timeline-task-label small {
  display: block;
  color: var(--muted);
  font-size: 11px;
}

.timeline-scroll {
  overflow-x: auto;
  background: var(--panel);
}

.timeline-grid {
  min-width: 100%;
}

.day-header,
.timeline-row {
  display: grid;
}

.day-header {
  background: rgba(37, 99, 235, 0.05);
}

.day-header > div,
.timeline-row > span {
  padding: 10px 6px;
  border-right: 1px solid rgba(0, 0, 0, 0.03);
  text-align: center;
}

.day-header > div:last-child,
.timeline-row > span:last-child {
  border-right: none;
}

.day-cell {
  min-height: 54px;
}

.day-cell.today,
.day-header > div.today {
  background: rgba(37, 99, 235, 0.08);
}

.timeline-row {
  position: relative;
  min-height: 68px;
  border-bottom: 1px solid var(--line-light);
}

.timeline-bar {
  position: absolute;
  top: 14px;
  height: 40px;
  border: none;
  border-radius: 10px;
  color: white;
  padding: 0 10px;
  display: flex;
  align-items: center;
  gap: 10px;
  cursor: pointer;
}

.timeline-bar.is-normal { background: #3b82f6; }
.timeline-bar.is-due { background: #f59e0b; }
.timeline-bar.is-stale { background: #ea580c; }
.timeline-bar.is-overdue { background: #dc2626; }

.timeline-bar .bar-progress {
  position: absolute;
  left: 0;
  bottom: 0;
  height: 4px;
  border-radius: 0 0 10px 10px;
  background: rgba(255, 255, 255, 0.55);
}

.timeline-bar strong {
  position: relative;
  z-index: 1;
  font-size: 12px;
}

.timeline-frame .timeline-labels,
.timeline-scroll {
  min-height: 320px;
}

.timeline-frame .timeline-scroll {
  scroll-behavior: smooth;
}

/* Empty Map */
.empty-roadmap-card {
  padding: 48px 32px;
  text-align: center;
  background: var(--panel);
  border: 1px dashed var(--line);
  border-radius: var(--radius-shell);
}

.empty-roadmap-content {
  max-width: 580px;
  margin: 0 auto;
}

/* Roadmap Track Card */
.roadmap-track-card {
  padding: 28px;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: var(--radius-shell);
}

.roadmap-metrics-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 24px;
  padding-bottom: 16px;
  border-bottom: 1px solid var(--line);
  flex-wrap: wrap;
}

.metric-pill {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.metric-label {
  font-size: 12px;
  color: var(--muted);
}

.metric-value {
  font-size: 15px;
  font-weight: 700;
}

.health-tag {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
}

.metric-progress-wrap {
  display: flex;
  align-items: center;
}

.mini-progress-rail {
  width: 100px;
  height: 8px;
  background: var(--line);
  border-radius: 4px;
  overflow: hidden;
}

.mini-progress-fill {
  height: 100%;
  background: var(--qaly-success);
  border-radius: 4px;
}

.milestone-filter-group {
  display: flex;
  gap: 6px;
  background: var(--bg);
  padding: 3px;
  border-radius: 8px;
  border: 1px solid var(--line);
}

.filter-pill {
  padding: 4px 10px;
  font-size: 11px;
  font-weight: 600;
  border: none;
  background: transparent;
  color: var(--muted);
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.filter-pill.active {
  background: var(--panel);
  color: var(--text-strong);
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.1);
}

/* Scroll Container for Node Path */
.roadmap-scroll-wrapper {
  overflow-x: auto;
  padding: 30px 10px 20px 10px;
}

.roadmap-visual-container {
  position: relative;
  min-width: 850px;
}

.connecting-line {
  position: absolute;
  top: 26px;
  left: 60px;
  right: 60px;
  height: 6px;
  background: var(--line);
  border-radius: 3px;
  z-index: 1;
}

.connecting-line-fill {
  height: 100%;
  background: linear-gradient(
    90deg,
    var(--qaly-success),
    var(--qaly-primary),
    var(--qaly-ai)
  );
  border-radius: 3px;
  transition: width 0.4s ease;
}

.milestones-nodes-row {
  display: flex;
  justify-content: space-between;
  position: relative;
  z-index: 2;
}

.milestone-node {
  display: flex;
  flex-direction: column;
  align-items: center;
  width: 185px;
  cursor: pointer;
  transition: transform 0.2s ease;
}

.milestone-node:hover {
  transform: translateY(-4px);
}

.node-circle {
  width: 52px;
  height: 52px;
  border-radius: 50%;
  background: var(--panel);
  border: 3px solid var(--line);
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  position: relative;
  transition: all 0.3s ease;
}

.milestone-node.is-completed .node-circle {
  border-color: var(--qaly-success);
  background: rgba(22, 163, 74, 0.1);
}

.milestone-node.is-current .node-circle {
  border-color: var(--qaly-primary);
  background: rgba(37, 99, 235, 0.15);
  box-shadow: 0 0 20px rgba(37, 99, 235, 0.4);
  animation: pulseGlow 2s infinite alternate;
}

@keyframes pulseGlow {
  0% {
    transform: scale(1);
    box-shadow: 0 0 10px rgba(37, 99, 235, 0.3);
  }
  100% {
    transform: scale(1.08);
    box-shadow: 0 0 22px rgba(37, 99, 235, 0.8);
  }
}

.current-location-flag {
  position: absolute;
  top: -32px;
  background: linear-gradient(135deg, var(--qaly-primary), var(--qaly-ai));
  color: #fff;
  font-size: 10px;
  font-weight: 700;
  padding: 2px 8px;
  border-radius: 10px;
  white-space: nowrap;
  display: flex;
  align-items: center;
  gap: 4px;
  box-shadow: 0 4px 10px rgba(37, 99, 235, 0.3);
}

.current-location-flag::after {
  content: "";
  position: absolute;
  bottom: -4px;
  left: 50%;
  transform: translateX(-50%);
  border-width: 4px 4px 0;
  border-style: solid;
  border-color: var(--qaly-ai) transparent;
}

.milestone-node-card {
  background: var(--panel-soft);
  border: 1px solid var(--line);
  border-radius: 12px;
  padding: 14px;
  width: 100%;
  margin-top: 16px;
  text-align: center;
  transition: all 0.2s ease;
}

.milestone-node.is-selected .milestone-node-card {
  border-color: var(--qaly-primary);
  box-shadow: 0 4px 16px rgba(37, 99, 235, 0.2);
}

.node-title {
  font-size: 13px;
  font-weight: 700;
  margin: 6px 0;
  line-height: 1.3;
}

.node-dates {
  font-size: 11px;
  color: var(--muted);
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  margin-bottom: 4px;
}

.node-days-info {
  font-size: 10px;
  font-weight: 600;
  color: var(--qaly-primary);
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 3px;
  margin-bottom: 8px;
}

.node-days-info.is-overdue {
  color: var(--qaly-danger);
}

.node-progress-rail {
  height: 5px;
  background: var(--line);
  border-radius: 3px;
  overflow: hidden;
  margin-bottom: 6px;
}

.node-progress-fill {
  height: 100%;
  border-radius: 3px;
}

.node-task-count {
  font-size: 11px;
  color: var(--muted);
}

.badge-tag {
  font-size: 10px;
  font-weight: 700;
  padding: 2px 6px;
  border-radius: 4px;
  display: inline-block;
}

.tag-success {
  background: rgba(22, 163, 74, 0.15);
  color: var(--qaly-success);
}
.tag-primary {
  background: rgba(37, 99, 235, 0.15);
  color: var(--qaly-primary);
}
.tag-danger {
  background: rgba(220, 38, 38, 0.15);
  color: var(--qaly-danger);
}
.tag-muted {
  background: rgba(148, 163, 184, 0.15);
  color: var(--muted);
}

/* Fast Access Toolbar */
.fast-access-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  background: linear-gradient(
    90deg,
    rgba(37, 99, 235, 0.08),
    rgba(99, 102, 241, 0.04)
  );
  border: 1px solid rgba(37, 99, 235, 0.2);
  border-radius: 12px;
  flex-wrap: wrap;
  gap: 12px;
}

.toolbar-title {
  font-size: 13px;
  font-weight: 700;
  color: var(--qaly-primary);
}

.toolbar-right {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.toolbar-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  font-size: 12px;
  font-weight: 600;
  border-radius: 8px;
  border: 1px solid var(--line);
  background: var(--panel);
  color: var(--text);
  cursor: pointer;
  transition: all 0.2s ease;
}

.toolbar-btn:hover {
  transform: translateY(-1px);
}

.btn-primary-gradient {
  background: linear-gradient(135deg, var(--qaly-primary), var(--qaly-ai));
  color: #fff;
  border: none;
  box-shadow: 0 2px 8px rgba(37, 99, 235, 0.3);
}

.btn-success-light {
  background: rgba(22, 163, 74, 0.15);
  color: var(--qaly-success);
  border-color: rgba(22, 163, 74, 0.3);
}

.btn-kanban {
  background: var(--bg);
  border-color: var(--line);
}

.quick-status-select {
  padding: 6px 10px;
  font-size: 12px;
  font-weight: 600;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 8px;
  color: var(--text);
}

/* Detail Control Panel */
.milestone-detail-panel {
  padding: 24px;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: var(--radius-shell);
}

.sprint-ai-summary {
  margin-top: 20px;
}

.detail-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding-bottom: 16px;
  border-bottom: 1px solid var(--line);
  flex-wrap: wrap;
  gap: 16px;
}

.detail-header h4 {
  font-size: 18px;
  font-weight: 700;
  margin: 4px 0;
}

.detail-goal {
  font-size: 13px;
  color: var(--text-strong);
  margin-bottom: 6px;
}

.detail-dates-info {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--muted);
}

.detail-header-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

/* Deliverables Box */
.milestone-deliverables-box {
  padding: 14px 18px;
  background: var(--panel-soft);
  border: 1px solid var(--line);
  border-radius: 12px;
}

.deliverables-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  margin-bottom: 10px;
}

.deliverable-badge {
  font-size: 11px;
  font-weight: 700;
  padding: 2px 8px;
  border-radius: 12px;
  background: rgba(37, 99, 235, 0.12);
  color: var(--qaly-primary);
  margin-left: auto;
}

.deliverables-grid {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.deliverable-item {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: var(--muted);
  padding: 6px 10px;
  background: var(--bg);
  border-radius: 6px;
  border: 1px solid var(--line);
}

.deliverable-item.is-done {
  color: var(--text-strong);
  font-weight: 500;
  border-color: rgba(22, 163, 74, 0.3);
}

.milestone-members-bar {
  padding: 12px 16px;
  background: var(--panel-soft);
  border: 1px solid var(--line);
  border-radius: 8px;
  font-size: 13px;
}

.section-label {
  display: flex;
  align-items: center;
  font-size: 12px;
  font-weight: 700;
  color: var(--muted);
}

.members-chips-row {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.member-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  background: var(--bg);
  border: 1px solid var(--line);
  border-radius: 16px;
  font-size: 12px;
}

.member-task-badge {
  font-size: 10px;
  font-weight: 700;
  background: rgba(37, 99, 235, 0.15);
  color: var(--qaly-primary);
  padding: 1px 6px;
  border-radius: 10px;
}

/* Tasks Filter & Grid */
.tasks-section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
}

.tasks-filter-tools {
  display: flex;
  gap: 8px;
}

.search-box-wrap {
  position: relative;
  display: flex;
  align-items: center;
}

.search-icon {
  position: absolute;
  left: 8px;
  color: var(--muted);
}

.task-search-input {
  padding: 6px 10px 6px 28px;
  font-size: 12px;
  background: var(--bg);
  border: 1px solid var(--line);
  border-radius: 6px;
  color: var(--text);
}

.task-status-filter {
  padding: 6px 10px;
  font-size: 12px;
  background: var(--bg);
  border: 1px solid var(--line);
  border-radius: 6px;
  color: var(--text);
}

.milestone-tasks-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 14px;
}

.milestone-task-item {
  padding: 14px;
  background: var(--panel-soft);
  border: 1px solid var(--line);
  border-radius: 10px;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  gap: 10px;
  transition: all 0.2s ease;
}

.milestone-task-item:hover {
  border-color: var(--qaly-primary);
}

.task-item-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 11px;
}

.task-number {
  font-family: monospace;
  font-weight: 700;
  color: var(--primary);
}

.task-item-title {
  font-size: 13px;
  font-weight: 600;
  line-height: 1.4;
}

.task-item-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 11px;
  padding-top: 8px;
  border-top: 1px solid var(--line);
}

.task-inline-status-select {
  padding: 3px 6px;
  font-size: 11px;
  font-weight: 600;
  border-radius: 4px;
  background: var(--bg);
  border: 1px solid var(--line);
  color: var(--text);
}

.empty-tasks-box {
  padding: 24px;
  text-align: center;
  background: var(--panel-soft);
  border: 1px dashed var(--line);
  border-radius: 8px;
  color: var(--muted);
  font-size: 13px;
}

/* Modals */
.modal-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 16px;
}

.preset-modal {
  width: 100%;
  max-width: 820px;
  padding: 24px;
  background: var(--panel);
  border-radius: var(--radius-shell);
  border: 1px solid var(--line);
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.modal-header h4 {
  margin: 0;
  font-size: 16px;
  font-weight: 700;
}

.preset-options-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
}

@media (max-width: 768px) {
  .preset-options-grid {
    grid-template-columns: 1fr;
  }
}

.preset-option-card {
  padding: 20px;
  background: var(--panel-soft);
  border: 1px solid var(--line);
  border-radius: 12px;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  gap: 14px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.preset-option-card:hover {
  border-color: var(--qaly-primary);
  transform: translateY(-2px);
}

.preset-card-header {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.preset-card-header h5 {
  margin: 0;
  font-size: 14px;
  font-weight: 700;
}

.preset-desc {
  font-size: 12px;
  color: var(--muted);
  line-height: 1.5;
  margin: 0;
}

.milestone-modal,
.task-assign-modal {
  width: 100%;
  max-width: 540px;
  padding: 24px;
  background: var(--panel);
  border-radius: var(--radius-shell);
  border: 1px solid var(--line);
}

.modal-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-group label {
  font-size: 12px;
  font-weight: 600;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.modal-input {
  padding: 8px 12px;
  font-size: 13px;
  background: var(--bg);
  border: 1px solid var(--line);
  border-radius: 6px;
  color: var(--text);
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}

.assign-tasks-list {
  max-height: 320px;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding-right: 4px;
}

.assign-task-row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  background: var(--panel-soft);
  border: 1px solid var(--line);
  border-radius: 8px;
  cursor: pointer;
  font-size: 13px;
  transition: all 0.2s ease;
}

.assign-task-row.is-selected {
  border-color: var(--qaly-primary);
  background: rgba(37, 99, 235, 0.08);
}

/* Roadmap workspace redesign: executive overview -> journey -> milestone workspace */
.project-roadmap-shell {
  --rm-blue: #1358c8;
  --rm-blue-dark: #0c3f98;
  --rm-blue-soft: #eaf2ff;
  --rm-ink: #13213a;
  --rm-muted: #65748b;
  --rm-line: #dbe4f0;
  --rm-milk: #fffdf8;
  gap: 24px;
  padding: 4px;
  color: var(--rm-ink);
}

.role-mode-bar,
.roadmap-header,
.roadmap-track-card,
.milestone-detail-panel,
.empty-roadmap-card {
  background: rgba(255, 253, 248, 0.97);
  border-color: var(--rm-line);
  box-shadow: 0 12px 34px rgba(34, 60, 96, 0.07);
}

.role-mode-bar {
  min-height: 52px;
  padding: 8px 10px 8px 16px;
  border-radius: 14px;
}

.chip-admin {
  background: var(--rm-blue-soft);
  border-color: #b9d2f7;
  color: var(--rm-blue-dark);
}

.mode-text {
  color: var(--rm-muted);
  line-height: 1.4;
}

.view-mode-toggle {
  padding: 4px;
  background: #edf2f8;
  border-color: #dce5ef;
  border-radius: 10px;
}

.toggle-btn {
  min-height: 34px;
  padding: 6px 13px;
  border-radius: 8px;
  transition:
    background-color 0.3s ease-out,
    color 0.3s ease-out,
    transform 0.3s ease-out;
}

.toggle-btn.is-active {
  background: #fff;
  color: var(--rm-blue-dark);
  box-shadow: 0 3px 10px rgba(31, 56, 88, 0.1);
}

.roadmap-header {
  position: relative;
  isolation: isolate;
  min-height: 116px;
  padding: 24px 28px;
  overflow: hidden;
  border-radius: 18px;
}

.roadmap-header::after {
  content: "";
  position: absolute;
  z-index: -1;
  top: -90px;
  right: 120px;
  width: 260px;
  height: 190px;
  border-radius: 50%;
  background: radial-gradient(circle, rgba(19, 88, 200, 0.14), transparent 70%);
}

.icon-glow-box {
  width: 54px;
  height: 54px;
  flex: 0 0 54px;
  background: linear-gradient(145deg, #246ee0, #0d4cad);
  border: 0;
  border-radius: 16px;
  box-shadow: 0 10px 24px rgba(19, 88, 200, 0.25);
}

.icon-glow-box :deep(svg) {
  color: #fff !important;
}

.roadmap-header h3 {
  margin: 0 0 5px;
  color: var(--rm-ink);
  font-size: clamp(20px, 2vw, 26px);
  letter-spacing: -0.025em;
}

.roadmap-header p {
  max-width: 650px;
  margin: 0;
  color: var(--rm-muted) !important;
  font-size: 13px;
  line-height: 1.55;
}

.roadmap-header__actions button,
.toolbar-btn {
  min-height: 40px;
  border-radius: 10px;
}

.btn-outsource-preset,
.btn-primary-gradient {
  background: linear-gradient(135deg, #1762d5, #0d4cad);
  box-shadow: 0 8px 18px rgba(19, 88, 200, 0.22);
}

.roadmap-track-card {
  padding: 0;
  overflow: hidden;
  border-radius: 18px;
}

.roadmap-metrics-bar {
  display: grid;
  grid-template-columns: 0.9fr minmax(260px, 1.65fr) 1fr 0.7fr;
  gap: 0;
  margin: 0;
  padding: 0;
  background: linear-gradient(180deg, #fffdf8, #f8fbff);
  border-bottom: 1px solid var(--rm-line);
}

.metric-pill {
  min-height: 94px;
  justify-content: center;
  gap: 8px;
  padding: 18px 22px;
  border-right: 1px solid var(--rm-line);
}

.metric-pill:nth-child(4) {
  border-right: 0;
}
.metric-label {
  color: var(--rm-muted);
  font-size: 11px;
  font-weight: 650;
}
.metric-value {
  color: var(--rm-ink) !important;
  font-size: 14px;
  line-height: 1.35;
}
.health-tag {
  width: fit-content;
  min-height: 28px;
  padding: 5px 9px;
  border-radius: 8px;
}
.mini-progress-rail {
  width: min(150px, 70%);
  height: 8px;
  background: #dfe7f1;
}
.mini-progress-fill {
  background: linear-gradient(90deg, #16865c, #45b889);
}

.milestone-filter-group {
  grid-column: 1 / -1;
  justify-content: flex-start;
  gap: 6px;
  padding: 10px 18px;
  background: #f6f9fd;
  border: 0;
  border-top: 1px solid var(--rm-line);
  border-radius: 0;
}

.filter-pill {
  min-height: 32px;
  padding: 6px 12px;
  color: #607087;
  border: 1px solid transparent;
  transition:
    background-color 0.3s ease-out,
    border-color 0.3s ease-out,
    color 0.3s ease-out;
}

.filter-pill:hover {
  background: #fff;
  border-color: #d9e3ef;
  color: var(--rm-ink);
}
.filter-pill.active {
  background: #fff;
  border-color: #b8cff0;
  color: var(--rm-blue-dark);
  box-shadow: 0 3px 10px rgba(19, 88, 200, 0.08);
}

.roadmap-scroll-wrapper {
  padding: 52px 24px 28px;
  scroll-snap-type: x proximity;
}
.roadmap-visual-container {
  min-width: max(1040px, 100%);
}
.connecting-line {
  top: 23px;
  left: 88px;
  right: 88px;
  height: 4px;
  background: #dce5ef;
}
.connecting-line-fill {
  background: linear-gradient(90deg, #16865c, #1762d5);
}

.milestone-node {
  width: 196px;
  scroll-snap-align: center;
  transition: transform 0.3s ease-out;
}

.milestone-node:hover {
  transform: translateY(-5px);
}

.node-circle {
  width: 48px;
  height: 48px;
  color: #465a75;
  background: var(--rm-milk);
  border: 3px solid #d9e3ef;
  box-shadow: 0 5px 14px rgba(33, 58, 91, 0.08);
}

.milestone-node.is-current .node-circle {
  color: var(--rm-blue-dark);
  background: #edf4ff;
  border-color: var(--rm-blue);
  box-shadow: 0 0 0 7px rgba(19, 88, 200, 0.1);
  animation: none;
}

.current-location-flag {
  top: -35px;
  min-height: 23px;
  padding: 3px 9px;
  background: var(--rm-blue-dark);
  border-radius: 6px;
  box-shadow: 0 5px 12px rgba(12, 63, 152, 0.2);
}

.current-location-flag::after {
  border-color: var(--rm-blue-dark) transparent;
}

.milestone-node-card {
  min-height: 170px;
  padding: 14px 13px;
  background: #f8fafc;
  border-color: #dfe6ef;
  border-radius: 13px;
  box-shadow: 0 5px 14px rgba(36, 57, 84, 0.04);
  transition:
    transform 0.3s ease-out,
    border-color 0.3s ease-out,
    background-color 0.3s ease-out;
}

.milestone-node.is-selected .milestone-node-card {
  background: #fff;
  border-color: #6e9fe1;
  box-shadow: 0 10px 24px rgba(19, 88, 200, 0.13);
}

.node-title {
  min-height: 51px;
  margin: 9px 0 7px;
  color: var(--rm-ink);
  font-size: 13px;
  line-height: 1.32;
}
.node-dates,
.node-task-count {
  color: var(--rm-muted);
}
.node-days-info {
  color: var(--rm-blue);
  font-size: 11px;
}
.badge-tag {
  padding: 4px 7px;
  border-radius: 6px;
  font-size: 10px;
  line-height: 1.2;
}

.milestone-detail-panel {
  padding: 0;
  overflow: hidden;
  border-radius: 18px;
}

.fast-access-toolbar {
  margin: 0 !important;
  padding: 15px 20px;
  background: linear-gradient(100deg, #edf4ff, #f8fbff);
  border: 0;
  border-bottom: 1px solid #cdddf1;
  border-radius: 0;
}

.toolbar-title {
  color: var(--rm-blue-dark);
  font-size: 12px;
}
.toolbar-btn,
.quick-status-select {
  min-height: 36px;
  background: #fff;
  border-color: #cfdae8;
  color: #30445f;
}
.toolbar-btn:hover {
  border-color: #87abe0;
  color: var(--rm-blue-dark);
  transform: translateY(-1px);
}
.btn-primary-gradient {
  color: #fff;
  border: 0;
}
.btn-success-light {
  background: #eaf8f2;
  border-color: #a9dbc7;
  color: #116a49;
}

.detail-header,
.milestone-deliverables-box,
.sprint-ai-summary,
.milestone-members-bar,
.detail-body {
  margin-left: 22px;
  margin-right: 22px;
}

.detail-header {
  padding: 22px 0 18px;
}
.detail-header h4 {
  color: var(--rm-ink);
  font-size: 21px;
  letter-spacing: -0.02em;
}
.detail-goal {
  max-width: 850px;
  color: #374b66;
  font-size: 13px;
  line-height: 1.55;
}

.milestone-deliverables-box {
  padding: 17px;
  background: #f8fbff;
  border-color: #d8e3ef;
}
.deliverables-header {
  margin-bottom: 13px;
  color: var(--rm-ink);
  font-size: 13px;
}
.deliverable-item {
  min-height: 38px;
  padding: 8px 11px;
  background: #fff;
  border-color: #dce5ef;
  color: #586981;
  line-height: 1.45;
}
.deliverable-item.is-done {
  border-color: #b8dfd0;
  color: #28483c;
}

.milestone-members-bar {
  padding: 14px 16px;
  background: #f8fbff;
  border-color: #dbe5f0;
  border-radius: 11px;
}
.member-chip {
  min-height: 30px;
  background: #fff;
  border-color: #d6e1ee;
}
.detail-body {
  margin-top: 24px !important;
  padding-bottom: 24px;
}
.tasks-section-header {
  padding-bottom: 12px;
  border-bottom: 1px solid var(--rm-line);
}
.tasks-section-header h5 {
  display: flex;
  align-items: center;
  margin: 0;
  color: var(--rm-ink);
  font-size: 15px;
}

.task-search-input,
.task-status-filter {
  min-height: 38px;
  background: #fff;
  border-color: #d5dfeb;
  border-radius: 9px;
}

.task-search-input:focus,
.task-status-filter:focus,
.quick-status-select:focus {
  border-color: #6e9fe1;
  box-shadow: 0 0 0 3px rgba(19, 88, 200, 0.12);
  outline: none;
}

.milestone-task-item {
  min-height: 130px;
  background: #fffdf8;
  border-color: #dce5ef;
  border-radius: 12px;
  box-shadow: 0 4px 12px rgba(34, 58, 88, 0.04);
  transition:
    transform 0.3s ease-out,
    border-color 0.3s ease-out;
}

.milestone-task-item:hover {
  border-color: #7fa8df;
  transform: translateY(-2px);
}

.empty-tasks-box {
  min-height: 92px;
  display: grid;
  place-items: center;
  background: #f8fbff;
  border-color: #cfdbea;
  border-radius: 11px;
}

.preset-modal,
.milestone-modal,
.task-assign-modal {
  background: var(--rm-milk);
  border-color: var(--rm-line);
  box-shadow: 0 26px 70px rgba(20, 42, 72, 0.24);
}

.preset-option-card,
.assign-task-row {
  background: #f8fbff;
  border-color: #dbe4ef;
  transition:
    transform 0.3s ease-out,
    border-color 0.3s ease-out,
    background-color 0.3s ease-out;
}

.preset-option-card:hover {
  background: #fff;
  border-color: #7fa8df;
  transform: translateY(-4px);
}

@media (max-width: 1050px) {
  .roadmap-metrics-bar {
    grid-template-columns: 1fr 1fr;
  }
  .metric-pill:nth-child(2) {
    border-right: 0;
  }
  .metric-pill:nth-child(-n + 2) {
    border-bottom: 1px solid var(--rm-line);
  }
  .role-badge-box {
    align-items: flex-start;
    flex-direction: column;
  }
}

@media (max-width: 760px) {
  .project-roadmap-shell {
    gap: 16px;
    padding: 0;
  }
  .role-mode-bar,
  .roadmap-header,
  .detail-header,
  .tasks-section-header {
    align-items: stretch;
    flex-direction: column;
  }
  .roadmap-header {
    padding: 20px;
  }
  .roadmap-header__actions,
  .roadmap-header__actions button,
  .view-mode-toggle,
  .toggle-btn {
    width: 100%;
  }
  .roadmap-header__actions button,
  .toggle-btn {
    justify-content: center;
  }
  .roadmap-metrics-bar {
    grid-template-columns: 1fr;
  }
  .metric-pill {
    min-height: 76px;
    border-right: 0;
    border-bottom: 1px solid var(--rm-line);
  }
  .milestone-filter-group,
  .toolbar-right,
  .tasks-filter-tools {
    width: 100%;
    overflow-x: auto;
    flex-wrap: nowrap;
  }
  .filter-pill,
  .toolbar-btn {
    flex: 0 0 auto;
  }
  .detail-header,
  .milestone-deliverables-box,
  .sprint-ai-summary,
  .milestone-members-bar,
  .detail-body {
    margin-left: 16px;
    margin-right: 16px;
  }
  .task-search-input {
    min-width: 220px;
  }
}

@media (prefers-reduced-motion: reduce) {
  .project-roadmap-shell *,
  .project-roadmap-shell *::before,
  .project-roadmap-shell *::after {
    scroll-behavior: auto !important;
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
  }
}

/* Forms, buttons and confirmation dialogs */
.toolbar-btn.btn-primary-gradient {
  background: linear-gradient(135deg, #1762d5, #0d4cad);
  color: #fff;
  border: 0;
  box-shadow: 0 8px 18px rgba(19, 88, 200, 0.22);
}

.toolbar-btn.btn-primary-gradient:hover {
  background: linear-gradient(135deg, #0f55c4, #093b8f);
  color: #fff;
}

.modal-backdrop {
  display: grid;
  place-items: center;
  overflow-y: auto;
  padding: clamp(16px, 3vw, 32px);
  background: rgba(10, 15, 25, 0.72);
  backdrop-filter: blur(10px) saturate(110%);
  -webkit-backdrop-filter: blur(10px) saturate(110%);
}

.roadmap-modal-shell {
  display: flex;
  flex-direction: column;
  width: min(700px, calc(100vw - 32px));
  max-height: 88vh;
  padding: 0;
  overflow: hidden;
  border: 1px solid #cbd9e9;
  border-radius: 20px;
  background: var(--panel);
  box-shadow: 0 28px 72px rgba(12, 31, 56, 0.26);
}

.roadmap-modal-shell--compact {
  width: min(680px, calc(100vw - 32px));
}

.roadmap-modal-shell--wide {
  width: min(720px, calc(100vw - 32px));
}

.roadmap-modal-shell .modal-header {
  min-height: 76px;
  margin: 0;
  padding: 18px 24px 18px 22px;
  background: linear-gradient(135deg, #fffdf8, #f0f6ff);
  border-bottom: 1px solid #dbe4f0;
}

.roadmap-modal-shell .modal-header h4 {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
  max-width: calc(100% - 56px);
  margin: 0;
  color: #13213a;
  font-size: 18px;
  line-height: 1.35;
  overflow-wrap: anywhere;
}

.roadmap-modal-shell .modal-header .icon-button {
  width: 40px;
  height: 40px;
  flex: 0 0 40px;
  display: grid;
  place-items: center;
  padding: 0;
  background: #fff;
  border: 1px solid #d4dfec;
  border-radius: 11px;
  color: #53657c;
  box-shadow: 0 4px 12px rgba(24, 48, 78, 0.08);
}

.roadmap-modal-shell .modal-header .icon-button:hover {
  background: #edf4ff;
  border-color: #91b3e3;
  color: #0c3f98;
}

.roadmap-modal-shell .modal-body {
  flex: 1 1 auto;
  min-height: 0;
  gap: 18px;
  padding: 22px 24px;
  overflow-y: auto;
}

.roadmap-modal-shell .form-group {
  gap: 8px;
}

.roadmap-modal-shell .form-group label {
  color: #31445f;
  font-size: 13px;
  font-weight: 700;
}

.roadmap-modal-shell .form-row {
  gap: 14px;
}

.roadmap-modal-shell .modal-input {
  width: 100%;
  min-height: 46px;
  padding: 10px 13px;
  background: #fff;
  border: 1px solid #ccd9e8;
  border-radius: 10px;
  color: #17243a;
  font: inherit;
  line-height: 1.45;
  box-shadow: inset 0 1px 2px rgba(20, 42, 72, 0.03);
}

.roadmap-modal-shell textarea.modal-input {
  min-height: 100px;
  resize: vertical;
}

.roadmap-modal-shell .modal-input:focus {
  border-color: #5f91d3;
  box-shadow: 0 0 0 3px rgba(19, 88, 200, 0.12);
  outline: none;
}

.roadmap-modal-shell .modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin: 2px -24px -24px;
  padding: 18px 24px 22px;
  background: #f7f9fc;
  border-top: 1px solid #dbe4f0;
}

.roadmap-modal-shell .modal-actions .btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 44px;
  padding: 10px 16px;
  border-radius: 10px;
  font-size: 13px;
  font-weight: 700;
  line-height: 1;
}

.roadmap-modal-shell .modal-actions .btn--ghost {
  background: #fff;
  border-color: #cbd7e5 !important;
  color: #40516a;
}

.roadmap-modal-shell .modal-actions .btn--ghost:hover {
  background: #eef3f8;
  border-color: #aebfd2 !important;
}

.roadmap-modal-shell .modal-actions .btn--primary {
  background: linear-gradient(135deg, #1762d5, #0d4cad);
  color: #fff;
  box-shadow: 0 7px 16px rgba(19, 88, 200, 0.22);
}

.roadmap-modal-shell .modal-actions .btn--primary:hover {
  background: linear-gradient(135deg, #0f55c4, #093b8f);
}

.roadmap-modal-shell--wide .assign-tasks-list {
  max-height: min(44vh, 430px);
}

.roadmap-modal-shell--wide .assign-task-row {
  min-height: 64px;
  padding: 12px 15px;
  background: #fff;
  border-radius: 11px;
}

.roadmap-modal-shell--wide .assign-task-row input[type="checkbox"] {
  width: 18px;
  height: 18px;
  flex: 0 0 18px;
  accent-color: #1358c8;
}

.roadmap-modal-shell--wide .assign-task-row .task-info {
  min-width: 0;
  line-height: 1.45;
}

.roadmap-modal-shell--wide .assign-task-row .task-info strong {
  display: block;
  color: #17243a;
  font-size: 13px;
  overflow-wrap: anywhere;
}

.roadmap-modal-shell--wide .assign-task-row .task-info small {
  display: block;
  margin: 3px 0 0 !important;
  color: #687991 !important;
}

.roadmap-modal-shell--wide .assign-task-row.is-selected {
  background: #edf4ff;
  border-color: #82a9df;
}

.btn--danger {
  background: #c93636;
  color: #fff;
  box-shadow: 0 7px 16px rgba(201, 54, 54, 0.2);
}

.btn--danger:hover {
  background: #ab2929;
}

.confirmation-modal {
  width: min(460px, calc(100vw - 32px));
  padding: 26px;
  background: #fffdf8;
  border: 1px solid #d2deeb;
  border-radius: 18px;
  box-shadow: 0 28px 70px rgba(12, 31, 56, 0.28);
}

.confirmation-icon {
  width: 50px;
  height: 50px;
  display: grid;
  place-items: center;
  margin-bottom: 17px;
  border-radius: 14px;
}

.confirmation-icon.is-complete {
  background: #e8f7f0;
  color: #157452;
}
.confirmation-icon.is-delete {
  background: #fff0ef;
  color: #c93636;
}
.confirmation-content h4 {
  margin: 0 0 8px;
  color: #13213a;
  font-size: 19px;
}
.confirmation-content p {
  margin: 0;
  color: #5c6c82;
  font-size: 14px;
  line-height: 1.6;
}
.confirmation-content strong {
  color: #263a57;
}

.confirmation-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 24px;
}

@media (max-width: 600px) {
  .form-row {
    grid-template-columns: 1fr;
  }
  .roadmap-modal-shell {
    width: min(100vw - 24px, 720px);
    max-height: 90dvh;
  }
  .roadmap-modal-shell .modal-header,
  .roadmap-modal-shell .modal-body {
    padding-left: 16px;
    padding-right: 16px;
  }
  .roadmap-modal-shell .modal-actions {
    margin-left: -16px;
    margin-right: -16px;
    padding-left: 16px;
    padding-right: 16px;
  }
  .roadmap-modal-shell .modal-actions .btn,
  .confirmation-actions .btn {
    flex: 1;
    justify-content: center;
  }
}
</style>
