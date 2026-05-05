<script setup lang="ts">
import { Pin } from 'lucide-vue-next'
import type { TeamChatMessage } from './chat-types'

defineProps<{
  message: TeamChatMessage
  currentUserId: string
}>()

defineEmits<{
  pin: [messageId: string]
}>()
</script>

<template>
  <article class="team-message" :class="{ 'is-mine': message.senderId === currentUserId }">
    <div class="team-message__avatar">{{ message.senderInitials }}</div>
    <div class="team-message__bubble">
      <div class="team-message__meta">
        <strong>{{ message.senderName }}</strong>
        <span>{{ message.createdAt }}</span>
        <button type="button" :aria-label="message.pinned ? 'Bỏ ghim' : 'Ghim tin nhắn'" @click="$emit('pin', message.id)">
          <Pin :size="13" />
        </button>
      </div>
      <p v-if="message.text">{{ message.text }}</p>

      <div v-if="message.attachments.length" class="team-message__attachments">
        <span v-for="file in message.attachments" :key="file.name">{{ file.name }} · {{ file.sizeLabel }}</span>
      </div>

      <div v-if="message.poll" class="team-message__poll">
        <strong>{{ message.poll.question }}</strong>
        <button v-for="option in message.poll.options" :key="option" type="button">{{ option }}</button>
      </div>
    </div>
  </article>
</template>
