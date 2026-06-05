<script setup lang="ts">
import { computed, nextTick, ref, watch } from "vue";
import {
  Image as ImageIcon,
  Info,
  Palette,
  Paperclip,
  Pencil,
  Pin,
  Send,
  SmilePlus,
  Trash2,
  X,
} from "lucide-vue-next";
import { showError, showSuccess } from "../../composables/use-toast";
import MessageItem from "./MessageItem.vue";
import type {
  ChatGroupModel,
  TeamChatAttachment,
  TeamChatMessage,
  TeamChatPoll,
} from "./chat-types";

const props = defineProps<{
  group: ChatGroupModel | null;
  messages: TeamChatMessage[];
  currentUserId: string;
  backgroundTheme?: string;
}>();

const emit = defineEmits<{
  send: [
    payload: {
      text: string;
      attachments: TeamChatAttachment[];
      poll?: TeamChatPoll;
    },
  ];
  edit: [messageId: string, text: string];
  pin: [messageId: string, isPinned: boolean];
  recall: [messageId: string];
  hide: [messageIds: string[]];
  changeBackground: [];
  joinMeeting: [meetingId: string];
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

const pinnedMessages = computed(() =>
  props.messages.filter((message) => message.pinned && !message.isDeleted),
);
const selectedMessages = computed(() =>
  props.messages.filter((message) => selectedIds.value.has(message.id)),
);
const emojiOptions = ["👍", "✅", "🔥", "🎯", "🙏", "💡"];
const groupInitials = computed(() => initials(props.group?.name ?? "Qaly"));

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
  action: "copy" | "pin" | "select" | "detail" | "edit" | "recall" | "hide",
  message: TeamChatMessage,
) {
  openMenuId.value = null;

  if (action === "copy") {
    await copyText(message.text);
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
    return;
  }

  emit("send", {
    text,
    attachments: pendingAttachments.value,
  });
  draft.value = "";
  pendingAttachments.value = [];
}
</script>

<template>
  <section
    class="team-chat-window glass-card"
    :class="`team-chat-window--${backgroundTheme ?? 'clean'}`"
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
        <button
          class="icon-button icon-button--small"
          type="button"
          aria-label="Đổi nền chat"
          @click.stop="$emit('changeBackground')"
        >
          <Palette :size="16" />
        </button>
        <div v-if="pinnedMessages.length" class="team-pinned">
          <Pin :size="14" />
          <span>{{ pinnedMessages.length }} tin đã ghim</span>
        </div>
      </div>
    </header>

    <div v-if="pinnedMessages.length" class="team-pinned-list">
      <Pin :size="15" />
      <span>{{ pinnedMessages[0]?.text || "Tin nhắn đã ghim" }}</span>
      <small v-if="pinnedMessages.length > 1">+{{ pinnedMessages.length - 1 }}</small>
    </div>

    <div ref="bodyRef" class="team-chat-body no-scrollbar">
      <div v-if="!messages.length" class="team-chat-empty">
        <strong>{{ group ? "Chưa có tin nhắn" : "Chọn nhóm chat" }}</strong>
        <span>
          {{ group ? "Gửi tin nhắn đầu tiên để bắt đầu cuộc trò chuyện." : "Danh sách tin nhắn sẽ xuất hiện ở đây." }}
        </span>
      </div>

      <MessageItem
        v-for="(message, index) in messages"
        :key="message.id"
        :message="message"
        :current-user-id="currentUserId"
        :is-consecutive="
          index > 0 &&
          messages[index - 1].senderId === message.senderId &&
          !messages[index - 1].meeting
        "
        :menu-open="openMenuId === message.id"
        :selection-mode="selectionMode"
        :selected="selectedIds.has(message.id)"
        @menu="toggleMenu"
        @action="handleMessageAction"
        @toggle-select="toggleSelected"
        @join-meeting="$emit('joinMeeting', $event)"
      />
    </div>

    <div v-if="selectionMode" class="team-selection-toolbar">
      <button type="button" class="selection-close" @click="exitSelectionMode"><X :size="18" /></button>
      <strong>{{ selectedIds.size }} tin nhắn đã chọn</strong>
      <button type="button" :disabled="!selectedIds.size" @click="copySelected">Sao chép</button>
      <button type="button" class="is-danger" :disabled="!selectedIds.size" @click="hideSelected">
        <Trash2 :size="16" /> Xóa phía tôi
      </button>
    </div>

    <div v-else class="team-chat-composer-wrapper">
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
          @keydown.enter.exact.prevent="sendMessage"
        ></textarea>
        <button class="primary-button composer-send-btn" type="submit" :aria-label="editingMessage ? 'Lưu chỉnh sửa' : 'Gửi tin nhắn'">
          <Send :size="16" />
        </button>
      </form>
    </div>

    <div v-if="showEmoji" class="team-emoji-picker glass-card" @click.stop>
      <button v-for="emoji in emojiOptions" :key="emoji" type="button" @click="addEmoji(emoji)">
        {{ emoji }}
      </button>
    </div>

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
  border-radius: 10px;
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

.team-selection-toolbar {
  min-height: 58px;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 12px;
  border: 1px solid #dbeafe;
  border-radius: 14px;
  background: #fff;
  box-shadow: 0 12px 28px rgba(15, 23, 42, 0.08);
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
  border-radius: 9px;
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

.team-chat-composer-wrapper {
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
  border-radius: 8px;
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
  border-radius: 16px;
  background: #fff;
  box-shadow: 0 10px 26px rgba(15, 23, 42, 0.06);
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
  border-radius: 12px;
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

.composer-send-btn {
  width: 42px;
  height: 42px;
  flex: 0 0 auto;
  display: grid;
  place-items: center;
  border-radius: 13px;
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
  border-radius: 12px;
  background: #fff;
  box-shadow: 0 12px 30px rgba(15, 23, 42, 0.16);
}

.team-emoji-picker button {
  border: 0;
  border-radius: 8px;
  padding: 7px;
  background: transparent;
  font-size: 1.15rem;
  cursor: pointer;
}

.team-chat-window--soft {
  background: linear-gradient(135deg, #fff, #eef6ff);
}

.team-chat-window--mint {
  background: linear-gradient(135deg, #ecfdf5, #fff);
}

.team-chat-window--paper {
  background:
    linear-gradient(90deg, rgba(148, 163, 184, 0.08) 1px, transparent 1px),
    linear-gradient(180deg, rgba(148, 163, 184, 0.08) 1px, transparent 1px),
    #fffdf8;
  background-size: 24px 24px;
}

.team-chat-window--dark {
  background: linear-gradient(135deg, #101827, #172033);
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
  border-radius: 15px;
  background: #fff;
  box-shadow: 0 28px 70px rgba(15, 23, 42, 0.24);
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
  border-radius: 10px;
  background: #f8fafc;
  white-space: pre-wrap;
}
</style>
