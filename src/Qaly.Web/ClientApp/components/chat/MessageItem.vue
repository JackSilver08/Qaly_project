<script setup lang="ts">
import { computed } from "vue";
import { CalendarDays, Download, ExternalLink, Pin } from "lucide-vue-next";
import type { TeamChatMessage } from "./chat-types";
import PollCard from "./PollCard.vue";

const props = defineProps<{
  message: TeamChatMessage;
  currentUserId: string;
  isConsecutive?: boolean;
}>();

defineEmits<{
  pin: [messageId: string];
  joinMeeting: [meetingId: string];
}>();

const renderedText = computed(() => renderLightMarkdown(props.message.text));

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
    .replace(/`([^`]+)`/g, "<code>$1</code>")
    .replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>")
    .replace(/_([^_]+)_/g, "<em>$1</em>")
    .replace(/\n/g, "<br>");
}
</script>

<template>
  <article
    v-if="message.meeting"
    class="team-message team-message--system"
  >
    <div class="team-message__system-card">
      <span class="team-message__system-icon">
        <CalendarDays :size="18" />
      </span>
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
    class="team-message"
    :class="{ 
      'is-mine': message.senderId === currentUserId,
      'is-consecutive': isConsecutive
    }"
  >
    <div class="team-message__avatar-container">
      <div v-if="!isConsecutive" class="team-message__avatar">{{ message.senderInitials }}</div>
    </div>
    <div class="team-message__bubble">
      <div v-if="!isConsecutive" class="team-message__meta">
        <strong>{{ message.senderName }}</strong>
        <span>{{ message.createdAt }}</span>
        <button
          type="button"
          :aria-label="message.pinned ? 'Bỏ ghim' : 'Ghim tin nhắn'"
          @click="$emit('pin', message.id)"
        >
          <Pin :size="13" />
        </button>
      </div>
      <p v-if="message.text" class="team-message__text" v-html="renderedText"></p>

      <div v-if="message.attachments.length" class="team-message__attachments">
        <a
          v-for="file in message.attachments"
          :key="`${file.name}-${file.url ?? ''}`"
          class="message-attachment-card"
          :class="{ 'is-clickable': Boolean(file.url) }"
          :href="file.url"
          target="_blank"
          rel="noopener noreferrer"
          :download="file.kind === 'file' ? file.name : undefined"
          :aria-label="file.url ? `Mở ${file.name}` : file.name"
        >
          <div class="message-attachment-icon">
            <svg v-if="file.kind === 'image'" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="18" height="18" x="3" y="3" rx="2" ry="2"/><circle cx="9" cy="9" r="2"/><path d="m21 15-3.086-3.086a2 2 0 0 0-2.828 0L6 21"/></svg>
            <svg v-else width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5L14.5 2z"/><polyline points="14 2 14 8 20 8"/></svg>
          </div>
          <div class="message-attachment-info">
            <strong>{{ file.name }}</strong>
            <span>{{ file.url ? `Bấm để ${file.kind === 'image' ? 'xem' : 'tải'}` : file.sizeLabel }}</span>
          </div>
          <ExternalLink v-if="file.kind === 'image' && file.url" class="message-attachment-action" :size="15" />
          <Download v-else-if="file.url" class="message-attachment-action" :size="15" />
        </a>
      </div>

      <div v-if="message.poll" class="team-message__poll">
        <PollCard 
          :group-id="message.groupId" 
          :poll="message.poll"
          :current-user-id="currentUserId"
          :creator-id="message.senderId"
        />
      </div>
    </div>
  </article>
</template>

<style scoped>
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
  border-radius: 14px;
  padding: 12px 14px;
  background: #f8fbff;
  color: #0f172a;
}

.team-message__system-icon {
  width: 36px;
  height: 36px;
  display: grid;
  place-items: center;
  border-radius: 12px;
  color: #1677ff;
  background: #eff6ff;
}

.team-message__system-card div {
  min-width: 0;
  display: grid;
  gap: 2px;
}

.team-message__system-card small {
  color: #1d4ed8;
  font-size: 0.72rem;
  font-weight: 800;
  text-transform: uppercase;
}

.team-message__system-card strong {
  color: #0f172a;
  font-size: 0.9rem;
  line-height: 1.35;
}

.team-message__system-card div > span {
  color: #64748b;
  font-size: 0.78rem;
}

.team-message__system-card button {
  width: max-content;
  border: 0;
  border-radius: 999px;
  padding: 8px 12px;
  background: #1677ff;
  color: #fff;
  font-weight: 800;
  cursor: pointer;
}

.team-message__text {
  white-space: normal;
}

.team-message__text :deep(a) {
  color: inherit;
  font-weight: 800;
  text-decoration: underline;
  text-underline-offset: 3px;
}

.team-message__text :deep(code) {
  border-radius: 6px;
  padding: 2px 5px;
  background: rgba(15, 23, 42, 0.08);
  font-family: ui-monospace, SFMono-Regular, Consolas, "Liberation Mono", monospace;
  font-size: 0.92em;
}

.team-message {
  display: flex;
  gap: 12px;
  align-items: flex-end;
  max-width: min(75%, 720px);
  margin-bottom: 4px;
  width: max-content;
}

.team-message.is-mine {
  align-self: flex-end;
  flex-direction: row-reverse;
}

.team-message.is-consecutive {
  margin-top: -2px;
}

.team-message__avatar-container {
  width: 36px;
  height: 36px;
  flex-shrink: 0;
}

.team-message.is-mine .team-message__avatar-container {
  display: none;
}

.team-message__avatar {
  width: 100%;
  height: 100%;
  border-radius: 12px;
  background: #e2e8f0;
  color: #475569;
  display: grid;
  place-items: center;
  font-size: 0.8rem;
  font-weight: 700;
}

.team-message__bubble {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 10px 14px;
  border-radius: 18px 18px 18px 4px;
  background: #f1f5f9;
  color: #1e293b;
  font-size: 0.95rem;
  line-height: 1.5;
  box-shadow: 0 1px 2px rgba(0,0,0,0.05);
  min-width: 0;
}

.team-message.is-consecutive .team-message__bubble {
  border-radius: 4px 18px 18px 4px;
}

.team-message.is-mine .team-message__bubble {
  border-radius: 18px 18px 4px 18px;
  background: #2563eb;
  color: #ffffff;
}

.team-message.is-mine.is-consecutive .team-message__bubble {
  border-radius: 18px 4px 4px 18px;
}

.team-message__meta {
  display: flex;
  align-items: baseline;
  gap: 8px;
  margin-bottom: 2px;
}

.team-message.is-mine .team-message__meta {
  flex-direction: row-reverse;
}

.team-message__meta strong {
  font-weight: 600;
  font-size: 0.85rem;
  color: #0f172a;
}

.team-message.is-mine .team-message__meta strong {
  display: none;
}

.team-message__meta span {
  font-size: 0.75rem;
  color: #64748b;
}

.team-message.is-mine .team-message__meta span {
  color: #94a3b8;
}

.team-message__meta button {
  background: none;
  border: none;
  color: inherit;
  opacity: 0;
  transition: opacity 0.2s;
  cursor: pointer;
  padding: 2px;
}

.team-message:hover .team-message__meta button {
  opacity: 0.7;
}

.team-message__meta button:hover {
  opacity: 1;
}

.team-message__text {
  word-break: break-word;
  margin: 0;
}

.message-attachment-card {
  display: flex;
  align-items: center;
  gap: 10px;
  color: inherit;
  text-decoration: none;
  padding: 8px 12px;
  background: rgba(255, 255, 255, 0.8);
  border-radius: 10px;
  margin-top: 6px;
  transition: background 0.2s;
  cursor: pointer;
}

.message-attachment-card:not(.is-clickable) {
  cursor: default;
}

.team-message.is-mine .message-attachment-card {
  background: rgba(255, 255, 255, 0.2);
  color: #ffffff;
}

.message-attachment-card:hover {
  background: rgba(255, 255, 255, 1);
}

.team-message.is-mine .message-attachment-card:hover {
  background: rgba(255, 255, 255, 0.3);
}

.message-attachment-icon {
  width: 32px;
  height: 32px;
  border-radius: 8px;
  background: #e2e8f0;
  display: grid;
  place-items: center;
  color: #475569;
}

.team-message.is-mine .message-attachment-icon {
  background: rgba(255, 255, 255, 0.2);
  color: #ffffff;
}

.message-attachment-info {
  display: flex;
  flex-direction: column;
}

.message-attachment-info strong {
  font-size: 0.85rem;
  line-height: 1.2;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 200px;
}

.message-attachment-info span {
  font-size: 0.75rem;
  opacity: 0.8;
}

.message-attachment-action {
  margin-left: 4px;
  flex-shrink: 0;
  opacity: 0.72;
}
</style>
