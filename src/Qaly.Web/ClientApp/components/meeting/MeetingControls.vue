<script setup lang="ts">
import {
  Mic,
  MicOff,
  MonitorUp,
  Phone,
  PhoneOff,
  Video,
  VideoOff,
  Captions,
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
  if (!confirm("Kết thúc cuộc họp?")) return;
  emit("end");
}
</script>

<template>
  <div class="mc-dock" :class="{ 'mc-dock--light': light }">
    <!-- Mic -->
    <button
      class="mc-btn"
      :class="{ 'mc-btn--danger': micMuted }"
      type="button"
      :aria-label="micMuted ? 'Bật micro' : 'Tắt micro'"
      @click="$emit('toggleMic')"
    >
      <MicOff v-if="micMuted" :size="20" />
      <Mic v-else :size="20" />
      <span class="mc-tooltip">{{ micMuted ? 'Bật Mic' : 'Tắt Mic' }}</span>
    </button>

    <!-- Camera -->
    <button
      class="mc-btn"
      :class="{ 'mc-btn--danger': cameraMuted }"
      type="button"
      :aria-label="cameraMuted ? 'Bật camera' : 'Tắt camera'"
      @click="$emit('toggleCamera')"
    >
      <VideoOff v-if="cameraMuted" :size="20" />
      <Video v-else :size="20" />
      <span class="mc-tooltip">{{ cameraMuted ? 'Bật Camera' : 'Tắt Camera' }}</span>
    </button>

    <div class="mc-separator"></div>

    <!-- AI Transcript -->
    <button
      v-if="active"
      class="mc-btn mc-btn--transcript"
      :class="{ 'mc-btn--transcript-active': speechActive }"
      type="button"
      :aria-label="speechActive ? 'Tắt phụ đề AI' : 'Bật phụ đề AI'"
      @click="$emit('toggleSpeech')"
    >
      <Captions :size="20" />
      <span class="mc-tooltip">{{ speechActive ? 'Tắt Phụ đề' : 'Bật Phụ đề' }}</span>
      <span v-if="speechActive" class="mc-recording-dot"></span>
    </button>

    <!-- Screen Share -->
    <button
      class="mc-btn mc-btn--accent"
      type="button"
      :aria-label="'Chia sẻ màn hình'"
      @click="$emit('share')"
    >
      <MonitorUp :size="20" />
      <span class="mc-tooltip">Chia sẻ</span>
    </button>

    <div class="mc-separator"></div>

    <!-- Join / Leave -->
    <button
      v-if="!active"
      class="mc-btn mc-btn--join"
      type="button"
      :aria-label="'Bắt đầu cuộc họp'"
      @click="start"
    >
      <Phone :size="20" />
      <span class="mc-join-label">Tham gia</span>
    </button>
    <button
      v-else
      class="mc-btn mc-btn--leave"
      type="button"
      :aria-label="'Kết thúc cuộc họp'"
      @click="end"
    >
      <PhoneOff :size="20" />
      <span class="mc-tooltip">Rời phòng</span>
    </button>
  </div>
</template>

<style scoped>
.mc-dock {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  border-radius: 9999px;
  background: rgba(15, 23, 42, 0.82);
  border: 1px solid rgba(255, 255, 255, 0.08);
  box-shadow:
    0 8px 32px rgba(0, 0, 0, 0.35),
    0 0 0 1px rgba(255, 255, 255, 0.04) inset;
  backdrop-filter: blur(20px) saturate(1.6);
  -webkit-backdrop-filter: blur(20px) saturate(1.6);
}

.mc-dock--light {
  background: rgba(255, 255, 255, 0.85);
  border-color: rgba(148, 163, 184, 0.28);
  box-shadow:
    0 8px 32px rgba(0, 0, 0, 0.1),
    0 0 0 1px rgba(255, 255, 255, 0.6) inset;
}

.mc-separator {
  width: 1px;
  height: 28px;
  background: rgba(255, 255, 255, 0.12);
  border-radius: 1px;
  flex-shrink: 0;
}

.mc-dock--light .mc-separator {
  background: rgba(148, 163, 184, 0.32);
}

/* --- Button Base --- */
.mc-btn {
  position: relative;
  width: 48px;
  height: 48px;
  display: grid;
  place-items: center;
  border: 0;
  border-radius: 9999px;
  background: rgba(255, 255, 255, 0.1);
  color: #f1f5f9;
  cursor: pointer;
  transition: all 180ms cubic-bezier(0.4, 0, 0.2, 1);
  flex-shrink: 0;
}

.mc-btn:hover {
  background: rgba(255, 255, 255, 0.18);
  transform: translateY(-2px);
}

.mc-btn:active {
  transform: scale(0.95);
}

.mc-dock--light .mc-btn {
  background: rgba(241, 245, 249, 0.9);
  color: #1e293b;
}

.mc-dock--light .mc-btn:hover {
  background: rgba(226, 232, 240, 0.95);
}

/* --- Danger (Mic/Camera off) --- */
.mc-btn--danger {
  background: rgba(239, 68, 68, 0.85) !important;
  color: #fff !important;
}

.mc-btn--danger:hover {
  background: rgba(220, 38, 38, 0.95) !important;
}

/* --- Accent (Screen Share) --- */
.mc-btn--accent {
  background: rgba(59, 130, 246, 0.2);
  color: #93c5fd;
}

.mc-btn--accent:hover {
  background: rgba(59, 130, 246, 0.35);
}

.mc-dock--light .mc-btn--accent {
  background: rgba(59, 130, 246, 0.12);
  color: #2563eb;
}

.mc-dock--light .mc-btn--accent:hover {
  background: rgba(59, 130, 246, 0.22);
}

/* --- Transcript button --- */
.mc-btn--transcript {
  background: rgba(168, 85, 247, 0.12);
  color: #c4b5fd;
}

.mc-btn--transcript:hover {
  background: rgba(168, 85, 247, 0.24);
}

.mc-dock--light .mc-btn--transcript {
  background: rgba(139, 92, 246, 0.1);
  color: #7c3aed;
}

.mc-btn--transcript-active {
  background: linear-gradient(135deg, #7c3aed, #a855f7) !important;
  color: #fff !important;
  box-shadow: 0 0 0 0 rgba(168, 85, 247, 0.5);
  animation: pulse-transcript 2.2s cubic-bezier(0.4, 0, 0.6, 1) infinite;
}

@keyframes pulse-transcript {
  0%, 100% {
    box-shadow: 0 0 0 0 rgba(168, 85, 247, 0.55);
  }
  50% {
    box-shadow: 0 0 0 10px rgba(168, 85, 247, 0);
  }
}

.mc-recording-dot {
  position: absolute;
  top: 8px;
  right: 8px;
  width: 8px;
  height: 8px;
  border-radius: 9999px;
  background: #f43f5e;
  box-shadow: 0 0 6px rgba(244, 63, 94, 0.7);
  animation: blink-dot 1.4s ease-in-out infinite;
}

@keyframes blink-dot {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.25; }
}

/* --- Join --- */
.mc-btn--join {
  width: auto;
  gap: 8px;
  padding: 0 20px;
  display: inline-flex;
  background: linear-gradient(135deg, #16a34a, #22c55e);
  color: #fff;
  font-weight: 700;
  font-size: 0.88rem;
}

.mc-btn--join:hover {
  background: linear-gradient(135deg, #15803d, #16a34a);
  transform: translateY(-2px);
  box-shadow: 0 4px 16px rgba(22, 163, 74, 0.35);
}

.mc-join-label {
  white-space: nowrap;
}

/* --- Leave --- */
.mc-btn--leave {
  width: 56px;
  background: linear-gradient(135deg, #dc2626, #ef4444);
  color: #fff;
}

.mc-btn--leave:hover {
  background: linear-gradient(135deg, #b91c1c, #dc2626);
  box-shadow: 0 4px 16px rgba(220, 38, 38, 0.35);
}

/* --- Tooltip --- */
.mc-tooltip {
  position: absolute;
  bottom: calc(100% + 10px);
  left: 50%;
  transform: translateX(-50%) translateY(4px);
  padding: 6px 12px;
  border-radius: 8px;
  background: rgba(15, 23, 42, 0.92);
  color: #f1f5f9;
  font-size: 0.72rem;
  font-weight: 700;
  white-space: nowrap;
  pointer-events: none;
  opacity: 0;
  transition: all 160ms ease;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.25);
}

.mc-dock--light .mc-tooltip {
  background: rgba(255, 255, 255, 0.95);
  color: #1e293b;
  border: 1px solid rgba(148, 163, 184, 0.22);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
}

.mc-btn:hover .mc-tooltip {
  opacity: 1;
  transform: translateX(-50%) translateY(0);
}
</style>
