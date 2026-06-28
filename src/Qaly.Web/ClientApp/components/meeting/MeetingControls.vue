<script setup lang="ts">
import {
  Mic,
  MicOff,
  MonitorUp,
  Phone,
  PhoneOff,
  Video,
  VideoOff,
  Sparkles,
} from "lucide-vue-next";

defineProps<{
  active: boolean;
  micMuted?: boolean;
  cameraMuted?: boolean;
  speechActive?: boolean;
  light?: boolean;
}>();
const emit = defineEmits<{
  start: [];
  end: [];
  share: [];
  toggleMic: [];
  toggleCamera: [];
  toggleSpeech: [];
}>();

function start() {
  emit("start");
}

function end() {
  if (!confirm("K\u1ebft th\u00fac cu\u1ed9c h\u1ecdp?")) return;
  emit("end");
}
</script>

<template>
  <div class="meet-controls" :class="{ 'meet-controls--light': light }">
    <button
      class="meet-control"
      :class="{ 'meet-control--off': micMuted }"
      type="button"
      :aria-label="micMuted ? 'B\u1eadt micro' : 'T\u1eaft micro'"
      :title="micMuted ? 'B\u1eadt micro' : 'T\u1eaft micro'"
      @click="$emit('toggleMic')"
    >
      <MicOff v-if="micMuted" :size="20" />
      <Mic v-else :size="20" />
    </button>
    <button
      class="meet-control"
      :class="{ 'meet-control--off': cameraMuted }"
      type="button"
      :aria-label="cameraMuted ? 'B\u1eadt camera' : 'T\u1eaft camera'"
      :title="cameraMuted ? 'B\u1eadt camera' : 'T\u1eaft camera'"
      @click="$emit('toggleCamera')"
    >
      <VideoOff v-if="cameraMuted" :size="20" />
      <Video v-else :size="20" />
    </button>
    <button
      v-if="active"
      class="meet-control meet-control--ai"
      :class="{ 'meet-control--ai-active': speechActive }"
      type="button"
      :aria-label="speechActive ? 'Tắt trợ lý AI' : 'Bật trợ lý AI'"
      :title="speechActive ? 'Tắt trợ lý AI' : 'Bật trợ lý AI'"
      @click="$emit('toggleSpeech')"
    >
      <Sparkles :size="20" />
    </button>
    <button
      class="meet-control meet-control--share"
      type="button"
      :aria-label="'Chia sẻ màn hình'"
      @click="$emit('share')"
    >
      <MonitorUp :size="20" />
    </button>
    <button
      v-if="!active"
      class="meet-control meet-control--join"
      type="button"
      :aria-label="'Bắt đầu cuộc họp'"
      @click="start"
    >
      <Phone :size="21" />
    </button>
    <button
      v-else
      class="meet-control meet-control--leave"
      type="button"
      :aria-label="'Kết thúc cuộc họp'"
      @click="end"
    >
      <PhoneOff :size="21" />
    </button>
  </div>
</template>

<style scoped>
.meet-control--ai-active {
  background: linear-gradient(135deg, #6366f1, #a855f7) !important;
  color: #ffffff !important;
  box-shadow: var(--qaly-shadow-md);
  animation: pulse-ai 2s infinite;
}

@keyframes pulse-ai {
  0% {
    box-shadow: 0 0 0 0 rgba(168, 85, 247, 0.7);
  }
  70% {
    box-shadow: 0 0 0 8px rgba(168, 85, 247, 0);
  }
  100% {
    box-shadow: 0 0 0 0 rgba(168, 85, 247, 0);
  }
}

.meet-controls {
  display: inline-flex;
  align-items: center;
  gap: 12px;
  padding: 10px;
  border: 1px solid rgba(148, 163, 184, 0.18);
  border-radius: 999px;
  background: rgba(15, 23, 42, 0.76);
  box-shadow: var(--qaly-shadow-md);
  backdrop-filter: none;
}

.meet-controls--light {
  border-color: rgba(148, 163, 184, 0.32);
  background: rgba(255, 255, 255, 0.82);
  box-shadow: var(--qaly-shadow-md);
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
