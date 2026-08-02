<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    variant?: 'loading' | 'empty' | 'error' | 'success'
    title: string
    message: string
    skeletonRows?: number
  }>(),
  {
    variant: 'empty',
    skeletonRows: 4,
  },
)

const isLoading = computed(() => props.variant === 'loading')
</script>

<template>
  <section
    class="page-state-panel"
    :class="`page-state-panel--${variant}`"
    :aria-busy="isLoading ? 'true' : undefined"
    :role="variant === 'error' ? 'alert' : 'status'"
  >
    <div v-if="isLoading" class="page-state-panel__loading" aria-hidden="true">
      <span
        v-for="row in skeletonRows"
        :key="row"
        class="page-state-panel__skeleton"
      />
    </div>

    <template v-else>
      <div v-if="$slots.icon" class="page-state-panel__icon" aria-hidden="true">
        <slot name="icon" />
      </div>
      <h2>{{ title }}</h2>
      <p>{{ message }}</p>
      <div v-if="$slots.actions" class="page-state-panel__actions">
        <slot name="actions" />
      </div>
    </template>
  </section>
</template>

<style scoped>
.page-state-panel {
  display: grid;
  justify-items: center;
  gap: 14px;
  padding: 32px 20px;
  border: 1px solid var(--line, #e2e8f0);
  border-radius: 16px;
  background: var(--panel, #ffffff);
  text-align: center;
}

.page-state-panel h2 {
  color: var(--text-strong, #0f172a);
  font-size: 1.2rem;
  line-height: 1.25;
}

.page-state-panel p {
  max-width: 58ch;
  color: var(--muted, #64748b);
  line-height: 1.6;
}

.page-state-panel__icon {
  display: grid;
  place-items: center;
  width: 56px;
  height: 56px;
  border-radius: 16px;
  background: var(--primary-soft, rgba(37, 99, 235, 0.1));
  color: var(--primary, #2563eb);
}

.page-state-panel__actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 10px;
}

.page-state-panel__loading {
  width: min(100%, 520px);
  display: grid;
  gap: 10px;
}

.page-state-panel__skeleton {
  display: block;
  height: 20px;
  border-radius: 999px;
  background: linear-gradient(90deg, #edf2f7, #f8fafc, #edf2f7);
  background-size: 200% 100%;
  animation: page-state-shimmer 1.2s ease-in-out infinite;
}

.page-state-panel__skeleton:nth-child(1) {
  width: 55%;
}

.page-state-panel__skeleton:nth-child(2) {
  width: 90%;
}

.page-state-panel__skeleton:nth-child(3) {
  width: 78%;
}

.page-state-panel__skeleton:nth-child(4) {
  width: 66%;
}

.page-state-panel--error {
  border-color: rgba(248, 113, 113, 0.32);
  background: rgba(254, 242, 242, 0.84);
}

.page-state-panel--success {
  border-color: rgba(34, 197, 94, 0.3);
  background: rgba(240, 253, 244, 0.8);
}

@keyframes page-state-shimmer {
  0% {
    background-position: 200% 0;
  }

  100% {
    background-position: -200% 0;
  }
}

@media (prefers-reduced-motion: reduce) {
  .page-state-panel__skeleton {
    animation: none;
  }
}
</style>
