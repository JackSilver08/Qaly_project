<script setup lang="ts">
import { computed } from 'vue'
import { Bell } from 'lucide-vue-next'

const props = defineProps<{
  count: number
  open: boolean
}>()

const emit = defineEmits<{
  click: []
}>()

const normalizedCount = computed(() => Math.max(0, Math.floor(props.count)))
const hasNotifications = computed(() => normalizedCount.value > 0)
const displayedCount = computed(() => normalizedCount.value > 99 ? '99+' : String(normalizedCount.value))
const accessibleLabel = computed(() => hasNotifications.value
  ? `Thông báo, ${normalizedCount.value} mục cần chú ý`
  : 'Thông báo')
</script>

<template>
  <button
    class="header-notification-button"
    :class="{
      'has-notifications': hasNotifications,
      'is-open': open,
    }"
    type="button"
    :aria-label="accessibleLabel"
    :title="accessibleLabel"
    aria-controls="notification-panel"
    :aria-expanded="open"
    @click="emit('click')"
  >
    <span class="header-notification-button__bell" aria-hidden="true">
      <Bell :size="18" :stroke-width="2" />
      <i v-if="hasNotifications"></i>
    </span>
    <span v-if="hasNotifications" class="header-notification-button__count" aria-hidden="true">
      {{ displayedCount }}
    </span>
  </button>
</template>

<style scoped>
.header-notification-button {
  min-width: 40px;
  height: 38px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 5px;
  padding: 0 9px;
  border: 1px solid var(--border, #dce2ea);
  border-radius: 12px;
  color: var(--text-primary, #162033);
  background: var(--surface, #fff);
  box-shadow: var(--qaly-shadow-sm, 0 1px 3px rgba(15, 23, 42, 0.08));
  cursor: pointer;
  transition: transform 300ms ease-out, border-color 300ms ease-out, color 300ms ease-out, background 300ms ease-out;
}

.header-notification-button__bell {
  position: relative;
  width: 20px;
  height: 20px;
  display: grid;
  place-items: center;
}

.header-notification-button__bell i {
  position: absolute;
  top: 0;
  right: 0;
  width: 6px;
  height: 6px;
  border: 1.5px solid var(--surface, #fff);
  border-radius: 999px;
  background: #ef4444;
}

.header-notification-button__count {
  min-width: 20px;
  height: 20px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0 5px;
  border-radius: 7px;
  color: #fff;
  background: linear-gradient(145deg, #f43f5e, #dc2626);
  box-shadow: 0 4px 10px rgba(220, 38, 38, 0.2);
  font-size: 10px;
  font-weight: 900;
  line-height: 1;
  font-variant-numeric: tabular-nums;
}

.header-notification-button.has-notifications {
  border-color: #fecaca;
  color: #b91c1c;
  background: linear-gradient(145deg, #fff, #fff7f7);
}

.header-notification-button:hover,
.header-notification-button:focus-visible {
  transform: translateY(-1px);
  border-color: #93c5fd;
  color: #1d4ed8;
  background: #eff6ff;
}

.header-notification-button.is-open {
  border-color: #60a5fa;
  color: #1d4ed8;
  background: #eff6ff;
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
}

:global(:root[data-theme='dark'] .header-notification-button) {
  border-color: var(--line);
  color: var(--text);
  background: var(--panel-soft);
  box-shadow: var(--shadow-soft);
}

:global(:root[data-theme='dark'] .header-notification-button.has-notifications) {
  border-color: rgba(248, 113, 113, 0.34);
  color: #fca5a5;
  background: rgba(127, 29, 29, 0.16);
}

:global(:root[data-theme='dark'] .header-notification-button:hover),
:global(:root[data-theme='dark'] .header-notification-button:focus-visible),
:global(:root[data-theme='dark'] .header-notification-button.is-open) {
  border-color: rgba(96, 165, 250, 0.5);
  color: #93c5fd;
  background: rgba(30, 64, 175, 0.2);
}

:global(:root[data-theme='dark'] .header-notification-button__bell i) {
  border-color: var(--panel-soft);
}

@media (max-width: 760px) {
  .header-notification-button {
    height: 36px;
    padding: 0 7px;
  }
}

@media (prefers-reduced-motion: reduce) {
  .header-notification-button {
    transition: none;
  }
}
</style>
