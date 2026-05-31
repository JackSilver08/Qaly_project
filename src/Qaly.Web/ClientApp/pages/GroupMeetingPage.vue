<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from "vue";
import {
  CalendarDays,
  Copy,
  Info,
  MessageCircle,
  Moon,
  MonitorUp,
  ShieldCheck,
  Sparkles,
  Sun,
  Users,
  VideoOff,
} from "lucide-vue-next";
import {
  HubConnectionBuilder,
  HubConnectionState,
  type HubConnection,
} from "@microsoft/signalr";
import ScreenSharePanel from "../components/meeting/ScreenSharePanel.vue";
import MeetingControls from "../components/meeting/MeetingControls.vue";
import { apiResult } from "../utils/api-client";
import { showError, showSuccess } from "../composables/use-toast";
import { useRoute, useRouter } from "vue-router";

const route = useRoute();
const router = useRouter();
const groupId = route.params.groupId as string;

const active = ref(false);
const isStarting = ref(false);
const meetingId = ref<string | null>((route.query.meetingId as string | undefined) ?? null);
const joinUrl = ref<string | null>(null);
const roomName = ref<string | null>(null);
const micMuted = ref(false);
const cameraMuted = ref(false);
const theme = ref<"dark" | "light">(
  localStorage.getItem("qaly-meeting-theme") === "light" ? "light" : "dark",
);
const localVideoRef = ref<HTMLVideoElement | null>(null);
const screenShareRef = ref<InstanceType<typeof ScreenSharePanel> | null>(null);
const participants = ref<{ connectionId: string; lastSeen: string }[]>([]);
let hubConnection: HubConnection | null = null;
let localStream: MediaStream | null = null;

const meetingShortCode = computed(() =>
  groupId ? groupId.slice(0, 8).toUpperCase() : "QALY-MEET",
);

const participantCount = computed(() => participants.value.length + (active.value ? 1 : 0));
const isLightTheme = computed(() => theme.value === "light");

onMounted(async () => {
  if (meetingId.value) {
    await joinExistingMeeting(meetingId.value);
  }
});

async function startMeeting() {
  isStarting.value = true;

  try {
    const dto = await apiResult<any>(`/api/groups/${groupId}/meetings/start`, {
      method: "POST",
    });
    meetingId.value = dto?.id ?? dto?.Id ?? null;
    joinUrl.value = dto?.joinUrl ?? dto?.JoinUrl ?? null;
    roomName.value = dto?.roomId ?? dto?.RoomId ?? `qaly-${groupId}`;
  } catch (e) {
    console.warn("Could not start meeting session via API", e);
    showError("Không thể tạo phiên họp.");
    isStarting.value = false;
    return;
  } finally {
    isStarting.value = false;
  }

  active.value = true;
  await nextTick();
  await connectRealtime();
  await startLocalMedia();
}

async function joinExistingMeeting(id: string) {
  isStarting.value = true;

  try {
    const dto = await apiResult<any>(`/api/groups/${groupId}/meetings/${id}/join`, {
      method: "POST",
    });
    meetingId.value = dto?.id ?? dto?.Id ?? id;
    joinUrl.value = dto?.joinUrl ?? dto?.JoinUrl ?? null;
    roomName.value = dto?.roomId ?? dto?.RoomId ?? `qaly-${groupId}`;
    active.value = true;
    await nextTick();
    await connectRealtime();
    await startLocalMedia();
  } catch (e) {
    console.warn("Could not join meeting session via API", e);
    showError("Không thể tham gia cuộc họp.");
  } finally {
    isStarting.value = false;
  }
}

async function connectRealtime() {
  if (hubConnection?.state === HubConnectionState.Connected) return;

  hubConnection = new HubConnectionBuilder()
    .withUrl("/hubs/groups")
    .withAutomaticReconnect()
    .build();

  hubConnection.on("peerSignal", (payload: any) => {
    const from = payload?.from;
    if (!from) return;

    const existing = participants.value.find((p) => p.connectionId === from);
    if (!existing) {
      participants.value.push({
        connectionId: from,
        lastSeen: new Date().toISOString(),
      });
    } else {
      existing.lastSeen = new Date().toISOString();
    }
  });

  try {
    await hubConnection.start();
    if (hubConnection.state === HubConnectionState.Connected) {
      await hubConnection.invoke("JoinGroup", groupId).catch(() => undefined);
    }
  } catch (e) {
    console.warn("Could not start group hub", e);
  }
}

async function endMeeting() {
  const endingMeetingId = meetingId.value;
  if (endingMeetingId) {
    await apiResult(`/api/groups/${groupId}/meetings/${endingMeetingId}/end`, {
      method: "POST",
    }).catch(() => undefined);
  }

  if (hubConnection && hubConnection.state === HubConnectionState.Connected) {
    try {
      await hubConnection.invoke("LeaveGroup", groupId).catch(() => undefined);
      await hubConnection.stop();
    } catch {}
    hubConnection = null;
  }

  active.value = false;
  participants.value = [];
  meetingId.value = null;
  joinUrl.value = null;
  roomName.value = null;
  micMuted.value = false;
  cameraMuted.value = false;
  stopLocalMedia();
}

async function copyMeetingLink() {
  const text = window.location.href;
  try {
    await navigator.clipboard.writeText(text);
    showSuccess("Đã sao chép link cuộc họp");
  } catch {
    showError("Không thể sao chép link.");
  }
}

function openScreenShare() {
  screenShareRef.value?.startShare();
}

function toggleTheme() {
  theme.value = isLightTheme.value ? "dark" : "light";
  localStorage.setItem("qaly-meeting-theme", theme.value);
}

function toggleMic() {
  micMuted.value = !micMuted.value;
  localStream?.getAudioTracks().forEach((track) => {
    track.enabled = !micMuted.value;
  });
}

function toggleCamera() {
  cameraMuted.value = !cameraMuted.value;
  localStream?.getVideoTracks().forEach((track) => {
    track.enabled = !cameraMuted.value;
  });
}

function formatTime(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "vừa xong";
  return new Intl.DateTimeFormat("vi", {
    hour: "2-digit",
    minute: "2-digit",
  }).format(date);
}

onBeforeUnmount(async () => {
  if (hubConnection) {
    try {
      if (hubConnection.state === HubConnectionState.Connected) {
        await hubConnection.invoke("LeaveGroup", groupId).catch(() => undefined);
      }
      await hubConnection.stop();
    } catch {}
    hubConnection = null;
  }
  stopLocalMedia();
});

async function startLocalMedia() {
  try {
    stopLocalMedia();
    localStream = await navigator.mediaDevices.getUserMedia({
      audio: true,
      video: {
        width: { ideal: 1280 },
        height: { ideal: 720 },
      },
    });
    if (localVideoRef.value) {
      localVideoRef.value.srcObject = localStream;
    }
  } catch (e) {
    console.warn("Could not start local media", e);
    showError("Không thể mở camera/micro. Bạn vẫn có thể ở trong phòng.");
    cameraMuted.value = true;
    micMuted.value = true;
  }
}

function stopLocalMedia() {
  localStream?.getTracks().forEach((track) => track.stop());
  localStream = null;
  if (localVideoRef.value) {
    localVideoRef.value.srcObject = null;
  }
}
</script>

<template>
  <div class="meeting-page" :class="{ 'meeting-page--light': isLightTheme }">
    <section class="meeting-room">
      <header class="meeting-room__header">
        <button class="meeting-back" type="button" @click="router.push({ name: 'group-detail', params: { groupId } })">
          <MessageCircle :size="16" />
          Trở lại trò chuyện
        </button>
        <div>
          <span>Qaly Meet</span>
          <h1>Phòng họp nhóm</h1>
        </div>
        <div class="meeting-status" :class="{ active }">
          <span></span>
          {{ active ? "Đang họp" : "Sẵn sàng" }}
        </div>
      </header>

      <div class="meeting-layout">
        <main class="meeting-stage">
          <div class="meeting-stage__topbar">
            <div class="meeting-code">
              <CalendarDays :size="16" />
              <span>{{ meetingShortCode }}</span>
            </div>
            <div class="meeting-stage__actions">
              <button class="stage-action" type="button" @click="toggleTheme">
                <Moon v-if="isLightTheme" :size="15" />
                <Sun v-else :size="15" />
                {{ isLightTheme ? "Giao diện tối" : "Giao diện sáng" }}
              </button>
              <button class="stage-action" type="button" @click="copyMeetingLink">
                <Copy :size="15" />
                Sao chép link
              </button>
            </div>
          </div>

          <div v-if="active && roomName" class="meeting-frame-shell">
            <div class="qaly-meet-stage">
              <video
                v-show="!cameraMuted"
                ref="localVideoRef"
                class="local-video"
                autoplay
                playsinline
                muted
              ></video>
              <div v-if="cameraMuted" class="camera-off-state">
                <div class="meeting-avatar meeting-avatar--large">QT</div>
                <span><VideoOff :size="18" /> Camera đang tắt</span>
              </div>
              <div class="meeting-brand-chip">QALY Meet</div>
              <div class="meeting-live-chip">
                <span></span>
                {{ micMuted ? "Mic tắt" : "Mic bật" }} · {{ cameraMuted ? "Camera tắt" : "Camera bật" }}
              </div>
            </div>
          </div>

          <div v-else class="meeting-prejoin">
            <div class="meeting-orbit">
              <div class="meeting-avatar">QT</div>
            </div>
            <span class="meeting-eyebrow">
              <Sparkles :size="15" /> Không gian họp Qaly
            </span>
            <h2>{{ isStarting ? "Đang chuẩn bị phòng họp..." : "Sẵn sàng bắt đầu cuộc họp" }}</h2>
            <p>Kiểm tra camera, chia sẻ màn hình hoặc vào phòng họp cho nhóm này.</p>
            <button class="meeting-start-button" type="button" @click="startMeeting" :disabled="isStarting">
              Bắt đầu cuộc họp
            </button>
          </div>

          <div class="meeting-control-dock">
            <MeetingControls
              :active="active"
              :mic-muted="micMuted"
              :camera-muted="cameraMuted"
              :light="isLightTheme"
              @start="startMeeting"
              @end="endMeeting"
              @share="openScreenShare"
              @toggle-mic="toggleMic"
              @toggle-camera="toggleCamera"
            />
          </div>
        </main>

        <aside class="meeting-side">
          <article class="meeting-info-card">
            <div class="meeting-info-card__icon">
              <ShieldCheck :size="20" />
            </div>
            <div>
              <span>Bảo mật cuộc họp</span>
              <strong>{{ participantCount }} người trong phòng</strong>
              <p>Room id: {{ groupId }}</p>
            </div>
          </article>

          <ScreenSharePanel ref="screenShareRef" />

          <article class="participants-card">
            <header>
              <div>
                <span>Thành viên</span>
                <strong>Thành viên</strong>
              </div>
              <div class="participant-count">
                <Users :size="15" /> {{ participantCount }}
              </div>
            </header>

            <div class="participant-list">
              <div class="participant-row">
                <div class="participant-avatar">QT</div>
                <div>
                  <strong>Bạn</strong>
                  <span>Chủ phòng · trực tuyến</span>
                </div>
              </div>
              <div v-for="p in participants" :key="p.connectionId" class="participant-row">
                <div class="participant-avatar participant-avatar--soft">
                  {{ p.connectionId.slice(0, 2).toUpperCase() }}
                </div>
                <div>
                  <strong>{{ p.connectionId.slice(0, 10) }}</strong>
                  <span>Hoạt động {{ formatTime(p.lastSeen) }}</span>
                </div>
              </div>
              <div v-if="participants.length === 0" class="participants-empty">
                <Info :size="20" />
                <span>Chưa có thành viên khác tham gia.</span>
              </div>
            </div>
          </article>

          <article class="meeting-tip">
            <MonitorUp :size="18" />
            <span>Dùng nút chia sẻ màn hình ở dock dưới để trình bày nhanh.</span>
          </article>
        </aside>
      </div>
    </section>
  </div>
</template>

<style scoped>
.meeting-page {
  --meet-page-bg:
    radial-gradient(circle at 16% 4%, rgba(37, 99, 235, 0.13), transparent 28%),
    radial-gradient(circle at 88% 12%, rgba(20, 184, 166, 0.1), transparent 26%),
    #f8fafc;
  --meet-stage-bg:
    radial-gradient(circle at 50% 0%, rgba(37, 99, 235, 0.24), transparent 34%),
    linear-gradient(135deg, #020617, #101827 55%, #111827);
  --meet-tile-bg:
    radial-gradient(circle at 50% 42%, rgba(20, 184, 166, 0.22), transparent 24%),
    linear-gradient(135deg, #050816, #020617 55%, #111827);
  --meet-chip-bg: rgba(15, 23, 42, 0.62);
  --meet-chip-color: #e2e8f0;
  --meet-stage-border: rgba(15, 23, 42, 0.12);
  --meet-heading-color: #f8fafc;
  --meet-copy-color: #94a3b8;
  --meet-eyebrow-bg: rgba(255, 255, 255, 0.09);
  --meet-eyebrow-color: #bfdbfe;
  min-height: calc(100dvh - 74px);
  padding: 18px;
  background: var(--meet-page-bg);
}

.meeting-page--light {
  --meet-page-bg:
    radial-gradient(circle at 12% 2%, rgba(14, 165, 233, 0.18), transparent 26%),
    radial-gradient(circle at 92% 8%, rgba(34, 197, 94, 0.12), transparent 24%),
    #eef4ff;
  --meet-stage-bg:
    linear-gradient(135deg, #f8fbff, #eaf2ff 54%, #f8fafc);
  --meet-tile-bg:
    radial-gradient(circle at 50% 42%, rgba(59, 130, 246, 0.18), transparent 26%),
    linear-gradient(135deg, #ffffff, #edf4ff 58%, #f8fafc);
  --meet-chip-bg: rgba(255, 255, 255, 0.76);
  --meet-chip-color: #0f172a;
  --meet-stage-border: rgba(148, 163, 184, 0.32);
  --meet-heading-color: #0f172a;
  --meet-copy-color: #64748b;
  --meet-eyebrow-bg: rgba(37, 99, 235, 0.1);
  --meet-eyebrow-color: #1d4ed8;
}

.meeting-room {
  min-height: calc(100dvh - 110px);
  display: grid;
  grid-template-rows: auto minmax(0, 1fr);
  gap: 16px;
}

.meeting-room__header,
.meeting-layout,
.meeting-stage__topbar,
.meeting-info-card,
.participants-card header,
.participant-row,
.meeting-tip {
  display: flex;
  align-items: center;
}

.meeting-room__header {
  justify-content: space-between;
  gap: 16px;
}

.meeting-room__header span {
  color: #64748b;
  font-size: 0.78rem;
  font-weight: 900;
  text-transform: uppercase;
}

.meeting-room__header h1 {
  margin: 3px 0 0;
  color: #0f172a;
  font-size: 1.45rem;
}

.meeting-back,
.stage-action {
  border: 1px solid rgba(203, 213, 225, 0.82);
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.82);
  color: #334155;
  font-weight: 800;
  cursor: pointer;
}

.meeting-back {
  padding: 10px 16px;
}

.meeting-status {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  border-radius: 999px;
  padding: 9px 13px;
  background: #e2e8f0;
  color: #475569;
  font-size: 0.82rem;
  font-weight: 900;
}

.meeting-status span {
  width: 9px;
  height: 9px;
  border-radius: 999px;
  background: #94a3b8;
}

.meeting-status.active {
  background: #dcfce7;
  color: #166534;
}

.meeting-status.active span {
  background: #22c55e;
}

.meeting-layout {
  align-items: stretch;
  gap: 18px;
  min-height: 0;
}

.meeting-stage {
  position: relative;
  min-width: 0;
  flex: 1;
  overflow: hidden;
  border: 1px solid var(--meet-stage-border);
  border-radius: 28px;
  background: var(--meet-stage-bg);
  box-shadow: 0 34px 90px rgba(15, 23, 42, 0.2);
}

.meeting-stage__topbar {
  position: absolute;
  inset: 18px 18px auto 18px;
  z-index: 3;
  justify-content: space-between;
}

.meeting-stage__actions {
  display: inline-flex;
  align-items: center;
  gap: 10px;
}

.meeting-code,
.stage-action {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 10px 13px;
  background: var(--meet-chip-bg);
  border-color: rgba(255, 255, 255, 0.1);
  color: var(--meet-chip-color);
  backdrop-filter: blur(16px);
}

.meeting-frame-shell {
  height: 100%;
  min-height: 640px;
  padding: 72px 18px 100px;
}

.qaly-meet-stage {
  position: relative;
  width: 100%;
  height: 100%;
  min-height: 520px;
  border: 0;
  border-radius: 22px;
  background: var(--meet-tile-bg);
  overflow: hidden;
}

.local-video {
  width: 100%;
  height: 100%;
  min-height: 520px;
  object-fit: cover;
  transform: scaleX(-1);
}

.camera-off-state {
  min-height: 520px;
  display: grid;
  place-items: center;
  align-content: center;
  gap: 18px;
  color: var(--meet-chip-color);
}

.meeting-avatar--large {
  width: 148px;
  height: 148px;
  font-size: 3.4rem;
  box-shadow:
    0 0 0 18px rgba(255, 255, 255, 0.045),
    0 34px 90px rgba(37, 99, 235, 0.22);
}

.camera-off-state span,
.meeting-brand-chip,
.meeting-live-chip {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  border-radius: 999px;
  padding: 9px 13px;
  background: var(--meet-chip-bg);
  color: var(--meet-chip-color);
  font-size: 0.82rem;
  font-weight: 900;
  backdrop-filter: blur(16px);
}

.meeting-brand-chip {
  position: absolute;
  left: 18px;
  top: 18px;
}

.meeting-live-chip {
  position: absolute;
  right: 18px;
  top: 18px;
}

.meeting-live-chip span {
  width: 8px;
  height: 8px;
  border-radius: 999px;
  background: #22c55e;
  box-shadow: 0 0 0 5px rgba(34, 197, 94, 0.13);
}

.meeting-prejoin {
  min-height: 640px;
  display: grid;
  place-items: center;
  align-content: center;
  gap: 14px;
  padding: 80px 28px 120px;
  text-align: center;
  color: var(--meet-heading-color);
}

.meeting-orbit {
  width: 172px;
  height: 172px;
  display: grid;
  place-items: center;
  border-radius: 999px;
  background:
    linear-gradient(135deg, rgba(37, 99, 235, 0.32), rgba(20, 184, 166, 0.18)),
    rgba(255, 255, 255, 0.08);
  box-shadow:
    0 0 0 18px rgba(255, 255, 255, 0.035),
    0 34px 80px rgba(37, 99, 235, 0.18);
}

.meeting-avatar {
  width: 104px;
  height: 104px;
  display: grid;
  place-items: center;
  border-radius: 999px;
  background: linear-gradient(135deg, #1d4ed8, #60a5fa);
  color: #fff;
  font-size: 2rem;
  font-weight: 950;
}

.meeting-eyebrow {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  border-radius: 999px;
  padding: 8px 12px;
  background: var(--meet-eyebrow-bg);
  color: var(--meet-eyebrow-color);
  font-size: 0.78rem;
  font-weight: 900;
}

.meeting-prejoin h2 {
  margin: 0;
  color: var(--meet-heading-color);
  font-size: clamp(2rem, 4vw, 3.6rem);
  line-height: 1;
}

.meeting-prejoin p {
  max-width: 520px;
  margin: 0;
  color: var(--meet-copy-color);
}

.meeting-start-button {
  min-height: 48px;
  border: 0;
  border-radius: 999px;
  padding: 0 22px;
  background: #2563eb;
  color: #fff;
  font-weight: 900;
  cursor: pointer;
  box-shadow: 0 20px 40px rgba(37, 99, 235, 0.26);
}

.meeting-control-dock {
  position: absolute;
  left: 50%;
  bottom: 20px;
  z-index: 4;
  transform: translateX(-50%);
}

.meeting-side {
  width: min(380px, 28vw);
  min-width: 320px;
  display: grid;
  align-content: start;
  gap: 14px;
}

.meeting-info-card,
.participants-card,
.meeting-tip {
  border: 1px solid rgba(203, 213, 225, 0.76);
  border-radius: 22px;
  background: rgba(255, 255, 255, 0.92);
  box-shadow: 0 24px 58px rgba(15, 23, 42, 0.08);
  backdrop-filter: blur(18px);
}

.meeting-info-card {
  gap: 13px;
  padding: 16px;
}

.meeting-info-card__icon {
  width: 44px;
  height: 44px;
  display: grid;
  place-items: center;
  border-radius: 16px;
  background: #eff6ff;
  color: #2563eb;
}

.meeting-info-card span,
.participants-card header span {
  color: #64748b;
  font-size: 0.72rem;
  font-weight: 900;
  text-transform: uppercase;
}

.meeting-info-card strong,
.participants-card header strong {
  display: block;
  color: #0f172a;
  font-size: 1rem;
}

.meeting-info-card p {
  max-width: 250px;
  margin: 2px 0 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: #64748b;
  font-size: 0.78rem;
}

.participants-card {
  display: grid;
  gap: 12px;
  padding: 16px;
}

.participants-card header {
  justify-content: space-between;
}

.participant-count {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  border-radius: 999px;
  padding: 7px 10px;
  background: #f1f5f9;
  color: #334155;
  font-weight: 900;
}

.participant-list {
  display: grid;
  gap: 10px;
}

.participant-row {
  gap: 10px;
  padding: 10px;
  border: 1px solid rgba(226, 232, 240, 0.9);
  border-radius: 16px;
  background: #fff;
}

.participant-avatar {
  width: 38px;
  height: 38px;
  display: grid;
  place-items: center;
  border-radius: 999px;
  background: linear-gradient(135deg, #1d4ed8, #60a5fa);
  color: #fff;
  font-size: 0.78rem;
  font-weight: 950;
}

.participant-avatar--soft {
  background: #e2e8f0;
  color: #334155;
}

.participant-row strong {
  display: block;
  color: #0f172a;
}

.participant-row span,
.participants-empty span {
  color: #64748b;
  font-size: 0.82rem;
}

.participants-empty {
  display: grid;
  place-items: center;
  gap: 8px;
  min-height: 120px;
  border: 1px dashed rgba(148, 163, 184, 0.48);
  border-radius: 18px;
  text-align: center;
  color: #64748b;
}

.meeting-tip {
  gap: 10px;
  padding: 14px;
  color: #475569;
  font-size: 0.86rem;
  font-weight: 750;
}

@media (max-width: 1180px) {
  .meeting-layout {
    display: grid;
  }

  .meeting-side {
    width: 100%;
    min-width: 0;
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 760px) {
  .meeting-page {
    padding: 10px;
  }

  .meeting-room__header {
    align-items: flex-start;
  }

  .meeting-side {
    grid-template-columns: 1fr;
  }

  .meeting-frame-shell,
  .meeting-prejoin {
    min-height: 560px;
  }
}
</style>
