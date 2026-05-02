<script setup lang="ts">
const erumiRobotUrl = '/images/erumi-chatbot.png'

withDefaults(
  defineProps<{
    size?: 'small' | 'medium' | 'launcher'
  }>(),
  {
    size: 'small',
  },
)
</script>

<template>
  <span class="chatbot-avatar" :class="`chatbot-avatar--${size}`">
    <img :src="erumiRobotUrl" alt="Erumi chatbot" />
  </span>
</template>
