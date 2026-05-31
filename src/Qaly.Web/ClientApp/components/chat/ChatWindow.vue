<script setup lang="ts">
import { computed, nextTick, ref, watch } from "vue";
import { FileUp, Palette, Pin, Send, SmilePlus, Vote } from "lucide-vue-next";
import MessageItem from "./MessageItem.vue";
import { apiResult } from "../../utils/api-client";
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
const showPoll = ref(false);
const pollQuestion = ref("");
const pollOptions = ref(["", ""]);
const pendingAttachments = ref<TeamChatAttachment[]>([]);
const bodyRef = ref<HTMLElement | null>(null);

const pinnedMessages = computed(() =>
  props.messages.filter((message) => message.pinned),
);
const emojiOptions = ["\u{1F44D}", "\u2705", "\u{1F525}", "\u{1F3AF}", "\u{1F64F}", "\u{1F4A1}"];

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

function attachFiles(event: Event) {
  const input = event.target as HTMLInputElement;
  const files = Array.from(input.files ?? []);

  pendingAttachments.value = files.map((file) => ({
    name: file.name,
    sizeLabel:
      file.size < 1024
        ? `${file.size} B`
        : `${Math.round(file.size / 1024)} KB`,
  }));
  input.value = "";
}

function addEmoji(emoji: string) {
  draft.value += emoji;
  showEmoji.value = false;
}

async function sendMessage() {
  const text = draft.value.trim();
  const options = pollOptions.value
    .map((option) => option.trim())
    .filter(Boolean);
  const poll =
    showPoll.value && pollQuestion.value.trim() && options.length >= 2
      ? { question: pollQuestion.value.trim(), options }
      : undefined;

  if (!text && pendingAttachments.value.length === 0 && !poll) return;

  // If composing a poll, create a poll entity first so messages can reference pollId
  let pollWithId = poll;
  if (poll && typeof (window as any) !== "undefined") {
    try {
      const created = await apiResult<any>(
        `/api/groups/${props.group?.id ?? ""}/polls`,
        {
          method: "POST",
          body: JSON.stringify({
            question: poll.question,
            options: poll.options.map((o: string) => ({ content: o })),
            allowMultiple: false,
          }),
        },
      );
      if (created && (created.id || created.Id)) {
        pollWithId = { ...poll, id: created.id ?? created.Id };
      }
    } catch (e) {
      // if poll creation fails, still send plain message
      console.warn("Không thể tạo bình chọn trước khi gửi tin nhắn", e);
    }
  }

  emit("send", {
    text,
    attachments: pendingAttachments.value,
    poll: pollWithId,
  });

  draft.value = "";
  pendingAttachments.value = [];
  pollQuestion.value = "";
  pollOptions.value = ["", ""];
  showPoll.value = false;
}
</script>

<template>
  <section
    class="team-chat-window glass-card"
    :class="`team-chat-window--${backgroundTheme ?? 'clean'}`"
  >
    <header class="team-chat-window__header">
      <div>
        <span>Trò chuyện</span>
        <h2>{{ group?.name ?? "Chọn nhóm chat" }}</h2>
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
      <MessageItem
        v-for="message in messages"
        :key="message.id"
        :message="message"
        :current-user-id="currentUserId"
        @pin="$emit('pin', $event)"
        @join-meeting="$emit('joinMeeting', $event)"
      />
    </div>

    <div v-if="showPoll" class="team-poll-composer">
      <input
        v-model="pollQuestion"
        type="text"
        placeholder="Câu hỏi bình chọn"
      />
      <input
        v-for="(_, index) in pollOptions"
        :key="index"
        v-model="pollOptions[index]"
        type="text"
        :placeholder="`Lựa chọn ${index + 1}`"
      />
      <button class="text-button" type="button" @click="pollOptions.push('')">
        Thêm lựa chọn
      </button>
    </div>

    <div v-if="pendingAttachments.length" class="team-attachment-preview">
      <span v-for="file in pendingAttachments" :key="file.name">{{
        file.name
      }}</span>
    </div>

    <form class="team-chat-composer" @submit.prevent="sendMessage">
      <label class="icon-button icon-button--small" aria-label="Gửi tệp">
        <FileUp :size="16" />
        <input type="file" multiple @change="attachFiles" />
      </label>
      <button
        class="icon-button icon-button--small"
        type="button"
        aria-label="Biểu cảm"
        @click="showEmoji = !showEmoji"
      >
        <SmilePlus :size="16" />
      </button>
      <button
        class="icon-button icon-button--small"
        type="button"
        aria-label="Tạo bình chọn"
        @click="showPoll = !showPoll"
      >
        <Vote :size="16" />
      </button>
      <input v-model="draft" type="text" placeholder="Nhập tin nhắn..." />
      <button class="primary-button primary-button--compact" type="submit">
        <Send :size="15" />
      </button>
    </form>

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
</style>
