<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import ChatbotAvatar from '../ChatbotAvatar.vue'

const router = useRouter()
const route = useRoute()

const isAnalyticsPage = computed(() => route.path === '/analytics')

function navigateToAnalytics() {
  void router.push('/analytics')
}
</script>
<template>
  <!-- Floating Chatbot Removed -->
</template>
