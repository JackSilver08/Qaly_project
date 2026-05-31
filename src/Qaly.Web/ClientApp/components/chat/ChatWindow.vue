<script setup lang="ts">
import { computed, nextTick, ref, watch } from "vue";
import { FileUp, Pin, Send, SmilePlus, Vote } from "lucide-vue-next";
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
const emojiOptions = ["👍", "✅", "🔥", "🎯", "🙏", "💡"];

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
      console.warn("Could not create poll before sending message", e);
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
  <section class="team-chat-window glass-card">
    <header class="team-chat-window__header">
      <div>
        <span>Chat</span>
        <h2>{{ group?.name ?? "Chọn nhóm chat" }}</h2>
      </div>
      <div v-if="pinnedMessages.length" class="team-pinned">
        <Pin :size="14" />
        <span>{{ pinnedMessages.length }} pinned</span>
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
      <label class="icon-button icon-button--small" aria-label="Gửi file">
        <FileUp :size="16" />
        <input type="file" multiple @change="attachFiles" />
      </label>
      <button
        class="icon-button icon-button--small"
        type="button"
        aria-label="Emoji"
        @click="showEmoji = !showEmoji"
      >
        <SmilePlus :size="16" />
      </button>
      <button
        class="icon-button icon-button--small"
        type="button"
        aria-label="Tạo poll"
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
