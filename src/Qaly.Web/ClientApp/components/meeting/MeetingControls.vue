<script setup lang="ts">
import {
  Mic,
  MicOff,
  MonitorUp,
  Phone,
  PhoneOff,
  Video,
  VideoOff,
} from "lucide-vue-next";

defineProps<{
  active: boolean;
  micMuted?: boolean;
  cameraMuted?: boolean;
  light?: boolean;
}>();
const emit = defineEmits<{
  start: [];
  end: [];
  share: [];
  toggleMic: [];
  toggleCamera: [];
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
  <div class="meet-controls" :class="{ 'meet-controls--light': light }">
    <button
      class="meet-control"
      :class="{ 'meet-control--off': micMuted }"
      type="button"
      :aria-label="micMuted ? 'Bật micro' : 'Tắt micro'"
      :title="micMuted ? 'Bật micro' : 'Tắt micro'"
      @click="$emit('toggleMic')"
    >
      <MicOff v-if="micMuted" :size="20" />
      <Mic v-else :size="20" />
    </button>
    <button
      class="meet-control"
      :class="{ 'meet-control--off': cameraMuted }"
      type="button"
      :aria-label="cameraMuted ? 'Bật camera' : 'Tắt camera'"
      :title="cameraMuted ? 'Bật camera' : 'Tắt camera'"
      @click="$emit('toggleCamera')"
    >
      <VideoOff v-if="cameraMuted" :size="20" />
      <Video v-else :size="20" />
    </button>
    <button class="meet-control meet-control--share" type="button" aria-label="Share screen" @click="$emit('share')">
      <MonitorUp :size="20" />
    </button>
    <button
      v-if="!active"
      class="meet-control meet-control--join"
      type="button"
      aria-label="Start meeting"
      @click="start"
    >
      <Phone :size="21" />
    </button>
    <button
      v-else
      class="meet-control meet-control--leave"
      type="button"
      aria-label="End meeting"
      @click="end"
    >
      <PhoneOff :size="21" />
    </button>
  </div>
</template>

<style scoped>
.meet-controls {
  display: inline-flex;
  align-items: center;
  gap: 12px;
  padding: 10px;
  border: 1px solid rgba(148, 163, 184, 0.18);
  border-radius: 999px;
  background: rgba(15, 23, 42, 0.76);
  box-shadow: 0 24px 60px rgba(0, 0, 0, 0.28);
  backdrop-filter: blur(18px);
}

.meet-controls--light {
  border-color: rgba(148, 163, 184, 0.32);
  background: rgba(255, 255, 255, 0.82);
  box-shadow: 0 24px 60px rgba(15, 23, 42, 0.16);
}

.meet-controls--light .meet-control {
  background: #e2e8f0;
  color: #0f172a;
}

.meet-controls--light .meet-control:hover {
  background: #cbd5e1;
}

.meet-controls--light .meet-control--share {
  background: #2563eb;
  color: #fff;
}

.meet-controls--light .meet-control--off,
.meet-controls--light .meet-control--leave {
  background: #ef4444;
  color: #fff;
}

.meet-control {
  width: 46px;
  height: 46px;
  display: grid;
  place-items: center;
  border: 0;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.12);
  color: #f8fafc;
  cursor: pointer;
  transition: transform 160ms ease, background 160ms ease;
}

.meet-control:hover {
  transform: translateY(-1px);
  background: rgba(255, 255, 255, 0.2);
}

.meet-control--share {
  background: rgba(37, 99, 235, 0.92);
}

.meet-control--off {
  background: rgba(239, 68, 68, 0.92);
}

.meet-control--join {
  width: 58px;
  background: #16a34a;
}

.meet-control--leave {
  width: 58px;
  background: #dc2626;
}
</style>
