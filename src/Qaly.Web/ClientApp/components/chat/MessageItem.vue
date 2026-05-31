<script setup lang="ts">
import { CalendarDays, Pin } from "lucide-vue-next";
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
</style>
