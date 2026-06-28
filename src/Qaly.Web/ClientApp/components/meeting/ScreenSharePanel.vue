<script setup lang="ts">
import { ref, onBeforeUnmount } from "vue";
import { MonitorUp, Square } from "lucide-vue-next";

import { showError } from "../../composables/use-toast";

const stream = ref<MediaStream | null>(null);
const previewEl = ref<HTMLVideoElement | null>(null);

async function startShare() {
  try {
    if (!navigator.mediaDevices || !(navigator.mediaDevices as any).getDisplayMedia) {
      showError("Trình duyệt của bạn không hỗ trợ chia sẻ màn hình.");
      return;
    }
    // Request screen media
    const s = await (navigator.mediaDevices as any).getDisplayMedia({
      video: true,
      audio: false,
    });
    stream.value = s as MediaStream;
    if (previewEl.value) previewEl.value.srcObject = stream.value;
  } catch (e: any) {
    console.warn("Lỗi hoặc hủy chia sẻ màn hình", e);
    if (e.name === "NotAllowedError") {
      showError("Quyền chia sẻ màn hình bị từ chối.");
    } else {
      showError(`Không thể chia sẻ màn hình: ${e.message || e}`);
    }
  }
}

function stopShare() {
  stream.value?.getTracks().forEach((t) => t.stop());
  stream.value = null;
  if (previewEl.value) previewEl.value.srcObject = null;
}

onBeforeUnmount(() => stopShare());

defineExpose({ startShare, stopShare });
</script>

<template>
  <div class="screen-share-card">
    <div class="screen-share-card__header">
      <div>
        <span>Chia sẻ màn hình</span>
        <strong>{{ stream ? "Đang chia sẻ màn hình" : "Sẵn sàng chia sẻ" }}</strong>
      </div>
      <div :class="['screen-share-card__status', { active: stream }]"></div>
    </div>
    <video
      ref="previewEl"
      autoplay
      playsinline
      muted
      class="screen-share-preview"
    ></video>
    <div class="screen-share-actions">
      <button class="share-button share-button--primary" type="button" @click="startShare">
        <MonitorUp :size="16" /> Chia sẻ
      </button>
      <button class="share-button" type="button" @click="stopShare" :disabled="!stream">
        <Square :size="15" /> Dừng
      </button>
    </div>
  </div>
</template>

<style scoped>
.screen-share-card {
  display: grid;
  gap: 12px;
  padding: 14px;
  border: 1px solid rgba(148, 163, 184, 0.18);
  border-radius: var(--qaly-radius-lg);
  background: rgba(15, 23, 42, 0.72);
  color: #f8fafc;
  box-shadow: var(--qaly-shadow-md);
}

.screen-share-card__header,
.screen-share-actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.screen-share-card__header span {
  display: block;
  color: #94a3b8;
  font-size: 0.72rem;
  font-weight: 900;
  text-transform: uppercase;
}

.screen-share-card__header strong {
  font-size: 0.92rem;
}

.screen-share-card__status {
  width: 10px;
  height: 10px;
  border-radius: 999px;
  background: #64748b;
}

.screen-share-card__status.active {
  background: #22c55e;
  box-shadow: 0 0 0 5px rgba(34, 197, 94, 0.14);
}

.screen-share-preview {
  width: 100%;
  aspect-ratio: 16 / 9;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: var(--qaly-radius-lg);
  background:
    radial-gradient(circle at 50% 40%, rgba(59, 130, 246, 0.2), transparent 28%),
    #020617;
  object-fit: cover;
}

.share-button {
  min-height: 38px;
  display: inline-flex;
  align-items: center;
  gap: 7px;
  border: 1px solid rgba(255, 255, 255, 0.18);
  border-radius: var(--qaly-radius-lg);
  padding: 8px 11px;
  background: rgba(255, 255, 255, 0.08);
  color: #f8fafc;
  font-weight: 800;
  cursor: pointer;
}

.share-button--primary {
  border-color: transparent;
  background: #2563eb;
}

.share-button:disabled {
  opacity: 0.45;
  cursor: default;
}
</style>
