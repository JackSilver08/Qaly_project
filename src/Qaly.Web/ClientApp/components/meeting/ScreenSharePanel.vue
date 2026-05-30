<script setup lang="ts">
import { ref, onBeforeUnmount } from "vue";

const stream = ref<MediaStream | null>(null);
const previewEl = ref<HTMLVideoElement | null>(null);

async function startShare() {
  try {
    // Request screen media
    const s = await (navigator.mediaDevices as any).getDisplayMedia({
      video: true,
      audio: false,
    });
    stream.value = s as MediaStream;
    if (previewEl.value) previewEl.value.srcObject = stream.value;
  } catch (e) {
    console.warn("Screen share cancelled", e);
  }
}

function stopShare() {
  stream.value?.getTracks().forEach((t) => t.stop());
  stream.value = null;
  if (previewEl.value) previewEl.value.srcObject = null;
}

onBeforeUnmount(() => stopShare());
</script>

<template>
  <div class="glass-card p-4">
    <h3 class="font-medium mb-2">Screen Share</h3>
    <video
      ref="previewEl"
      autoplay
      playsinline
      muted
      class="w-full h-48 bg-black rounded-md"
    ></video>
    <div class="mt-3 flex gap-2">
      <button class="primary-button" @click="startShare">Start Share</button>
      <button class="text-button" @click="stopShare" :disabled="!stream">
        Stop
      </button>
    </div>
    <p class="text-sm text-slate-500 mt-3">
      Uses browser screen capture API for provider integration demo.
    </p>
  </div>
</template>

<style scoped>
.glass-card {
  background: white;
  border-radius: 8px;
  padding: 12px;
  box-shadow: 0 6px 20px rgba(2, 6, 23, 0.06);
}
.primary-button {
  background: #2563eb;
  color: white;
  padding: 8px 12px;
  border-radius: 6px;
}
.text-button {
  background: transparent;
  border: 1px solid #cbd5e1;
  padding: 8px 12px;
  border-radius: 6px;
}
</style>
