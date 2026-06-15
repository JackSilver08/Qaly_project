<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from "vue";
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
import {
  ConnectionState,
  createLocalTracks,
  Room,
  RoomEvent,
  Track,
  type LocalAudioTrack,
  type LocalTrack,
  type LocalVideoTrack,
  type RemoteParticipant,
  type RemoteTrack,
  type RemoteTrackPublication,
} from "livekit-client";
import ScreenSharePanel from "../components/meeting/ScreenSharePanel.vue";
import MeetingControls from "../components/meeting/MeetingControls.vue";
import { apiResult } from "../utils/api-client";
import { showError, showSuccess } from "../composables/use-toast";
import { useRoute, useRouter } from "vue-router";
import { useSpeechRecognition } from "../composables/use-speech-recognition";
import { useMeetingRecovery } from "../composables/use-meeting-recovery";
import { useDashboardContext } from "../composables/dashboard-context";

const route = useRoute();
const router = useRouter();
const groupId = route.params.groupId as string;

const active = ref(false);
const isStarting = ref(false);
const meetingId = ref<string | null>((route.query.meetingId as string | undefined) ?? null);
const joinUrl = ref<string | null>(null);
const roomName = ref<string | null>(null);
const liveKitUrl = ref<string | null>(null);
const liveKitToken = ref<string | null>(null);
const liveKitTokenExpiresAt = ref<string | null>(null);
const micMuted = ref(false);
const cameraMuted = ref(false);
const meetingError = ref<string | null>(null);
const meetingConnectionState = ref<ConnectionState>(ConnectionState.Disconnected);
const theme = ref<"dark" | "light">(
  localStorage.getItem("qaly-meeting-theme") === "light" ? "light" : "dark",
);
const localVideoRef = ref<HTMLVideoElement | null>(null);
const screenShareRef = ref<InstanceType<typeof ScreenSharePanel> | null>(null);
const participants = ref<{ connectionId: string; lastSeen: string }[]>([]);
type RemoteTile = {
  identity: string;
  name: string;
  initials: string;
  cameraOn: boolean;
  micOn: boolean;
  lastSeen: string;
  videoTrack: RemoteTrack | null;
  audioTrack: RemoteTrack | null;
};
const remoteTiles = ref<RemoteTile[]>([]);
let hubConnection: HubConnection | null = null;
let liveKitRoom: Room | null = null;
let localAudioTrack: LocalAudioTrack | null = null;
let localVideoTrack: LocalVideoTrack | null = null;
const remoteVideoEls = new Map<string, HTMLVideoElement>();
const remoteAudioEls = new Map<string, HTMLAudioElement>();

// --- AI Auto Checknote States ---
const activeSidebarTab = ref<"participants" | "transcript" | "checknote">("participants");
const transcriptList = ref<{ senderName: string; text: string; timestamp: number }[]>([]);
const editingTranscriptIdx = ref<number | null>(null);
const editingTranscriptText = ref("");
const transcriptEditInputRef = ref<HTMLInputElement | null>(null);
const selectedProjectId = ref("");
const isGeneratingChecknote = ref(false);
const checknoteResult = ref<any>(null);
const projectMembers = ref<any[]>([]);
const isCreatingTask = ref<number | null>(null);

const { saveBuffer, getBuffer, clearBuffer } = useMeetingRecovery();
const { currentUser, dashboard } = useDashboardContext();

const currentUserName = computed(() => {
  return currentUser.value?.fullName || currentUser.value?.email || "Thành viên";
});

const projects = computed(() => {
  return dashboard.value?.projects || [];
});

const speechRec = useSpeechRecognition((text, isFinal) => {
  if (isFinal) {
    const now = Date.now();
    if (hubConnection && hubConnection.state === HubConnectionState.Connected && meetingId.value) {
      hubConnection.invoke("SendMeetingSignal", groupId, meetingId.value, {
        type: "speech-transcript",
        senderName: currentUserName.value,
        text: text,
        clientTimestamp: now,
      }).catch((err) => console.warn("Failed to send speech transcript:", err));
    }
    appendLocalTranscript(currentUserName.value, text, now);
  } else {
    liveCaptionText.value = text;
    clearTimeout(liveCaptionTimeoutId);
    liveCaptionTimeoutId = window.setTimeout(() => {
      liveCaptionText.value = "";
    }, 4000);
  }
});

const isSpeechListening = computed(() => speechRec.isListening.value);
const liveCaptionText = ref("");
let liveCaptionTimeoutId: number | undefined;

function toggleSpeech() {
  if (speechRec.isListening.value) {
    speechRec.stop();
  } else {
    speechRec.start();
  }
}

function appendLocalTranscript(senderName: string, text: string, timestamp: number) {
  transcriptList.value.push({ senderName, text, timestamp });
  transcriptList.value.sort((a, b) => a.timestamp - b.timestamp);
  if (meetingId.value) {
    saveBuffer(meetingId.value, transcriptList.value);
  }
}

function editTranscriptMessage(idx: number) {
  editingTranscriptIdx.value = idx;
  editingTranscriptText.value = transcriptList.value[idx].text;
  nextTick(() => {
    transcriptEditInputRef.value?.focus();
  });
}

function saveTranscriptMessage(idx: number) {
  if (editingTranscriptIdx.value !== idx) return;
  const val = editingTranscriptText.value.trim();
  if (val) {
    transcriptList.value[idx].text = val;
    if (meetingId.value) {
      saveBuffer(meetingId.value, transcriptList.value);
    }
  }
  editingTranscriptIdx.value = null;
  editingTranscriptText.value = "";
}

function formatDateForInput(dateStr: string | null) {
  if (!dateStr) return "";
  const date = new Date(dateStr);
  if (Number.isNaN(date.getTime())) return "";
  const yyyy = date.getFullYear();
  const mm = String(date.getMonth() + 1).padStart(2, "0");
  const dd = String(date.getDate()).padStart(2, "0");
  return `${yyyy}-${mm}-${dd}`;
}

async function generateChecknote() {
  if (!selectedProjectId.value || transcriptList.value.length === 0 || !meetingId.value) return;

  isGeneratingChecknote.value = true;
  try {
    const compiledTranscriptText = transcriptList.value
      .map((log) => `[${formatTime(log.timestamp)}] ${log.senderName}: ${log.text}`)
      .join("\n");

    const participantsList = remoteTiles.value.map((tile) => tile.name);
    participantsList.push(currentUserName.value);

    const result = await apiResult<any>(`/api/meetings/${meetingId.value}/auto-checknote`, {
      method: "POST",
      body: JSON.stringify({
        projectId: selectedProjectId.value,
        title: `Biên bản họp - ${new Date().toLocaleDateString("vi-VN")}`,
        transcriptText: compiledTranscriptText,
        participants: participantsList,
      }),
    });

    if (result && result.actionItems) {
      checknoteResult.value = {
        meetingImportId: result.meetingImportId,
        summary: result.summary,
        actionItems: result.actionItems.map((item: any) => ({
          ...item,
          dueDateFormatted: formatDateForInput(item.dueDate),
          assigneeId: "",
        })),
      };
      showSuccess("Đã tạo biên bản AI thành công.");
    } else {
      showError("AI không trả về kết quả hợp lệ.");
    }
  } catch (e: any) {
    console.error(e);
    showError(e?.message || "Không thể phân tích cuộc họp qua AI.");
  } finally {
    isGeneratingChecknote.value = false;
  }
}

async function createTaskFromCard(idx: number) {
  if (!checknoteResult.value || !checknoteResult.value.actionItems[idx]) return;

  const card = checknoteResult.value.actionItems[idx];
  if (!card.title.trim()) {
    showError("Tiêu đề công việc không được để trống.");
    return;
  }

  isCreatingTask.value = idx;
  try {
    const importId = checknoteResult.value.meetingImportId;
    const body = {
      assigneeId: card.assigneeId || null,
      title: card.title.trim(),
      description: card.description || "",
      priority: card.priority || "Medium",
      dueDate: card.dueDateFormatted ? new Date(card.dueDateFormatted).toISOString() : null,
      labelIds: [],
    };

    const result = await apiResult<any>(`/api/meetings/${importId}/action-items/${idx}/create-task`, {
      method: "POST",
      body: JSON.stringify(body),
    });

    if (result) {
      card.mappingStatus = "Linked";
      showSuccess("Đã tạo Task thành công.");
    }
  } catch (e: any) {
    console.error(e);
    showError(e?.message || "Không thể tạo Task từ Action Item.");
  } finally {
    isCreatingTask.value = null;
  }
}

function resetChecknote() {
  checknoteResult.value = null;
}

watch(selectedProjectId, async (newVal) => {
  if (newVal) {
    try {
      const result = await apiResult<any[]>(`/api/projects/${newVal}/members`);
      projectMembers.value = result || [];
    } catch {
      projectMembers.value = [];
    }
  } else {
    projectMembers.value = [];
  }
});

const meetingShortCode = computed(() =>
  groupId ? groupId.slice(0, 8).toUpperCase() : "QALY-MEET",
);

const participantCount = computed(() => remoteTiles.value.length + (active.value ? 1 : 0));
const stageTileCount = computed(() => remoteTiles.value.length + 1);
const isLightTheme = computed(() => theme.value === "light");
const meetingConnectionLabel = computed(() => {
  if (meetingError.value) return "Cần kiểm tra kết nối";

  switch (meetingConnectionState.value) {
    case ConnectionState.Connected:
      return "LiveKit đã kết nối";
    case ConnectionState.Connecting:
      return "Đang kết nối LiveKit";
    case ConnectionState.Reconnecting:
    case ConnectionState.SignalReconnecting:
      return "Đang nối lại LiveKit";
    default:
      return active.value ? "LiveKit chưa kết nối" : "Sẵn sàng";
  }
});

onMounted(async () => {
  if (meetingId.value) {
    await joinExistingMeeting(meetingId.value);
    // Load recovery transcript from IndexedDB
    const recovered = await getBuffer(meetingId.value);
    if (recovered && recovered.length > 0) {
      transcriptList.value = recovered;
      showSuccess(`Đã khôi phục ${recovered.length} đoạn hội thoại từ bộ nhớ đệm.`);
    }
  }
});

async function startMeeting() {
  isStarting.value = true;

  try {
    const dto = await apiResult<any>(`/api/groups/${groupId}/meetings/start`, {
      method: "POST",
    });
    applyMeetingDto(dto);
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
  await connectLiveKit();
}

async function joinExistingMeeting(id: string) {
  isStarting.value = true;

  try {
    const dto = await apiResult<any>(`/api/groups/${groupId}/meetings/${id}/join`, {
      method: "POST",
    });
    applyMeetingDto(dto, id);
    active.value = true;
    await nextTick();
    await connectRealtime();
    await connectLiveKit();
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

  hubConnection.onreconnected(async () => {
    await joinRealtimeGroups();
  });

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

  hubConnection.on("meetingPeerSignal", (payload: any) => {
    const signal = payload?.payload;
    if (signal?.type === "speech-transcript") {
      appendLocalTranscript(signal.senderName, signal.text, signal.clientTimestamp || Date.now());
    }
  });

  hubConnection.on("meetingParticipantJoined", (payload: any) => {
    const from = payload?.connectionId;
    if (!from || from === hubConnection?.connectionId) return;

    const existing = participants.value.find((p) => p.connectionId === from);
    if (!existing) {
      participants.value.push({
        connectionId: from,
        lastSeen: new Date().toISOString(),
      });
    } else {
      existing.lastSeen = new Date().toISOString();
    }

    if (meetingConnectionState.value !== ConnectionState.Connected) {
      const memberName = `Thành viên ${from.slice(0, 4)}`;
      if (!remoteTiles.value.some((t) => t.identity === from)) {
        remoteTiles.value.push({
          identity: from,
          name: memberName,
          initials: memberName.slice(0, 2).toUpperCase(),
          cameraOn: false,
          micOn: false,
          lastSeen: new Date().toISOString(),
          videoTrack: null,
          audioTrack: null,
        });
      }
    }
  });

  hubConnection.on("meetingParticipantLeft", (payload: any) => {
    const from = payload?.connectionId;
    if (!from) return;

    participants.value = participants.value.filter((p) => p.connectionId !== from);
    remoteTiles.value = remoteTiles.value.filter((t) => t.identity !== from);
  });

  hubConnection.on("meetingEnded", (payload: any) => {
    if (payload?.meetingId === meetingId.value) {
      showSuccess("Cuộc họp đã kết thúc bởi chủ phòng.");
      endMeeting();
    }
  });

  try {
    await hubConnection.start();
    if (hubConnection.state === HubConnectionState.Connected) {
      await joinRealtimeGroups();
    }
  } catch (e) {
    console.warn("Could not start group hub", e);
  }
}

async function joinRealtimeGroups() {
  if (!hubConnection || hubConnection.state !== HubConnectionState.Connected) return;

  await hubConnection.invoke("JoinGroup", groupId).catch(() => undefined);
  if (meetingId.value) {
    await hubConnection.invoke("JoinMeeting", groupId, meetingId.value).catch(() => undefined);
  }
}

async function leaveRealtimeGroups() {
  if (!hubConnection || hubConnection.state !== HubConnectionState.Connected) return;

  if (meetingId.value) {
    await hubConnection.invoke("LeaveMeeting", groupId, meetingId.value).catch(() => undefined);
  }
  await hubConnection.invoke("LeaveGroup", groupId).catch(() => undefined);
}

async function endMeeting() {
  speechRec.stop();
  if (meetingId.value) {
    clearBuffer(meetingId.value);
  }
  const endingMeetingId = meetingId.value;
  if (endingMeetingId) {
    await apiResult(`/api/groups/${groupId}/meetings/${endingMeetingId}/end`, {
      method: "POST",
    }).catch(() => undefined);
  }

  if (hubConnection && hubConnection.state === HubConnectionState.Connected) {
    try {
      await leaveRealtimeGroups();
      await hubConnection.stop();
    } catch {}
    hubConnection = null;
  }

  active.value = false;
  participants.value = [];
  remoteTiles.value = [];
  meetingId.value = null;
  joinUrl.value = null;
  roomName.value = null;
  liveKitUrl.value = null;
  liveKitToken.value = null;
  liveKitTokenExpiresAt.value = null;
  meetingError.value = null;
  meetingConnectionState.value = ConnectionState.Disconnected;
  micMuted.value = false;
  cameraMuted.value = false;
  disconnectLiveKit();
}

async function copyMeetingLink() {
  const text = meetingId.value
    ? `${window.location.origin}/groups/${groupId}/meeting?meetingId=${meetingId.value}`
    : window.location.href;
  try {
    await navigator.clipboard.writeText(text);
    showSuccess("Đã sao chép link cuộc họp");
  } catch {
    showError("Không thể sao chép link.");
  }
}

async function openScreenShare() {
  if (liveKitRoom && liveKitRoom.state === ConnectionState.Connected) {
    try {
      await liveKitRoom.localParticipant.setScreenShareEnabled(true);
      showSuccess("Đang chia sẻ màn hình trong phòng họp.");
      return;
    } catch (e) {
      console.warn("Could not start LiveKit screen share", e);
      showError("Không thể chia sẻ màn hình qua LiveKit.");
    }
  }

  screenShareRef.value?.startShare();
}

function toggleTheme() {
  theme.value = isLightTheme.value ? "dark" : "light";
  localStorage.setItem("qaly-meeting-theme", theme.value);
}

async function toggleMic() {
  micMuted.value = !micMuted.value;
  if (!localAudioTrack) return;

  try {
    if (micMuted.value) {
      await localAudioTrack.mute();
    } else {
      await localAudioTrack.unmute();
    }
  } catch (e) {
    console.warn("Could not toggle microphone", e);
    showError("Không thể đổi trạng thái micro.");
  }
}

async function toggleCamera() {
  cameraMuted.value = !cameraMuted.value;
  if (!localVideoTrack) return;

  try {
    if (cameraMuted.value) {
      await localVideoTrack.mute();
    } else {
      await localVideoTrack.unmute();
    }
  } catch (e) {
    console.warn("Could not toggle camera", e);
    showError("Không thể đổi trạng thái camera.");
  }
}

function formatTime(value: string | number) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "vừa xong";
  return new Intl.DateTimeFormat("vi", {
    hour: "2-digit",
    minute: "2-digit",
  }).format(date);
}

onBeforeUnmount(async () => {
  speechRec.stop();
  if (hubConnection) {
    try {
      if (hubConnection.state === HubConnectionState.Connected) {
        await leaveRealtimeGroups();
      }
      await hubConnection.stop();
    } catch {}
    hubConnection = null;
  }
  disconnectLiveKit();
});

function applyMeetingDto(dto: any, fallbackMeetingId: string | null = null) {
  meetingId.value = dto?.id ?? dto?.Id ?? fallbackMeetingId;
  joinUrl.value = dto?.joinUrl ?? dto?.JoinUrl ?? null;
  roomName.value = dto?.roomId ?? dto?.RoomId ?? `qaly-${groupId}`;
  liveKitUrl.value = dto?.providerUrl ?? dto?.ProviderUrl ?? null;
  liveKitToken.value = dto?.accessToken ?? dto?.AccessToken ?? null;
  liveKitTokenExpiresAt.value =
    dto?.accessTokenExpiresAt ?? dto?.AccessTokenExpiresAt ?? null;
}

async function connectLiveKit() {
  meetingError.value = null;
  disconnectLiveKit();

  if (!liveKitUrl.value || !liveKitToken.value) {
    meetingError.value = "LiveKit chưa được cấu hình cho phiên họp này.";
    cameraMuted.value = true;
    micMuted.value = true;
    showError("LiveKit chưa được cấu hình. Kiểm tra appsettings và token meeting.");
    return;
  }

  const room = new Room({
    adaptiveStream: true,
    dynacast: true,
  });
  liveKitRoom = room;

  room.on(RoomEvent.ConnectionStateChanged, (state) => {
    meetingConnectionState.value = state;
  });
  room.on(RoomEvent.Reconnecting, () => {
    meetingConnectionState.value = ConnectionState.Reconnecting;
  });
  room.on(RoomEvent.Reconnected, () => {
    meetingConnectionState.value = ConnectionState.Connected;
  });
  room.on(RoomEvent.Disconnected, () => {
    meetingConnectionState.value = ConnectionState.Disconnected;
  });
  room.on(RoomEvent.ParticipantConnected, (participant) => {
    upsertRemoteParticipant(participant);
  });
  room.on(RoomEvent.ParticipantDisconnected, (participant) => {
    removeRemoteParticipant(participant.identity);
  });
  room.on(RoomEvent.TrackSubscribed, (track, publication, participant) => {
    bindRemoteTrack(participant, publication, track);
  });
  room.on(RoomEvent.TrackUnsubscribed, (track, publication, participant) => {
    unbindRemoteTrack(participant, publication, track);
  });
  room.on(RoomEvent.TrackMuted, (publication, participant) => {
    syncParticipantMediaState(participant.identity, publication as RemoteTrackPublication);
  });
  room.on(RoomEvent.TrackUnmuted, (publication, participant) => {
    syncParticipantMediaState(participant.identity, publication as RemoteTrackPublication);
  });
  room.on(RoomEvent.ParticipantNameChanged, (_name, participant) => {
    if (participant.isLocal) return;
    upsertRemoteParticipant(participant as RemoteParticipant);
  });

  try {
    meetingConnectionState.value = ConnectionState.Connecting;
    await room.connect(liveKitUrl.value, liveKitToken.value);
    meetingConnectionState.value = room.state;
    room.remoteParticipants.forEach((participant) => {
      upsertRemoteParticipant(participant);
      participant.trackPublications.forEach((publication) => {
        if (publication.track) {
          bindRemoteTrack(participant, publication, publication.track as RemoteTrack);
        }
      });
    });
    await publishLocalTracks(room);
  } catch (e) {
    console.warn("Could not connect LiveKit room", e);
    const reason = liveKitErrorMessage(e);
    meetingError.value = reason
      ? `Không kết nối được LiveKit: ${reason}`
      : "Không kết nối được LiveKit. Hãy kiểm tra LiveKit server hoặc cấu hình token.";
    showError("Không kết nối được LiveKit. Phòng vẫn mở nhưng media chưa hoạt động.");
    cameraMuted.value = true;
    micMuted.value = true;
    disconnectLiveKit();
  }
}

async function publishLocalTracks(room: Room) {
  try {
    const tracks = await createLocalTracks({
      audio: true,
      video: {
        resolution: {
          width: 1280,
          height: 720,
        },
      },
    });

    for (const track of tracks) {
      await room.localParticipant.publishTrack(track);
      bindLocalTrack(track);
    }

    micMuted.value = false;
    cameraMuted.value = false;
  } catch (e) {
    console.warn("Could not publish local media", e);
    meetingError.value = "Không thể mở camera/micro. Bạn vẫn có thể ở trong phòng.";
    showError("Không thể mở camera/micro. Kiểm tra quyền trình duyệt.");
    cameraMuted.value = true;
    micMuted.value = true;
  }
}

function bindLocalTrack(track: LocalTrack) {
  if (track.kind === Track.Kind.Audio) {
    localAudioTrack = track as LocalAudioTrack;
    return;
  }

  if (track.kind === Track.Kind.Video) {
    localVideoTrack = track as LocalVideoTrack;
    if (localVideoRef.value) {
      track.attach(localVideoRef.value);
    }
  }
}

function upsertRemoteParticipant(participant: RemoteParticipant) {
  const identity = participant.identity;
  const name = participant.name || participant.metadata || `Khách ${identity.slice(0, 4)}`;
  const existing = remoteTiles.value.find((tile) => tile.identity === identity);

  if (existing) {
    existing.name = name;
    existing.initials = initialsFromName(name);
    existing.cameraOn = participant.isCameraEnabled || participant.isScreenShareEnabled;
    existing.micOn = participant.isMicrophoneEnabled;
    existing.lastSeen = new Date().toISOString();
    return existing;
  }

  const tile: RemoteTile = {
    identity,
    name,
    initials: initialsFromName(name),
    cameraOn: participant.isCameraEnabled || participant.isScreenShareEnabled,
    micOn: participant.isMicrophoneEnabled,
    lastSeen: new Date().toISOString(),
    videoTrack: null,
    audioTrack: null,
  };
  remoteTiles.value.push(tile);
  return tile;
}

function removeRemoteParticipant(identity: string) {
  const tile = remoteTiles.value.find((item) => item.identity === identity);
  tile?.videoTrack?.detach();
  tile?.audioTrack?.detach();
  remoteVideoEls.delete(identity);
  remoteAudioEls.delete(identity);
  remoteTiles.value = remoteTiles.value.filter((item) => item.identity !== identity);
}

function bindRemoteTrack(
  participant: RemoteParticipant,
  publication: RemoteTrackPublication,
  track: RemoteTrack,
) {
  const tile = upsertRemoteParticipant(participant);

  if (track.kind === Track.Kind.Video) {
    tile.videoTrack?.detach();
    tile.videoTrack = track;
    tile.cameraOn = !publication.isMuted;
    const element = remoteVideoEls.get(tile.identity);
    if (element) {
      track.attach(element);
    }
  }

  if (track.kind === Track.Kind.Audio) {
    tile.audioTrack?.detach();
    tile.audioTrack = track;
    tile.micOn = !publication.isMuted;
    const element = remoteAudioEls.get(tile.identity);
    if (element) {
      track.attach(element);
      void element.play().catch(() => undefined);
    }
  }
}

function unbindRemoteTrack(
  participant: RemoteParticipant,
  publication: RemoteTrackPublication,
  track: RemoteTrack,
) {
  const tile = remoteTiles.value.find((item) => item.identity === participant.identity);
  if (!tile) return;

  track.detach();
  if (track.kind === Track.Kind.Video && tile.videoTrack === track) {
    tile.videoTrack = null;
    tile.cameraOn = false;
  }
  if (track.kind === Track.Kind.Audio && tile.audioTrack === track) {
    tile.audioTrack = null;
    tile.micOn = false;
  }
  syncParticipantMediaState(participant.identity, publication);
}

function syncParticipantMediaState(identity: string, publication: RemoteTrackPublication) {
  const tile = remoteTiles.value.find((item) => item.identity === identity);
  if (!tile) return;

  if (
    publication.kind === Track.Kind.Video ||
    publication.source === Track.Source.Camera ||
    publication.source === Track.Source.ScreenShare
  ) {
    tile.cameraOn = !publication.isMuted && Boolean(tile.videoTrack);
  }

  if (
    publication.kind === Track.Kind.Audio ||
    publication.source === Track.Source.Microphone ||
    publication.source === Track.Source.ScreenShareAudio
  ) {
    tile.micOn = !publication.isMuted && Boolean(tile.audioTrack);
  }

  tile.lastSeen = new Date().toISOString();
}

function setRemoteVideoRef(identity: string, element: HTMLVideoElement | null) {
  if (!element) {
    remoteVideoEls.delete(identity);
    return;
  }

  remoteVideoEls.set(identity, element);
  const tile = remoteTiles.value.find((item) => item.identity === identity);
  tile?.videoTrack?.attach(element);
}

function setRemoteAudioRef(identity: string, element: HTMLAudioElement | null) {
  if (!element) {
    remoteAudioEls.delete(identity);
    return;
  }

  remoteAudioEls.set(identity, element);
  const tile = remoteTiles.value.find((item) => item.identity === identity);
  if (tile?.audioTrack) {
    tile.audioTrack.attach(element);
    void element.play().catch(() => undefined);
  }
}

function initialsFromName(value: string) {
  return value
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("") || "QT";
}

function liveKitErrorMessage(error: unknown) {
  if (error instanceof Error) return error.message;
  if (typeof error === "string") return error;
  if (error && typeof error === "object" && "message" in error) {
    return String((error as { message?: unknown }).message ?? "");
  }
  return "";
}

function disconnectLiveKit() {
  remoteTiles.value.forEach((tile) => {
    tile.videoTrack?.detach();
    tile.audioTrack?.detach();
  });
  remoteTiles.value = [];
  remoteVideoEls.clear();
  remoteAudioEls.clear();
  localVideoTrack?.detach();
  localVideoTrack?.stop();
  localAudioTrack?.stop();
  localVideoTrack = null;
  localAudioTrack = null;
  if (localVideoRef.value) {
    localVideoRef.value.srcObject = null;
  }
  liveKitRoom?.removeAllListeners();
  liveKitRoom?.disconnect();
  liveKitRoom = null;
  meetingConnectionState.value = ConnectionState.Disconnected;
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
          {{ meetingConnectionLabel }}
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
            <div class="qaly-meet-stage" :class="`qaly-meet-stage--count-${Math.min(stageTileCount, 4)}`">
              <article class="meeting-video-tile local-participant-tile">
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
                <footer class="meeting-tile-footer">
                  <strong>Bạn</strong>
                  <span>{{ micMuted ? "Mic tắt" : "Mic bật" }}</span>
                </footer>
              </article>

              <article
                v-for="tile in remoteTiles"
                :key="tile.identity"
                class="meeting-video-tile remote-participant-tile"
              >
                <video
                  v-show="tile.cameraOn && tile.videoTrack"
                  :ref="(el) => setRemoteVideoRef(tile.identity, el as HTMLVideoElement | null)"
                  class="remote-video"
                  autoplay
                  playsinline
                ></video>
                <audio
                  :ref="(el) => setRemoteAudioRef(tile.identity, el as HTMLAudioElement | null)"
                  autoplay
                ></audio>
                <div v-if="!tile.cameraOn || !tile.videoTrack" class="camera-off-state">
                  <div class="meeting-avatar meeting-avatar--large">{{ tile.initials }}</div>
                  <span><VideoOff :size="18" /> Camera đang tắt</span>
                </div>
                <footer class="meeting-tile-footer">
                  <strong>{{ tile.name }}</strong>
                  <span>{{ tile.micOn ? "Mic bật" : "Mic tắt" }}</span>
                </footer>
              </article>

              <div class="meeting-brand-chip">QALY Meet</div>
              <div class="meeting-live-chip">
                <span></span>
                {{ participantCount }} người · {{ micMuted ? "Mic tắt" : "Mic bật" }}
              </div>
              <div v-if="meetingError" class="meeting-error-chip">
                {{ meetingError }}
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

          <!-- Live Captions Overlay -->
          <div v-if="liveCaptionText" class="live-captions-overlay">
            <div class="live-caption-inner">{{ liveCaptionText }}</div>
          </div>

          <div class="meeting-control-dock">
            <MeetingControls
              :active="active"
              :mic-muted="micMuted"
              :camera-muted="cameraMuted"
              :speech-active="isSpeechListening"
              :light="isLightTheme"
              @start="startMeeting"
              @end="endMeeting"
              @share="openScreenShare"
              @toggle-mic="toggleMic"
              @toggle-camera="toggleCamera"
              @toggle-speech="toggleSpeech"
            />
          </div>
        </main>

        <aside class="meeting-side">
          <!-- Tab switch header -->
          <div class="sidebar-tabs">
            <button
              class="sidebar-tab"
              :class="{ 'sidebar-tab--active': activeSidebarTab === 'participants' }"
              @click="activeSidebarTab = 'participants'"
            >
              <Users :size="16" /> Thành viên
            </button>
            <button
              class="sidebar-tab"
              :class="{ 'sidebar-tab--active': activeSidebarTab === 'transcript' }"
              @click="activeSidebarTab = 'transcript'"
            >
              <MessageCircle :size="16" /> Nhật ký
            </button>
            <button
              class="sidebar-tab"
              :class="{ 'sidebar-tab--active': activeSidebarTab === 'checknote' }"
              @click="activeSidebarTab = 'checknote'"
            >
              <Sparkles :size="16" /> Biên bản AI
            </button>
          </div>

          <!-- Tab 1: Participants (Original content) -->
          <div v-if="activeSidebarTab === 'participants'" class="tab-pane">
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
                <div v-for="tile in remoteTiles" :key="tile.identity" class="participant-row">
                  <div class="participant-avatar participant-avatar--soft">
                    {{ tile.initials }}
                  </div>
                  <div>
                    <strong>{{ tile.name }}</strong>
                    <span>{{ tile.micOn ? "Mic bật" : "Mic tắt" }} · {{ tile.cameraOn ? "Camera bật" : "Camera tắt" }}</span>
                  </div>
                </div>
                <div v-if="remoteTiles.length === 0" class="participants-empty">
                  <Info :size="20" />
                  <span>Chưa có thành viên khác tham gia.</span>
                </div>
              </div>
            </article>

            <article class="meeting-tip">
              <MonitorUp :size="18" />
              <span>Dùng nút chia sẻ màn hình ở dock dưới để trình bày nhanh.</span>
            </article>
          </div>

          <!-- Tab 2: Transcript (Nhật ký) -->
          <div v-else-if="activeSidebarTab === 'transcript'" class="tab-pane transcript-pane">
            <div class="transcript-log">
              <div
                v-for="(log, idx) in transcriptList"
                :key="idx"
                class="transcript-message"
              >
                <div class="transcript-message-header">
                  <span class="transcript-sender">{{ log.senderName }}</span>
                  <span class="transcript-time">{{ formatTime(log.timestamp) }}</span>
                </div>
                <div
                  class="transcript-text"
                  @dblclick="editTranscriptMessage(idx)"
                  v-if="editingTranscriptIdx !== idx"
                  title="Nhấp đúp chuột để chỉnh sửa"
                >
                  {{ log.text }}
                </div>
                <input
                  v-else
                  type="text"
                  v-model="editingTranscriptText"
                  class="transcript-edit-input"
                  @blur="saveTranscriptMessage(idx)"
                  @keyup.enter="saveTranscriptMessage(idx)"
                  ref="transcriptEditInputRef"
                />
              </div>
              <div v-if="transcriptList.length === 0" class="transcript-empty">
                <Info :size="20" />
                <span>Chưa có nhật ký cuộc họp. Hãy nói gì đó hoặc bật micro/AI ghi chú.</span>
              </div>
            </div>
          </div>

          <!-- Tab 3: AI Checknote (Biên bản AI) -->
          <div v-else-if="activeSidebarTab === 'checknote'" class="tab-pane checknote-pane">
            <!-- Project Selector and Generate Button -->
            <div v-if="!checknoteResult && !isGeneratingChecknote" class="checknote-setup">
              <label class="checknote-label">Chọn dự án để đồng bộ Task:</label>
              <select v-model="selectedProjectId" class="checknote-select">
                <option value="">-- Chọn dự án --</option>
                <option v-for="proj in projects" :key="proj.id" :value="proj.id">
                  {{ proj.name }}
                </option>
              </select>

              <button
                class="checknote-btn-generate"
                @click="generateChecknote"
                :disabled="!selectedProjectId || transcriptList.length === 0"
              >
                <Sparkles :size="16" /> Tạo biên bản AI
              </button>
              <p class="checknote-hint" v-if="transcriptList.length === 0">
                Nhật ký trống, không thể phân tích biên bản.
              </p>
            </div>

            <!-- Loading indicator -->
            <div v-else-if="isGeneratingChecknote" class="checknote-loading">
              <div class="checknote-spinner"></div>
              <span>Đang gọi AI phân tích cuộc họp...</span>
            </div>

            <!-- Results View -->
            <div v-else class="checknote-results">
              <div class="checknote-section">
                <h3>Tóm tắt cuộc họp</h3>
                <p class="checknote-summary">{{ checknoteResult.summary }}</p>
              </div>

              <div class="checknote-section">
                <h3>Danh sách Action Items do AI đề xuất</h3>
                <div class="action-items-list">
                  <div
                    v-for="(item, idx) in checknoteResult.actionItems"
                    :key="idx"
                    class="action-item-card"
                    :class="{ 'action-item-card--linked': item.mappingStatus === 'Linked' }"
                  >
                    <!-- Editable title -->
                    <div class="action-item-title-row">
                      <input
                        type="text"
                        v-model="item.title"
                        class="action-item-input-title"
                        placeholder="Tiêu đề công việc"
                        :disabled="item.mappingStatus === 'Linked'"
                      />
                    </div>
                    <!-- Description -->
                    <textarea
                      v-model="item.description"
                      class="action-item-textarea"
                      placeholder="Mô tả chi tiết..."
                      :disabled="item.mappingStatus === 'Linked'"
                    ></textarea>

                    <!-- Priority & Datepicker -->
                    <div class="action-item-meta-row">
                      <div class="meta-field">
                        <span class="meta-lbl">Ưu tiên</span>
                        <select
                          v-model="item.priority"
                          class="meta-val-select"
                          :disabled="item.mappingStatus === 'Linked'"
                        >
                          <option value="Low">Thấp</option>
                          <option value="Medium">Trung bình</option>
                          <option value="High">Cao</option>
                        </select>
                      </div>

                      <div class="meta-field">
                        <span class="meta-lbl">Hạn chót</span>
                        <input
                          type="date"
                          v-model="item.dueDateFormatted"
                          class="meta-val-date"
                          :disabled="item.mappingStatus === 'Linked'"
                        />
                      </div>
                    </div>

                    <!-- Member Selection -->
                    <div class="action-item-assignee">
                      <span class="meta-lbl">Gán cho</span>
                      <select
                        v-model="item.assigneeId"
                        class="assignee-select"
                        :disabled="item.mappingStatus === 'Linked'"
                      >
                        <option value="">-- Chọn thành viên --</option>
                        <option
                          v-for="m in projectMembers"
                          :key="m.userId"
                          :value="m.userId"
                        >
                          {{ m.fullName }}
                        </option>
                      </select>
                    </div>

                    <!-- Actions -->
                    <div class="action-item-actions">
                      <button
                        v-if="item.mappingStatus !== 'Linked'"
                        class="action-item-btn-create"
                        @click="createTaskFromCard(idx)"
                        :disabled="isCreatingTask === idx"
                      >
                        {{ isCreatingTask === idx ? "Đang tạo..." : "Tạo Task" }}
                      </button>
                      <span v-else class="action-item-linked-badge">
                        ✓ Đã tạo Task trong dự án
                      </span>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Reset/Clear button -->
              <button class="checknote-btn-reset" @click="resetChecknote">
                Phân tích lại / Nhập mới
              </button>
            </div>
          </div>
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
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(360px, 100%), 1fr));
  grid-auto-rows: minmax(220px, 1fr);
  gap: 14px;
  padding: 66px 18px 72px;
  border: 0;
  border-radius: 22px;
  background: var(--meet-tile-bg);
  overflow: hidden;
}

.qaly-meet-stage--count-1 {
  grid-template-columns: 1fr;
}

.qaly-meet-stage--count-2 {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.qaly-meet-stage--count-3,
.qaly-meet-stage--count-4 {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.meeting-video-tile {
  position: relative;
  min-width: 0;
  min-height: 220px;
  overflow: hidden;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 20px;
  background:
    radial-gradient(circle at 50% 35%, rgba(37, 99, 235, 0.15), transparent 28%),
    rgba(2, 6, 23, 0.46);
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.05);
}

.meeting-page--light .meeting-video-tile {
  border-color: rgba(148, 163, 184, 0.28);
  background:
    radial-gradient(circle at 50% 35%, rgba(37, 99, 235, 0.13), transparent 30%),
    rgba(255, 255, 255, 0.72);
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.85);
}

.local-video {
  transform: scaleX(-1);
}

.local-video,
.remote-video {
  width: 100%;
  height: 100%;
  min-height: 220px;
  object-fit: cover;
}

.camera-off-state {
  width: 100%;
  height: 100%;
  min-height: 220px;
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

.meeting-tile-footer {
  position: absolute;
  left: 14px;
  right: 14px;
  bottom: 14px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  border-radius: 999px;
  padding: 9px 12px;
  background: rgba(2, 6, 23, 0.62);
  color: #e2e8f0;
  backdrop-filter: blur(16px);
}

.meeting-page--light .meeting-tile-footer {
  background: rgba(255, 255, 255, 0.78);
  color: #0f172a;
}

.meeting-tile-footer strong,
.meeting-tile-footer span {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.meeting-tile-footer strong {
  font-size: 0.9rem;
}

.meeting-tile-footer span {
  color: inherit;
  opacity: 0.74;
  font-size: 0.78rem;
  font-weight: 900;
}

.camera-off-state span,
.meeting-brand-chip,
.meeting-live-chip,
.meeting-error-chip {
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

.meeting-error-chip {
  position: absolute;
  left: 50%;
  bottom: 22px;
  max-width: min(620px, calc(100% - 40px));
  transform: translateX(-50%);
  background: rgba(254, 242, 242, 0.92);
  color: #991b1b;
  text-align: center;
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


/* --- Sidebar Tabs --- */
.sidebar-tabs {
  display: flex;
  gap: 6px;
  background: rgba(15, 23, 42, 0.08);
  border: 1px solid rgba(148, 163, 184, 0.12);
  border-radius: 12px;
  padding: 4px;
  margin-bottom: 16px;
}

.meeting-page--light .sidebar-tabs {
  background: rgba(255, 255, 255, 0.6);
  border-color: rgba(148, 163, 184, 0.22);
}

.sidebar-tab {
  flex: 1;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 8px 12px;
  border-radius: 9px;
  border: 0;
  background: transparent;
  color: #64748b;
  font-size: 0.8rem;
  font-weight: 800;
  cursor: pointer;
  transition: all 150ms ease;
}

.sidebar-tab--active {
  background: #ffffff;
  color: #2563eb;
  box-shadow: 0 4px 10px rgba(15, 23, 42, 0.05);
}

.meeting-page:not(.meeting-page--light) .sidebar-tab--active {
  background: rgba(255, 255, 255, 0.15);
  color: #ffffff;
}

/* --- Transcript --- */
.transcript-pane {
  display: flex;
  flex-direction: column;
}

.transcript-log {
  display: flex;
  flex-direction: column;
  gap: 12px;
  max-height: 520px;
  overflow-y: auto;
  padding-right: 4px;
}

.transcript-message {
  background: rgba(255, 255, 255, 0.5);
  border: 1px solid rgba(148, 163, 184, 0.18);
  border-radius: 12px;
  padding: 10px 12px;
  font-size: 0.86rem;
  text-align: left;
}

.meeting-page:not(.meeting-page--light) .transcript-message {
  background: rgba(15, 23, 42, 0.4);
  border-color: rgba(255, 255, 255, 0.08);
}

.transcript-message-header {
  display: flex;
  justify-content: space-between;
  margin-bottom: 4px;
}

.transcript-sender {
  font-weight: 900;
  color: #0f172a;
}

.meeting-page:not(.meeting-page--light) .transcript-sender {
  color: #f8fafc;
}

.transcript-time {
  font-size: 0.75rem;
  color: #94a3b8;
}

.transcript-text {
  color: #334155;
  white-space: pre-wrap;
  cursor: pointer;
}

.meeting-page:not(.meeting-page--light) .transcript-text {
  color: #cbd5e1;
}

.transcript-edit-input {
  width: 100%;
  padding: 4px 8px;
  border-radius: 6px;
  border: 1px solid #cbd5e1;
  background: #ffffff;
  color: #0f172a;
  outline: none;
  font-size: 0.86rem;
}

.transcript-empty {
  display: grid;
  place-items: center;
  gap: 8px;
  min-height: 120px;
  border: 1px dashed rgba(148, 163, 184, 0.48);
  border-radius: 18px;
  text-align: center;
  color: #64748b;
  padding: 16px;
  font-size: 0.86rem;
}

/* --- Checknote --- */
.checknote-setup {
  display: flex;
  flex-direction: column;
  gap: 14px;
  text-align: left;
}

.checknote-label {
  font-weight: 800;
  font-size: 0.86rem;
  color: #475569;
}

.meeting-page:not(.meeting-page--light) .checknote-label {
  color: #94a3b8;
}

.checknote-select {
  padding: 10px 12px;
  border-radius: 10px;
  border: 1px solid #cbd5e1;
  background: #ffffff;
  color: #0f172a;
  outline: none;
}

.meeting-page:not(.meeting-page--light) .checknote-select {
  background: #1e293b;
  border-color: rgba(255, 255, 255, 0.1);
  color: #f8fafc;
}

.checknote-btn-generate {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 12px;
  border-radius: 10px;
  border: 0;
  background: linear-gradient(135deg, #3b82f6, #8b5cf6);
  color: #ffffff;
  font-weight: 800;
  cursor: pointer;
  transition: opacity 150ms ease;
  box-shadow: 0 4px 14px rgba(139, 92, 246, 0.3);
}

.checknote-btn-generate:hover:not(:disabled) {
  opacity: 0.9;
}

.checknote-btn-generate:disabled {
  background: #cbd5e1;
  color: #94a3b8;
  cursor: not-allowed;
  box-shadow: none;
}

.checknote-hint {
  font-size: 0.8rem;
  color: #ef4444;
  text-align: center;
}

.checknote-loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 36px 0;
  gap: 16px;
  color: #64748b;
  font-size: 0.9rem;
}

.checknote-spinner {
  width: 36px;
  height: 36px;
  border: 3px solid rgba(37, 99, 235, 0.15);
  border-top-color: #2563eb;
  border-radius: 999px;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.checknote-results {
  text-align: left;
}

.checknote-section {
  margin-bottom: 20px;
}

.checknote-section h3 {
  font-size: 0.95rem;
  font-weight: 800;
  color: #0f172a;
  margin-bottom: 8px;
}

.meeting-page:not(.meeting-page--light) .checknote-section h3 {
  color: #f8fafc;
}

.checknote-summary {
  background: rgba(255, 255, 255, 0.6);
  border: 1px solid rgba(148, 163, 184, 0.18);
  border-radius: 12px;
  padding: 12px;
  font-size: 0.86rem;
  line-height: 1.5;
  color: #334155;
}

.meeting-page:not(.meeting-page--light) .checknote-summary {
  background: rgba(15, 23, 42, 0.35);
  border-color: rgba(255, 255, 255, 0.08);
  color: #cbd5e1;
}

/* Action Items Card */
.action-items-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
  max-height: 360px;
  overflow-y: auto;
  padding-right: 4px;
}

.action-item-card {
  background: #ffffff;
  border: 1px solid rgba(148, 163, 184, 0.22);
  border-radius: 16px;
  padding: 14px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  transition: all 150ms ease;
}

.meeting-page:not(.meeting-page--light) .action-item-card {
  background: rgba(30, 41, 59, 0.45);
  border-color: rgba(255, 255, 255, 0.06);
}

.action-item-card--linked {
  border-color: #10b981 !important;
  background: rgba(16, 185, 129, 0.04) !important;
}

.action-item-input-title {
  width: 100%;
  font-weight: 800;
  border: 0;
  border-bottom: 1px solid transparent;
  padding: 2px 0;
  background: transparent;
  outline: none;
  font-size: 0.9rem;
  color: #0f172a;
}

.meeting-page:not(.meeting-page--light) .action-item-input-title {
  color: #f8fafc;
}

.action-item-input-title:focus {
  border-color: #2563eb;
}

.action-item-textarea {
  width: 100%;
  min-height: 48px;
  border: 0;
  resize: vertical;
  background: transparent;
  outline: none;
  font-size: 0.8rem;
  color: #475569;
}

.meeting-page:not(.meeting-page--light) .action-item-textarea {
  color: #94a3b8;
}

.action-item-meta-row {
  display: flex;
  gap: 12px;
}

.meta-field {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.meta-lbl {
  font-size: 0.72rem;
  font-weight: 900;
  text-transform: uppercase;
  color: #94a3b8;
}

.meta-val-select, .meta-val-date, .assignee-select {
  padding: 6px 8px;
  border-radius: 8px;
  border: 1px solid #cbd5e1;
  background: #ffffff;
  color: #0f172a;
  outline: none;
  font-size: 0.8rem;
  width: 100%;
}

.meeting-page:not(.meeting-page--light) .meta-val-select,
.meeting-page:not(.meeting-page--light) .meta-val-date,
.meeting-page:not(.meeting-page--light) .assignee-select {
  background: #1e293b;
  border-color: rgba(255, 255, 255, 0.1);
  color: #f8fafc;
}

.action-item-assignee {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.action-item-actions {
  display: flex;
  justify-content: flex-end;
  margin-top: 4px;
}

.action-item-btn-create {
  padding: 8px 14px;
  border-radius: 8px;
  border: 0;
  background: #2563eb;
  color: #ffffff;
  font-weight: 800;
  font-size: 0.8rem;
  cursor: pointer;
}

.action-item-btn-create:hover {
  background: #1d4ed8;
}

.action-item-linked-badge {
  font-size: 0.8rem;
  font-weight: 800;
  color: #10b981;
}

.checknote-btn-reset {
  width: 100%;
  padding: 10px;
  border-radius: 10px;
  border: 1px solid #cbd5e1;
  background: transparent;
  color: #64748b;
  font-weight: 800;
  font-size: 0.86rem;
  cursor: pointer;
  transition: all 150ms ease;
  margin-top: 10px;
}

.meeting-page:not(.meeting-page--light) .checknote-btn-reset {
  border-color: rgba(255, 255, 255, 0.1);
  color: #94a3b8;
}

.checknote-btn-reset:hover {
  background: rgba(148, 163, 184, 0.1);
}

/* Live Captions Overlay */
.live-captions-overlay {
  position: absolute;
  bottom: 84px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 10;
  max-width: 80%;
  pointer-events: none;
}

.live-caption-inner {
  background: rgba(15, 23, 42, 0.85);
  border: 1px solid rgba(255, 255, 255, 0.12);
  color: #f8fafc;
  font-weight: 600;
  padding: 10px 18px;
  border-radius: 999px;
  font-size: 0.95rem;
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.3);
  backdrop-filter: blur(12px);
  text-align: center;
  animation: fadeIn 200ms ease;
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
