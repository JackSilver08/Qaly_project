<script setup lang="ts">
import {
  HubConnectionBuilder,
  HubConnectionState,
  type HubConnection,
} from "@microsoft/signalr";
import {
  CalendarDays,
  Check,
  Mail,
  Plus,
  Settings,
  UserPlus,
  Users,
  Vote,
  Sparkles,
  PanelRightClose,
  PanelRightOpen,
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
  TeamChatMeeting,
  TeamChatMessage,
  TeamChatPoll,
} from "../components/chat/chat-types";
import { useDashboardContext } from "../composables/dashboard-context";
import { showError, showSuccess } from "../composables/use-toast";
import type { PagedResult, UserDto } from "../types";
import { apiCommand, apiResult, errorMessage } from "../utils/api-client";

interface GroupDto {
  id: string;
  name: string;
  description: string | null;
  color: string | null;
  currentUserRole: string;
  memberCount: number;
  messageCount: number;
  openPollCount: number;
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
  createdAt: string;
}

interface GroupFileUploadDto {
  name: string;
  url: string;
  sizeLabel: string;
  kind: "file" | "image";
  contentType?: string;
}

const route = useRoute();
const router = useRouter();
const { currentUser } = useDashboardContext();

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
const showCreateModal = ref(false);
const createForm = ref({ name: "", description: "", color: "#2563eb" });
const inviteEmail = ref("");
const addUserId = ref("");
const addRole = ref("Member");
const projectForm = ref({ name: "", code: "", description: "" });
const pollForm = ref({ question: "", options: ["", ""] });
const backgroundTheme = ref(localStorage.getItem("qaly.chatBackground") ?? "clean");
const isDetailPanelCollapsed = ref(false);

let hubConnection: HubConnection | null = null;

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
const canManageGroup = computed(() =>
  ["Owner", "Admin"].includes(activeDetail.value?.currentUserRole ?? ""),
);
const availableUsers = computed(() => {
  const memberIds = new Set(members.value.map((member) => member.userId));
  return users.value.filter((user) => user.isActive && !memberIds.has(user.id));
});

function roleLabel(role?: string) {
  if (!role) return "-";
  const map: Record<string, string> = { Owner: "Chủ nhóm", Admin: "Quản trị viên", Member: "Thành viên" };
  return map[role] ?? role;
}

onMounted(async () => {
  await Promise.all([loadGroups(), loadUsers()]);
  await connectRealtime();
});

onBeforeUnmount(async () => {
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
  if (previous && hubConnection?.state === HubConnectionState.Connected) {
    await hubConnection.invoke("LeaveGroup", previous).catch(() => undefined);
  }

  if (!next) return;
  await Promise.all([loadGroupDetail(next), loadMessages(next), loadMembers(next)]);

  if (canManageGroup.value) {
    await loadInvitations(next);
  } else {
    invitations.value = [];
  }

  if (route.params.groupId !== next) {
    await router.replace({ name: "group-detail", params: { groupId: next } });
  }

  if (hubConnection?.state === HubConnectionState.Connected) {
    await hubConnection.invoke("JoinGroup", next).catch(() => undefined);
  }
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
    if (!addUserId.value && availableUsers.value[0]) addUserId.value = availableUsers.value[0].id;
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
    realtimeState.value = "connected";
    if (activeGroupId.value) {
      await hubConnection?.invoke("JoinGroup", activeGroupId.value).catch(() => undefined);
    }
  });
  hubConnection.onclose(() => {
    realtimeState.value = "offline";
  });
  hubConnection.on("groupMessageReceived", (message: GroupMessageDto) => {
    upsertMessage(toMessageModel(message));
  });
  hubConnection.on("meetingStarted", async (payload: { groupId?: string }) => {
    const groupId = payload?.groupId;
    if (groupId && groupId === activeGroupId.value) await loadMessages(groupId);
  });
  hubConnection.on("meetingEnded", async (payload: { groupId?: string }) => {
    const groupId = payload?.groupId;
    if (groupId && groupId === activeGroupId.value) await loadMessages(groupId);
  });

  try {
    await hubConnection.start();
    realtimeState.value = "connected";
    if (activeGroupId.value) {
      await hubConnection.invoke("JoinGroup", activeGroupId.value).catch(() => undefined);
    }
  } catch {
    realtimeState.value = "offline";
  }
}

async function createGroup() {
  if (!createForm.value.name.trim()) return;

  try {
    const group = await apiResult<GroupDto>("/api/groups", {
      method: "POST",
      body: JSON.stringify({
        name: createForm.value.name.trim(),
        description: createForm.value.description.trim() || null,
        color: createForm.value.color,
      }),
    });

    groupDetails.value[group.id] = group;
    const mapped = toGroupModel(group);
    groups.value = [mapped, ...groups.value.filter((item) => item.id !== mapped.id)];
    showCreateModal.value = false;
    createForm.value = { name: "", description: "", color: "#2563eb" };
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
  if (!window.confirm(`Xóa ${member.fullName} khỏi nhóm?`)) return;

  try {
    await apiCommand(`/api/groups/${activeGroupId.value}/members/${member.userId}`, {
      method: "DELETE",
    });
    await Promise.all([loadMembers(activeGroupId.value), loadGroupDetail(activeGroupId.value)]);
    showSuccess("Đã xóa thành viên khỏi nhóm");
  } catch (error) {
    showError(errorMessage(error, "Không thể xóa thành viên."));
  }
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
  if (!activeGroupId.value || !projectForm.value.name.trim()) return;

  try {
    const result = await apiResult<any>(`/api/groups/${activeGroupId.value}/create-project`, {
      method: "POST",
      body: JSON.stringify({
        name: projectForm.value.name.trim(),
        code: projectForm.value.code.trim() || null,
        description: projectForm.value.description.trim() || null,
      }),
    });
    projectForm.value = { name: "", code: "", description: "" };
    showSuccess(`Đã tạo project từ nhóm (${result.membersAdded ?? 0} thành viên)`);
  } catch (error) {
    showError(errorMessage(error, "Không thể tạo project từ nhóm."));
  }
}

async function createPanelPoll() {
  if (!activeGroupId.value) return;

  const question = pollForm.value.question.trim() || "Bình chọn";
  const options = pollForm.value.options.map((option) => option.trim()).filter(Boolean);
  if (options.length < 2) {
    showError("Poll cần ít nhất 2 lựa chọn.");
    return;
  }

  try {
    const poll = await apiResult<any>(`/api/groups/${activeGroupId.value}/polls`, {
      method: "POST",
      body: JSON.stringify({
        question,
        options: options.map((content) => ({ content })),
        allowMultiple: false,
      }),
    });

    await sendMessage({
      text: "",
      attachments: [],
      poll: {
        id: poll.id ?? poll.Id,
        question,
        options,
      },
    });

    pollForm.value = { question: "", options: ["", ""] };
    showSuccess("Đã tạo bình chọn trong nhóm");
  } catch (error) {
    showError(errorMessage(error, "Không thể tạo bình chọn."));
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
}) {
  if (!activeGroupId.value) return;

  try {
    const attachments = await uploadPendingAttachments(payload.attachments);
    const content = serializeMessagePayload({ ...payload, attachments });
    const messageType = payload.poll ? "Poll" : "Text";

    if (hubConnection?.state === HubConnectionState.Connected) {
      await hubConnection.invoke("SendMessage", activeGroupId.value, content, messageType);
      return;
    }

    const saved = await apiResult<GroupMessageDto>(
      `/api/groups/${activeGroupId.value}/messages`,
      {
        method: "POST",
        body: JSON.stringify({ content, messageType }),
      },
    );
    upsertMessage(toMessageModel(saved));
  } catch (error) {
    showError(errorMessage(error, "Không thể gửi tin nhắn."));
  }
}

async function uploadPendingAttachments(attachments: TeamChatAttachment[]) {
  if (!activeGroupId.value || attachments.length === 0) return attachments;

  const uploaded: TeamChatAttachment[] = [];
  for (const attachment of attachments) {
    if (!attachment.rawFile) {
      uploaded.push(attachment);
      continue;
    }

    const form = new FormData();
    form.append("file", attachment.rawFile, attachment.name);
    const result = await apiResult<GroupFileUploadDto>(
      `/api/groups/${activeGroupId.value}/files`,
      {
        method: "POST",
        body: form,
      },
    );

    uploaded.push({
      name: result.name,
      url: result.url,
      sizeLabel: result.sizeLabel,
      kind: result.kind,
      contentType: result.contentType,
    });
  }

  return uploaded;
}

function togglePin(messageId: string) {
  const target = messages.value.find((message) => message.id === messageId);
  messages.value = messages.value.map((message) =>
    message.id === messageId ? { ...message, pinned: !message.pinned } : message,
  );
  if (target) showSuccess(target.pinned ? "Đã bỏ ghim tin nhắn" : "Đã ghim tin nhắn");
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

function changeBackground() {
  const themes = ["clean", "soft", "mint", "paper", "dark"];
  const next = themes[(themes.indexOf(backgroundTheme.value) + 1) % themes.length];
  backgroundTheme.value = next;
  localStorage.setItem("qaly.chatBackground", next);
}

function upsertMessage(message: TeamChatMessage) {
  messages.value = [...messages.value.filter((item) => item.id !== message.id), message];

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
    description:
      group.description ??
      `${group.memberCount ?? 0} thành viên · ${group.messageCount ?? 0} tin nhắn`,
    unreadCount: 0,
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
      .split("\n")
      .filter((line) => !/^\[attachments?\]/i.test(line.trim()))
      .join("\n")
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
    pinned: false,
    attachments,
    poll: parsePoll(message),
    meeting,
  };
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
      const params = new URLSearchParams({
        name: file.name,
        url: file.url ?? "",
        size: file.sizeLabel,
        kind: file.kind ?? "file",
      });
      if (file.contentType) params.set("contentType", file.contentType);
      lines.push(`[attachment] ${params.toString()}`);
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
  const attachmentLines = message.content
    .split("\n")
    .map((line) => line.trim())
    .filter((line) => line.toLowerCase().startsWith("[attachment] "));

  if (attachmentLines.length > 0) {
    return attachmentLines
      .map((line) => {
        const raw = line.replace(/^\[attachment\]\s*/i, "");
        const params = new URLSearchParams(raw);
        const name = params.get("name")?.trim();
        if (!name) return null;
        const kind = params.get("kind") === "image" ? "image" : "file";
        return {
          name,
          url: params.get("url") || undefined,
          sizeLabel: params.get("size") || "Đã tải lên",
          kind,
          contentType: params.get("contentType") || undefined,
        } satisfies TeamChatAttachment;
      })
      .filter((item): item is TeamChatAttachment => Boolean(item));
  }

  const match = message.content.match(/\[attachments\]\s*(.+)/i);
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
      <section
        class="team-chat-page groups-workspace"
        :class="{ 'groups-workspace--detail-collapsed': isDetailPanelCollapsed }"
      >
        <div v-if="loadError" class="team-chat-banner team-chat-banner--error">
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
          :background-theme="backgroundTheme"
          @send="sendMessage"
          @pin="togglePin"
          @change-background="changeBackground"
          @join-meeting="joinMeeting"
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
            <div>
              <span>CHI TIẾT</span>
              <h2>{{ activeGroup?.name ?? "Chọn nhóm" }}</h2>
              <p>{{ activeDetail?.memberCount ?? members.length }} thành viên</p>
            </div>
            <div class="group-detail-header__actions">
              <div class="group-role-badge">{{ roleLabel(activeDetail?.currentUserRole) }}</div>
              <button
                class="group-detail-icon-button"
                type="button"
                aria-label="Thu gọn chi tiết nhóm"
                @click="isDetailPanelCollapsed = true"
              >
                <PanelRightClose :size="17" />
              </button>
            </div>
          </header>

          <nav class="group-detail-tabs" aria-label="Group tools">
            <button :class="{ active: activeTab === 'members' }" @click="activeTab = 'members'">
              <Users :size="15" /> Thành viên
            </button>
            <button :class="{ active: activeTab === 'invites' }" @click="activeTab = 'invites'">
              <Mail :size="15" /> Lời mời
            </button>
            <button :class="{ active: activeTab === 'polls' }" @click="activeTab = 'polls'">
              <Vote :size="15" /> Bình chọn
            </button>
            <button :class="{ active: activeTab === 'meeting' }" @click="activeTab = 'meeting'">
              <CalendarDays :size="15" /> Cuộc họp
            </button>
            <button :class="{ active: activeTab === 'project' }" @click="activeTab = 'project'">
              <Settings :size="15" /> Dự án
            </button>
            <button :class="{ active: activeTab === 'ai' }" @click="activeTab = 'ai'">
              <Sparkles :size="15" /> AI
            </button>
          </nav>

          <div v-if="activeTab === 'members'" class="group-tool-body">
            <form v-if="canManageGroup" class="group-inline-form" @submit.prevent="addExistingMember">
              <select v-model="addUserId">
                <option value="">Chọn tài khoản đã đăng ký</option>
                <option v-for="user in availableUsers" :key="user.id" :value="user.id">
                  {{ user.fullName }} - {{ user.email }}
                </option>
              </select>
              <select v-model="addRole">
                <option value="Member">Thành viên</option>
                <option value="Admin">Quản trị viên</option>
              </select>
              <button class="primary-button primary-button--compact" type="submit">
                  <UserPlus :size="15" /> Thêm
              </button>
            </form>

            <div class="group-member-list">
              <article v-for="member in members" :key="member.userId" class="group-member-row">
                <div class="group-avatar">{{ initials(member.fullName) }}</div>
                <div>
                  <strong>{{ member.fullName }}</strong>
                  <span>{{ member.email }}</span>
                </div>
                <select
                  v-if="canManageGroup && member.role !== 'Owner'"
                  :value="member.role"
                  @change="updateMemberRole(member, ($event.target as HTMLSelectElement).value)"
                >
                  <option value="Member">Thành viên</option>
                  <option value="Admin">Quản trị viên</option>
                </select>
                <small v-else>{{ roleLabel(member.role) }}</small>
                <button
                  v-if="canManageGroup && member.role !== 'Owner'"
                  class="text-button"
                  type="button"
                  @click="removeMember(member)"
                >
                  Xóa
                </button>
              </article>
            </div>
          </div>

          <div v-else-if="activeTab === 'invites'" class="group-tool-body">
            <form v-if="canManageGroup" class="group-inline-form" @submit.prevent="inviteMember">
              <input v-model="inviteEmail" type="email" placeholder="Email đã có tài khoản Qaly" />
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
              <div class="group-poll-actions">
                <button class="secondary-button" type="button" @click="addPollOption">
                  <Plus :size="15" /> Thêm lựa chọn
                </button>
                <button class="primary-button" type="button" @click="createPanelPoll">
                  <Vote :size="15" /> Gửi poll
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
            <strong>Meeting nhóm</strong>
            <p>Bắt đầu phiên meeting cho thành viên trong nhóm.</p>
            <button class="primary-button" type="button" @click="startMeeting">Bắt đầu cuộc họp</button>
          </div>

          <div v-else-if="activeTab === 'ai'" class="group-tool-body" style="padding: 0; min-height: 0;">
            <GroupAiPanel :group-id="activeGroupId" :members="members" />
          </div>

          <div v-else class="group-tool-body">
            <form class="group-stack-form" @submit.prevent="createProjectFromGroup">
              <input v-model="projectForm.name" type="text" placeholder="Tên project" />
              <input v-model="projectForm.code" type="text" placeholder="Mã project" />
              <textarea v-model="projectForm.description" rows="3" placeholder="Mô tả"></textarea>
              <button class="primary-button" type="submit">
                <Check :size="15" /> Tạo project từ nhóm
              </button>
            </form>
          </div>
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

    <div v-if="showCreateModal" class="group-modal-backdrop" @click.self="showCreateModal = false">
      <form class="group-modal glass-card" @submit.prevent="createGroup">
        <header>
          <h2>Tạo nhóm chat</h2>
          <button type="button" class="text-button" @click="showCreateModal = false">Đóng</button>
        </header>
        <input v-model="createForm.name" type="text" placeholder="Tên nhóm" required />
        <textarea v-model="createForm.description" rows="3" placeholder="Mô tả nhóm"></textarea>
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

.groups-workspace :deep(.glass-card) {
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
  border-radius: 14px;
  color: #64748b;
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
  border-radius: 12px;
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
  background: #1677ff;
  font-weight: 800;
  letter-spacing: 0;
}

.groups-workspace :deep(.team-chat-group__avatar) {
  width: 38px;
  height: 38px;
  border-radius: 13px;
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
  background: #fbfdff;
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
  border-radius: 15px;
  font-size: 0.82rem;
}

.groups-workspace :deep(.team-chat-window__actions .icon-button) {
  width: 38px;
  height: 38px;
  border-radius: 12px;
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
  border-radius: 10px;
  background: rgba(239, 246, 255, 0.92);
  color: #1e3a8a;
  padding: 10px 14px;
  font-size: 0.88rem;
  font-weight: 600;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.08);
}

.team-chat-banner--error {
  border-color: rgba(239, 68, 68, 0.22);
  background: rgba(254, 242, 242, 0.94);
  color: #991b1b;
}

.group-detail-panel {
  padding: 20px 18px;
  display: flex;
  flex-direction: column;
  gap: 16px;
  min-width: 0;
  overflow-x: hidden;
  overflow-y: hidden;
  background: #ffffff;
}

.group-detail-panel.is-collapsed {
  align-items: center;
  padding: 16px 8px;
}

.group-detail-content {
  min-width: 0;
  min-height: 0;
  height: 100%;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.group-detail-header {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  min-width: 0;
}

.group-detail-header > div:first-child {
  min-width: 0;
}

.group-detail-header h2,
.group-detail-header p {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.group-detail-header span {
  color: #64748b;
  font-size: 0.78rem;
  font-weight: 800;
  text-transform: uppercase;
}

.group-detail-header h2 {
  margin: 4px 0;
  color: #111827;
  font-size: 1.35rem;
  line-height: 1.1;
}

.group-detail-header p {
  margin: 0;
  color: #64748b;
  font-size: 0.88rem;
}

.group-detail-header__actions {
  flex: 0 0 auto;
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.group-detail-icon-button,
.group-detail-collapse-button {
  border: 1px solid #dbe3ef;
  border-radius: 12px;
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
  height: 34px;
  border-radius: 999px;
  padding: 8px 13px;
  background: #eff6ff;
  color: #1d4ed8;
  font-size: 0.78rem;
  font-weight: 800;
  box-shadow: inset 0 0 0 1px rgba(37, 99, 235, 0.12);
}

.group-detail-tabs {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 6px;
  min-width: 0;
}

.group-detail-tabs button {
  min-width: 0;
  min-height: 44px;
  border: 1px solid rgba(148, 163, 184, 0.24);
  border-radius: 12px;
  background: #ffffff;
  color: #475569;
  display: inline-flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 3px;
  padding: 8px 4px;
  overflow: hidden;
  text-align: center;
  font-size: 0.64rem;
  font-weight: 800;
  line-height: 1.15;
  transition:
    border-color 160ms ease,
    background 160ms ease,
    color 160ms ease;
}

.group-detail-tabs button:hover {
  border-color: #bfdbfe;
  background: #f8fbff;
  color: #1677ff;
}

.group-detail-tabs button.active {
  border-color: rgba(37, 99, 235, 0.45);
  background: #eff6ff;
  color: #1d4ed8;
  box-shadow: none;
}

.group-tool-body {
  min-width: 0;
  min-height: 0;
  flex: 1;
  overflow-x: hidden;
  overflow-y: auto;
}

.group-inline-form,
.group-stack-form {
  display: grid;
  gap: 8px;
}

.group-inline-form {
  grid-template-columns: minmax(0, 1fr) auto;
  margin-bottom: 14px;
}

.group-inline-form select:first-child {
  grid-column: 1 / -1;
}

.group-inline-form input,
.group-inline-form select,
.group-stack-form input,
.group-stack-form textarea,
.group-modal input,
.group-modal textarea {
  width: 100%;
  border: 1px solid #dbe3ef;
  border-radius: 12px;
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
  gap: 10px;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  padding: 10px;
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

.group-member-row select {
  max-width: 92px;
  border: 1px solid rgba(148, 163, 184, 0.28);
  border-radius: 8px;
  padding: 7px;
}

.group-member-row .text-button {
  grid-column: 3;
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
  width: 38px;
  height: 38px;
  border-radius: 999px;
  display: grid;
  place-items: center;
  background: #1677ff;
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
  border-radius: 14px;
  background: #f8fbff;
  box-shadow: none;
}

.group-poll-composer input {
  width: 100%;
  min-height: 42px;
  border: 1px solid #dbe3ef;
  border-radius: 12px;
  padding: 10px 12px;
  background: #ffffff;
  color: #111827;
  font-weight: 600;
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
  background: #1677ff;
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
  border-radius: 13px;
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
  border-radius: 14px;
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
  border-radius: 14px;
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
  box-shadow: 0 6px 18px rgba(15, 23, 42, 0.06);
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
