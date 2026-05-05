<script setup lang="ts">
import { Box } from 'lucide-vue-next'
import ChatbotAvatar from './ChatbotAvatar.vue'
import type { ShellNavItem } from './shell-models'

defineProps<{
  items: ShellNavItem[]
  activeTarget: string
  teamInitials: string[]
  assistantLabel: string
}>()

defineEmits<{
  navigate: [target: string]
  assistant: []
}>()
</script>

<template>
  <aside class="shell-sidebar no-scrollbar">
    <nav class="shell-nav" aria-label="Main navigation">
      <button
        v-for="item in items"
        :key="item.target"
        class="shell-nav-item"
        :class="{ 'is-active': item.target === activeTarget }"
        type="button"
        @click="$emit('navigate', item.target)"
      >
        <component :is="item.icon" :size="20" />
        <span>{{ item.label }}</span>
      </button>
    </nav>

    <div class="shell-sidebar-divider"></div>

    <section class="shell-team-block" aria-label="Team snapshot">
      <p>Team</p>
      <div class="shell-team-avatars">
        <span v-for="member in teamInitials" :key="member">{{ member }}</span>
      </div>
    </section>

    <button class="shell-assistant-card" type="button" @click="$emit('assistant')">
      <ChatbotAvatar size="launcher" />
      <span>{{ assistantLabel }}</span>
    </button>

    <button class="shell-archive-button" type="button">
      <Box :size="19" />
      <span>Archived Projects</span>
    </button>
  </aside>
</template>
