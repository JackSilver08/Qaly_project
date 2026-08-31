<script setup lang="ts">
import {
  HubConnectionBuilder,
  HubConnectionState,
  type HubConnection,
} from "@microsoft/signalr";
import {
  CalendarDays,
  Camera,
  Check,
  ChevronRight,
  Crown,
  Download,
  FileText,
  FileVideo,
  FolderKanban,
  Image as ImageIcon,
  Loader2,
  Mail,
  LogOut,
  Plus,
  Settings,
  ShieldCheck,
  UserPlus,
  UserRound,
  Users,
  Vote,
  Sparkles,
  Trash2,
  PanelRightClose,
  PanelRightOpen,
  Bell,
  BellOff,
  Search,
  X,
} from "lucide-vue-next";
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import ChatSidebar from "../components/chat/ChatSidebar.vue";
import ChatWindow from "../components/chat/ChatWindow.vue";
import PollCard from "../components/chat/PollCard.vue";
import GroupAiPanel from "../components/chat/GroupAiPanel.vue";
import type {
  ChatGroupModel,
  TeamChatAttachment,
  TeamChatMemberMention,
  TeamChatMeeting,
  TeamChatMessage,
  TeamChatReaction,
  TeamChatPoll,
} from "../components/chat/chat-types";
import { useDashboardContext } from "../composables/dashboard-context";
import { showError, showSuccess } from "../composables/use-toast";
import { confirmDialog } from "../composables/use-confirm-dialog";
import type { PagedResult, UserDto } from "../types";
import { apiCommand, apiResult, errorMessage } from "../utils/api-client";

interface GroupDto {
  id: string;
  name: string;
  avatarUrl: string | null;
  color: string | null;
  backgroundTheme: string | null;
  backgroundImageUrl: string | null;
  currentUserRole: string;
  memberCount: number;
  messageCount: number;
  openPollCount: number;
  unreadCount: number;
  isMuted: boolean;
}

interface GroupMemberDto {
  userId: string;
  fullName: string;
  email: string;
  role: string;
  joinedAt: string;
}

interface GroupInvitationDto {
  id: string;
  groupId: string;
  email: string;
  status: string;
  expiredAt: string;
  createdAt: string;
}

interface GroupMessageDto {
  id: string;
  workGroupId: string;
  userId: string;
  senderName: string;
  content: string;
  messageType: string;
  isDeleted: boolean;
  createdAt: string;
  editedAt: string | null;
  isPinned: boolean;
  pinnedAt: string | null;
  pinnedByUserId: string | null;
  reactions: GroupMessageReactionDto[];
  replyTo: GroupMessageReferenceDto | null;
  forwardedFrom: GroupMessageReferenceDto | null;
}

interface GroupMessageReferenceDto {
  id: string;
  workGroupId: string;
  userId: string;
  senderName: string;
  content: string;
  messageType: string;
  isDeleted: boolean;
}

interface GroupMessageReactionDto {
  emoji: string;
  count: number;
  userIds: string[];
  reactedByCurrentUser: boolean;
}

interface GroupAttachmentDto {
  id: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  createdAt: string;
}

const route = useRoute();
const router = useRouter();
const { currentUser, loadDashboard, selectProject, openChatWithPrompt } = useDashboardContext();

const groups = ref<ChatGroupModel[]>([]);
const groupDetails = ref<Record<string, GroupDto>>({});
const members = ref<GroupMemberDto[]>([]);
const users = ref<UserDto[]>([]);
const invitations = ref<GroupInvitationDto[]>([]);
const messages = ref<TeamChatMessage[]>([]);
const activeGroupId = ref("");
const activeTab = ref<"members" | "invites" | "polls" | "meeting" | "project" | "ai">("members");
const isLoadingGroups = ref(false);
const isLoadingMessages = ref(false);
const loadError = ref<string | null>(null);
const realtimeState = ref<"connecting" | "connected" | "offline">("offline");
const isActiveGroupReady = ref(false);
const showCreateModal = ref(false);
const createForm = ref({ name: "", color: "#2563eb" });
const inviteEmail = ref("");
const addUserId = ref("");
const addUserSearch = ref("");
const showAddUserSuggestions = ref(false);
const addRole = ref("Member");
const projectForm = ref({ name: "", code: "", description: "" });
const isCreatingProject = ref(false);
const lastCreatedProject = ref<{ id: string; name: string } | null>(null);
const pollForm = ref({ question: "", options: ["", ""], allowMultiple: false });
const isCreatingPoll = ref(false);
const isDetailPanelCollapsed = ref(false);
const isUploadingAvatar = ref(false);
const selectedAiRequest = ref<{
  action: "summary" | "task-draft";
  messageIds: string[];
  nonce: number;
} | null>(null);
const typingUsers = ref<Record<string, { name: string; timeoutId: number }>>({});

let hubConnection: HubConnection | null = null;
let localTypingTimer: number | undefined;
let lastTypingState = false;

function suggestPollWithAi() {
  const seed = pollForm.value.question.trim();
  const groupName = activeGroup.value?.name ?? "nhóm hiện tại";
  openChatWithPrompt(
    `Trong nhóm "${groupName}", hãy soạn một bình chọn ngắn, trung lập và dễ trả lời${seed ? ` về chủ đề: ${seed}` : " dựa trên ngữ cảnh trao đổi gần đây"}. Trả về card bình chọn có câu hỏi, 2-5 lựa chọn và tùy chọn chọn nhiều; chỉ tạo draft để tôi chỉnh và xác nhận, không tự đăng.`,
  );
}

function analyzeSelectedMessages(action: "summary" | "task-draft", messageIds: string[]) {
  selectedAiRequest.value = { action, messageIds, nonce: Date.now() };
  activeTab.value = "ai";
  isDetailPanelCollapsed.value = false;
}

const currentUserId = computed(() => currentUser.value?.id ?? "me");
const activeGroup = computed(
  () => groups.value.find((group) => group.id === activeGroupId.value) ?? null,
);
const activeDetail = computed(() =>
  activeGroupId.value ? groupDetails.value[activeGroupId.value] : null,
);
const activeMessages = computed(() =>
  messages.value.filter((message) => message.groupId === activeGroupId.value),
);
const activePollMessages = computed(() =>
  activeMessages.value.filter((message) => message.poll?.id),
);
const sharedAttachments = computed(() =>
  activeMessages.value.flatMap((message) =>
    message.attachments.map((attachment) => ({
      ...attachment,
      messageId: message.id,
      senderName: message.senderName,
      createdAt: message.createdAt,
    })),
  ),
);
const sharedImages = computed(() =>
  sharedAttachments.value.filter((attachment) => attachment.kind === "image"),
);
const sharedFiles = computed(() =>
  sharedAttachments.value.filter((attachment) => attachment.kind !== "image"),
);
const canManageGroup = computed(() =>
  ["Owner", "Admin"].includes(activeDetail.value?.currentUserRole ?? ""),
);
const isGroupOwner = computed(() => activeDetail.value?.currentUserRole === "Owner");
const activeBackgroundTheme = computed(() => activeDetail.value?.backgroundTheme || "clean");
const activeBackgroundImage = computed(() => activeDetail.value?.backgroundImageUrl || "");
const availableUsers = computed(() => {
  const memberIds = new Set(members.value.map((member) => member.userId));
  return users.value.filter((user) => user.isActive && !memberIds.has(user.id));
});
const selectedAddUser = computed(() =>
  availableUsers.value.find((user) => user.id === addUserId.value) ?? null,
);
const filteredAvailableUsers = computed(() => {
  const query = addUserSearch.value.trim().toLowerCase();
  if (!query) return availableUsers.value.slice(0, 6);
  return availableUsers.value
    .filter((user) => user.email.toLowerCase().includes(query))
    .slice(0, 8);
});
const mentionMembers = computed<TeamChatMemberMention[]>(() =>
  members.value.map((member) => ({
    id: member.userId,
    name: member.fullName,
    initials: initials(member.fullName),
  })),
);
const activeTypingUsers = computed(() => Object.values(typingUsers.value).map((item) => item.name));

function roleLabel(role?: string) {
  if (!role) return "-";
  const map: Record<string, string> = { Owner: "Chủ nhóm", Admin: "Quản trị viên", Member: "Thành viên" };
  return map[role] ?? role;
}

function canManageMember(member: GroupMemberDto) {
  const currentRole = activeDetail.value?.currentUserRole;
  if (currentRole === "Owner") return member.role !== "Owner";
  if (currentRole === "Admin") return member.role === "Member";
  return false;
}

onMounted(async () => {
  await Promise.all([loadGroups(), loadUsers()]);
  await connectRealtime();
});

onBeforeUnmount(async () => {
  if (localTypingTimer) window.clearTimeout(localTypingTimer);
  Object.values(typingUsers.value).forEach((item) => window.clearTimeout(item.timeoutId));
  if (hubConnection) {
    await hubConnection.stop();
    hubConnection = null;
  }
});

watch(
  () => route.params.groupId,
  (groupId) => {
    if (typeof groupId === "string" && groupId && groupId !== activeGroupId.value) {
      activeGroupId.value = groupId;
    }
  },
);

watch(activeGroupId, async (next, previous) => {
  isActiveGroupReady.value = false;
  if (previous && hubConnection?.state === HubConnectionState.Connected) {
    realtimeState.value = "connecting";
    await hubConnection.invoke("TypingStopped", previous).catch(() => undefined);
    await hubConnection.invoke("LeaveGroup", previous).catch(() => undefined);
  }

  if (!next) return;
  typingUsers.value = {};
  lastTypingState = false;
  await Promise.all([loadGroupDetail(next), loadMessages(next), loadMembers(next)]);
  await markGroupRead(next);

  if (canManageGroup.value) {
    await loadInvitations(next);
  } else {
    invitations.value = [];
  }

  if (route.params.groupId !== next) {
    await router.replace({ name: "group-detail", params: { groupId: next } });
  }

  if (hubConnection?.state === HubConnectionState.Connected) {
    try {
      await hubConnection.invoke("JoinGroup", next);
      realtimeState.value = "connected";
    } catch {
      realtimeState.value = "offline";
    }
  }

  if (activeGroupId.value === next) isActiveGroupReady.value = true;
});

async function loadGroups() {
  isLoadingGroups.value = true;
  loadError.value = null;

  try {
    const result = await apiResult<PagedResult<GroupDto>>("/api/groups?pageSize=50");
    result.items.forEach((group) => {
      groupDetails.value[group.id] = group;
    });
    groups.value = result.items.map(toGroupModel);

    const routeGroupId = route.params.groupId;
    activeGroupId.value =
      typeof routeGroupId === "string" && routeGroupId
        ? routeGroupId
        : groups.value[0]?.id ?? "";
  } catch (error) {
    loadError.value = errorMessage(error, "Không thể tải danh sách nhóm chat.");
    showError(loadError.value);
  } finally {
    isLoadingGroups.value = false;
  }
}

async function loadGroupDetail(groupId: string) {
  try {
    const group = await apiResult<GroupDto>(`/api/groups/${groupId}`);
    groupDetails.value[group.id] = group;
    groups.value = groups.value.map((item) =>
      item.id === group.id ? toGroupModel(group) : item,
    );
  } catch (error) {
    showError(errorMessage(error, "Không thể tải chi tiết nhóm."));
  }
}

async function loadUsers() {
  try {
    users.value = await apiResult<UserDto[]>("/api/users");
  } catch (error) {
    showError(errorMessage(error, "Không thể tải danh sách tài khoản."));
  }
}

async function loadMembers(groupId: string) {
  try {
    members.value = await apiResult<GroupMemberDto[]>(`/api/groups/${groupId}/members`);
    addUserId.value = "";
    addUserSearch.value = "";
  } catch (error) {
    showError(errorMessage(error, "Không thể tải thành viên nhóm."));
  }
}

async function loadInvitations(groupId: string) {
  try {
    invitations.value = await apiResult<GroupInvitationDto[]>(
      `/api/groups/${groupId}/invitations?status=Pending`,
    );
  } catch (error) {
    invitations.value = [];
    showError(errorMessage(error, "Không thể tải lời mời đang chờ."));
  }
}

async function loadMessages(groupId: string) {
  isLoadingMessages.value = true;

  try {
    const result = await apiResult<PagedResult<GroupMessageDto>>(
      `/api/groups/${groupId}/messages?pageSize=100`,
    );
    const mapped = result.items.map(toMessageModel);
    messages.value = [
      ...messages.value.filter((message) => message.groupId !== groupId),
      ...mapped,
    ];
  } catch (error) {
    showError(errorMessage(error, "Không thể tải tin nhắn nhóm."));
  } finally {
    isLoadingMessages.value = false;
  }
}

async function markGroupRead(groupId: string) {
  if (!groupId) return;
  groups.value = groups.value.map((group) =>
    group.id === groupId ? { ...group, unreadCount: 0 } : group,
  );
  try {
    const updated = await apiResult<GroupDto>(`/api/groups/${groupId}/read`, { method: "POST" });
    groupDetails.value[updated.id] = updated;
  } catch {
    // A temporary read-receipt failure must not interrupt chat usage.
  }
}

async function toggleGroupMute() {
  if (!activeGroupId.value || !activeDetail.value) return;
  const isMuted = !activeDetail.value.isMuted;
  try {
    const updated = await apiResult<GroupDto>(
      `/api/groups/${activeGroupId.value}/notification-preference`,
      { method: "PUT", body: JSON.stringify({ isMuted }) },
    );
    groupDetails.value[updated.id] = updated;
    groups.value = groups.value.map((group) =>
      group.id === updated.id ? toGroupModel(updated) : group,
    );
    showSuccess(isMuted ? "Đã tắt thông báo nhóm" : "Đã bật thông báo nhóm");
  } catch (error) {
    showError(errorMessage(error, "Không thể cập nhật thông báo nhóm."));
  }
}

async function connectRealtime() {
  realtimeState.value = "connecting";
  hubConnection = new HubConnectionBuilder()
    .withUrl("/hubs/groups")
    .withAutomaticReconnect()
    .build();

  hubConnection.onreconnecting(() => {
    realtimeState.value = "connecting";
  });
  hubConnection.onreconnected(async () => {
    realtimeState.value = "connecting";
    try {
      if (activeGroupId.value) {
        await hubConnection?.invoke("JoinGroup", activeGroupId.value);
      }
      realtimeState.value = "connected";
    } catch {
      realtimeState.value = "offline";
    }
  });
  hubConnection.onclose(() => {
    realtimeState.value = "offline";
  });
  hubConnection.on("groupMessageReceived", (message: GroupMessageDto) => {
    const mapped = toMessageModel(message);
    upsertMessage(mapped);
    if (mapped.groupId === activeGroupId.value) void markGroupRead(mapped.groupId);
  });
  hubConnection.on("groupMessageChanged", (message: GroupMessageDto) => {
    upsertMessage(toMessageModel(message), false);
  });
  hubConnection.on("groupUpdated", (group: GroupDto) => {
    groupDetails.value[group.id] = group;
    groups.value = groups.value.map((item) =>
      item.id === group.id ? toGroupModel(group) : item,
    );
  });
  hubConnection.on("groupMemberRemoved", async (payload: { groupId?: string; userId?: string }) => {
    if (!payload?.groupId) return;
    if (payload.userId?.toLowerCase() === currentUserId.value.toLowerCase()) {
      await removeGroupLocally(payload.groupId, "Bạn đã được đưa ra khỏi nhóm.");
      return;
    }

    if (payload.groupId === activeGroupId.value) {
      await Promise.all([loadMembers(payload.groupId), loadGroupDetail(payload.groupId)]);
    }
  });
  hubConnection.on("groupRemoved", async (payload: { groupId?: string; reason?: string }) => {
    if (!payload?.groupId) return;
    await removeGroupLocally(
      payload.groupId,
      payload.reason === "dissolved" ? "Nhóm đã được giải tán." : "Nhóm đã bị xóa.",
    );
  });
  hubConnection.on("typingStarted", (payload: { groupId?: string; userId?: string; userName?: string }) => {
    handleTypingSignal(payload, true);
  });
  hubConnection.on("typingStopped", (payload: { groupId?: string; userId?: string; userName?: string }) => {
    handleTypingSignal(payload, false);
  });
  hubConnection.on("meetingStarted", async (payload: { groupId?: string }) => {
    const groupId = payload?.groupId;
    if (groupId && groupId === activeGroupId.value) await loadMessages(groupId);
  });
  hubConnection.on("meetingEnded", async (payload: { groupId?: string }) => {
    const groupId = payload?.groupId;
    if (groupId && groupId === activeGroupId.value) await loadMessages(groupId);
  });
  hubConnection.on("groupProjectCreated", async (payload: { groupId?: string }) => {
    if (payload?.groupId === activeGroupId.value) await loadDashboard();
  });

  try {
    await hubConnection.start();
    if (activeGroupId.value) {
      await hubConnection.invoke("JoinGroup", activeGroupId.value);
    }
    realtimeState.value = "connected";
  } catch {
    realtimeState.value = "offline";
  }
}

function handleTypingSignal(
  payload: { groupId?: string; userId?: string; userName?: string },
  isTyping: boolean,
) {
  if (!payload?.groupId || payload.groupId !== activeGroupId.value) return;
  if (!payload.userId || payload.userId === currentUserId.value) return;

  const next = { ...typingUsers.value };
  const existing = next[payload.userId];
  if (existing?.timeoutId) window.clearTimeout(existing.timeoutId);

  if (!isTyping) {
    delete next[payload.userId];
    typingUsers.value = next;
    return;
  }

  next[payload.userId] = {
    name: payload.userName || "Thành viên",
    timeoutId: window.setTimeout(() => {
      const latest = { ...typingUsers.value };
      delete latest[payload.userId!];
      typingUsers.value = latest;
    }, 2600),
  };
  typingUsers.value = next;
}

async function createGroup() {
  if (!createForm.value.name.trim()) return;

  try {
    const group = await apiResult<GroupDto>("/api/groups", {
      method: "POST",
      body: JSON.stringify({
        name: createForm.value.name.trim(),
        color: createForm.value.color,
      }),
    });

    groupDetails.value[group.id] = group;
    const mapped = toGroupModel(group);
    groups.value = [mapped, ...groups.value.filter((item) => item.id !== mapped.id)];
    showCreateModal.value = false;
    createForm.value = { name: "", color: "#2563eb" };
    activeGroupId.value = mapped.id;
    showSuccess(`Tạo nhóm "${mapped.name}" thành công`);
  } catch (error) {
    showError(errorMessage(error, "Không thể tạo nhóm chat."));
  }
}

async function inviteMember() {
  if (!activeGroupId.value || !inviteEmail.value.trim()) return;

  try {
    await apiResult<GroupInvitationDto>(`/api/groups/${activeGroupId.value}/invitations`, {
      method: "POST",
      body: JSON.stringify({ email: inviteEmail.value.trim() }),
    });
    inviteEmail.value = "";
    await loadInvitations(activeGroupId.value);
    showSuccess("Đã gửi lời mời thành viên");
  } catch (error) {
    showError(errorMessage(error, "Không thể mời thành viên."));
  }
}

async function addExistingMember() {
  if (!activeGroupId.value || !addUserId.value) return;

  try {
    await apiCommand(`/api/groups/${activeGroupId.value}/members`, {
      method: "POST",
      body: JSON.stringify({ userId: addUserId.value, role: addRole.value }),
    });
    await Promise.all([loadMembers(activeGroupId.value), loadGroupDetail(activeGroupId.value)]);
    addUserId.value = "";
    addUserSearch.value = "";
    showSuccess("Đã thêm thành viên vào nhóm");
  } catch (error) {
    showError(errorMessage(error, "Không thể thêm thành viên."));
  }
}

async function updateMemberRole(member: GroupMemberDto, role: string) {
  if (!activeGroupId.value || member.role === "Owner") return;

  try {
    await apiCommand(`/api/groups/${activeGroupId.value}/members/${member.userId}`, {
      method: "PUT",
      body: JSON.stringify({ role }),
    });
    await loadMembers(activeGroupId.value);
    showSuccess("Đã cập nhật role");
  } catch (error) {
    showError(errorMessage(error, "Không thể cập nhật role."));
  }
}

async function removeMember(member: GroupMemberDto) {
  if (!activeGroupId.value || member.role === "Owner") return;
  if (!await confirmDialog({ tone:"danger", title:"Xóa thành viên khỏi nhóm?", subject:member.fullName, message:"Thành viên sẽ mất quyền truy cập nhóm và nội dung mới.", confirmLabel:"Xóa thành viên" })) return;

  try {
    await apiCommand(`/api/groups/${activeGroupId.value}/members/${member.userId}`, {
      method: "DELETE",
    });
    await Promise.all([loadMembers(activeGroupId.value), loadGroupDetail(activeGroupId.value)]);
    showSuccess("Đã kick thành viên khỏi nhóm");
  } catch (error) {
    showError(errorMessage(error, "Không thể xóa thành viên."));
  }
}

async function leaveGroup() {
  if (!activeGroupId.value) return;
  if (isGroupOwner.value) {
    showError("Chủ nhóm cần giải tán nhóm hoặc chuyển quyền trước khi rời.");
    return;
  }
  const groupName = activeGroup.value?.name ?? "nhóm này";
  if (!await confirmDialog({ tone:"warning", title:"Rời khỏi nhóm?", subject:groupName, message:"Bạn sẽ không còn xem được tin nhắn và nội dung trong nhóm.", confirmLabel:"Rời nhóm" })) return;

  const leavingGroupId = activeGroupId.value;
  try {
    await apiCommand(`/api/groups/${leavingGroupId}/members/me`, { method: "DELETE" });
    groups.value = groups.value.filter((group) => group.id !== leavingGroupId);
    delete groupDetails.value[leavingGroupId];
    messages.value = messages.value.filter((message) => message.groupId !== leavingGroupId);
    activeGroupId.value = groups.value[0]?.id ?? "";
    if (activeGroupId.value) {
      await router.replace({ name: "group-detail", params: { groupId: activeGroupId.value } });
    } else {
      await router.replace({ name: "groups" });
    }
    showSuccess("Bạn đã rời nhóm");
  } catch (error) {
    showError(errorMessage(error, "Không thể rời nhóm."));
  }
}

async function dissolveGroup() {
  if (!activeGroupId.value || !isGroupOwner.value) return;
  const groupName = activeGroup.value?.name ?? "nhóm này";
  const confirmed = await confirmDialog({ tone:"critical", title:"Giải tán nhóm?", subject:groupName, message:"Tất cả thành viên sẽ bị đưa ra khỏi nhóm và nhóm sẽ biến mất khỏi danh sách.", confirmLabel:"Giải tán nhóm", requireText:groupName });
  if (!confirmed) return;

  const dissolvedGroupId = activeGroupId.value;
  try {
    await apiCommand(`/api/groups/${dissolvedGroupId}/dissolve`, { method: "DELETE" });
    groups.value = groups.value.filter((group) => group.id !== dissolvedGroupId);
    delete groupDetails.value[dissolvedGroupId];
    messages.value = messages.value.filter((message) => message.groupId !== dissolvedGroupId);
    activeGroupId.value = groups.value[0]?.id ?? "";
    if (activeGroupId.value) {
      await router.replace({ name: "group-detail", params: { groupId: activeGroupId.value } });
    } else {
      await router.replace({ name: "groups" });
    }
    showSuccess("Đã giải tán nhóm");
  } catch (error) {
    showError(errorMessage(error, "Không thể giải tán nhóm."));
  }
}

async function removeGroupLocally(groupId: string, message?: string) {
  const hadGroup = groups.value.some((group) => group.id === groupId);
  if (!hadGroup) return;

  const wasActive = activeGroupId.value === groupId;
  groups.value = groups.value.filter((group) => group.id !== groupId);
  delete groupDetails.value[groupId];
  messages.value = messages.value.filter((item) => item.groupId !== groupId);

  if (!wasActive) {
    if (message) showSuccess(message);
    return;
  }

  activeGroupId.value = groups.value[0]?.id ?? "";
  if (activeGroupId.value) {
    await router.replace({ name: "group-detail", params: { groupId: activeGroupId.value } });
  } else {
    await router.replace({ name: "groups" });
  }
  if (message) showSuccess(message);
}

async function startMeeting() {
  if (!activeGroupId.value) return;

  try {
    const meeting = await apiResult<any>(`/api/groups/${activeGroupId.value}/meetings/start`, {
      method: "POST",
    });
    showSuccess("Đã tạo phiên meeting cho nhóm");
    await router.push({
      name: "group-meeting",
      params: { groupId: activeGroupId.value },
      query: { meetingId: meeting?.id ?? meeting?.Id },
    });
  } catch (error) {
    showError(errorMessage(error, "Không thể bắt đầu meeting."));
  }
}

async function createProjectFromGroup() {
  if (!activeGroupId.value || !projectForm.value.name.trim() || isCreatingProject.value) return;

  isCreatingProject.value = true;
  try {
    const result = await apiResult<any>(`/api/groups/${activeGroupId.value}/create-project`, {
      method: "POST",
      body: JSON.stringify({
        name: projectForm.value.name.trim(),
        code: projectForm.value.code.trim() || null,
        description: projectForm.value.description.trim() || null,
      }),
    });
    const project = result.project ?? result.Project;
    const refreshed = await loadDashboard();
    projectForm.value = { name: "", code: "", description: "" };
    lastCreatedProject.value = project?.id
      ? { id: project.id, name: project.name || "Dự án mới" }
      : null;
    if (refreshed) {
      showSuccess(`Đã tạo project từ nhóm (${result.membersAdded ?? 0} thành viên) và cập nhật toàn ứng dụng.`);
    } else {
      showError("Project đã được tạo, nhưng danh sách chung chưa tải lại được. Hãy thử mở dự án bằng liên kết bên dưới.");
    }
  } catch (error) {
    showError(errorMessage(error, "Không thể tạo project từ nhóm."));
  } finally {
    isCreatingProject.value = false;
  }
}

function openLastCreatedProject() {
  if (lastCreatedProject.value) selectProject(lastCreatedProject.value.id);
}

async function createPanelPoll() {
  if (!activeGroupId.value || isCreatingPoll.value) return;

  const question = pollForm.value.question.trim();
  const options = pollForm.value.options.map((option) => option.trim()).filter(Boolean);
  if (!question) {
    showError("Vui lòng nhập câu hỏi bình chọn.");
    return;
  }
  if (options.length < 2) {
    showError("Poll cần ít nhất 2 lựa chọn.");
    return;
  }

  isCreatingPoll.value = true;
  let createdPollId = "";
  try {
    const poll = await apiResult<any>(`/api/groups/${activeGroupId.value}/polls`, {
      method: "POST",
      body: JSON.stringify({
        question,
        options: options.map((content) => ({ content })),
        allowMultiple: pollForm.value.allowMultiple,
      }),
    });
    createdPollId = poll.id ?? poll.Id;

    const sent = await sendMessage({
      text: "",
      attachments: [],
      poll: {
        id: createdPollId,
        question,
        options,
      },
    });
    if (!sent) {
      await apiCommand(`/api/groups/${activeGroupId.value}/polls/${createdPollId}`, {
        method: "DELETE",
      }).catch(() => undefined);
      return;
    }

    pollForm.value = { question: "", options: ["", ""], allowMultiple: false };
    showSuccess("Đã tạo bình chọn trong nhóm");
  } catch (error) {
    if (createdPollId) {
      await apiCommand(`/api/groups/${activeGroupId.value}/polls/${createdPollId}`, {
        method: "DELETE",
      }).catch(() => undefined);
    }
    showError(errorMessage(error, "Không thể tạo bình chọn."));
  } finally {
    isCreatingPoll.value = false;
  }
}

function addPollOption() {
  pollForm.value.options.push("");
}

function removePollOption(index: number) {
  if (pollForm.value.options.length <= 2) return;
  pollForm.value.options.splice(index, 1);
}

async function sendMessage(payload: {
  text: string;
  attachments: TeamChatAttachment[];
  poll?: TeamChatPoll;
  replyToMessageId?: string;
}) {
  if (!activeGroupId.value) return false;

  try {
    const uploadedAttachments = await uploadAttachments(payload.attachments);
    const content = serializeMessagePayload({
      ...payload,
      attachments: uploadedAttachments,
    });
    const messageType = payload.poll ? "Poll" : "Text";

    if (
      hubConnection?.state === HubConnectionState.Connected &&
      realtimeState.value === "connected"
    ) {
      try {
        const saved = await hubConnection.invoke<GroupMessageDto>(
          "SendMessage",
          activeGroupId.value,
          content,
          messageType,
          payload.replyToMessageId ?? null,
        );
        upsertMessage(toMessageModel(saved));
        return true;
      } catch {
        realtimeState.value = "offline";
      }
    }

    const saved = await apiResult<GroupMessageDto>(
      `/api/groups/${activeGroupId.value}/messages`,
      {
        method: "POST",
        body: JSON.stringify({ content, messageType, replyToMessageId: payload.replyToMessageId ?? null }),
      },
    );
    upsertMessage(toMessageModel(saved));
    return true;
  } catch (error) {
    showError(errorMessage(error, "Không thể gửi tin nhắn."));
    return false;
  }
}

async function setTypingState(isTyping: boolean) {
  if (!activeGroupId.value || hubConnection?.state !== HubConnectionState.Connected) return;

  if (localTypingTimer) window.clearTimeout(localTypingTimer);

  if (isTyping) {
    if (!lastTypingState) {
      await hubConnection.invoke("TypingStarted", activeGroupId.value).catch(() => undefined);
      lastTypingState = true;
    }
    localTypingTimer = window.setTimeout(() => {
      void setTypingState(false);
    }, 1800);
    return;
  }

  if (lastTypingState) {
    await hubConnection.invoke("TypingStopped", activeGroupId.value).catch(() => undefined);
    lastTypingState = false;
  }
}

async function reactToMessage(messageId: string, emoji: string) {
  if (!activeGroupId.value) return;

  try {
    if (hubConnection?.state === HubConnectionState.Connected) {
      await hubConnection.invoke("ReactToMessage", activeGroupId.value, messageId, emoji);
      return;
    }

    const updated = await apiResult<GroupMessageDto>(
      `/api/groups/${activeGroupId.value}/messages/${messageId}/reactions`,
      {
        method: "POST",
        body: JSON.stringify({ emoji }),
      },
    );
    upsertMessage(toMessageModel(updated), false);
  } catch (error) {
    showError(errorMessage(error, "Không thể thả cảm xúc."));
  }
}

async function uploadAttachments(attachments: TeamChatAttachment[]) {
  const uploaded: TeamChatAttachment[] = [];

  for (const attachment of attachments) {
    if (!attachment.sourceFile) {
      uploaded.push(attachment);
      continue;
    }

    const formData = new FormData();
    formData.append("file", attachment.sourceFile);
    const result = await apiResult<GroupAttachmentDto>(
      `/api/groups/${activeGroupId.value}/attachments`,
      {
        method: "POST",
        body: formData,
      },
    );
    const url = `/api/groups/${activeGroupId.value}/attachments/${result.id}`;
    uploaded.push({
      id: result.id,
      name: result.fileName,
      contentType: result.contentType,
      sizeLabel: formatFileSize(result.fileSize),
      kind: attachmentKind(result.fileName, result.contentType),
      url,
      downloadUrl: `${url}?download=true`,
    });
  }

  return uploaded;
}

async function changeGroupAvatar(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = "";
  if (!file || !activeGroupId.value || !canManageGroup.value) return;

  if (!["image/jpeg", "image/png", "image/gif", "image/webp"].includes(file.type)) {
    showError("Avatar phải là ảnh JPG, PNG, GIF hoặc WEBP.");
    return;
  }
  if (file.size > 5 * 1024 * 1024) {
    showError("Avatar không được vượt quá 5 MB.");
    return;
  }

  isUploadingAvatar.value = true;
  try {
    const formData = new FormData();
    formData.append("file", file);
    const updated = await apiResult<GroupDto>(
      `/api/groups/${activeGroupId.value}/avatar`,
      {
        method: "POST",
        body: formData,
      },
    );
    groupDetails.value[updated.id] = updated;
    groups.value = groups.value.map((group) =>
      group.id === updated.id ? toGroupModel(updated) : group,
    );
    showSuccess("Đã cập nhật ảnh đại diện nhóm");
  } catch (error) {
    showError(errorMessage(error, "Không thể cập nhật ảnh đại diện nhóm."));
  } finally {
    isUploadingAvatar.value = false;
  }
}

async function editMessage(messageId: string, text: string) {
  if (!activeGroupId.value || !text.trim()) return;
  try {
    const updated = await apiResult<GroupMessageDto>(
      `/api/groups/${activeGroupId.value}/messages/${messageId}`,
      {
        method: "PATCH",
        body: JSON.stringify({ content: text.trim() }),
      },
    );
    upsertMessage(toMessageModel(updated), false);
    showSuccess("Đã chỉnh sửa tin nhắn");
  } catch (error) {
    showError(errorMessage(error, "Không thể chỉnh sửa tin nhắn."));
  }
}

async function recallMessage(messageId: string) {
  if (!activeGroupId.value) return;
  try {
    const updated = await apiResult<GroupMessageDto>(
      `/api/groups/${activeGroupId.value}/messages/${messageId}/recall`,
      { method: "POST" },
    );
    upsertMessage(toMessageModel(updated), false);
    showSuccess("Đã thu hồi tin nhắn");
  } catch (error) {
    showError(errorMessage(error, "Không thể thu hồi tin nhắn."));
  }
}

async function setMessagePin(messageId: string, isPinned: boolean) {
  if (!activeGroupId.value) return;
  try {
    const updated = await apiResult<GroupMessageDto>(
      `/api/groups/${activeGroupId.value}/messages/${messageId}/pin`,
      {
        method: "PUT",
        body: JSON.stringify({ isPinned }),
      },
    );
    upsertMessage(toMessageModel(updated), false);
    showSuccess(isPinned ? "Đã ghim tin nhắn" : "Đã bỏ ghim tin nhắn");
  } catch (error) {
    showError(errorMessage(error, "Không thể cập nhật ghim."));
  }
}

async function hideMessages(messageIds: string[]) {
  if (!activeGroupId.value || !messageIds.length) return;
  try {
    await Promise.all(
      messageIds.map((messageId) =>
        apiCommand(`/api/groups/${activeGroupId.value}/messages/${messageId}/for-me`, {
          method: "DELETE",
        }),
      ),
    );
    const hidden = new Set(messageIds);
    messages.value = messages.value.filter((message) => !hidden.has(message.id));
    showSuccess(
      messageIds.length > 1
        ? "Đã xóa các tin nhắn ở phía bạn"
        : "Đã xóa tin nhắn ở phía bạn",
    );
  } catch (error) {
    showError(errorMessage(error, "Không thể xóa tin nhắn."));
  }
}

async function hideSharedAttachment(messageId: string) {
  if (!await confirmDialog({ tone:"danger", title:"Ẩn tin nhắn chứa tệp?", message:"Tin nhắn chỉ bị ẩn ở phía bạn.", confirmLabel:"Ẩn tin nhắn" })) return;
  void hideMessages([messageId]);
}

function joinMeeting(meetingId: string) {
  const meeting = messages.value.find((message) => message.meeting?.id === meetingId)?.meeting;
  if (meeting?.joinUrl) {
    window.open(meeting.joinUrl, "_blank", "noopener,noreferrer");
    return;
  }

  void router.push({ name: "group-meeting", params: { groupId: activeGroupId.value } });
}

function selectGroup(groupId: string) {
  activeGroupId.value = groupId;
}

async function setChatBackground(theme: string) {
  if (!activeGroupId.value || !canManageGroup.value) return;

  try {
    const updated = await apiResult<GroupDto>(`/api/groups/${activeGroupId.value}/background`, {
      method: "PATCH",
      body: JSON.stringify({ theme, imageUrl: null }),
    });
    groupDetails.value[updated.id] = updated;
    groups.value = groups.value.map((group) =>
      group.id === updated.id ? toGroupModel(updated) : group,
    );
    showSuccess("Đã đổi nền nhóm");
  } catch (error) {
    showError(errorMessage(error, "Không thể đổi nền nhóm."));
  }
}

async function setChatBackgroundImage(file: File | null) {
  if (!activeGroupId.value || !canManageGroup.value) return;

  try {
    const updated = file
      ? await uploadGroupBackgroundImage(file)
      : await apiResult<GroupDto>(`/api/groups/${activeGroupId.value}/background`, {
          method: "PATCH",
          body: JSON.stringify({ theme: "clean", imageUrl: null }),
        });
    groupDetails.value[updated.id] = updated;
    groups.value = groups.value.map((group) =>
      group.id === updated.id ? toGroupModel(updated) : group,
    );
    showSuccess(file ? "Đã đổi ảnh nền nhóm" : "Đã đặt lại nền nhóm");
  } catch (error) {
    showError(errorMessage(error, "Không thể cập nhật ảnh nền nhóm."));
  }
}

async function uploadGroupBackgroundImage(file: File) {
  const formData = new FormData();
  formData.append("file", file);
  return await apiResult<GroupDto>(`/api/groups/${activeGroupId.value}/background-image`, {
    method: "POST",
    body: formData,
  });
}

function upsertMessage(message: TeamChatMessage, incrementUnread = true) {
  const existingIndex = messages.value.findIndex((item) => item.id === message.id);
  if (existingIndex >= 0) {
    const next = [...messages.value];
    next[existingIndex] = message;
    messages.value = next;
  } else {
    messages.value = [...messages.value, message];
  }

  if (!incrementUnread) return;
  groups.value = groups.value.map((group) =>
    group.id === message.groupId
      ? {
          ...group,
          unreadCount: group.id === activeGroupId.value ? 0 : group.unreadCount + 1,
        }
      : group,
  );
}

function toGroupModel(group: GroupDto): ChatGroupModel {
  return {
    id: group.id,
    name: group.name,
    avatarUrl: group.avatarUrl ?? undefined,
    summary: `${group.memberCount ?? 0} thành viên · ${group.messageCount ?? 0} tin nhắn`,
    unreadCount: group.unreadCount ?? 0,
  };
}

function toMessageModel(message: GroupMessageDto): TeamChatMessage {
  const meeting = parseMeeting(message);
  const attachments = parseAttachments(message);
  
  let cleanText = message.content;
  if (message.messageType === "Poll" || meeting) {
    cleanText = "";
  } else if (attachments.length > 0) {
    cleanText = cleanText
      .replace(/\[attachments\][^\n]*/gi, "")
      .replace(/\[attachment\][^\n]*/gi, "")
      .trim();
  }

  return {
    id: message.id,
    groupId: message.workGroupId,
    senderId: message.userId,
    senderName: message.senderName,
    senderInitials: initials(message.senderName),
    text: cleanText,
    createdAt: formatMessageTime(message.createdAt),
    createdAtRaw: message.createdAt,
    messageType: message.messageType,
    isDeleted: message.isDeleted,
    editedAt: message.editedAt ?? undefined,
    pinned: message.isPinned,
    pinnedAt: message.pinnedAt ?? undefined,
    pinnedByUserId: message.pinnedByUserId ?? undefined,
    attachments,
    reactions: toMessageReactions(message.reactions ?? []),
    poll: message.isDeleted ? undefined : parsePoll(message),
    meeting,
    replyTo: toMessageReferenceModel(message.replyTo),
    forwardedFrom: toMessageReferenceModel(message.forwardedFrom),
  };
}

function updateAddUserSearch(value: string) {
  addUserSearch.value = value;
  if (selectedAddUser.value?.email.toLowerCase() !== value.trim().toLowerCase()) {
    addUserId.value = "";
  }
  showAddUserSuggestions.value = true;
}

function selectAddUser(user: UserDto) {
  addUserId.value = user.id;
  addUserSearch.value = user.email;
  showAddUserSuggestions.value = false;
}

function closeAddUserSuggestions() {
  window.setTimeout(() => {
    showAddUserSuggestions.value = false;
  }, 140);
}

async function forwardMessage(messageId: string, targetGroupId: string) {
  if (!activeGroupId.value || !targetGroupId) return;
  try {
    const forwarded = await apiResult<GroupMessageDto>(
      `/api/groups/${activeGroupId.value}/messages/${messageId}/forward`,
      {
        method: "POST",
        body: JSON.stringify({ targetGroupId }),
      },
    );
    if (targetGroupId === activeGroupId.value) upsertMessage(toMessageModel(forwarded));
    showSuccess("Đã chuyển tiếp tin nhắn");
  } catch (error) {
    showError(errorMessage(error, "Không thể chuyển tiếp tin nhắn."));
  }
}

function toMessageReferenceModel(reference: GroupMessageReferenceDto | null) {
  if (!reference) return undefined;
  return {
    id: reference.id,
    groupId: reference.workGroupId,
    senderId: reference.userId,
    senderName: reference.senderName,
    text: reference.isDeleted ? "Tin nhắn đã được thu hồi" : cleanReferenceText(reference),
    messageType: reference.messageType,
    isDeleted: reference.isDeleted,
    attachments: parseAttachmentsContent(reference.content, reference.workGroupId),
  };
}

function cleanReferenceText(reference: GroupMessageReferenceDto) {
  if (reference.messageType === "Poll" || reference.messageType === "Meeting") return reference.messageType === "Poll" ? "Bình chọn" : "Cuộc họp nhóm";
  return reference.content
    .replace(/\[attachments\][^\n]*/gi, "")
    .replace(/\[attachment\][^\n]*/gi, "")
    .trim();
}

function toMessageReactions(reactions: GroupMessageReactionDto[]): TeamChatReaction[] {
  return reactions.map((reaction) => ({
    emoji: reaction.emoji,
    count: reaction.count,
    userIds: reaction.userIds,
    reactedByCurrentUser:
      reaction.reactedByCurrentUser ||
      reaction.userIds.some((userId) => userId.toLowerCase() === currentUserId.value.toLowerCase()),
  }));
}

function serializeMessagePayload(payload: {
  text: string;
  attachments: TeamChatAttachment[];
  poll?: TeamChatPoll;
}) {
  const lines = [payload.text];

  if (payload.poll) {
    lines.push(`[poll] ${payload.poll.question}`);
    if (payload.poll.id) lines.push(`[pollid] ${payload.poll.id}`);
    payload.poll.options.forEach((option, index) => lines.push(`${index + 1}. ${option}`));
  }

  if (payload.attachments.length > 0) {
    payload.attachments.forEach((file) => {
      lines.push(
        `[attachment] ${file.id ?? ""}|${encodeURIComponent(file.name)}|${encodeURIComponent(file.contentType ?? "application/octet-stream")}|${file.sizeLabel}`,
      );
    });
  }

  return lines.filter(Boolean).join("\n");
}

function parsePoll(message: GroupMessageDto): TeamChatPoll | undefined {
  if (message.messageType !== "Poll") return undefined;

  const lines = message.content
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean);
  const pollIdLine = lines.find((line) => line.startsWith("[pollid] "));
  const question = lines.find((line) => line.startsWith("[poll] "))?.replace("[poll] ", "");
  const options = lines
    .filter((line) => /^\d+\.\s+/.test(line))
    .map((line) => line.replace(/^\d+\.\s+/, ""));
  const poll: TeamChatPoll | undefined =
    question && options.length >= 2 ? { question, options } : undefined;
  if (poll && pollIdLine) poll.id = pollIdLine.replace("[pollid] ", "");
  return poll;
}

function parseMeeting(message: GroupMessageDto): TeamChatMeeting | undefined {
  if (message.messageType === "Poll" || !message.content.includes("[meeting")) return undefined;

  const meetingId = message.content.match(/\[meetingid\]\s*([^\s]+)/i)?.[1] ?? message.id;
  const joinUrl = message.content.match(/\[joinurl\]\s*(\S+)/i)?.[1];
  const ended = /\[meeting-ended\]/i.test(message.content);
  const started = /\[meeting-started\]/i.test(message.content);

  if (!started && !ended) return undefined;

  return {
    id: meetingId,
    joinUrl,
    active: started && !ended,
    text: ended
      ? "Cuộc họp nhóm đã kết thúc."
      : `${message.senderName} đã bắt đầu cuộc họp nhóm.`,
  };
}

function parseAttachments(message: GroupMessageDto): TeamChatAttachment[] {
  return parseAttachmentsContent(message.content, message.workGroupId);
}

function parseAttachmentsContent(content: string, groupId: string): TeamChatAttachment[] {
  const structured = content
    .split("\n")
    .filter((line) => line.startsWith("[attachment] "))
    .map((line) => {
      const [id, encodedName, encodedContentType, sizeLabel] = line
        .replace("[attachment] ", "")
        .split("|");
      const name = decodeURIComponent(encodedName ?? "");
      const contentType = decodeURIComponent(encodedContentType ?? "application/octet-stream");
      const url = id ? `/api/groups/${groupId}/attachments/${id}` : undefined;
      return {
        id: id || undefined,
        name,
        contentType,
        sizeLabel: sizeLabel || "Đã tải lên",
        kind: attachmentKind(name, contentType),
        url,
        downloadUrl: url ? `${url}?download=true` : undefined,
      } satisfies TeamChatAttachment;
    })
    .filter((attachment) => attachment.name);
  if (structured.length) return structured;

  const match = content.match(/\[attachments\]\s*(.+)/i);
  if (!match) return [];
  
  const names = match[1].split(",").map(name => name.trim()).filter(Boolean);
  return names.map(name => {
    const ext = name.split(".").pop()?.toLowerCase();
    const isImage = ext && ["png", "jpg", "jpeg", "gif", "webp", "svg"].includes(ext);
    return {
      name,
      sizeLabel: "Đã tải lên",
      kind: isImage ? "image" : "file"
    };
  });
}

function attachmentKind(
  fileName: string,
  contentType?: string,
): TeamChatAttachment["kind"] {
  const previewableImages = ["image/jpeg", "image/png", "image/gif", "image/webp"];
  if (contentType && previewableImages.includes(contentType.toLowerCase())) return "image";
  if (contentType?.startsWith("video/")) return "video";

  const ext = fileName.split(".").pop()?.toLowerCase();
  if (ext && ["png", "jpg", "jpeg", "gif", "webp"].includes(ext)) return "image";
  if (ext && ["mp4", "webm", "mov", "avi", "mkv"].includes(ext)) return "video";
  return "file";
}

function formatFileSize(size: number) {
  if (size < 1024) return `${size} B`;
  if (size < 1024 * 1024) return `${Math.round(size / 1024)} KB`;
  return `${(size / 1024 / 1024).toFixed(1)} MB`;
}

function initials(name: string) {
  return name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");
}

function formatMessageTime(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  return new Intl.DateTimeFormat("vi", {
    hour: "2-digit",
    minute: "2-digit",
  }).format(date);
}
</script>

<template>
  <div class="dashboard-scroll dashboard-scroll--embedded no-scrollbar">
    <div class="dashboard-main project-home-main no-scrollbar">
      <h1 class="sr-only">Nhóm và cộng tác</h1>
      <section
        class="team-chat-page groups-workspace"
        :class="{ 'groups-workspace--detail-collapsed': isDetailPanelCollapsed }"
      >
        <div v-if="loadError" class="team-chat-banner team-chat-banner--error" role="alert">
          {{ loadError }}
        </div>
        <div v-else-if="isLoadingGroups" class="team-chat-banner">
          Đang tải nhóm chat...
        </div>

        <ChatSidebar
          :groups="groups"
          :active-group-id="activeGroupId"
          @select="selectGroup"
          @create="showCreateModal = true"
        />

        <ChatWindow
          :group="activeGroup"
          :messages="activeMessages"
          :current-user-id="currentUserId"
          :members="mentionMembers"
          :typing-users="activeTypingUsers"
          :background-theme="activeBackgroundTheme"
          :background-image="activeBackgroundImage"
          :can-customize-background="canManageGroup"
          :available-groups="groups"
          :can-send="isActiveGroupReady"
          @send="sendMessage"
          @edit="editMessage"
          @pin="setMessagePin"
          @recall="recallMessage"
          @hide="hideMessages"
          @react="reactToMessage"
          @forward="forwardMessage"
          @typing="setTypingState"
          @set-background="setChatBackground"
          @set-background-image="setChatBackgroundImage"
          @join-meeting="joinMeeting"
          @analyze-selection="analyzeSelectedMessages"
        />

        <aside class="group-detail-panel glass-card" :class="{ 'is-collapsed': isDetailPanelCollapsed }">
          <button
            v-if="isDetailPanelCollapsed"
            class="group-detail-collapse-button"
            type="button"
            aria-label="Mở chi tiết nhóm"
            @click="isDetailPanelCollapsed = false"
          >
            <PanelRightOpen :size="18" />
          </button>

          <div v-else class="group-detail-content">
          <header class="group-detail-header">
            <div class="group-detail-titlebar">
              <strong>Thông tin nhóm</strong>
              <div class="group-detail-titlebar__actions">
                <button
                  class="group-detail-icon-button"
                  type="button"
                  :aria-label="activeDetail?.isMuted ? 'Bật thông báo nhóm' : 'Tắt thông báo nhóm'"
                  :title="activeDetail?.isMuted ? 'Bật thông báo nhóm' : 'Tắt thông báo nhóm'"
                  @click="toggleGroupMute"
                >
                  <BellOff v-if="activeDetail?.isMuted" :size="17" />
                  <Bell v-else :size="17" />
                </button>
                <button
                  class="group-detail-icon-button"
                  type="button"
                  aria-label="Thu gọn chi tiết nhóm"
                  @click="isDetailPanelCollapsed = true"
                >
                  <PanelRightClose :size="17" />
                </button>
              </div>
            </div>

            <div class="group-detail-profile">
              <div class="group-detail-avatar">
                <img
                  v-if="activeDetail?.avatarUrl"
                  :src="activeDetail.avatarUrl"
                  :alt="activeGroup?.name ?? 'Ảnh nhóm'"
                />
                <span v-else>{{ initials(activeGroup?.name ?? "Qaly") }}</span>
                <label
                  v-if="canManageGroup"
                  class="group-detail-avatar__edit"
                  :class="{ 'is-loading': isUploadingAvatar }"
                  :aria-label="isUploadingAvatar ? 'Đang tải ảnh lên' : 'Thay ảnh đại diện nhóm'"
                >
                  <Camera :size="17" />
                  <input
                    type="file"
                    accept="image/jpeg,image/png,image/gif,image/webp"
                    :disabled="isUploadingAvatar"
                    @change="changeGroupAvatar"
                  />
                </label>
              </div>
              <div class="group-detail-profile__copy">
                <h2>{{ activeGroup?.name ?? "Chọn nhóm" }}</h2>
                <p>
                  {{ activeDetail?.memberCount ?? members.length }} thành viên
                  <span aria-hidden="true">·</span>
                  {{ activeDetail?.messageCount ?? 0 }} tin nhắn
                </p>
              </div>
              <div class="group-role-badge">
                <Crown v-if="activeDetail?.currentUserRole === 'Owner'" :size="14" />
                <ShieldCheck v-else-if="activeDetail?.currentUserRole === 'Admin'" :size="14" />
                <UserRound v-else :size="14" />
                {{ roleLabel(activeDetail?.currentUserRole) }}
              </div>
            </div>
          </header>

          <nav class="group-detail-tabs" aria-label="Công cụ nhóm">
            <button type="button" :class="{ active: activeTab === 'members' }" :aria-pressed="activeTab === 'members'" @click="activeTab = 'members'">
              <span><Users :size="19" /></span>
              Thành viên
            </button>
            <button type="button" :class="{ active: activeTab === 'invites' }" :aria-pressed="activeTab === 'invites'" @click="activeTab = 'invites'">
              <span><Mail :size="19" /></span>
              Lời mời
            </button>
            <button type="button" :class="{ active: activeTab === 'polls' }" :aria-pressed="activeTab === 'polls'" @click="activeTab = 'polls'">
              <span><Vote :size="19" /></span>
              Bình chọn
            </button>
            <button type="button" :class="{ active: activeTab === 'meeting' }" :aria-pressed="activeTab === 'meeting'" @click="activeTab = 'meeting'">
              <span><CalendarDays :size="19" /></span>
              Cuộc họp
            </button>
            <button type="button" :class="{ active: activeTab === 'project' }" :aria-pressed="activeTab === 'project'" @click="activeTab = 'project'">
              <span><Settings :size="19" /></span>
              Dự án
            </button>
            <button type="button" :class="{ active: activeTab === 'ai' }" :aria-pressed="activeTab === 'ai'" @click="activeTab = 'ai'">
              <span><Sparkles :size="19" /></span>
              AI
            </button>
          </nav>

          <div v-if="activeTab === 'members'" class="group-tool-body">
            <div class="group-tool-heading">
              <div>
                <strong>Thành viên nhóm</strong>
                <span>{{ members.length }} người đang tham gia</span>
              </div>
              <ShieldCheck :size="19" />
            </div>

            <form v-if="canManageGroup" class="group-inline-form" @submit.prevent="addExistingMember">
              <div class="group-user-search">
                <Search :size="17" />
                <input
                  :value="addUserSearch"
                  type="email"
                  autocomplete="off"
                  placeholder="Tìm tài khoản bằng email..."
                  aria-label="Tìm tài khoản bằng email"
                  @input="updateAddUserSearch(($event.target as HTMLInputElement).value)"
                  @focus="showAddUserSuggestions = true"
                  @blur="closeAddUserSuggestions"
                />
                <button
                  v-if="addUserSearch"
                  type="button"
                  aria-label="Xóa email tìm kiếm"
                  @mousedown.prevent="addUserSearch = ''; addUserId = ''; showAddUserSuggestions = true"
                >
                  <X :size="15" />
                </button>

                <div v-if="showAddUserSuggestions" class="group-user-suggestions">
                  <button
                    v-for="user in filteredAvailableUsers"
                    :key="user.id"
                    type="button"
                    :class="{ 'is-selected': user.id === addUserId }"
                    :aria-pressed="user.id === addUserId"
                    @mousedown.prevent="selectAddUser(user)"
                  >
                    <span class="group-user-suggestion__avatar">{{ initials(user.fullName) }}</span>
                    <span class="group-user-suggestion__identity">
                      <strong>{{ user.email }}</strong>
                      <small>{{ user.fullName }}</small>
                    </span>
                    <Check v-if="user.id === addUserId" :size="16" />
                  </button>
                  <div v-if="!filteredAvailableUsers.length" class="group-user-suggestions__empty">
                    Không tìm thấy tài khoản nào với email này.
                  </div>
                </div>
              </div>
              <select v-model="addRole" aria-label="Vai trò của thành viên được thêm">
                <option value="Member">Thành viên</option>
                <option value="Admin">Quản trị viên</option>
              </select>
              <button class="primary-button primary-button--compact" type="submit" :disabled="!addUserId">
                  <UserPlus :size="15" /> Thêm
              </button>
            </form>

            <div class="group-member-list">
              <article v-for="member in members" :key="member.userId" class="group-member-row">
                <div class="group-avatar">{{ initials(member.fullName) }}</div>
                <div class="group-member-identity">
                  <strong>{{ member.fullName }}</strong>
                  <span>{{ member.email }}</span>
                </div>
                <div class="group-member-actions">
                  <label
                    class="group-member-role"
                    :class="`group-member-role--${member.role.toLowerCase()}`"
                  >
                    <Crown v-if="member.role === 'Owner'" :size="13" />
                    <ShieldCheck v-else-if="member.role === 'Admin'" :size="13" />
                    <UserRound v-else :size="13" />
                    <select
                      v-if="canManageMember(member)"
                      :value="member.role"
                      aria-label="Vai trò thành viên"
                      @change="updateMemberRole(member, ($event.target as HTMLSelectElement).value)"
                    >
                      <option value="Member">Thành viên</option>
                      <option value="Admin">Quản trị viên</option>
                    </select>
                    <span v-else>{{ roleLabel(member.role) }}</span>
                  </label>
                  <button
                    v-if="canManageMember(member)"
                    class="group-member-remove"
                    type="button"
                    @click="removeMember(member)"
                  >
                    Xóa khỏi nhóm
                  </button>
                </div>
              </article>
            </div>

            <div class="group-danger-zone">
              <button
                v-if="!isGroupOwner"
                class="group-danger-action"
                type="button"
                @click="leaveGroup"
              >
                <LogOut :size="16" />
                Rời khỏi nhóm
              </button>
              <button
                v-if="isGroupOwner"
                class="group-danger-action group-danger-action--strong"
                type="button"
                @click="dissolveGroup"
              >
                <Trash2 :size="16" />
                Giải tán nhóm
              </button>
            </div>
          </div>

          <div v-else-if="activeTab === 'invites'" class="group-tool-body">
            <form v-if="canManageGroup" class="group-inline-form" @submit.prevent="inviteMember">
              <input v-model="inviteEmail" type="email" autocomplete="email" aria-label="Email người được mời" placeholder="Email đã có tài khoản Qaly" />
              <button class="primary-button primary-button--compact" type="submit">
                <Mail :size="15" /> Mời
              </button>
            </form>
            <div class="group-pending-list">
              <article v-for="invite in invitations" :key="invite.id">
                <strong>{{ invite.email }}</strong>
                <span>Pending · hết hạn {{ new Date(invite.expiredAt).toLocaleDateString("vi") }}</span>
              </article>
              <p v-if="!invitations.length">Chưa có lời mời đang chờ.</p>
            </div>
          </div>

          <div v-else-if="activeTab === 'polls'" class="group-tool-body group-tool-body--polls">
            <div class="group-poll-composer">
              <div class="group-section-title">
                <span><Vote :size="16" /> Tạo bình chọn</span>
                <small>{{ activePollMessages.length }} poll</small>
              </div>
              <input
                v-model="pollForm.question"
                type="text"
                aria-label="Câu hỏi bình chọn"
                placeholder="Hỏi mọi người một câu..."
              />
              <div class="group-poll-options">
                <label v-for="(_, index) in pollForm.options" :key="index">
                  <span>{{ index + 1 }}</span>
                  <input
                    v-model="pollForm.options[index]"
                    type="text"
                    :placeholder="`Lựa chọn ${index + 1}`"
                  />
                  <button
                    type="button"
                    aria-label="Xóa lựa chọn"
                    @click="removePollOption(index)"
                  >
                    ×
                  </button>
                </label>
              </div>
              <label class="group-poll-multiple">
                <input v-model="pollForm.allowMultiple" type="checkbox" />
                <span>
                  <strong>Cho phép chọn nhiều đáp án</strong>
                  <small>Thành viên có thể chọn nhiều phương án trong cùng một lần.</small>
                </span>
              </label>
              <div class="group-poll-actions">
                <button class="secondary-button" type="button" @click="suggestPollWithAi">
                  <Sparkles :size="15" /> AI gợi ý poll
                </button>
                <button class="secondary-button" type="button" @click="addPollOption">
                  <Plus :size="15" /> Thêm lựa chọn
                </button>
                <button
                  class="primary-button"
                  type="button"
                  :disabled="isCreatingPoll"
                  @click="createPanelPoll"
                >
                  <Vote :size="15" /> {{ isCreatingPoll ? "Đang gửi..." : "Gửi bình chọn" }}
                </button>
              </div>
            </div>

            <div class="group-poll-list">
              <article v-for="message in activePollMessages" :key="message.id" class="group-poll-item">
                <div class="group-poll-item__meta">
                  <strong>{{ message.senderName }}</strong>
                  <span>{{ message.createdAt }}</span>
                </div>
                <PollCard
                  v-if="message.poll"
                  :group-id="activeGroupId"
                  :poll="message.poll"
                  :current-user-id="currentUserId"
                  :creator-id="message.senderId"
                />
              </article>
              <div v-if="!activePollMessages.length" class="group-empty-state">
                <Vote :size="26" />
                <strong>Chưa có bình chọn</strong>
                <span>Tạo poll đầu tiên để mọi người vote ngay trong nhóm.</span>
              </div>
            </div>
          </div>

          <div v-else-if="activeTab === 'meeting'" class="group-tool-body group-tool-empty">
            <CalendarDays :size="28" />
            <strong>Cuộc họp nhóm</strong>
            <p>Bắt đầu phiên họp cho các thành viên trong nhóm.</p>
            <button class="primary-button" type="button" @click="startMeeting">Bắt đầu cuộc họp</button>
          </div>

          <div v-else-if="activeTab === 'ai'" class="group-tool-body" style="padding: 0; min-height: 0;">
            <GroupAiPanel
              :group-id="activeGroupId"
              :members="members"
              :selected-request="selectedAiRequest"
            />
          </div>

          <div v-else class="group-tool-body">
            <form class="group-stack-form" @submit.prevent="createProjectFromGroup">
              <input v-model="projectForm.name" type="text" aria-label="Tên dự án" placeholder="Tên project" />
              <input v-model="projectForm.code" type="text" aria-label="Mã dự án" placeholder="Mã project" />
              <textarea v-model="projectForm.description" aria-label="Mô tả dự án" rows="3" placeholder="Mô tả"></textarea>
              <button class="primary-button" type="submit" :disabled="isCreatingProject">
                <Loader2 v-if="isCreatingProject" :size="15" class="spin" />
                <Check v-else :size="15" />
                {{ isCreatingProject ? 'Đang tạo...' : 'Tạo project từ nhóm' }}
              </button>
            </form>
            <button
              v-if="lastCreatedProject"
              class="group-created-project-link"
              type="button"
              @click="openLastCreatedProject"
            >
              <FolderKanban :size="15" />
              Mở {{ lastCreatedProject.name }}
              <ChevronRight :size="15" />
            </button>
          </div>

          <section v-if="sharedAttachments.length" class="group-shared-section">
            <div class="group-shared-heading">
              <div>
                <strong>Nội dung đã chia sẻ</strong>
                <span>{{ sharedAttachments.length }} mục trong cuộc trò chuyện</span>
              </div>
              <FileText :size="19" />
            </div>

            <div v-if="sharedImages.length" class="group-shared-block">
              <div class="group-shared-block__title">
                <span><ImageIcon :size="16" /> Ảnh</span>
                <small>{{ sharedImages.length }}</small>
              </div>
              <div class="group-shared-images">
                <article
                  v-for="file in sharedImages.slice(-6).reverse()"
                  :key="`${file.messageId}-${file.id ?? file.name}`"
                >
                  <img v-if="file.url" :src="file.url" :alt="file.name" loading="lazy" />
                  <div class="group-shared-actions">
                    <a
                      v-if="file.downloadUrl || file.url"
                      :href="file.downloadUrl || file.url"
                      :download="file.name"
                      aria-label="Tải ảnh xuống"
                    >
                      <Download :size="15" />
                    </a>
                    <button
                      type="button"
                      aria-label="Xóa ảnh ở phía tôi"
                      @click="hideSharedAttachment(file.messageId)"
                    >
                      <Trash2 :size="15" />
                    </button>
                  </div>
                </article>
              </div>
            </div>

            <div v-if="sharedFiles.length" class="group-shared-block">
              <div class="group-shared-block__title">
                <span><FileText :size="16" /> Video và tài liệu</span>
                <small>{{ sharedFiles.length }}</small>
              </div>
              <div class="group-shared-files">
                <article
                  v-for="file in sharedFiles.slice(-6).reverse()"
                  :key="`${file.messageId}-${file.id ?? file.name}`"
                >
                  <span class="group-shared-file-icon">
                    <FileVideo v-if="file.kind === 'video'" :size="19" />
                    <FileText v-else :size="19" />
                  </span>
                  <div>
                    <strong>{{ file.name }}</strong>
                    <span>{{ file.kind === "video" ? "Video" : "Tài liệu" }} · {{ file.sizeLabel }}</span>
                  </div>
                  <a
                    v-if="file.downloadUrl || file.url"
                    :href="file.downloadUrl || file.url"
                    :download="file.name"
                    aria-label="Tải file xuống"
                  >
                    <Download :size="16" />
                  </a>
                  <button
                    type="button"
                    aria-label="Xóa file ở phía tôi"
                    @click="hideSharedAttachment(file.messageId)"
                  >
                    <Trash2 :size="16" />
                  </button>
                </article>
              </div>
            </div>
          </section>
          </div>
        </aside>

        <div class="team-chat-status">
          <span :class="`team-chat-status__dot team-chat-status__dot--${realtimeState}`"></span>
          {{
            realtimeState === "connected"
              ? "Realtime đang bật"
              : realtimeState === "connecting"
                ? "Đang nối realtime"
                : "Realtime tạm thời offline"
          }}
          <span v-if="isLoadingMessages"> · Đang tải tin nhắn...</span>
        </div>
      </section>
    </div>

    <div
      v-if="showCreateModal"
      class="group-modal-backdrop"
      @click.self="showCreateModal = false"
      @keydown.esc="showCreateModal = false"
    >
      <form
        class="group-modal glass-card"
        role="dialog"
        aria-modal="true"
        aria-labelledby="create-group-title"
        @submit.prevent="createGroup"
      >
        <header>
          <h2 id="create-group-title">Tạo nhóm chat</h2>
          <button type="button" class="text-button" @click="showCreateModal = false">Đóng</button>
        </header>
        <input v-model="createForm.name" type="text" aria-label="Tên nhóm" placeholder="Tên nhóm" required />
        <label class="group-color-field">
          Màu nhóm
          <input v-model="createForm.color" type="color" />
        </label>
        <button class="primary-button" type="submit">Tạo nhóm</button>
      </form>
    </div>
  </div>
</template>

<style scoped>
.groups-workspace {
  position: relative;
  height: 100%;
  min-height: calc(100dvh - 72px);
  width: 100%;
  min-width: 0;
  grid-template-columns:
    minmax(250px, 280px)
    minmax(420px, 1fr)
    minmax(380px, 400px);
  gap: 0;
  padding: 0;
  overflow: hidden;
  border-top: 1px solid #e2e8f0;
  background: #ffffff;
}

.groups-workspace--detail-collapsed {
  grid-template-columns:
    minmax(250px, 280px)
    minmax(420px, 1fr)
    58px;
}

.dashboard-scroll--embedded {
  height: 100%;
  min-height: 0;
  overflow: hidden;
}

.project-home-main {
  height: 100%;
  min-height: 0;
  padding: 0 !important;
  display: block;
  background: #ffffff !important;
}

.groups-workspace :deep(.glass-card:not(.team-chat-window)) {
  border: 0;
  border-radius: 0;
  background: #ffffff;
  box-shadow: none;
  backdrop-filter: none;
}

.groups-workspace :deep(.team-chat-sidebar),
.groups-workspace :deep(.team-chat-window),
.group-detail-panel {
  min-width: 0;
  min-height: 0;
  height: 100%;
}

.groups-workspace :deep(.team-chat-sidebar) {
  padding: 20px 12px;
  border-right: 1px solid #e2e8f0;
  background: #ffffff;
  gap: 8px;
}

.groups-workspace :deep(.team-chat-sidebar__header),
.groups-workspace :deep(.team-chat-window__header) {
  min-height: 56px;
  padding: 0 4px 14px;
  border-bottom: 1px solid #eef2f7;
}

.groups-workspace :deep(.team-chat-sidebar__empty),
.groups-workspace :deep(.team-chat-empty) {
  display: grid;
  place-items: center;
  align-content: center;
  gap: 6px;
  min-height: 180px;
  border: 1px dashed #cbd5e1;
  border-radius: var(--qaly-radius-lg);
  color: #475569;
  text-align: center;
  padding: 18px;
}

.groups-workspace :deep(.team-chat-sidebar__empty strong),
.groups-workspace :deep(.team-chat-empty strong) {
  color: #0f172a;
  font-size: 0.92rem;
}

.groups-workspace :deep(.team-chat-empty) {
  width: min(360px, 88%);
  align-self: center;
  margin: auto;
  background: #ffffff;
}

.groups-workspace :deep(.team-chat-sidebar__header h2),
.groups-workspace :deep(.team-chat-window__header h2) {
  font-size: 1.18rem;
  letter-spacing: 0;
}

.groups-workspace :deep(.team-chat-sidebar__header span),
.groups-workspace :deep(.team-chat-window__header span) {
  font-size: 0.72rem;
  letter-spacing: 0.02em;
}

.groups-workspace :deep(.team-chat-group) {
  min-height: 66px;
  align-items: center;
  justify-content: flex-start;
  border-radius: var(--qaly-radius-lg);
  padding: 11px 12px;
  border-left: 3px solid transparent;
  transition:
    border-color 180ms ease,
    background 180ms ease,
    color 180ms ease;
}

.groups-workspace :deep(.team-chat-group__avatar),
.groups-workspace :deep(.team-chat-window__avatar) {
  flex: 0 0 auto;
  display: grid;
  place-items: center;
  color: #ffffff;
  background: #0f5dcc;
  font-weight: 800;
  letter-spacing: 0;
  overflow: hidden;
}

.groups-workspace :deep(.team-chat-group__avatar img),
.groups-workspace :deep(.team-chat-window__avatar img) {
  width: 100%;
  height: 100%;
  display: block;
  object-fit: cover;
}

.groups-workspace :deep(.team-chat-group__avatar) {
  width: 38px;
  height: 38px;
  border-radius: var(--qaly-radius-lg);
  font-size: 0.78rem;
}

.groups-workspace :deep(.team-chat-group:hover) {
  background: #f8fafc !important;
  box-shadow: none;
}

.groups-workspace :deep(.team-chat-group.is-active) {
  border-color: #2563eb;
  background: #eff6ff !important;
  box-shadow: none;
}

.groups-workspace :deep(.team-chat-group strong) {
  color: #0f172a;
  font-size: 0.92rem;
  font-weight: 700;
}

.groups-workspace :deep(.team-chat-group span) {
  color: #64748b;
  font-size: 0.82rem;
  font-weight: 500;
}

.groups-workspace :deep(.team-chat-group small) {
  margin-left: auto;
}

.groups-workspace :deep(.team-chat-window) {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 18px 22px 16px;
  overflow: hidden;
  border-right: 1px solid #e2e8f0;
}

.groups-workspace :deep(.team-chat-body) {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: 16px 12px 12px;
  gap: 10px;
  display: flex;
  flex-direction: column;
}

.groups-workspace :deep(.team-chat-window__identity) {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 11px;
}

.groups-workspace :deep(.team-chat-window__identity > div) {
  min-width: 0;
  display: grid;
  gap: 2px;
}

.groups-workspace :deep(.team-chat-window__identity h2),
.groups-workspace :deep(.team-chat-window__identity span:not(.team-chat-window__avatar)) {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.groups-workspace :deep(.team-chat-window__avatar) {
  width: 42px;
  height: 42px;
  border-radius: var(--qaly-radius-lg);
  font-size: 0.82rem;
}

.groups-workspace :deep(.team-chat-window__actions .icon-button) {
  width: 38px;
  height: 38px;
  border-radius: var(--qaly-radius-lg);
  box-shadow: none;
  transition:
    border-color 160ms ease,
    background 160ms ease,
    color 160ms ease;
}

.groups-workspace :deep(.team-chat-window__actions .icon-button:hover) {
  color: #1677ff;
  border-color: #bfdbfe;
  background: #eff6ff;
}


.team-chat-banner {
  position: absolute;
  inset: 16px 24px auto 24px;
  z-index: 2;
  border: 1px solid rgba(59, 130, 246, 0.18);
  border-radius: var(--qaly-radius-lg);
  background: rgba(239, 246, 255, 0.92);
  color: #1e3a8a;
  padding: 10px 14px;
  font-size: 0.88rem;
  font-weight: 600;
  box-shadow: var(--qaly-shadow-md);
}

.team-chat-banner--error {
  border-color: rgba(239, 68, 68, 0.22);
  background: rgba(254, 242, 242, 0.94);
  color: #991b1b;
}

.group-detail-panel {
  padding: 0;
  display: flex;
  flex-direction: column;
  min-width: 0;
  overflow-x: hidden;
  overflow-y: auto;
  overscroll-behavior: contain;
  scrollbar-gutter: stable;
  background: #ffffff;
}

.group-detail-panel.is-collapsed {
  align-items: center;
  padding: 16px 8px;
}

.group-detail-content {
  min-width: 0;
  min-height: 100%;
  display: flex;
  flex-direction: column;
}

.group-detail-header {
  flex: 0 0 auto;
  min-width: 0;
  border-bottom: 1px solid #edf1f5;
  background: linear-gradient(180deg, #ffffff 0%, #fbfdff 100%);
}

.group-detail-titlebar {
  min-height: 58px;
  padding: 0 16px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid #f1f5f9;
  color: #0f172a;
}

.group-detail-titlebar > strong {
  font-size: 1rem;
  font-weight: 800;
}

.group-detail-profile {
  padding: 22px 18px 20px;
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
}

.group-detail-avatar {
  position: relative;
  width: 68px;
  height: 68px;
  display: grid;
  place-items: center;
  border: 3px solid #ffffff;
  border-radius: var(--qaly-radius-lg);
  background: linear-gradient(145deg, #1677ff, #2456d8);
  color: #ffffff;
  font-size: 1.1rem;
  font-weight: 900;
  box-shadow: var(--qaly-shadow-md);
  overflow: visible;
}

.group-detail-avatar > img {
  width: 100%;
  height: 100%;
  display: block;
  border-radius: var(--qaly-radius-lg);
  object-fit: cover;
}

.group-detail-avatar > span {
  display: grid;
  place-items: center;
}

.group-detail-avatar__edit {
  position: absolute;
  right: -7px;
  bottom: -7px;
  width: 31px;
  height: 31px;
  display: grid;
  place-items: center;
  border: 2px solid #ffffff;
  border-radius: 999px;
  color: #ffffff;
  background: #1d4ed8;
  box-shadow: var(--qaly-shadow-md);
  cursor: pointer;
  transition:
    background 160ms ease,
    transform 160ms ease;
}

.group-detail-avatar__edit:hover {
  background: #1e40af;
  transform: scale(1.05);
}

.group-detail-avatar__edit.is-loading {
  opacity: 0.65;
  cursor: wait;
}

.group-detail-avatar__edit input {
  display: none;
}

.group-detail-profile__copy {
  min-width: 0;
  width: 100%;
}

.group-detail-profile h2 {
  margin: 12px 0 4px;
  overflow: hidden;
  color: #0f172a;
  font-size: 1.22rem;
  font-weight: 850;
  line-height: 1.25;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.group-detail-profile p {
  margin: 0;
  color: #64748b;
  font-size: 0.82rem;
}

.group-detail-profile p span {
  margin: 0 4px;
  color: #cbd5e1;
}

.group-detail-icon-button,
.group-detail-collapse-button {
  border: 1px solid #dbe3ef;
  border-radius: var(--qaly-radius-lg);
  color: #475569;
  background: #ffffff;
  display: grid;
  place-items: center;
}

.group-detail-icon-button {
  width: 34px;
  height: 34px;
}

.group-detail-collapse-button {
  width: 40px;
  height: 40px;
}

.group-detail-icon-button:hover,
.group-detail-collapse-button:hover {
  color: #1677ff;
  border-color: #bfdbfe;
  background: #eff6ff;
}

.group-role-badge {
  flex: 0 0 auto;
  min-height: 30px;
  margin-top: 12px;
  border-radius: 999px;
  padding: 6px 11px;
  background: #eff6ff;
  color: #1d4ed8;
  display: inline-flex;
  align-items: center;
  gap: 5px;
  font-size: 0.75rem;
  font-weight: 800;
  box-shadow: inset 0 0 0 1px rgba(37, 99, 235, 0.12);
}

.group-detail-tabs {
  flex: 0 0 auto;
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 8px;
  min-width: 0;
  padding: 14px 16px 16px;
  border-bottom: 8px solid #f5f7fa;
}

.group-detail-tabs button {
  min-width: 0;
  min-height: 68px;
  border: 1px solid #e5eaf1;
  border-radius: var(--qaly-radius-lg);
  background: #ffffff;
  color: #475569;
  display: inline-flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 7px;
  padding: 9px 4px;
  overflow: hidden;
  text-align: center;
  font-size: 0.7rem;
  font-weight: 800;
  line-height: 1.15;
  transition:
    border-color 160ms ease,
    background 160ms ease,
    color 160ms ease;
}

.group-detail-tabs button > span {
  width: 32px;
  height: 32px;
  display: grid;
  place-items: center;
  border-radius: var(--qaly-radius-lg);
  background: #f1f5f9;
  color: #526176;
  transition:
    background 160ms ease,
    color 160ms ease;
}

.group-detail-tabs button:hover {
  border-color: #bfdbfe;
  background: #f8fbff;
  color: #1e40af;
}

.group-detail-tabs button:hover > span,
.group-detail-tabs button.active > span {
  background: #dbeafe;
  color: #1d4ed8;
}

.group-detail-tabs button.active {
  border-color: rgba(37, 99, 235, 0.45);
  background: #eff6ff;
  color: #1d4ed8;
  box-shadow: none;
}

.group-tool-body {
  min-width: 0;
  min-height: auto;
  flex: 0 0 auto;
  overflow-x: hidden;
  overflow-y: visible;
  padding: 16px;
}

.group-tool-heading {
  min-width: 0;
  margin-bottom: 14px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  color: #2563eb;
}

.group-tool-heading > div {
  min-width: 0;
  display: grid;
  gap: 2px;
}

.group-tool-heading strong {
  color: #0f172a;
  font-size: 0.94rem;
}

.group-tool-heading span {
  color: #64748b;
  font-size: 0.76rem;
}

.group-inline-form,
.group-stack-form {
  display: grid;
  gap: 8px;
}

.group-created-project-link {
  width: 100%;
  min-height: 40px;
  margin-top: 10px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  border: 1px solid #bfdbfe;
  border-radius: var(--qaly-radius-lg);
  padding: 9px 11px;
  color: #1d4ed8;
  background: #eff6ff;
  font-weight: 750;
  cursor: pointer;
}

.group-created-project-link:hover {
  border-color: #60a5fa;
  background: #dbeafe;
}

.spin {
  animation: group-tool-spin 900ms linear infinite;
}

@keyframes group-tool-spin {
  to { transform: rotate(360deg); }
}

.group-inline-form {
  grid-template-columns: minmax(0, 1fr) auto;
  margin-bottom: 14px;
}

.group-user-search {
  grid-column: 1 / -1;
  position: relative;
  min-width: 0;
  min-height: 44px;
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 9px;
  border: 1px solid #dbe3ef;
  border-radius: var(--qaly-radius-lg);
  padding: 0 11px;
  color: #64748b;
  background: #ffffff;
  transition: border-color 160ms ease, box-shadow 160ms ease;
}

.group-detail-titlebar__actions {
  display: flex;
  align-items: center;
  gap: 7px;
}

.group-user-search:focus-within {
  border-color: #60a5fa;
  box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
}

.group-user-search > input {
  min-width: 0;
  height: 42px;
  border: 0 !important;
  border-radius: 0 !important;
  padding: 0 !important;
  outline: 0;
  box-shadow: none !important;
}

.group-user-search > button {
  width: 28px;
  height: 28px;
  border: 0;
  border-radius: var(--qaly-radius-lg);
  display: grid;
  place-items: center;
  color: #475569;
  background: #f1f5f9;
  cursor: pointer;
}

.group-user-suggestions {
  position: absolute;
  z-index: 60;
  inset: calc(100% + 7px) 0 auto 0;
  max-height: 290px;
  overflow-y: auto;
  display: grid;
  gap: 3px;
  border: 1px solid #dbe3ef;
  border-radius: var(--qaly-radius-lg);
  padding: 6px;
  background: #ffffff;
  box-shadow: var(--qaly-shadow-md);
}

.group-user-suggestions > button {
  min-width: 0;
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 10px;
  border: 0;
  border-radius: var(--qaly-radius-lg);
  padding: 9px;
  color: #0f172a;
  background: transparent;
  text-align: left;
  cursor: pointer;
}

.group-user-suggestions > button:hover,
.group-user-suggestions > button.is-selected {
  color: #1d4ed8;
  background: #eff6ff;
}

.group-user-suggestion__avatar {
  width: 34px;
  height: 34px;
  display: grid;
  place-items: center;
  border-radius: var(--qaly-radius-lg);
  color: #ffffff;
  background: #2563eb;
  font-size: 0.7rem;
  font-weight: 850;
}

.group-user-suggestion__identity {
  min-width: 0;
  display: grid;
  gap: 2px;
}

.group-user-suggestion__identity strong,
.group-user-suggestion__identity small {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.group-user-suggestion__identity strong { font-size: 0.8rem; }
.group-user-suggestion__identity small { color: #64748b; font-size: 0.72rem; }

.group-user-suggestions__empty {
  padding: 18px 12px;
  color: #64748b;
  text-align: center;
  font-size: 0.78rem;
}

.group-inline-form input,
.group-inline-form select,
.group-stack-form input,
.group-stack-form textarea,
.group-modal input,
.group-modal textarea {
  width: 100%;
  border: 1px solid #dbe3ef;
  border-radius: var(--qaly-radius-lg);
  padding: 10px 12px;
  color: #111827;
  background: #ffffff;
  box-shadow: none;
}

.group-member-list,
.group-pending-list {
  display: grid;
  gap: 10px;
}

.group-member-row,
.group-pending-list article {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 11px;
  border: 1px solid #e5e7eb;
  border-radius: var(--qaly-radius-lg);
  padding: 12px;
  background: #ffffff;
  box-shadow: none;
  transition:
    border-color 160ms ease,
    background 160ms ease;
}

.group-member-row:hover,
.group-pending-list article:hover,
.group-poll-item:hover {
  border-color: #bfdbfe;
  background: #f8fbff;
}

.group-member-identity {
  min-width: 0;
}

.group-member-actions {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 7px;
}

.group-member-role {
  min-height: 30px;
  display: inline-flex;
  align-items: center;
  gap: 5px;
  border-radius: var(--qaly-radius-lg);
  padding: 0 8px;
  background: #f1f5f9;
  color: #475569;
  font-size: 0.72rem;
  font-weight: 800;
}

.group-member-role--owner {
  background: #fff7ed;
  color: #c2410c;
}

.group-member-role--admin {
  background: #eff6ff;
  color: #1d4ed8;
}

.group-member-role select {
  max-width: 105px;
  border: 0;
  padding: 0;
  outline: 0;
  color: inherit;
  background: transparent;
  font: inherit;
  cursor: pointer;
}

.group-member-remove {
  border: 0;
  padding: 0;
  color: #dc2626;
  background: transparent;
  font-size: 0.7rem;
  font-weight: 700;
  cursor: pointer;
}

.group-member-remove:hover {
  text-decoration: underline;
}

.group-danger-zone {
  margin-top: 16px;
  padding-top: 14px;
  border-top: 1px solid #fee2e2;
}

.group-danger-action {
  width: 100%;
  min-height: 44px;
  border: 1px solid #fecaca;
  border-radius: var(--qaly-radius-lg);
  color: #b91c1c;
  background: #fff7f7;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  font-size: 0.84rem;
  font-weight: 850;
  cursor: pointer;
  transition:
    border-color 160ms ease,
    background 160ms ease,
    transform 160ms ease;
}

.group-danger-action:hover {
  border-color: #fca5a5;
  background: #fee2e2;
  transform: translateY(-1px);
}

.group-danger-action--strong {
  color: #ffffff;
  border-color: #dc2626;
  background: #dc2626;
  box-shadow: var(--qaly-shadow-md);
}

.group-danger-action--strong:hover {
  border-color: #b91c1c;
  background: #b91c1c;
}

.group-shared-section {
  flex: 0 0 auto;
  display: grid;
  gap: 16px;
  padding: 18px 16px 24px;
  border-top: 8px solid #f5f7fa;
}

.group-shared-heading,
.group-shared-block__title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.group-shared-heading {
  color: #2563eb;
}

.group-shared-heading > div {
  display: grid;
  gap: 2px;
}

.group-shared-heading strong {
  color: #0f172a;
  font-size: 0.94rem;
}

.group-shared-heading span {
  color: #64748b;
  font-size: 0.75rem;
}

.group-shared-block {
  display: grid;
  gap: 10px;
}

.group-shared-block__title {
  color: #334155;
  font-size: 0.8rem;
  font-weight: 800;
}

.group-shared-block__title > span {
  display: inline-flex;
  align-items: center;
  gap: 7px;
}

.group-shared-block__title small {
  min-width: 24px;
  padding: 3px 7px;
  border-radius: 999px;
  color: #1d4ed8;
  background: #eff6ff;
  text-align: center;
}

.group-shared-images {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 6px;
}

.group-shared-images article {
  position: relative;
  aspect-ratio: 1;
  overflow: hidden;
  border-radius: var(--qaly-radius-lg);
  background: #e2e8f0;
}

.group-shared-images img {
  width: 100%;
  height: 100%;
  display: block;
  object-fit: cover;
}

.group-shared-actions {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  background: rgba(15, 23, 42, 0.5);
  opacity: 0;
  transition: opacity 150ms ease;
}

.group-shared-images article:hover .group-shared-actions,
.group-shared-images article:focus-within .group-shared-actions {
  opacity: 1;
}

.group-shared-actions a,
.group-shared-actions button,
.group-shared-files a,
.group-shared-files button {
  width: 30px;
  height: 30px;
  display: grid;
  place-items: center;
  border: 0;
  border-radius: var(--qaly-radius-lg);
  color: #334155;
  background: #ffffff;
  cursor: pointer;
}

.group-shared-files {
  display: grid;
  gap: 7px;
}

.group-shared-files article {
  min-width: 0;
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto auto;
  align-items: center;
  gap: 8px;
  padding: 9px;
  border: 1px solid #e5eaf1;
  border-radius: var(--qaly-radius-lg);
}

.group-shared-file-icon {
  width: 36px;
  height: 36px;
  display: grid;
  place-items: center;
  border-radius: var(--qaly-radius-lg);
  color: #1d4ed8;
  background: #eff6ff;
}

.group-shared-files article > div {
  min-width: 0;
  display: grid;
  gap: 2px;
}

.group-shared-files strong,
.group-shared-files span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.group-shared-files strong {
  color: #0f172a;
  font-size: 0.76rem;
}

.group-shared-files article > div span {
  color: #64748b;
  font-size: 0.68rem;
}

.group-shared-files button {
  color: #b91c1c;
  background: #fef2f2;
}

.group-member-row strong,
.group-pending-list strong {
  display: block;
  color: #111827;
  font-size: 0.88rem;
}

.group-member-row span,
.group-pending-list span {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: #64748b;
  font-size: 0.78rem;
}

.group-avatar {
  width: 42px;
  height: 42px;
  border-radius: var(--qaly-radius-lg);
  display: grid;
  place-items: center;
  background: #0f5dcc;
  color: #fff;
  font-size: 0.75rem;
  font-weight: 800;
  box-shadow: none;
}

.group-tool-body--polls {
  display: grid;
  gap: 14px;
  align-content: start;
  min-width: 0;
}

.group-section-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  min-width: 0;
}

.group-section-title span {
  min-width: 0;
  display: inline-flex;
  align-items: center;
  gap: 7px;
  color: #111827;
  font-weight: 900;
}

.group-section-title small {
  padding: 5px 9px;
  border-radius: 999px;
  background: #eff6ff;
  color: #1d4ed8;
  font-weight: 800;
}

.group-poll-composer {
  display: grid;
  gap: 12px;
  min-width: 0;
  padding: 12px;
  border: 1px solid #dbeafe;
  border-radius: var(--qaly-radius-lg);
  background: #f8fbff;
  box-shadow: none;
}

.group-poll-composer input {
  width: 100%;
  min-height: 42px;
  border: 1px solid #dbe3ef;
  border-radius: var(--qaly-radius-lg);
  padding: 10px 12px;
  background: #ffffff;
  color: #111827;
  font-weight: 600;
}

.group-poll-multiple {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  padding: 10px 12px;
  border: 1px solid #dbe5f2;
  border-radius: var(--qaly-radius-lg);
  background: #fff;
  cursor: pointer;
}

.group-poll-multiple input {
  width: 16px;
  height: 16px;
  margin-top: 2px;
  accent-color: #2563eb;
}

.group-poll-multiple span {
  display: grid;
  gap: 2px;
}

.group-poll-multiple strong {
  color: #172033;
  font-size: 0.78rem;
}

.group-poll-multiple small {
  color: #718096;
  font-size: 0.68rem;
  line-height: 1.35;
}

.group-poll-options {
  display: grid;
  gap: 8px;
}

.group-poll-options label {
  display: grid;
  grid-template-columns: 28px minmax(0, 1fr) 28px;
  align-items: center;
  gap: 8px;
}

.group-poll-options label > span {
  height: 28px;
  display: grid;
  place-items: center;
  border-radius: 999px;
  background: #0f5dcc;
  color: #fff;
  font-size: 0.78rem;
  font-weight: 900;
}

.group-poll-options button {
  width: 28px;
  height: 28px;
  border: 0;
  border-radius: 999px;
  background: #e2e8f0;
  color: #475569;
  font-size: 1.05rem;
  cursor: pointer;
}

.group-poll-actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 10px;
}

.group-poll-actions button {
  flex: 1 1 150px;
  min-width: 0;
  min-height: 40px;
  border-radius: var(--qaly-radius-lg);
  padding-inline: 10px;
}

.group-poll-list {
  display: grid;
  gap: 12px;
  min-width: 0;
}

.group-poll-item {
  display: grid;
  gap: 10px;
  min-width: 0;
  padding: 10px;
  border: 1px solid #e5e7eb;
  border-radius: var(--qaly-radius-lg);
  background: #ffffff;
  box-shadow: none;
  transition:
    border-color 160ms ease,
    background 160ms ease;
}

.group-poll-item__meta {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  color: #64748b;
  font-size: 0.78rem;
}

.group-poll-item__meta strong {
  color: #111827;
}

.group-empty-state {
  min-height: 150px;
  display: grid;
  place-items: center;
  align-content: center;
  gap: 8px;
  border: 1px dashed rgba(148, 163, 184, 0.5);
  border-radius: var(--qaly-radius-lg);
  color: #64748b;
  text-align: center;
}

.group-empty-state strong {
  color: #111827;
}

.group-tool-empty {
  display: grid;
  place-items: center;
  align-content: center;
  gap: 10px;
  text-align: center;
  color: #64748b;
}

.group-tool-empty strong {
  color: #111827;
}

.team-chat-status {
  position: absolute;
  right: 20px;
  bottom: 14px;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  border-radius: 999px;
  background: #ffffff;
  color: #475569;
  padding: 7px 12px;
  font-size: 0.78rem;
  box-shadow: var(--qaly-shadow-md);
  pointer-events: none;
}

.team-chat-status__dot {
  width: 8px;
  height: 8px;
  border-radius: 999px;
  background: #94a3b8;
}

.team-chat-status__dot--connected {
  background: #22c55e;
}

.team-chat-status__dot--connecting {
  background: #f59e0b;
}

.team-chat-status__dot--offline {
  background: #ef4444;
}

.group-modal-backdrop {
  position: fixed;
  inset: 0;
  z-index: 50;
  display: grid;
  place-items: center;
  background: rgba(15, 23, 42, 0.28);
  padding: 20px;
}

.group-modal {
  width: min(460px, 100%);
  padding: 22px;
  display: grid;
  gap: 12px;
}

.group-modal header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.group-modal h2 {
  margin: 0;
  font-size: 1.25rem;
  color: #111827;
}

.group-color-field {
  display: flex;
  align-items: center;
  justify-content: space-between;
  color: #475569;
  font-weight: 700;
}

:global(:root[data-theme='dark'] .groups-workspace),
:global(:root[data-theme='dark'] .project-home-main),
:global(:root[data-theme='dark'] .groups-workspace .glass-card:not(.team-chat-window)),
:global(:root[data-theme='dark'] .groups-workspace .team-chat-sidebar),
:global(:root[data-theme='dark'] .groups-workspace .team-chat-window),
:global(:root[data-theme='dark'] .group-detail-panel),
:global(:root[data-theme='dark'] .group-detail-header),
:global(:root[data-theme='dark'] .group-modal) {
  border-color: var(--border) !important;
  background: var(--surface) !important;
  color: var(--text-primary) !important;
  box-shadow: none !important;
}

:global(:root[data-theme='dark'] .groups-workspace .team-chat-sidebar__header),
:global(:root[data-theme='dark'] .groups-workspace .team-chat-window__header),
:global(:root[data-theme='dark'] .group-detail-titlebar),
:global(:root[data-theme='dark'] .group-detail-tabs),
:global(:root[data-theme='dark'] .group-detail-tabs button),
:global(:root[data-theme='dark'] .group-member-row),
:global(:root[data-theme='dark'] .group-pending-list article),
:global(:root[data-theme='dark'] .group-poll-composer),
:global(:root[data-theme='dark'] .group-poll-item),
:global(:root[data-theme='dark'] .team-chat-status) {
  border-color: var(--border) !important;
  background: var(--surface-muted) !important;
  color: var(--text-primary) !important;
}

:global(:root[data-theme='dark'] .groups-workspace .team-chat-group:hover),
:global(:root[data-theme='dark'] .groups-workspace .team-chat-group.is-active),
:global(:root[data-theme='dark'] .group-detail-icon-button:hover),
:global(:root[data-theme='dark'] .group-detail-collapse-button:hover),
:global(:root[data-theme='dark'] .group-detail-tabs button:hover),
:global(:root[data-theme='dark'] .group-detail-tabs button.active) {
  border-color: rgba(96, 165, 250, 0.36) !important;
  background: var(--primary-soft) !important;
  color: var(--primary-strong) !important;
}

:global(:root[data-theme='dark'] .groups-workspace .team-chat-group strong),
:global(:root[data-theme='dark'] .group-detail-titlebar),
:global(:root[data-theme='dark'] .group-detail-profile h2),
:global(:root[data-theme='dark'] .group-member-row strong),
:global(:root[data-theme='dark'] .group-pending-list strong),
:global(:root[data-theme='dark'] .group-poll-item__meta strong),
:global(:root[data-theme='dark'] .group-empty-state strong),
:global(:root[data-theme='dark'] .group-tool-empty strong),
:global(:root[data-theme='dark'] .group-modal h2) {
  color: var(--text-primary) !important;
}

:global(:root[data-theme='dark'] .groups-workspace .team-chat-group span),
:global(:root[data-theme='dark'] .group-detail-profile p),
:global(:root[data-theme='dark'] .group-member-row span),
:global(:root[data-theme='dark'] .group-pending-list span),
:global(:root[data-theme='dark'] .group-poll-item__meta),
:global(:root[data-theme='dark'] .group-empty-state),
:global(:root[data-theme='dark'] .group-tool-empty),
:global(:root[data-theme='dark'] .team-chat-status) {
  color: var(--text-secondary) !important;
}

:global(:root[data-theme='dark'] .team-chat-banner) {
  border-color: rgba(96, 165, 250, 0.34) !important;
  background: var(--primary-soft) !important;
  color: #bfdbfe !important;
}

:global(:root[data-theme='dark'] .team-chat-banner--error) {
  border-color: rgba(248, 113, 113, 0.34) !important;
  background: var(--danger-soft) !important;
  color: #fca5a5 !important;
}

:global(:root[data-theme='dark'] .groups-workspace .team-chat-window__actions .icon-button),
:global(:root[data-theme='dark'] .group-detail-icon-button),
:global(:root[data-theme='dark'] .group-detail-collapse-button) {
  border-color: var(--border) !important;
  background: var(--surface-muted) !important;
  color: var(--text-primary) !important;
}

:global(:root[data-theme='dark'] .groups-workspace .team-chat-window__actions .icon-button:hover) {
  color: var(--primary-strong) !important;
  border-color: rgba(96, 165, 250, 0.36) !important;
  background: var(--primary-soft) !important;
}

:global(:root[data-theme='dark'] .group-role-badge),
:global(:root[data-theme='dark'] .group-detail-tabs button:hover > span),
:global(:root[data-theme='dark'] .group-detail-tabs button.active > span),
:global(:root[data-theme='dark'] .group-created-project-link),
:global(:root[data-theme='dark'] .group-user-suggestions > button:hover),
:global(:root[data-theme='dark'] .group-user-suggestions > button.is-selected),
:global(:root[data-theme='dark'] .group-shared-block__title small),
:global(:root[data-theme='dark'] .group-shared-file-icon),
:global(:root[data-theme='dark'] .group-section-title small) {
  background: var(--primary-soft) !important;
  color: var(--primary-strong) !important;
}

:global(:root[data-theme='dark'] .group-created-project-link) {
  border-color: rgba(96, 165, 250, 0.4) !important;
}

:global(:root[data-theme='dark'] .group-created-project-link:hover) {
  background: rgba(96, 165, 250, 0.24) !important;
  border-color: rgba(96, 165, 250, 0.55) !important;
}

:global(:root[data-theme='dark'] .group-detail-tabs button > span),
:global(:root[data-theme='dark'] .group-user-search > button),
:global(:root[data-theme='dark'] .group-member-role),
:global(:root[data-theme='dark'] .group-poll-options button) {
  background: var(--surface-muted) !important;
  color: var(--text-secondary) !important;
}

:global(:root[data-theme='dark'] .group-tool-heading strong),
:global(:root[data-theme='dark'] .group-user-suggestions > button),
:global(:root[data-theme='dark'] .group-shared-heading strong),
:global(:root[data-theme='dark'] .group-shared-block__title),
:global(:root[data-theme='dark'] .group-shared-files strong),
:global(:root[data-theme='dark'] .group-section-title span),
:global(:root[data-theme='dark'] .group-poll-multiple strong) {
  color: var(--text-primary) !important;
}

:global(:root[data-theme='dark'] .group-tool-heading span),
:global(:root[data-theme='dark'] .group-user-suggestion__identity small),
:global(:root[data-theme='dark'] .group-user-suggestions__empty),
:global(:root[data-theme='dark'] .group-shared-heading span),
:global(:root[data-theme='dark'] .group-shared-files article > div span),
:global(:root[data-theme='dark'] .group-poll-multiple small) {
  color: var(--text-secondary) !important;
}

:global(:root[data-theme='dark'] .group-inline-form input),
:global(:root[data-theme='dark'] .group-inline-form select),
:global(:root[data-theme='dark'] .group-stack-form input),
:global(:root[data-theme='dark'] .group-stack-form textarea),
:global(:root[data-theme='dark'] .group-modal input),
:global(:root[data-theme='dark'] .group-modal textarea),
:global(:root[data-theme='dark'] .group-user-search),
:global(:root[data-theme='dark'] .group-user-suggestions),
:global(:root[data-theme='dark'] .group-poll-composer input),
:global(:root[data-theme='dark'] .group-poll-multiple) {
  border-color: var(--border) !important;
  background: var(--surface-muted) !important;
  color: var(--text-primary) !important;
}

:global(:root[data-theme='dark'] .group-member-row:hover),
:global(:root[data-theme='dark'] .group-pending-list article:hover),
:global(:root[data-theme='dark'] .group-poll-item:hover) {
  border-color: rgba(96, 165, 250, 0.36) !important;
  background: var(--primary-soft) !important;
}

:global(:root[data-theme='dark'] .group-member-role--owner) {
  background: var(--warning-soft) !important;
  color: var(--warning-dark) !important;
}

:global(:root[data-theme='dark'] .group-member-role--admin) {
  background: var(--primary-soft) !important;
  color: var(--primary-strong) !important;
}

:global(:root[data-theme='dark'] .group-danger-zone) {
  border-top-color: var(--danger-soft) !important;
}

:global(:root[data-theme='dark'] .group-danger-action) {
  border-color: rgba(248, 113, 113, 0.32) !important;
  background: var(--danger-soft) !important;
  color: var(--qaly-danger) !important;
}

:global(:root[data-theme='dark'] .group-danger-action:hover) {
  background: rgba(248, 113, 113, 0.24) !important;
}

:global(:root[data-theme='dark'] .group-shared-images article) {
  background: var(--surface-muted) !important;
}

:global(:root[data-theme='dark'] .group-shared-actions a),
:global(:root[data-theme='dark'] .group-shared-actions button),
:global(:root[data-theme='dark'] .group-shared-files a),
:global(:root[data-theme='dark'] .group-shared-files button) {
  color: var(--text-primary) !important;
  background: var(--surface) !important;
}

:global(:root[data-theme='dark'] .group-shared-files article) {
  border-color: var(--border) !important;
}

:global(:root[data-theme='dark'] .group-shared-files button) {
  color: var(--qaly-danger) !important;
  background: var(--danger-soft) !important;
}

:global(:root[data-theme='dark'] .group-color-field) {
  color: var(--text-secondary) !important;
}

@media (max-width: 1280px) {
  .groups-workspace {
    grid-template-columns: 250px minmax(0, 1fr);
    overflow: auto;
  }

  .group-detail-panel {
    grid-column: 1 / -1;
    min-height: auto;
    border-top: 1px solid #e2e8f0;
  }
}

@media (max-width: 900px) {
  .groups-workspace {
    grid-template-columns: 1fr;
  }

  .group-detail-tabs {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}
</style>
