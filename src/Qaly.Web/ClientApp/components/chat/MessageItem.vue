<script setup lang="ts">
import { Pin } from "lucide-vue-next";
import type { TeamChatMessage } from "./chat-types";
import PollCard from "./PollCard.vue";

defineProps<{
  message: TeamChatMessage;
  currentUserId: string;
}>();

defineEmits<{
  pin: [messageId: string];
  joinMeeting: [meetingId: string];
}>();
</script>

<template>
  <article
    class="team-message"
    :class="{ 'is-mine': message.senderId === currentUserId }"
  >
    <div class="team-message__avatar">{{ message.senderInitials }}</div>
    <div class="team-message__bubble">
      <div class="team-message__meta">
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
      <p v-if="message.text">{{ message.text }}</p>

      <div v-if="message.meeting" class="team-message__meeting">
        <span>Cuộc họp nhóm</span>
        <strong>{{ message.meeting.text }}</strong>
        <button
          type="button"
          @click="$emit('joinMeeting', message.meeting.id)"
        >
          Tham gia
        </button>
      </div>

      <div v-if="message.attachments.length" class="team-message__attachments">
        <span v-for="file in message.attachments" :key="file.name"
          >{{ file.name }} · {{ file.sizeLabel }}</span
        >
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
.team-message__meeting {
  display: grid;
  gap: 8px;
  margin-top: 8px;
  border: 1px solid rgba(147, 197, 253, 0.72);
  border-radius: 14px;
  padding: 12px;
  background: rgba(239, 246, 255, 0.96);
  color: #0f172a;
}

.team-message__meeting span {
  color: #1d4ed8;
  font-size: 0.72rem;
  font-weight: 900;
  text-transform: uppercase;
}

.team-message__meeting strong {
  line-height: 1.35;
}

.team-message__meeting button {
  width: max-content;
  border: 0;
  border-radius: 999px;
  padding: 8px 12px;
  background: #2563eb;
  color: #fff;
  font-weight: 900;
  cursor: pointer;
}
</style>
