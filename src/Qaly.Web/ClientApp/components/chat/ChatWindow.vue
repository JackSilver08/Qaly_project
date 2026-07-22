<script setup lang="ts">
import { computed, nextTick, ref, watch } from "vue";
import {
  ArrowLeft,
  Check,
  Image as ImageIcon,
  Info,
  Search,
  Images,
  Forward,
  Reply,
  Palette,
  Paperclip,
  Pencil,
  Pin,
  RotateCcw,
  Send,
  SmilePlus,
  Trash2,
  Sparkles,
  ListTodo,
  X,
} from "lucide-vue-next";
import { showError, showSuccess } from "../../composables/use-toast";
import MessageItem from "./MessageItem.vue";
import type {
  ChatGroupModel,
  TeamChatAttachment,
  TeamChatMemberMention,
  TeamChatMessage,
  TeamChatPoll,
} from "./chat-types";

const props = defineProps<{
  group: ChatGroupModel | null;
  messages: TeamChatMessage[];
  currentUserId: string;
  members?: TeamChatMemberMention[];
  typingUsers?: string[];
  backgroundTheme?: string;
  backgroundImage?: string;
  canCustomizeBackground?: boolean;
  availableGroups?: ChatGroupModel[];
}>();

const emit = defineEmits<{
  send: [
    payload: {
      text: string;
      attachments: TeamChatAttachment[];
      poll?: TeamChatPoll;
      replyToMessageId?: string;
    },
  ];
  edit: [messageId: string, text: string];
  pin: [messageId: string, isPinned: boolean];
  recall: [messageId: string];
  hide: [messageIds: string[]];
  react: [messageId: string, emoji: string];
  typing: [isTyping: boolean];
  setBackground: [theme: string];
  setBackgroundImage: [file: File | null];
  joinMeeting: [meetingId: string];
  forward: [messageId: string, targetGroupId: string];
  analyzeSelection: [action: "summary" | "task-draft", messageIds: string[]];
}>();

const draft = ref("");
const showEmoji = ref(false);
const pendingAttachments = ref<TeamChatAttachment[]>([]);
const bodyRef = ref<HTMLElement | null>(null);
const textareaRef = ref<HTMLTextAreaElement | null>(null);
const openMenuId = ref<string | null>(null);
const editingMessage = ref<TeamChatMessage | null>(null);
const detailMessage = ref<TeamChatMessage | null>(null);
const selectionMode = ref(false);
const selectedIds = ref<Set<string>>(new Set());
const mentionQuery = ref<string | null>(null);
const showBackgroundMenu = ref(false);
const replyingMessage = ref<TeamChatMessage | null>(null);
const forwardingMessage = ref<TeamChatMessage | null>(null);
const forwardTargetGroupId = ref("");
const showSearch = ref(false);
const searchQuery = ref("");
const showPinnedPanel = ref(false);
const galleryIndex = ref(-1);

const pinnedMessages = computed(() =>
  props.messages.filter((message) => message.pinned && !message.isDeleted),
);
const selectedMessages = computed(() =>
  props.messages.filter((message) => selectedIds.value.has(message.id)),
);
const filteredMessages = computed(() => {
  const query = searchQuery.value.trim().toLowerCase();
  if (!query) return props.messages;
  return props.messages.filter((message) =>
    `${message.senderName} ${message.text} ${message.forwardedFrom?.text ?? ""}`.toLowerCase().includes(query),
  );
});
const galleryImages = computed(() =>
  props.messages.flatMap((message) => [
    ...message.attachments,
    ...(message.forwardedFrom?.attachments ?? []),
  ]).filter((attachment) => attachment.kind === "image" && attachment.url),
);
const activeGalleryImage = computed(() => galleryImages.value[galleryIndex.value]);
const emojiOptions = ["👍", "✅", "🔥", "🎯", "🙏", "💡"];
const backgroundOptions = [
  { id: "clean", name: "Tối giản", previewClass: "bg-preview--clean" },
  { id: "soft", name: "Mây xanh", previewClass: "bg-preview--soft" },
  { id: "mint", name: "Vườn mint", previewClass: "bg-preview--mint" },
  { id: "paper", name: "Giấy lưới", previewClass: "bg-preview--paper" },
  { id: "aurora", name: "Aurora", previewClass: "bg-preview--aurora" },
  { id: "sunset", name: "Hoàng hôn", previewClass: "bg-preview--sunset" },
  { id: "night", name: "Đêm neon", previewClass: "bg-preview--night" },
  { id: "rose", name: "Ngày của mẹ", previewClass: "bg-preview--rose" },
  { id: "ocean", name: "Đại dương", previewClass: "bg-preview--ocean" },
];
const themeBackgrounds: Record<string, string> = {
  clean: "#fbfdff",
  soft:
    "radial-gradient(circle at 18% 18%, rgba(96, 165, 250, 0.32), transparent 30%), radial-gradient(circle at 82% 12%, rgba(186, 230, 253, 0.55), transparent 34%), linear-gradient(135deg, #ffffff 0%, #eaf4ff 100%)",
  mint:
    "radial-gradient(circle at 78% 22%, rgba(16, 185, 129, 0.24), transparent 30%), radial-gradient(circle at 12% 85%, rgba(125, 211, 252, 0.36), transparent 30%), linear-gradient(135deg, #f0fdfa 0%, #ffffff 100%)",
  paper:
    "linear-gradient(90deg, rgba(100, 116, 139, 0.11) 1px, transparent 1px), linear-gradient(180deg, rgba(100, 116, 139, 0.11) 1px, transparent 1px), linear-gradient(135deg, #fffef8, #f8fafc)",
  aurora:
    "radial-gradient(circle at 22% 22%, rgba(129, 140, 248, 0.48), transparent 32%), radial-gradient(circle at 72% 62%, rgba(45, 212, 191, 0.42), transparent 34%), linear-gradient(135deg, #eff6ff 0%, #faf5ff 52%, #ecfeff 100%)",
  sunset:
    "radial-gradient(circle at 65% 20%, rgba(251, 146, 60, 0.34), transparent 32%), radial-gradient(circle at 18% 80%, rgba(244, 114, 182, 0.3), transparent 34%), linear-gradient(135deg, #fff7ed 0%, #fef2f2 52%, #f8fafc 100%)",
  night:
    "radial-gradient(circle at 30% 18%, rgba(59, 130, 246, 0.36), transparent 26%), radial-gradient(circle at 78% 78%, rgba(168, 85, 247, 0.32), transparent 32%), linear-gradient(135deg, #0f172a 0%, #172554 100%)",
  rose:
    "radial-gradient(circle at 50% 52%, rgba(244, 114, 182, 0.28), transparent 26%), radial-gradient(circle at 72% 22%, rgba(251, 113, 133, 0.24), transparent 30%), linear-gradient(135deg, #fff1f2 0%, #faf5ff 100%)",
  ocean:
    "radial-gradient(circle at 18% 22%, rgba(14, 165, 233, 0.34), transparent 30%), radial-gradient(circle at 78% 70%, rgba(20, 184, 166, 0.24), transparent 34%), linear-gradient(135deg, #ecfeff 0%, #eff6ff 100%)",
};
const groupInitials = computed(() => initials(props.group?.name ?? "Qaly"));
const chatWindowStyle = computed(() => ({
  background: props.backgroundImage
    ? `linear-gradient(rgba(255, 255, 255, 0.48), rgba(255, 255, 255, 0.58)), url("${props.backgroundImage}")`
    : themeBackgrounds[props.backgroundTheme ?? "clean"] ?? themeBackgrounds.clean,
  backgroundSize: props.backgroundTheme === "paper" && !props.backgroundImage ? "26px 26px, 26px 26px, auto" : "cover",
  backgroundPosition: "center",
}));
const mentionOptions = computed(() => {
  if (mentionQuery.value == null) return [];
  const query = mentionQuery.value.toLowerCase();
  const allOption: TeamChatMemberMention = {
    id: "all",
    name: "all",
    initials: "@",
    isAll: true,
  };
  const members = (props.members ?? [])
    .filter((member) => member.id !== props.currentUserId)
    .filter((member) => member.name.toLowerCase().includes(query))
    .slice(0, 6);
  return query === "all" || "all".includes(query) ? [allOption, ...members] : members;
});
const typingText = computed(() => {
  const names = [...new Set(props.typingUsers ?? [])].filter(Boolean).slice(0, 2);
  if (names.length === 0) return "";
  return names.length === 1 ? `${names[0]} đang nhập` : `${names.join(", ")} đang nhập`;
});

watch(
  () => props.messages.length,
  async () => {
    await nextTick();
    bodyRef.value?.scrollTo({
      top: bodyRef.value.scrollHeight,
      behavior: "smooth",
    });
  },
);

watch(
  () => props.group?.id,
  () => {
    cancelEditing();
    exitSelectionMode();
    openMenuId.value = null;
    showBackgroundMenu.value = false;
    replyingMessage.value = null;
    forwardingMessage.value = null;
    searchQuery.value = "";
  },
);

function attachFiles(event: Event, kind: "file" | "image") {
  const input = event.target as HTMLInputElement;
  const files = Array.from(input.files ?? []);
  const remainingSlots = Math.max(0, 10 - pendingAttachments.value.length);
  if (files.length > remainingSlots) {
    showError("Mỗi tin nhắn chỉ được gửi tối đa 10 file.");
  }

  pendingAttachments.value = [
    ...pendingAttachments.value,
    ...files.slice(0, remainingSlots).map<TeamChatAttachment>((file) => ({
      name: file.name,
      sizeLabel:
        file.size < 1024
          ? `${file.size} B`
          : file.size < 1024 * 1024
            ? `${Math.round(file.size / 1024)} KB`
            : `${(file.size / 1024 / 1024).toFixed(1)} MB`,
      contentType: file.type || "application/octet-stream",
      kind: file.type.startsWith("image/")
        ? "image"
        : file.type.startsWith("video/")
          ? "video"
          : kind,
      sourceFile: file,
    })),
  ];
  input.value = "";
}

function addEmoji(emoji: string) {
  draft.value += emoji;
  showEmoji.value = false;
  void nextTick(() => textareaRef.value?.focus());
}

function updateMentionQuery() {
  const cursor = textareaRef.value?.selectionStart ?? draft.value.length;
  const beforeCursor = draft.value.slice(0, cursor);
  const match = beforeCursor.match(/(?:^|\s)@([\p{L}\p{N}_ .-]{0,32})$/u);
  mentionQuery.value = match ? match[1].trimStart() : null;
  emit("typing", Boolean(draft.value.trim()));
}

function insertMention(member: TeamChatMemberMention) {
  const textarea = textareaRef.value;
  const cursor = textarea?.selectionStart ?? draft.value.length;
  const beforeCursor = draft.value.slice(0, cursor);
  const afterCursor = draft.value.slice(cursor);
  const match = beforeCursor.match(/(?:^|\s)@([\p{L}\p{N}_ .-]{0,32})$/u);
  if (!match || match.index == null) return;

  const prefix = beforeCursor.slice(0, match.index);
  const separator = match[0].startsWith(" ") ? " " : "";
  const mentionText = member.isAll ? "@all" : `@${member.name}`;
  draft.value = `${prefix}${separator}${mentionText} ${afterCursor}`;
  mentionQuery.value = null;
  void nextTick(() => textareaRef.value?.focus());
}

function initials(name: string) {
  return name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");
}

function toggleMenu(messageId: string) {
  openMenuId.value = openMenuId.value === messageId ? null : messageId;
}

async function handleMessageAction(
  action: "copy" | "pin" | "select" | "detail" | "edit" | "recall" | "hide" | "reply" | "forward",
  message: TeamChatMessage,
) {
  openMenuId.value = null;

  if (action === "copy") {
    await copyText(message.text);
    return;
  }
  if (action === "reply") {
    replyingMessage.value = message;
    await nextTick();
    textareaRef.value?.focus();
    return;
  }
  if (action === "forward") {
    forwardingMessage.value = message;
    forwardTargetGroupId.value = (props.availableGroups ?? []).find((group) => group.id !== props.group?.id)?.id ?? "";
    return;
  }
  if (action === "pin") {
    emit("pin", message.id, !message.pinned);
    return;
  }
  if (action === "select") {
    selectionMode.value = true;
    selectedIds.value = new Set([message.id]);
    return;
  }
  if (action === "detail") {
    detailMessage.value = message;
    return;
  }
  if (action === "edit") {
    editingMessage.value = message;
    draft.value = message.text;
    pendingAttachments.value = [];
    await nextTick();
    textareaRef.value?.focus();
    return;
  }
  if (action === "recall") {
    if (window.confirm("Thu hồi tin nhắn này với mọi thành viên?")) {
      emit("recall", message.id);
    }
    return;
  }
  if (action === "hide" && window.confirm("Xóa tin nhắn này chỉ ở phía bạn?")) {
    emit("hide", [message.id]);
  }
}

function toggleSelected(messageId: string) {
  const next = new Set(selectedIds.value);
  if (next.has(messageId)) next.delete(messageId);
  else next.add(messageId);
  selectedIds.value = next;
}

function exitSelectionMode() {
  selectionMode.value = false;
  selectedIds.value = new Set();
}

async function copySelected() {
  const text = selectedMessages.value
    .filter((message) => !message.isDeleted && message.text)
    .map((message) => `${message.senderName}: ${message.text}`)
    .join("\n");
  if (text) await copyText(text);
}

function hideSelected() {
  const ids = [...selectedIds.value];
  if (!ids.length || !window.confirm(`Xóa ${ids.length} tin nhắn chỉ ở phía bạn?`)) return;
  emit("hide", ids);
  exitSelectionMode();
}

function analyzeSelected(action: "summary" | "task-draft") {
  const ids = selectedMessages.value
    .filter((message) => !message.isDeleted && Boolean(message.text))
    .map((message) => message.id);
  if (!ids.length) return;
  emit("analyzeSelection", action, ids);
  exitSelectionMode();
}

async function copyText(text: string) {
  try {
    await navigator.clipboard.writeText(text);
    showSuccess("Đã sao chép tin nhắn");
  } catch {
    showError("Không thể sao chép tin nhắn");
  }
}

function cancelEditing() {
  editingMessage.value = null;
  draft.value = "";
}

function sendMessage() {
  const text = draft.value.trim();
  if (!text && pendingAttachments.value.length === 0) return;

  if (editingMessage.value) {
    emit("edit", editingMessage.value.id, text);
    cancelEditing();
    emit("typing", false);
    return;
  }

  emit("send", {
    text,
    attachments: pendingAttachments.value,
    replyToMessageId: replyingMessage.value?.id,
  });
  draft.value = "";
  pendingAttachments.value = [];
  mentionQuery.value = null;
  replyingMessage.value = null;
  emit("typing", false);
}

function confirmForward() {
  if (!forwardingMessage.value || !forwardTargetGroupId.value) return;
  emit("forward", forwardingMessage.value.id, forwardTargetGroupId.value);
  forwardingMessage.value = null;
}

function openGallery(messageId: string, attachmentId?: string) {
  const message = props.messages.find((item) => item.id === messageId);
  const target = [...(message?.attachments ?? []), ...(message?.forwardedFrom?.attachments ?? [])]
    .find((attachment) => attachment.id === attachmentId) ?? message?.attachments.find((attachment) => attachment.kind === "image");
  const index = galleryImages.value.findIndex((attachment) =>
    attachment.id === target?.id && attachment.url === target?.url,
  );
  galleryIndex.value = index >= 0 ? index : 0;
}

function moveGallery(direction: number) {
  if (!galleryImages.value.length) return;
  galleryIndex.value = (galleryIndex.value + direction + galleryImages.value.length) % galleryImages.value.length;
}

function scrollToMessage(messageId: string) {
  document.getElementById(`group-message-${messageId}`)?.scrollIntoView({ behavior: "smooth", block: "center" });
  showPinnedPanel.value = false;
}

function relayReaction(messageId: string, emoji: string) {
  emit("react", messageId, emoji);
}

function selectBackground(theme: string) {
  emit("setBackground", theme);
  showBackgroundMenu.value = false;
}

function resetBackground() {
  emit("setBackgroundImage", null);
  showBackgroundMenu.value = false;
}

function uploadBackground(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = "";
  if (!file) return;

  if (!file.type.startsWith("image/")) {
    showError("Vui lòng chọn một file ảnh.");
    return;
  }
  if (file.size > 2.5 * 1024 * 1024) {
    showError("Ảnh nền nên nhỏ hơn 2.5MB để trình duyệt lưu ổn định.");
    return;
  }

  emit("setBackgroundImage", file);
  showBackgroundMenu.value = false;
}
</script>

<template>
  <section
    class="team-chat-window glass-card"
    :class="`team-chat-window--${backgroundTheme ?? 'clean'}`"
    :style="chatWindowStyle"
    @click="openMenuId = null"
  >
    <header class="team-chat-window__header">
      <div class="team-chat-window__identity">
        <span class="team-chat-window__avatar">
          <img v-if="group?.avatarUrl" :src="group.avatarUrl" :alt="group.name" />
          <template v-else>{{ groupInitials }}</template>
        </span>
        <div>
          <h2>{{ group?.name ?? "Chọn nhóm chat" }}</h2>
          <span>{{ group?.summary ?? "Chọn một nhóm để bắt đầu trò chuyện" }}</span>
        </div>
      </div>
      <div class="team-chat-window__actions">
        <button class="icon-button icon-button--small" type="button" aria-label="Tìm trong đoạn chat" @click.stop="showSearch = !showSearch">
          <Search :size="16" />
        </button>
        <button v-if="galleryImages.length" class="icon-button icon-button--small" type="button" aria-label="Xem thư viện ảnh" @click.stop="galleryIndex = 0">
          <Images :size="16" />
        </button>
        <button
          v-if="canCustomizeBackground"
          class="icon-button icon-button--small"
          type="button"
          aria-label="Đổi nền chat"
          @click.stop="showBackgroundMenu = !showBackgroundMenu"
        >
          <Palette :size="16" />
        </button>
        <button v-if="pinnedMessages.length" class="team-pinned" type="button" @click.stop="showPinnedPanel = !showPinnedPanel">
          <Pin :size="14" />
          <span>{{ pinnedMessages.length }} tin đã ghim</span>
        </button>
      </div>
    </header>

    <div v-if="showSearch" class="team-chat-search">
      <Search :size="16" />
      <input v-model="searchQuery" type="search" placeholder="Tìm nội dung hoặc người gửi..." autofocus />
      <span>{{ filteredMessages.length }} kết quả</span>
      <button type="button" aria-label="Đóng tìm kiếm" @click="showSearch = false; searchQuery = ''"><X :size="16" /></button>
    </div>

    <button v-if="pinnedMessages.length" class="team-pinned-list" type="button" @click="scrollToMessage(pinnedMessages[0].id)">
      <Pin :size="15" />
      <span>{{ pinnedMessages[0]?.text || "Tin nhắn đã ghim" }}</span>
      <small v-if="pinnedMessages.length > 1">+{{ pinnedMessages.length - 1 }}</small>
    </button>

    <div v-if="showPinnedPanel" class="team-pinned-panel">
      <button v-for="message in pinnedMessages" :key="message.id" type="button" @click="scrollToMessage(message.id)">
        <strong>{{ message.senderName }}</strong>
        <span>{{ message.text || 'Ảnh, file hoặc nội dung đặc biệt' }}</span>
      </button>
    </div>

    <div ref="bodyRef" class="team-chat-body no-scrollbar">
      <div v-if="!messages.length" class="team-chat-empty">
        <strong>{{ group ? "Chưa có tin nhắn" : "Chọn nhóm chat" }}</strong>
        <span>
          {{ group ? "Gửi tin nhắn đầu tiên để bắt đầu cuộc trò chuyện." : "Danh sách tin nhắn sẽ xuất hiện ở đây." }}
        </span>
      </div>

      <MessageItem
        v-for="(message, index) in filteredMessages"
        :key="message.id"
        :message="message"
        :current-user-id="currentUserId"
        :is-consecutive="
          index > 0 &&
          filteredMessages[index - 1].senderId === message.senderId &&
          !filteredMessages[index - 1].meeting
        "
        :menu-open="openMenuId === message.id"
        :selection-mode="selectionMode"
        :selected="selectedIds.has(message.id)"
        @menu="toggleMenu"
        @action="handleMessageAction"
        @toggle-select="toggleSelected"
        @react="relayReaction"
        @join-meeting="$emit('joinMeeting', $event)"
        @open-image="openGallery"
      />

      <div v-if="searchQuery && !filteredMessages.length" class="team-chat-empty">
        <strong>Không tìm thấy tin nhắn</strong>
        <span>Thử một từ khóa ngắn hơn hoặc tên người gửi.</span>
      </div>

      <div v-if="typingText" class="team-typing-indicator">
        <span class="typing-dots"><i></i><i></i><i></i></span>
        <strong>{{ typingText }}</strong>
      </div>
    </div>

    <div v-if="selectionMode" class="team-selection-toolbar">
      <button type="button" :disabled="!selectedIds.size" title="Summarize selected messages" @click="analyzeSelected('summary')">
        <Sparkles :size="16" /> TĂ³m táº¯t
      </button>
      <button type="button" :disabled="!selectedIds.size" title="Create a task draft from selected messages" @click="analyzeSelected('task-draft')">
        <ListTodo :size="16" /> Táº¡o task
      </button>
      <button type="button" class="selection-close" @click="exitSelectionMode"><X :size="18" /></button>
      <strong>{{ selectedIds.size }} tin nhắn đã chọn</strong>
      <button type="button" :disabled="!selectedIds.size" @click="copySelected">Sao chép</button>
      <button type="button" class="is-danger" :disabled="!selectedIds.size" @click="hideSelected">
        <Trash2 :size="16" /> Xóa phía tôi
      </button>
    </div>

    <div v-else class="team-chat-composer-wrapper">
      <div v-if="replyingMessage" class="team-reply-banner">
        <Reply :size="15" />
        <div>
          <strong>Trả lời {{ replyingMessage.senderName }}</strong>
          <span>{{ replyingMessage.text || 'Ảnh hoặc tệp đính kèm' }}</span>
        </div>
        <button type="button" aria-label="Hủy trả lời" @click="replyingMessage = null"><X :size="17" /></button>
      </div>
      <div v-if="editingMessage" class="team-edit-banner">
        <Pencil :size="15" />
        <div>
          <strong>Chỉnh sửa tin nhắn</strong>
          <span>{{ editingMessage.text }}</span>
        </div>
        <button type="button" aria-label="Hủy chỉnh sửa" @click="cancelEditing"><X :size="17" /></button>
      </div>

      <div v-if="pendingAttachments.length" class="team-attachment-preview">
        <div v-for="(file, index) in pendingAttachments" :key="index" class="attachment-chip">
          <ImageIcon v-if="file.kind === 'image'" :size="14" />
          <Paperclip v-else :size="14" />
          <span>{{ file.name }}</span>
        </div>
      </div>

      <form class="team-chat-composer" @submit.prevent="sendMessage">
        <button
          class="icon-button composer-tool-btn"
          type="button"
          aria-label="Biểu cảm"
          @click="showEmoji = !showEmoji"
        >
          <SmilePlus :size="18" />
        </button>
        <label v-if="!editingMessage" class="icon-button composer-tool-btn" aria-label="Đính kèm">
          <Paperclip :size="18" />
          <input type="file" multiple @change="attachFiles($event, 'file')" />
        </label>
        <textarea
          ref="textareaRef"
          v-model="draft"
          rows="1"
          :placeholder="editingMessage ? 'Chỉnh sửa nội dung...' : 'Nhập tin nhắn...'"
          @input="updateMentionQuery"
          @focus="updateMentionQuery"
          @blur="$emit('typing', false)"
          @keydown.enter.exact.prevent="sendMessage"
        ></textarea>
        <button class="primary-button composer-send-btn" type="submit" :aria-label="editingMessage ? 'Lưu chỉnh sửa' : 'Gửi tin nhắn'">
          <Send :size="16" />
        </button>
      </form>

      <div v-if="mentionOptions.length" class="team-mention-popover">
        <button
          v-for="member in mentionOptions"
          :key="member.id"
          type="button"
          @mousedown.prevent="insertMention(member)"
        >
          <span>{{ member.initials }}</span>
          <strong>{{ member.isAll ? "Tag tất cả" : member.name }}</strong>
          <small>{{ member.isAll ? "@all" : "Thành viên" }}</small>
        </button>
      </div>
    </div>

    <div v-if="showEmoji" class="team-emoji-picker glass-card" @click.stop>
      <button v-for="emoji in emojiOptions" :key="emoji" type="button" @click="addEmoji(emoji)">
        {{ emoji }}
      </button>
    </div>

    <Teleport to="body">
      <div v-if="forwardingMessage" class="message-detail-backdrop" @click.self="forwardingMessage = null">
        <section class="message-detail-card team-forward-card">
          <header><div><Forward :size="18" /><strong>Chuyển tiếp tin nhắn</strong></div><button type="button" @click="forwardingMessage = null"><X :size="18" /></button></header>
          <p>{{ forwardingMessage.text || 'Ảnh hoặc tệp đính kèm' }}</p>
          <label>Chọn nhóm nhận
            <select v-model="forwardTargetGroupId">
              <option value="">Chọn nhóm</option>
              <option v-for="target in availableGroups?.filter(item => item.id !== group?.id)" :key="target.id" :value="target.id">{{ target.name }}</option>
            </select>
          </label>
          <button class="primary-button" type="button" :disabled="!forwardTargetGroupId" @click="confirmForward">Chuyển tiếp</button>
        </section>
      </div>
    </Teleport>

    <Teleport to="body">
      <div v-if="activeGalleryImage" class="team-gallery-backdrop" @click.self="galleryIndex = -1">
        <button type="button" class="team-gallery-close" aria-label="Đóng thư viện" @click="galleryIndex = -1"><X :size="24" /></button>
        <button type="button" class="team-gallery-nav is-prev" aria-label="Ảnh trước" @click="moveGallery(-1)">‹</button>
        <figure><img :src="activeGalleryImage.url" :alt="activeGalleryImage.name" /><figcaption>{{ activeGalleryImage.name }} · {{ galleryIndex + 1 }}/{{ galleryImages.length }}</figcaption></figure>
        <button type="button" class="team-gallery-nav is-next" aria-label="Ảnh sau" @click="moveGallery(1)">›</button>
      </div>
    </Teleport>

    <Teleport to="body">
      <div v-if="showBackgroundMenu" class="chat-customize-backdrop" @click.self="showBackgroundMenu = false">
        <section class="chat-customize-sheet" @click.stop>
          <header class="chat-customize-header">
            <button type="button" aria-label="Quay lại" @click="showBackgroundMenu = false">
              <ArrowLeft :size="24" />
            </button>
            <h2>Tùy chỉnh</h2>
            <button type="button" aria-label="Đóng" @click="showBackgroundMenu = false">
              <X :size="22" />
            </button>
          </header>

          <nav class="chat-customize-tabs" aria-label="Tùy chỉnh đoạn chat">
            <button type="button" class="is-active">Chủ đề</button>
            <button type="button" disabled>Cảm xúc nhanh</button>
            <button type="button" disabled>Hiệu ứng từ ngữ</button>
          </nav>

          <div class="chat-customize-actions">
            <label class="chat-customize-action-card">
              <span><ImageIcon :size="31" /></span>
              <strong>Tải hình ảnh lên</strong>
              <input type="file" accept="image/*" @change="uploadBackground" />
            </label>
            <button type="button" class="chat-customize-action-card" @click="resetBackground">
              <span><RotateCcw :size="31" /></span>
              <strong>Đặt lại nền</strong>
            </button>
          </div>

          <div class="chat-customize-grid">
            <button
              v-for="option in backgroundOptions"
              :key="option.id"
              type="button"
              class="chat-theme-card"
              :class="{ 'is-active': !backgroundImage && backgroundTheme === option.id }"
              @click="selectBackground(option.id)"
            >
              <span class="chat-theme-card__preview" :class="option.previewClass">
                <i v-if="!backgroundImage && backgroundTheme === option.id">
                  <Check :size="18" />
                </i>
              </span>
              <strong>{{ option.name }}</strong>
            </button>
          </div>
        </section>
      </div>
    </Teleport>

    <Teleport to="body">
      <div v-if="detailMessage" class="message-detail-backdrop" @click.self="detailMessage = null">
        <section class="message-detail-card">
          <header>
            <div><Info :size="18" /><strong>Chi tiết tin nhắn</strong></div>
            <button type="button" @click="detailMessage = null"><X :size="18" /></button>
          </header>
          <dl>
            <div><dt>Người gửi</dt><dd>{{ detailMessage.senderName }}</dd></div>
            <div><dt>Thời gian</dt><dd>{{ new Date(detailMessage.createdAtRaw).toLocaleString("vi-VN") }}</dd></div>
            <div><dt>Trạng thái</dt><dd>{{ detailMessage.isDeleted ? "Đã thu hồi" : "Đã gửi" }}</dd></div>
            <div v-if="detailMessage.editedAt"><dt>Chỉnh sửa</dt><dd>{{ new Date(detailMessage.editedAt).toLocaleString("vi-VN") }}</dd></div>
            <div><dt>Ghim</dt><dd>{{ detailMessage.pinned ? "Đang ghim" : "Không" }}</dd></div>
          </dl>
          <p v-if="detailMessage.text && !detailMessage.isDeleted">{{ detailMessage.text }}</p>
        </section>
      </div>
    </Teleport>
  </section>
</template>

<style scoped>
.team-chat-window {
  position: relative;
}

.team-chat-window__actions,
.team-chat-window__identity {
  display: flex;
  align-items: center;
  gap: 10px;
}

.team-chat-window__identity > div {
  min-width: 0;
}

.team-pinned,
.team-pinned-list {
  display: inline-flex;
  align-items: center;
  gap: 7px;
}

.team-pinned {
  border-radius: 999px;
  padding: 7px 10px;
  color: #1d4ed8;
  background: #eff6ff;
  font-size: 0.75rem;
  font-weight: 750;
}

.team-pinned-list {
  min-height: 38px;
  padding: 8px 12px;
  border: 1px solid #dbeafe;
  border-radius: var(--qaly-radius-lg);
  color: #1d4ed8;
  background: #f8fbff;
  font-size: 0.8rem;
}

.team-pinned-list span {
  min-width: 0;
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.team-chat-search {
  min-height: 44px;
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto auto;
  align-items: center;
  gap: 9px;
  padding: 7px 10px;
  border: 1px solid #dbeafe;
  border-radius: var(--qaly-radius-lg);
  background: rgba(255, 255, 255, 0.96);
}

.team-chat-search input {
  min-width: 0;
  border: 0;
  outline: 0;
  color: #0f172a;
  background: transparent;
}

.team-chat-search span { color: #64748b; font-size: 0.72rem; }
.team-chat-search button { border: 0; background: transparent; cursor: pointer; }

.team-pinned-panel {
  position: absolute;
  z-index: 30;
  top: 72px;
  right: 18px;
  width: min(340px, calc(100% - 36px));
  max-height: 280px;
  overflow-y: auto;
  display: grid;
  gap: 5px;
  padding: 8px;
  border: 1px solid #dbe3ef;
  border-radius: var(--qaly-radius-lg);
  background: #fff;
  box-shadow: var(--qaly-shadow-md);
}

.team-pinned-panel button { display: grid; gap: 3px; border: 0; border-radius: var(--qaly-radius-lg); padding: 9px; background: transparent; text-align: left; cursor: pointer; }
.team-pinned-panel button:hover { background: #eff6ff; }
.team-pinned-panel span { overflow: hidden; color: #64748b; font-size: 0.76rem; text-overflow: ellipsis; white-space: nowrap; }

.team-reply-banner {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 10px;
  padding: 8px 11px;
  border-left: 3px solid #2563eb;
  border-radius: var(--qaly-radius-lg);
  color: #2563eb;
  background: #eff6ff;
}

.team-reply-banner div { min-width: 0; display: grid; }
.team-reply-banner span { overflow: hidden; color: #64748b; font-size: 0.74rem; text-overflow: ellipsis; white-space: nowrap; }
.team-reply-banner button { border: 0; background: transparent; cursor: pointer; }

.team-forward-card { display: grid; gap: 14px; }
.team-forward-card label { display: grid; gap: 7px; color: #475569; font-size: 0.8rem; font-weight: 750; }
.team-forward-card select { width: 100%; border: 1px solid #dbe3ef; border-radius: var(--qaly-radius-lg); padding: 10px; background: #fff; }

.team-gallery-backdrop {
  position: fixed;
  z-index: 1200;
  inset: 0;
  display: grid;
  grid-template-columns: 58px minmax(0, 1fr) 58px;
  align-items: center;
  padding: 24px;
  background: rgba(2, 6, 23, 0.94);
}

.team-gallery-backdrop figure { min-width: 0; height: min(86vh, 900px); margin: 0; display: grid; place-items: center; grid-template-rows: minmax(0, 1fr) auto; gap: 10px; }
.team-gallery-backdrop img { max-width: 100%; max-height: 100%; object-fit: contain; }
.team-gallery-backdrop figcaption { color: #e2e8f0; font-size: 0.82rem; }
.team-gallery-close, .team-gallery-nav { border: 0; color: #fff; background: rgba(30, 41, 59, 0.82); cursor: pointer; }
.team-gallery-close { position: absolute; top: 20px; right: 20px; width: 44px; height: 44px; display: grid; place-items: center; border-radius: 999px; }
.team-gallery-nav { width: 46px; height: 46px; border-radius: 999px; font-size: 2rem; }

.team-selection-toolbar {
  min-height: 58px;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 12px;
  border: 1px solid #dbeafe;
  border-radius: var(--qaly-radius-lg);
  background: #fff;
  box-shadow: var(--qaly-shadow-md);
}

.team-selection-toolbar strong {
  flex: 1;
  color: #0f172a;
  font-size: 0.88rem;
}

.team-selection-toolbar button {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  border: 0;
  border-radius: var(--qaly-radius-lg);
  padding: 9px 11px;
  color: #1d4ed8;
  background: #eff6ff;
  font-weight: 750;
  cursor: pointer;
}

.team-selection-toolbar button.is-danger {
  color: #dc2626;
  background: #fef2f2;
}

.team-selection-toolbar .selection-close {
  padding: 8px;
  color: #475569;
  background: #f1f5f9;
}

.team-typing-indicator {
  align-self: flex-start;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  margin-left: 44px;
  padding: 8px 11px;
  border: 1px solid #e2e8f0;
  border-radius: 999px;
  color: #64748b;
  background: rgba(255, 255, 255, 0.92);
  font-size: 0.78rem;
}

.typing-dots {
  display: inline-flex;
  gap: 3px;
}

.typing-dots i {
  width: 5px;
  height: 5px;
  border-radius: 999px;
  background: #94a3b8;
  animation: typingPulse 1s infinite ease-in-out;
}

.typing-dots i:nth-child(2) {
  animation-delay: 0.15s;
}

.typing-dots i:nth-child(3) {
  animation-delay: 0.3s;
}

@keyframes typingPulse {
  0%, 80%, 100% {
    transform: translateY(0);
    opacity: 0.45;
  }
  40% {
    transform: translateY(-3px);
    opacity: 1;
  }
}

.team-chat-composer-wrapper {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.team-edit-banner {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 10px;
  padding: 9px 12px;
  border-left: 3px solid #2563eb;
  border-radius: var(--qaly-radius-lg);
  color: #1d4ed8;
  background: #eff6ff;
}

.team-edit-banner div {
  min-width: 0;
  display: grid;
}

.team-edit-banner span {
  overflow: hidden;
  color: #64748b;
  font-size: 0.76rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.team-edit-banner button {
  border: 0;
  color: #64748b;
  background: transparent;
  cursor: pointer;
}

.team-attachment-preview {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.attachment-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 10px;
  border: 1px solid #e2e8f0;
  border-radius: 999px;
  color: #334155;
  background: #f8fafc;
  font-size: 0.78rem;
}

.team-chat-composer {
  display: flex;
  align-items: flex-end;
  gap: 6px;
  padding: 7px 8px;
  border: 1px solid #dbe5f1;
  border-radius: var(--qaly-radius-lg);
  background: #fff;
  box-shadow: var(--qaly-shadow-md);
}

.team-chat-composer:focus-within {
  border-color: #8dbaf8;
  box-shadow: 0 0 0 4px rgba(37, 99, 235, 0.1);
}

.composer-tool-btn {
  width: 38px;
  height: 38px;
  flex: 0 0 auto;
  display: grid;
  place-items: center;
  border: 1px solid #e2e8f0;
  border-radius: var(--qaly-radius-lg);
  color: #64748b;
  background: #f8fafc;
  cursor: pointer;
}

.composer-tool-btn input {
  display: none;
}

.team-chat-composer textarea {
  min-width: 0;
  min-height: 42px;
  max-height: 120px;
  flex: 1;
  border: 0 !important;
  padding: 10px 8px;
  color: #1e293b;
  background: transparent !important;
  box-shadow: none !important;
  resize: none;
}

.team-chat-composer textarea:focus {
  outline: 0;
}

.team-mention-popover {
  position: absolute;
  left: 92px;
  right: 58px;
  bottom: calc(100% + 8px);
  z-index: 20;
  display: grid;
  gap: 4px;
  padding: 7px;
  border: 1px solid #dbe3ef;
  border-radius: var(--qaly-radius-lg);
  background: #fff;
  box-shadow: var(--qaly-shadow-md);
}

.team-mention-popover button {
  display: grid;
  grid-template-columns: 30px minmax(0, 1fr) auto;
  align-items: center;
  gap: 9px;
  border: 0;
  border-radius: var(--qaly-radius-lg);
  padding: 8px;
  color: #0f172a;
  background: transparent;
  text-align: left;
  cursor: pointer;
}

.team-mention-popover button:hover {
  background: #eff6ff;
}

.team-mention-popover span {
  width: 30px;
  height: 30px;
  display: grid;
  place-items: center;
  border-radius: var(--qaly-radius-lg);
  color: #1d4ed8;
  background: #dbeafe;
  font-weight: 900;
}

.team-mention-popover strong {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.team-mention-popover small {
  color: #64748b;
  font-size: 0.72rem;
}

.composer-send-btn {
  width: 42px;
  height: 42px;
  flex: 0 0 auto;
  display: grid;
  place-items: center;
  border-radius: var(--qaly-radius-lg);
}

.team-emoji-picker {
  position: absolute;
  z-index: 12;
  left: 22px;
  bottom: 78px;
  display: flex;
  gap: 4px;
  padding: 8px;
  border: 1px solid #e2e8f0;
  border-radius: var(--qaly-radius-lg);
  background: #fff;
  box-shadow: var(--qaly-shadow-md);
}

.team-emoji-picker button {
  border: 0;
  border-radius: var(--qaly-radius-lg);
  padding: 7px;
  background: transparent;
  font-size: 1.15rem;
  cursor: pointer;
}

.team-chat-window--clean {
  background: #fbfdff;
}

.team-chat-window--soft {
  background:
    radial-gradient(circle at 18% 18%, rgba(96, 165, 250, 0.32), transparent 30%),
    radial-gradient(circle at 82% 12%, rgba(186, 230, 253, 0.55), transparent 34%),
    linear-gradient(135deg, #ffffff 0%, #eaf4ff 100%);
}

.team-chat-window--mint {
  background:
    radial-gradient(circle at 78% 22%, rgba(16, 185, 129, 0.24), transparent 30%),
    radial-gradient(circle at 12% 85%, rgba(125, 211, 252, 0.36), transparent 30%),
    linear-gradient(135deg, #f0fdfa 0%, #ffffff 100%);
}

.team-chat-window--paper {
  background:
    linear-gradient(90deg, rgba(100, 116, 139, 0.11) 1px, transparent 1px),
    linear-gradient(180deg, rgba(100, 116, 139, 0.11) 1px, transparent 1px),
    linear-gradient(135deg, #fffef8, #f8fafc);
  background-size: 26px 26px, 26px 26px, auto;
}

.team-chat-window--aurora {
  background:
    radial-gradient(circle at 22% 22%, rgba(129, 140, 248, 0.48), transparent 32%),
    radial-gradient(circle at 72% 62%, rgba(45, 212, 191, 0.42), transparent 34%),
    linear-gradient(135deg, #eff6ff 0%, #faf5ff 52%, #ecfeff 100%);
}

.team-chat-window--sunset {
  background:
    radial-gradient(circle at 65% 20%, rgba(251, 146, 60, 0.34), transparent 32%),
    radial-gradient(circle at 18% 80%, rgba(244, 114, 182, 0.3), transparent 34%),
    linear-gradient(135deg, #fff7ed 0%, #fef2f2 52%, #f8fafc 100%);
}

.team-chat-window--night {
  background:
    radial-gradient(circle at 30% 18%, rgba(59, 130, 246, 0.36), transparent 26%),
    radial-gradient(circle at 78% 78%, rgba(168, 85, 247, 0.32), transparent 32%),
    linear-gradient(135deg, #0f172a 0%, #172554 100%);
}

.team-chat-window--rose {
  background:
    radial-gradient(circle at 50% 52%, rgba(244, 114, 182, 0.28), transparent 26%),
    radial-gradient(circle at 72% 22%, rgba(251, 113, 133, 0.24), transparent 30%),
    linear-gradient(135deg, #fff1f2 0%, #faf5ff 100%);
}

.team-chat-window--ocean {
  background:
    radial-gradient(circle at 18% 22%, rgba(14, 165, 233, 0.34), transparent 30%),
    radial-gradient(circle at 78% 70%, rgba(20, 184, 166, 0.24), transparent 34%),
    linear-gradient(135deg, #ecfeff 0%, #eff6ff 100%);
}

.team-chat-window--night .team-chat-window__header h2,
.team-chat-window--night .team-chat-window__header span:not(.team-chat-window__avatar) {
  color: #ffffff;
}

.team-chat-window--night .team-chat-body {
  background: rgba(15, 23, 42, 0.16);
  border-radius: var(--qaly-radius-lg);
}

.chat-customize-backdrop {
  position: fixed;
  inset: 0;
  z-index: 130;
  display: grid;
  place-items: center;
  padding: 22px;
  background: rgba(0, 0, 0, 0.72);
  backdrop-filter: none;
}

.chat-customize-sheet {
  width: min(820px, 100%);
  max-height: min(860px, calc(100vh - 44px));
  overflow: auto;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 30px;
  padding: 24px;
  color: #f8fafc;
  background: #050505;
  box-shadow: var(--qaly-shadow-md);
}

.chat-customize-header {
  display: grid;
  grid-template-columns: 44px minmax(0, 1fr) 44px;
  align-items: center;
  gap: 12px;
  margin-bottom: 24px;
}

.chat-customize-header h2 {
  margin: 0;
  font-size: 1.8rem;
  font-weight: 900;
  letter-spacing: 0;
}

.chat-customize-header button {
  width: 44px;
  height: 44px;
  display: grid;
  place-items: center;
  border: 0;
  border-radius: 999px;
  color: #f8fafc;
  background: transparent;
  cursor: pointer;
}

.chat-customize-header button:hover {
  background: rgba(255, 255, 255, 0.09);
}

.chat-customize-tabs {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 28px;
  overflow-x: auto;
}

.chat-customize-tabs button {
  border: 0;
  border-radius: 999px;
  padding: 12px 20px;
  color: rgba(248, 250, 252, 0.52);
  background: transparent;
  font-size: 1rem;
  font-weight: 900;
  white-space: nowrap;
}

.chat-customize-tabs button.is-active {
  color: #ffffff;
  background: #252525;
}

.chat-customize-tabs button:disabled {
  cursor: default;
}

.chat-customize-actions {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 18px;
  margin-bottom: 30px;
}

.chat-customize-action-card {
  min-height: 150px;
  display: grid;
  place-items: center;
  align-content: center;
  gap: 16px;
  border: 0;
  border-radius: 26px;
  color: #f8fafc;
  background: #303030;
  font-size: 1rem;
  text-align: center;
  cursor: pointer;
}

.chat-customize-action-card:hover {
  background: #3a3a3a;
}

.chat-customize-action-card span {
  color: rgba(248, 250, 252, 0.82);
}

.chat-customize-action-card strong {
  font-size: 1.05rem;
  font-weight: 800;
}

.chat-customize-action-card input {
  display: none;
}

.chat-customize-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 26px 22px;
}

.chat-theme-card {
  min-width: 0;
  display: grid;
  gap: 12px;
  border: 0;
  padding: 0;
  color: #f8fafc;
  background: transparent;
  text-align: left;
  cursor: pointer;
}

.chat-theme-card__preview {
  position: relative;
  aspect-ratio: 0.72;
  overflow: hidden;
  border-radius: var(--qaly-radius-lg);
  box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.08);
}

.chat-theme-card:hover .chat-theme-card__preview {
  box-shadow: var(--qaly-shadow-md);
}

.chat-theme-card.is-active .chat-theme-card__preview {
  box-shadow:
    inset 0 0 0 3px #60a5fa,
    0 0 0 4px rgba(96, 165, 250, 0.18);
}

.chat-theme-card__preview i {
  position: absolute;
  right: 12px;
  top: 12px;
  width: 30px;
  height: 30px;
  display: grid;
  place-items: center;
  border-radius: 999px;
  color: #ffffff;
  background: #2563eb;
}

.chat-theme-card strong {
  overflow: hidden;
  font-size: 1rem;
  font-weight: 700;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.bg-preview--clean {
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.4), transparent 42%),
    #f8fafc;
}

.bg-preview--soft {
  background:
    radial-gradient(circle at 26% 18%, rgba(96, 165, 250, 0.85), transparent 28%),
    radial-gradient(circle at 78% 70%, rgba(191, 219, 254, 0.9), transparent 34%),
    linear-gradient(145deg, #eff6ff, #ffffff);
}

.bg-preview--mint {
  background:
    radial-gradient(circle at 70% 18%, rgba(52, 211, 153, 0.75), transparent 30%),
    radial-gradient(circle at 18% 78%, rgba(45, 212, 191, 0.56), transparent 34%),
    linear-gradient(145deg, #064e3b, #ecfdf5);
}

.bg-preview--paper {
  background:
    linear-gradient(90deg, rgba(71, 85, 105, 0.16) 1px, transparent 1px),
    linear-gradient(180deg, rgba(71, 85, 105, 0.16) 1px, transparent 1px),
    linear-gradient(145deg, #fff7ed, #f8fafc);
  background-size: 18px 18px, 18px 18px, auto;
}

.bg-preview--aurora {
  background:
    radial-gradient(circle at 20% 24%, #7c3aed, transparent 30%),
    radial-gradient(circle at 70% 62%, #14b8a6, transparent 36%),
    linear-gradient(145deg, #1e1b4b, #082f49);
}

.bg-preview--sunset {
  background:
    radial-gradient(circle at 66% 18%, #fb923c, transparent 28%),
    radial-gradient(circle at 24% 70%, #ec4899, transparent 34%),
    linear-gradient(145deg, #581c87, #f97316);
}

.bg-preview--night {
  background:
    radial-gradient(circle at 34% 18%, #2563eb, transparent 28%),
    radial-gradient(circle at 72% 72%, #a855f7, transparent 34%),
    linear-gradient(145deg, #020617, #172554);
}

.bg-preview--rose {
  background:
    radial-gradient(circle at 50% 48%, #ec4899, transparent 24%),
    radial-gradient(circle at 70% 22%, #fb7185, transparent 30%),
    linear-gradient(145deg, #2e1065, #831843);
}

.bg-preview--ocean {
  background:
    radial-gradient(circle at 22% 24%, #22d3ee, transparent 30%),
    radial-gradient(circle at 74% 70%, #0f766e, transparent 36%),
    linear-gradient(145deg, #082f49, #0e7490);
}

@media (max-width: 720px) {
  .chat-customize-sheet {
    min-height: calc(100vh - 28px);
    border-radius: 26px;
    padding: 20px;
  }

  .chat-customize-actions {
    grid-template-columns: 1fr;
  }

  .chat-customize-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 22px 16px;
  }
}

.message-detail-backdrop {
  position: fixed;
  inset: 0;
  z-index: 100;
  display: grid;
  place-items: center;
  padding: 20px;
  background: rgba(15, 23, 42, 0.34);
}

.message-detail-card {
  width: min(430px, 100%);
  display: grid;
  gap: 16px;
  padding: 20px;
  border-radius: var(--qaly-radius-lg);
  background: #fff;
  box-shadow: var(--qaly-shadow-md);
}

.message-detail-card header,
.message-detail-card header div {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.message-detail-card header button {
  border: 0;
  color: #64748b;
  background: transparent;
  cursor: pointer;
}

.message-detail-card dl {
  display: grid;
  gap: 10px;
  margin: 0;
}

.message-detail-card dl div {
  display: grid;
  grid-template-columns: 100px minmax(0, 1fr);
  gap: 12px;
}

.message-detail-card dt {
  color: #64748b;
}

.message-detail-card dd {
  margin: 0;
  color: #0f172a;
  font-weight: 650;
}

.message-detail-card p {
  margin: 0;
  padding: 12px;
  border-radius: var(--qaly-radius-lg);
  background: #f8fafc;
  white-space: pre-wrap;
}
</style>
