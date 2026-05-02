<script setup lang="ts">
import { ClipboardList, FolderKanban, Users } from 'lucide-vue-next'
import type { SummaryCardModel } from './dashboard-models'

defineProps<{
  cards: SummaryCardModel[]
}>()

const cardIcons = {
  projects: FolderKanban,
  tasks: ClipboardList,
  team: Users,
}
</script>

<template>
  <section class="summary-card-grid" aria-label="Tổng quan dự án">
    <article v-for="card in cards" :key="card.key" class="summary-card glass-card" :class="`summary-card--${card.tone}`">
      <div class="summary-card__icon">
        <component :is="cardIcons[card.key]" :size="18" />
      </div>
      <span>{{ card.label }}</span>
      <strong>{{ card.value }}</strong>
      <p>{{ card.detail }}</p>
    </article>
  </section>
</template>
