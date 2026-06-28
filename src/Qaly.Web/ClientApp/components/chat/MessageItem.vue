<script setup lang="ts">
import { computed, ref } from "vue";
import {
  CalendarDays,
  Check,
  Clipboard,
  Download,
  FileArchive,
  FileSpreadsheet,
  FileText,
  FileVideo,
  Info,
  ListChecks,
  MoreHorizontal,
  Pencil,
  Pin,
  Reply,
  Forward,
  RotateCcw,
  Trash2,
} from "lucide-vue-next";
import type { TeamChatMessage } from "./chat-types";
import PollCard from "./PollCard.vue";

const props = defineProps<{
  message: TeamChatMessage;
  currentUserId: string;
  isConsecutive?: boolean;
  menuOpen?: boolean;
  selectionMode?: boolean;
  selected?: boolean;
}>();

defineEmits<{
  menu: [messageId: string];
  action: [
    action: "copy" | "pin" | "select" | "detail" | "edit" | "recall" | "hide" | "reply" | "forward",
    message: TeamChatMessage,
  ];
  toggleSelect: [messageId: string];
  react: [messageId: string, emoji: string];
  joinMeeting: [meetingId: string];
  openImage: [messageId: string, attachmentId: string | undefined];
}>();

const renderedText = computed(() => renderLightMarkdown(props.message.text));
const isMine = computed(() => props.message.senderId === props.currentUserId);
const hasImageAttachments = computed(() => props.message.attachments.some((file) => file.kind === "image"));
const hasOnlyImageAttachments = computed(
  () => props.message.attachments.length > 0 && props.message.attachments.every((file) => file.kind === "image"),
);
const openAttachmentMenu = ref<string | null>(null);
const canEdit = computed(
  () =>
    isMine.value &&
    !props.message.isDeleted &&
    props.message.messageType === "Text" &&
    props.message.attachments.length === 0,
);

function escapeHtml(value: string) {
  return value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}

function renderLightMarkdown(value: string) {
  return escapeHtml(value)
    .replace(/\[([^\]]+)\]\((https?:\/\/[^)\s]+)\)/g, '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>')
    .replace(/(^|\s)@all\b/g, '$1<span class="chat-mention chat-mention--all">@all</span>')
    .replace(/(^|\s)@([\p{L}\p{N}_ .-]{2,40})/gu, '$1<span class="chat-mention">@$2</span>')
    .replace(/`([^`]+)`/g, "<code>$1</code>")
    .replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>")
    .replace(/_([^_]+)_/g, "<em>$1</em>")
    .replace(/\n/g, "<br>");
}

const reactionOptions = ["👍", "❤️", "😂", "😮", "😢", "🔥", "✅"];

function attachmentKey(name: string, id?: string) {
  return id ?? name;
}

function fileExtension(name: string) {
  return name.split(".").pop()?.toUpperCase() || "FILE";
}

function fileIcon(name: string, contentType?: string) {
  const extension = name.split(".").pop()?.toLowerCase();
  if (contentType?.startsWith("video/")) return FileVideo;
  if (["xls", "xlsx", "csv"].includes(extension ?? "")) return FileSpreadsheet;
  if (["zip", "rar", "7z", "tar", "gz"].includes(extension ?? "")) return FileArchive;
  return FileText;
}
</script>

<template>
  <article v-if="message.meeting" class="team-message team-message--system">
    <div class="team-message__system-card">
      <span class="team-message__system-icon"><CalendarDays :size="18" /></span>
      <div>
        <small>Cuộc họp nhóm</small>
        <strong>{{ message.meeting.text }}</strong>
        <span>{{ message.createdAt }}</span>
      </div>
      <button
        v-if="message.meeting.active"
        type="button"
        @click="$emit('joinMeeting', message.meeting.id)"
      >
        Tham gia
      </button>
    </div>
  </article>

  <article
    v-else
    :id="`group-message-${message.id}`"
    class="team-message"
    :class="{
      'is-mine': isMine,
      'is-consecutive': isConsecutive,
      'is-selected': selected,
      'is-selecting': selectionMode,
      'has-image-attachment': hasImageAttachments,
      'has-only-image-attachments': hasOnlyImageAttachments,
    }"
    @contextmenu.prevent="$emit('menu', message.id)"
  >
    <button
      v-if="selectionMode"
      class="team-message__selector"
      :class="{ 'is-checked': selected }"
      type="button"
      :aria-label="selected ? 'Bỏ chọn tin nhắn' : 'Chọn tin nhắn'"
      @click="$emit('toggleSelect', message.id)"
    >
      <Check v-if="selected" :size="14" />
    </button>

    <div class="team-message__avatar-container">
      <div v-if="!isConsecutive" class="team-message__avatar">{{ message.senderInitials }}</div>
    </div>

    <div class="team-message__bubble">
      <div v-if="!isConsecutive" class="team-message__meta">
        <strong>{{ message.senderName }}</strong>
        <span>{{ message.createdAt }}</span>
      </div>

      <p v-if="message.isDeleted" class="team-message__recalled">
        <RotateCcw :size="14" />
        Tin nhắn đã được thu hồi
      </p>
      <button
        v-if="!message.isDeleted && message.replyTo"
        class="team-message-reference"
        type="button"
        @click="$emit('action', 'detail', message)"
      >
        <span>Đang trả lời {{ message.replyTo.senderName }}</span>
        <strong>{{ message.replyTo.text || (message.replyTo.attachments.length ? 'Ảnh hoặc tệp đính kèm' : 'Tin nhắn') }}</strong>
      </button>
      <div v-if="!message.isDeleted && message.forwardedFrom" class="team-message-reference team-message-reference--forwarded">
        <span><Forward :size="12" /> Đã chuyển tiếp từ {{ message.forwardedFrom.senderName }}</span>
        <strong>{{ message.forwardedFrom.text || (message.forwardedFrom.attachments.length ? 'Ảnh hoặc tệp đính kèm' : 'Tin nhắn') }}</strong>
        <button
          v-if="message.forwardedFrom.attachments[0]?.kind === 'image'"
          type="button"
          @click="$emit('openImage', message.id, message.forwardedFrom.attachments[0]?.id)"
        >
          <img :src="message.forwardedFrom.attachments[0]?.url" alt="Ảnh được chuyển tiếp" />
        </button>
      </div>
      <p v-if="!message.isDeleted && message.text" class="team-message__text" v-html="renderedText"></p>

      <div v-if="!message.isDeleted && message.attachments.length" class="team-message__attachments">
        <div
          v-for="file in message.attachments"
          :key="attachmentKey(file.name, file.id)"
          class="message-attachment"
          :class="{ 'message-attachment--image': file.kind === 'image' }"
        >
          <button
            v-if="file.kind === 'image' && file.url"
            class="message-attachment-image"
            type="button"
            @click="$emit('openImage', message.id, file.id)"
          >
            <img :src="file.url" :alt="file.name" loading="lazy" />
          </button>
          <a
            v-else
            class="message-attachment-card"
            :href="file.downloadUrl || file.url"
            :download="file.name"
          >
            <div class="message-attachment-icon">
              <component :is="fileIcon(file.name, file.contentType)" :size="22" />
              <small>{{ fileExtension(file.name) }}</small>
            </div>
            <div class="message-attachment-info">
              <strong>{{ file.name }}</strong>
              <span>{{ file.kind === "video" ? "Video" : "Tài liệu" }} · {{ file.sizeLabel }}</span>
            </div>
          </a>

          <button
            class="message-attachment-more"
            type="button"
            aria-label="Tùy chọn file"
            @click.stop="
              openAttachmentMenu =
                openAttachmentMenu === attachmentKey(file.name, file.id)
                  ? null
                  : attachmentKey(file.name, file.id)
            "
          >
            <MoreHorizontal :size="17" />
          </button>
          <div
            v-if="openAttachmentMenu === attachmentKey(file.name, file.id)"
            class="message-attachment-menu"
            @click.stop
          >
            <a v-if="file.downloadUrl || file.url" :href="file.downloadUrl || file.url" :download="file.name">
              <Download :size="16" /> Tải xuống
            </a>
            <button
              class="is-danger"
              type="button"
              @click="$emit('action', 'hide', message); openAttachmentMenu = null"
            >
              <Trash2 :size="16" /> Xóa ở phía tôi
            </button>
          </div>
        </div>
      </div>

      <div v-if="!message.isDeleted && message.poll" class="team-message__poll">
        <PollCard
          :group-id="message.groupId"
          :poll="message.poll"
          :current-user-id="currentUserId"
          :creator-id="message.senderId"
        />
      </div>

      <div v-if="message.editedAt || message.pinned" class="team-message__flags">
        <span v-if="message.editedAt">Đã chỉnh sửa</span>
        <span v-if="message.pinned"><Pin :size="11" /> Đã ghim</span>
      </div>

      <div v-if="message.reactions.length" class="team-message__reactions">
        <button
          v-for="reaction in message.reactions"
          :key="reaction.emoji"
          type="button"
          :class="{ 'is-active': reaction.reactedByCurrentUser }"
          @click="$emit('react', message.id, reaction.emoji)"
        >
          <span>{{ reaction.emoji }}</span>
          <strong>{{ reaction.count }}</strong>
        </button>
      </div>

      <button
        v-if="!selectionMode"
        class="team-message__more"
        type="button"
        aria-label="Tùy chọn tin nhắn"
        @click.stop="$emit('menu', message.id)"
      >
        <MoreHorizontal :size="17" />
      </button>

      <div v-if="menuOpen" class="message-action-menu" @click.stop>
        <div v-if="!message.isDeleted" class="message-reaction-picker">
          <button
            v-for="emoji in reactionOptions"
            :key="emoji"
            type="button"
            @click="$emit('react', message.id, emoji)"
          >
            {{ emoji }}
          </button>
        </div>
        <div v-if="!message.isDeleted" class="message-action-menu__divider"></div>
        <button v-if="!message.isDeleted && message.text" type="button" @click="$emit('action', 'copy', message)">
          <Clipboard :size="17" /> Sao chép tin nhắn
        </button>
        <button v-if="!message.isDeleted" type="button" @click="$emit('action', 'reply', message)">
          <Reply :size="17" /> Trả lời
        </button>
        <button v-if="!message.isDeleted" type="button" @click="$emit('action', 'forward', message)">
          <Forward :size="17" /> Chuyển tiếp
        </button>
        <button v-if="!message.isDeleted" type="button" @click="$emit('action', 'pin', message)">
          <Pin :size="17" /> {{ message.pinned ? "Bỏ ghim tin nhắn" : "Ghim tin nhắn" }}
        </button>
        <button type="button" @click="$emit('action', 'select', message)">
          <ListChecks :size="17" /> Chọn nhiều tin nhắn
        </button>
        <button type="button" @click="$emit('action', 'detail', message)">
          <Info :size="17" /> Xem chi tiết
        </button>
        <button v-if="canEdit" type="button" @click="$emit('action', 'edit', message)">
          <Pencil :size="17" /> Chỉnh sửa tin nhắn
        </button>
        <div class="message-action-menu__divider"></div>
        <button v-if="canEdit" class="is-danger" type="button" @click="$emit('action', 'recall', message)">
          <RotateCcw :size="17" /> Thu hồi
        </button>
        <button class="is-danger" type="button" @click="$emit('action', 'hide', message)">
          <Trash2 :size="17" /> Xóa chỉ ở phía tôi
        </button>
      </div>
    </div>
  </article>
</template>

<style scoped>
.team-message {
  position: relative;
  display: flex;
  gap: 10px;
  align-items: flex-end;
  width: max-content;
  max-width: min(76%, 720px);
  margin-bottom: 4px;
}

.team-message.is-mine {
  align-self: flex-end;
  flex-direction: row-reverse;
}

.team-message.is-consecutive {
  margin-top: -2px;
}

.team-message.is-selected .team-message__bubble {
  box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.2);
}

.team-message__selector {
  width: 24px;
  height: 24px;
  flex: 0 0 auto;
  align-self: center;
  display: grid;
  place-items: center;
  border: 1px solid #94a3b8;
  border-radius: 999px;
  color: #fff;
  background: #fff;
  cursor: pointer;
}

.team-message__selector.is-checked {
  border-color: #2563eb;
  background: #2563eb;
}

.team-message__avatar-container {
  width: 34px;
  height: 34px;
  flex: 0 0 auto;
}

.team-message.is-mine .team-message__avatar-container {
  display: none;
}

.team-message__avatar {
  width: 100%;
  height: 100%;
  display: grid;
  place-items: center;
  border-radius: var(--qaly-radius-lg);
  color: #111827;
  background: #e2e8f0;
  font-size: 0.75rem;
  font-weight: 800;
}

.team-message__bubble {
  position: relative;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 5px;
  padding: 10px 13px;
  border: 1px solid #e5eaf1;
  border-radius: 6px 6px 6px 2px;
  color: #0f172a;
  background: #fff;
  box-shadow: var(--qaly-shadow-md);
  font-size: 0.92rem;
  line-height: 1.45;
}

.team-message.is-consecutive .team-message__bubble {
  border-radius: 6px 15px 15px 6px;
}

.team-message.is-mine .team-message__bubble {
  border-color: #dbe3ef !important;
  border-radius: 6px 6px 2px 6px;
  color: #0f172a !important;
  background: #ffffff !important;
  box-shadow: var(--qaly-shadow-md) !important;
}

.team-message.is-mine.is-consecutive .team-message__bubble {
  border-radius: var(--qaly-radius-lg);
}

.team-message.has-image-attachment .team-message__bubble {
  gap: 7px;
  border-color: transparent !important;
  padding: 0 !important;
  color: #0f172a !important;
  background: transparent !important;
  box-shadow: none !important;
}

.team-message.has-image-attachment .team-message__text {
  width: fit-content;
  max-width: min(340px, 62vw);
  margin-bottom: 1px;
  border: 1px solid rgba(226, 232, 240, 0.9);
  border-radius: var(--qaly-radius-lg);
  padding: 8px 11px;
  color: #0f172a;
  background: rgba(255, 255, 255, 0.94);
  box-shadow: var(--qaly-shadow-md);
}

.team-message.is-mine.has-image-attachment .team-message__text {
  align-self: flex-end;
}

.team-message__meta {
  display: flex;
  align-items: baseline;
  gap: 8px;
  margin-bottom: 1px;
}

.team-message.is-mine .team-message__meta {
  justify-content: flex-end;
}

.team-message__meta strong {
  color: #0f172a;
  font-size: 0.8rem;
  font-weight: 750;
}

.team-message.is-mine .team-message__meta strong {
  display: none;
}

.team-message__meta span {
  color: #64748b;
  font-size: 0.7rem;
}

.team-message__text {
  margin: 0;
  word-break: break-word;
}

.team-message__text :deep(a) {
  color: #1d4ed8;
  font-weight: 750;
  text-decoration: underline;
}

.team-message__text :deep(code) {
  border-radius: 5px;
  padding: 2px 5px;
  background: rgba(15, 23, 42, 0.08);
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
}

.team-message__text :deep(.chat-mention) {
  color: #0f63d8;
  background: transparent;
  font-weight: 900;
}

.team-message__text :deep(.chat-mention--all) {
  color: #0f63d8;
  background: transparent;
}

.team-message__recalled {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  margin: 0;
  color: #64748b;
  font-style: italic;
}

.team-message__flags {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  color: #64748b;
  font-size: 0.66rem;
}

.team-message__flags span {
  display: inline-flex;
  align-items: center;
  gap: 3px;
}

.team-message-reference {
  width: 100%;
  min-width: 0;
  display: grid;
  gap: 3px;
  border: 0;
  border-left: 3px solid #3b82f6;
  border-radius: var(--qaly-radius-lg);
  padding: 7px 9px;
  color: #334155;
  background: #f1f5f9;
  text-align: left;
}

.team-message-reference > span {
  display: flex;
  align-items: center;
  gap: 5px;
  color: #2563eb;
  font-size: 0.68rem;
  font-weight: 800;
}

.team-message-reference > strong {
  overflow: hidden;
  font-size: 0.76rem;
  font-weight: 650;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.team-message-reference--forwarded {
  border-left-color: #14b8a6;
}

.team-message-reference--forwarded > span {
  color: #0f766e;
}

.team-message-reference--forwarded > button {
  border: 0;
  border-radius: var(--qaly-radius-lg);
  padding: 0;
  overflow: hidden;
  background: transparent;
  cursor: pointer;
}

.team-message-reference--forwarded img {
  display: block;
  width: 100%;
  max-height: 180px;
  object-fit: cover;
}

.team-message__reactions {
  display: flex;
  flex-wrap: wrap;
  gap: 5px;
  margin-top: 2px;
}

.team-message__reactions button {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  border: 1px solid #dbeafe;
  border-radius: 999px;
  padding: 3px 7px;
  color: #334155;
  background: #f8fbff;
  font-size: 0.72rem;
  font-weight: 800;
  cursor: pointer;
}

.team-message__reactions button.is-active {
  border-color: #60a5fa;
  color: #1d4ed8;
  background: #dbeafe;
}

.team-message__more {
  position: absolute;
  top: 50%;
  right: calc(100% + 8px);
  width: 30px;
  height: 30px;
  display: grid;
  place-items: center;
  border-radius: 999px;
  color: #000;
  background: #fff;
  border: 1px solid #dbe3ef;
  box-shadow: var(--qaly-shadow-md);
  opacity: 0;
  visibility: hidden;
  transform: translateY(-50%);
  cursor: pointer;
  transition:
    opacity 140ms ease,
    visibility 140ms ease,
    color 160ms ease,
    border-color 160ms ease,
    background 160ms ease,
    transform 160ms ease;
}

.team-message__more :deep(svg) {
  color: #000 !important;
  stroke: #000 !important;
}

.team-message:not(.is-mine) .team-message__more {
  right: auto;
  left: calc(100% + 8px);
}

.team-message:hover .team-message__more,
.team-message:focus-within .team-message__more {
  opacity: 1;
  visibility: visible;
}

.team-message__more:hover,
.team-message__more:focus-visible {
  color: #000;
  border-color: #93c5fd;
  background: #eff6ff;
  transform: translateY(-50%) scale(1.04);
}

.message-action-menu {
  position: absolute;
  z-index: 20;
  top: calc(100% + 8px);
  right: 0;
  width: 236px;
  display: grid;
  padding: 8px;
  border: 1px solid #dbe3ef;
  border-radius: var(--qaly-radius-lg);
  color: #1e293b;
  background: #fff;
  box-shadow: var(--qaly-shadow-md);
}

.team-message:not(.is-mine) .message-action-menu {
  right: auto;
  left: 0;
}

.message-action-menu button {
  display: flex;
  align-items: center;
  gap: 11px;
  width: 100%;
  border: 0;
  border-radius: var(--qaly-radius-lg);
  padding: 10px;
  color: inherit;
  background: transparent;
  font-size: 0.86rem;
  font-weight: 650;
  text-align: left;
  cursor: pointer;
}

.message-action-menu button:hover {
  background: #f1f5f9;
}

.message-reaction-picker {
  display: flex;
  gap: 3px;
  padding: 3px 2px 6px;
}

.message-reaction-picker button {
  width: 28px;
  height: 28px;
  justify-content: center;
  border-radius: 999px;
  padding: 0;
  font-size: 1rem;
}

.message-action-menu button.is-danger {
  color: #dc2626;
}

.message-action-menu__divider {
  height: 1px;
  margin: 5px 4px;
  background: #e2e8f0;
}

.team-message__attachments {
  display: grid;
  gap: 8px;
}

.message-attachment {
  position: relative;
  min-width: min(280px, 64vw);
}

.message-attachment--image {
  min-width: 0;
}

.message-attachment-card {
  display: flex;
  align-items: center;
  gap: 9px;
  padding: 10px 42px 10px 10px;
  border: 1px solid rgba(148, 163, 184, 0.22);
  border-radius: var(--qaly-radius-lg);
  color: inherit;
  background: rgba(255, 255, 255, 0.78);
  text-decoration: none;
}

.message-attachment-image {
  position: relative;
  display: block;
  overflow: hidden;
  border: 1px solid rgba(226, 232, 240, 0.8);
  border-radius: var(--qaly-radius-lg);
  background: #e2e8f0;
  box-shadow: var(--qaly-shadow-md);
}

.message-attachment-image img {
  display: block;
  width: min(340px, 62vw);
  max-height: 320px;
  object-fit: cover;
}

.team-message.has-image-attachment .team-message__attachments {
  gap: 6px;
}

.message-attachment-icon {
  width: 40px;
  height: 40px;
  display: grid;
  place-items: center;
  border-radius: var(--qaly-radius-lg);
  color: #1d4ed8;
  background: #dbeafe;
}

.message-attachment-icon small {
  margin-top: -7px;
  font-size: 0.46rem;
  font-weight: 900;
}

.message-attachment-info {
  min-width: 0;
  display: grid;
}

.message-attachment-info strong {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 0.82rem;
}

.message-attachment-info span {
  color: #64748b;
  font-size: 0.7rem;
}

.message-attachment-more {
  position: absolute;
  z-index: 2;
  top: 8px;
  right: 8px;
  width: 28px;
  height: 28px;
  display: grid;
  place-items: center;
  border: 1px solid #dbe3ef;
  border-radius: 999px;
  color: #111827;
  background: rgba(255, 255, 255, 0.94);
  box-shadow: var(--qaly-shadow-md);
  opacity: 0;
  visibility: hidden;
  cursor: pointer;
  transition: opacity 140ms ease, visibility 140ms ease;
}

.message-attachment:hover .message-attachment-more,
.message-attachment:focus-within .message-attachment-more {
  opacity: 1;
  visibility: visible;
}

.message-attachment-menu {
  position: absolute;
  z-index: 24;
  top: 40px;
  right: 8px;
  width: 170px;
  display: grid;
  gap: 2px;
  padding: 6px;
  border: 1px solid #dbe3ef;
  border-radius: var(--qaly-radius-lg);
  color: #334155;
  background: #ffffff;
  box-shadow: var(--qaly-shadow-md);
}

.message-attachment-menu a,
.message-attachment-menu button {
  display: flex;
  align-items: center;
  gap: 8px;
  border: 0;
  border-radius: var(--qaly-radius-lg);
  padding: 9px;
  color: inherit;
  background: transparent;
  font-size: 0.78rem;
  font-weight: 700;
  text-decoration: none;
  cursor: pointer;
}

.message-attachment-menu a:hover,
.message-attachment-menu button:hover {
  background: #f1f5f9;
}

.message-attachment-menu .is-danger {
  color: #dc2626;
}

.team-message--system {
  width: min(520px, 86%);
  max-width: none;
  align-self: center;
}

.team-message__system-card {
  width: 100%;
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 12px;
  border: 1px solid #dbeafe;
  border-radius: var(--qaly-radius-lg);
  padding: 12px 14px;
  background: #f8fbff;
}

.team-message__system-icon {
  width: 36px;
  height: 36px;
  display: grid;
  place-items: center;
  border-radius: var(--qaly-radius-lg);
  color: #1677ff;
  background: #eff6ff;
}

.team-message__system-card div {
  min-width: 0;
  display: grid;
  gap: 2px;
}

.team-message__system-card small,
.team-message__system-card div > span {
  color: #64748b;
  font-size: 0.72rem;
}

.team-message__system-card button {
  border: 0;
  border-radius: 999px;
  padding: 8px 12px;
  color: #fff;
  background: #1677ff;
  font-weight: 800;
  cursor: pointer;
}

@media (max-width: 720px) {
  .team-message {
    max-width: 88%;
  }

  .team-message__more {
    opacity: 1;
    visibility: visible;
  }
}
</style>
