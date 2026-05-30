<script setup lang="ts">
import { defineProps, defineEmits } from "vue";

const props = defineProps<{ active: { value: boolean } | boolean }>();
const emit = defineEmits<{
  start: [];
  end: [];
}>();

function start() {
  emit("start");
}

function end() {
  if (!confirm("Kết thúc cuộc họp?")) return;
  emit("end");
}
</script>

<template>
  <div class="flex items-center gap-3">
    <button class="primary-button" @click="start">Start meeting</button>
    <button class="secondary-button" @click="end">End meeting</button>
    <div class="ml-auto text-sm text-slate-500">
      Status:
      <strong>{{ (props.active as any).value ? "Active" : "Stopped" }}</strong>
    </div>
  </div>
</template>

<style scoped>
.primary-button {
  background: #10b981;
  color: white;
  padding: 8px 12px;
  border-radius: 6px;
}
.secondary-button {
  background: transparent;
  border: 1px solid #cbd5e1;
  padding: 8px 12px;
  border-radius: 6px;
}
</style>
