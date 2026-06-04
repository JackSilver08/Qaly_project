<script setup lang="ts">
import { computed, nextTick, ref, watch } from "vue";
import {
  Bold,
  Code2,
  Image as ImageIcon,
  Italic,
  Link2,
  Palette,
  Paperclip,
  Pin,
  Send,
  SmilePlus,
} from "lucide-vue-next";
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
  pin: [messageId: string];
  changeBackground: [];
  joinMeeting: [meetingId: string];
}>();

const draft = ref("");
const showEmoji = ref(false);
const pendingAttachments = ref<TeamChatAttachment[]>([]);
const bodyRef = ref<HTMLElement | null>(null);
const textareaRef = ref<HTMLTextAreaElement | null>(null);

const pinnedMessages = computed(() =>
  props.messages.filter((message) => message.pinned),
);
const emojiOptions = ["\u{1F44D}", "\u2705", "\u{1F525}", "\u{1F3AF}", "\u{1F64F}", "\u{1F4A1}"];
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

function attachFiles(event: Event, kind: "file" | "image") {
  const input = event.target as HTMLInputElement;
  const files = Array.from(input.files ?? []);

  pendingAttachments.value = files.map((file) => ({
    name: file.name,
    sizeLabel:
      file.size < 1024
        ? `${file.size} B`
        : `${Math.round(file.size / 1024)} KB`,
    kind,
  }));
  input.value = "";
}

function addEmoji(emoji: string) {
  draft.value += emoji;
  showEmoji.value = false;
  void nextTick(() => textareaRef.value?.focus());
}

function insertMarkdown(prefix: string, suffix = prefix, placeholder = "nội dung") {
  const textarea = textareaRef.value;
  const start = textarea?.selectionStart ?? draft.value.length;
  const end = textarea?.selectionEnd ?? draft.value.length;
  const selected = draft.value.slice(start, end) || placeholder;
  const nextValue = `${draft.value.slice(0, start)}${prefix}${selected}${suffix}${draft.value.slice(end)}`;

  draft.value = nextValue;

  void nextTick(() => {
    const cursor = start + prefix.length + selected.length + suffix.length;
    textareaRef.value?.focus();
    textareaRef.value?.setSelectionRange(cursor, cursor);
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

async function sendMessage() {
  const text = draft.value.trim();
  if (!text && pendingAttachments.value.length === 0) return;

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
  >
    <header class="team-chat-window__header">
      <div class="team-chat-window__identity">
        <span class="team-chat-window__avatar">{{ groupInitials }}</span>
        <div>
          <h2>{{ group?.name ?? "Chọn nhóm chat" }}</h2>
          <span>{{ group?.description ?? "Chọn một nhóm để bắt đầu trò chuyện" }}</span>
        </div>
      </div>
      <div class="team-chat-window__actions">
        <button
          class="icon-button icon-button--small"
          type="button"
          aria-label="Đổi nền chat"
          @click="$emit('changeBackground')"
        >
          <Palette :size="15" />
        </button>
        <div v-if="pinnedMessages.length" class="team-pinned">
          <Pin :size="14" />
          <span>{{ pinnedMessages.length }} tin đã ghim</span>
        </div>
      </div>
    </header>

    <div v-if="pinnedMessages.length" class="team-pinned-list">
      <span v-for="message in pinnedMessages" :key="message.id">{{
        message.text || message.poll?.question
      }}</span>
    </div>

    <div ref="bodyRef" class="team-chat-body no-scrollbar">
      <div v-if="!messages.length" class="team-chat-empty">
        <strong>{{ group ? "Chưa có tin nhắn" : "Chọn nhóm chat" }}</strong>
        <span>{{
          group
            ? "Gửi tin nhắn đầu tiên để bắt đầu cuộc trò chuyện."
            : "Danh sách tin nhắn sẽ xuất hiện ở đây."
        }}</span>
      </div>

      <MessageItem
        v-for="(message, index) in messages"
        :key="message.id"
        :message="message"
        :current-user-id="currentUserId"
        :is-consecutive="index > 0 && messages[index - 1].senderId === message.senderId"
        @pin="$emit('pin', $event)"
        @join-meeting="$emit('joinMeeting', $event)"
      />
    </div>

    <div class="team-chat-composer-wrapper">
      <div v-if="pendingAttachments.length" class="team-attachment-preview">
        <div v-for="(file, index) in pendingAttachments" :key="index" class="attachment-chip">
          <ImageIcon v-if="file.kind === 'image'" :size="14" />
          <Paperclip v-else :size="14" />
          <span class="attachment-name">{{ file.name }}</span>
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
        <label class="icon-button composer-tool-btn" aria-label="Đính kèm">
          <Paperclip :size="18" />
          <input type="file" multiple @change="attachFiles($event, 'file')" />
        </label>
        
        <textarea
          ref="textareaRef"
          v-model="draft"
          rows="1"
          placeholder="Nhập tin nhắn..."
          @keydown.enter.exact.prevent="sendMessage"
        ></textarea>
        
        <button class="primary-button composer-send-btn" type="submit" aria-label="Gửi tin nhắn">
          <Send :size="16" />
        </button>
      </form>
    </div>

    <div v-if="showEmoji" class="team-emoji-picker glass-card">
      <button
        v-for="emoji in emojiOptions"
        :key="emoji"
        type="button"
        @click="addEmoji(emoji)"
      >
        {{ emoji }}
      </button>
    </div>
  </section>
</template>

<style scoped>
.team-chat-window__actions {
  display: inline-flex;
  align-items: center;
  gap: 10px;
}

.team-chat-window--soft {
  background:
    radial-gradient(circle at 20% 10%, rgba(219, 234, 254, 0.9), transparent 30%),
    linear-gradient(135deg, #ffffff 0%, #f8fafc 48%, #eef6ff 100%);
}

.team-chat-window--mint {
  background:
    linear-gradient(135deg, rgba(236, 253, 245, 0.92), rgba(255, 255, 255, 0.98)),
    repeating-linear-gradient(45deg, rgba(20, 184, 166, 0.08) 0 1px, transparent 1px 18px);
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

.team-chat-window--dark .team-chat-window__header span,
.team-chat-window--dark .team-chat-window__header h2 {
  color: #f8fafc;
}

.team-chat-composer-wrapper {
  display: flex;
  flex-direction: column;
  gap: 8px;
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
  padding: 6px 12px;
  background: #f1f5f9;
  border-radius: 999px;
  font-size: 0.8rem;
  color: #334155;
  border: 1px solid #e2e8f0;
}

.team-chat-composer {
  display: flex;
  align-items: flex-end;
  gap: 6px;
  padding: 7px 8px;
  background: #ffffff;
  border: 1px solid #dbe5f1;
  border-radius: 18px;
  box-shadow: 0 12px 28px rgba(15, 23, 42, 0.06);
  transition: border-color 0.2s, box-shadow 0.2s, background 0.2s;
}

.team-chat-composer:focus-within {
  border-color: #8dbaf8;
  box-shadow: 0 0 0 4px rgba(37, 99, 235, 0.12), 0 16px 32px rgba(15, 23, 42, 0.08);
}

.composer-tool-btn {
  flex-shrink: 0;
  width: 38px;
  height: 38px;
  border-radius: 13px;
  display: grid;
  place-items: center;
  color: #64748b;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  transition: background 0.2s, color 0.2s, border-color 0.2s;
  cursor: pointer;
  margin-bottom: 1px;
}

.composer-tool-btn:hover {
  background: #eff6ff;
  border-color: #bfdbfe;
  color: #1d4ed8;
}

.composer-tool-btn input[type="file"] {
  display: none;
}

.team-chat-composer textarea {
  flex: 1;
  min-width: 0;
  min-height: 42px;
  max-height: 120px;
  border: 0 !important;
  background: transparent !important;
  padding: 10px 8px;
  font-size: 0.95rem;
  resize: none;
  color: #1e293b;
  line-height: 1.4;
  box-shadow: none !important;
}

.team-chat-composer textarea:focus {
  outline: none;
}

.composer-send-btn {
  flex-shrink: 0;
  width: 42px;
  height: 42px;
  border-radius: 14px;
  background: linear-gradient(135deg, #0f52ba, #2563eb);
  color: #ffffff;
  display: grid;
  place-items: center;
  transition: transform 0.2s, box-shadow 0.2s, background 0.2s;
  margin-bottom: 0;
  box-shadow: 0 10px 20px rgba(37, 99, 235, 0.24);
}

.composer-send-btn:hover {
  background: linear-gradient(135deg, #0d47a1, #1d4ed8);
  box-shadow: 0 12px 24px rgba(37, 99, 235, 0.3);
}
</style>
