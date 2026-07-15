<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from "vue";
import {
  AlertTriangle,
  CalendarDays,
  Captions,
  CheckCircle2,
  ChevronDown,
  Cloud,
  Copy,
  Info,
  LockKeyhole,
  Loader2,
  MessageCircle,
  Moon,
  MonitorUp,
  PanelRightClose,
  PanelRightOpen,
  ShieldCheck,
  Server,
  Sparkles,
  Sun,
  Users,
  VideoOff,
  X,
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
const sidebarOpen = ref(true);
const transcriptLogRef = ref<HTMLElement | null>(null);
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
type PrivacyAction = "speech" | "checknote";
type MeetingPrivacyContext = {
  projectId: string;
  consentId: string;
  retentionPolicyId: string;
  processingMode: "local_only" | "cloud_allowed";
  retentionDays: number;
  noticeVersion: string;
};
type MeetingPrivacyPolicy = {
  id: string;
  name: string;
  purpose: string;
  defaultRetentionDays: number;
  allowedRetentionDays: number[];
  expiryAction: string;
  allowCloudProcessing: boolean;
  allowLocalProcessing: boolean;
  isActive: boolean;
  policyVersion: string;
};
type MeetingConsent = {
  id: string;
  projectId: string | null;
  sourceEntityId: string | null;
  retentionPolicyId: string | null;
  providerClass: string;
  purpose: string;
  status: string;
  expiresAt: string | null;
};
const showPrivacyGate = ref(false);
const privacyAction = ref<PrivacyAction | null>(null);
const privacyPolicies = ref<MeetingPrivacyPolicy[]>([]);
const privacyConsents = ref<MeetingConsent[]>([]);
const privacyPolicyId = ref("");
const privacyProcessingMode = ref<"local_only" | "cloud_allowed">("local_only");
const privacyRetentionDays = ref(30);
const privacyAccepted = ref(false);
const privacyLoading = ref(false);
const privacyError = ref("");
const privacyContext = ref<MeetingPrivacyContext | null>(null);
const privacyNoticeVersion = "qaly-meeting-privacy-v4.0";
const showMeetingFrame = computed(() => active.value && !!roomName.value);
const showChecknoteSetup = computed(() => !checknoteResult.value && !isGeneratingChecknote.value);
const showChecknoteHint = computed(() => transcriptList.value.length === 0);
const selectedPrivacyPolicy = computed(() =>
  privacyPolicies.value.find((policy) => policy.id === privacyPolicyId.value) ?? null,
);

function showRemoteVideo(tile: any) {
  return tile.cameraOn && !!tile.videoTrack;
}

function showRemoteCameraOff(tile: any) {
  return !tile.cameraOn || !tile.videoTrack;
}

const { saveBuffer, getBuffer, clearBuffer } = useMeetingRecovery();
const { currentUser, projects: dashboardProjects } = useDashboardContext();

const currentUserName = computed(() => {
  return currentUser.value?.fullName || currentUser.value?.email || "Thành viên";
});

const projects = computed(() => {
  return dashboardProjects.value || [];
});

const wasSpeechActiveBeforeMute = ref(false);

const speechRec = useSpeechRecognition((text, isFinal) => {
  if (micMuted.value) {
    return;
  }
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
const speechError = computed(() => speechRec.hasError.value);
const liveCaptionText = ref("");
let liveCaptionTimeoutId: number | undefined;

watch(micMuted, (isMuted) => {
  if (isMuted) {
    if (speechRec.isListening.value) {
      speechRec.stop();
      wasSpeechActiveBeforeMute.value = true;
    }
  } else {
    if (wasSpeechActiveBeforeMute.value) {
      speechRec.start();
      wasSpeechActiveBeforeMute.value = false;
    }
  }
});

async function toggleSpeech() {
  if (speechRec.isListening.value) {
    speechRec.stop();
    wasSpeechActiveBeforeMute.value = false;
    return;
  }

  if (micMuted.value) {
    showError("Micro đang bị tắt. Hãy bật micro trước khi bật phụ đề.");
    return;
  }

  if (await ensureMeetingPrivacy("speech")) speechRec.start();
}

async function ensureMeetingPrivacy(action: PrivacyAction) {
  if (!meetingId.value) {
    showError("Cuộc họp cần được khởi tạo trước khi ghi phụ đề.");
    return false;
  }

  if (!selectedProjectId.value) selectedProjectId.value = projects.value[0]?.id || "";
  if (!selectedProjectId.value) {
    showError("Hãy chọn dự án áp dụng cho dữ liệu cuộc họp.");
    return false;
  }

  if (privacyContext.value?.projectId === selectedProjectId.value) return true;
  privacyAction.value = action;
  privacyAccepted.value = false;
  privacyError.value = "";
  showPrivacyGate.value = true;
  await loadMeetingPrivacyOptions();
  return false;
}

async function loadMeetingPrivacyOptions() {
  const project = projects.value.find((item: any) => item.id === selectedProjectId.value);
  const tenantId = project?.organizationId || project?.id;
  if (!tenantId || !selectedProjectId.value) return;

  privacyLoading.value = true;
  privacyError.value = "";
  try {
    const [policies, consents] = await Promise.all([
      apiResult<MeetingPrivacyPolicy[]>(
        `/api/privacy/policies?tenantId=${encodeURIComponent(tenantId)}&projectId=${encodeURIComponent(selectedProjectId.value)}`,
      ),
      apiResult<MeetingConsent[]>(
        `/api/privacy/consents?projectId=${encodeURIComponent(selectedProjectId.value)}`,
      ),
    ]);
    privacyPolicies.value = policies.filter(
      (policy) => policy.isActive && policy.purpose === "meeting_action_extraction",
    );
    privacyConsents.value = consents.filter(
      (consent) =>
        consent.status === "granted" &&
        consent.purpose === "meeting_action_extraction" &&
        (!consent.expiresAt || new Date(consent.expiresAt).getTime() > Date.now()) &&
        (!consent.sourceEntityId || consent.sourceEntityId === meetingId.value),
    );
    privacyPolicyId.value = privacyPolicies.value[0]?.id || "";
    privacyRetentionDays.value = privacyPolicies.value[0]?.defaultRetentionDays || 30;
    if (!privacyPolicies.value.length) {
      privacyError.value = "Dự án chưa có retention policy đang hoạt động.";
    }
  } catch (error: any) {
    privacyError.value = error?.message || "Không thể tải policy và consent.";
  } finally {
    privacyLoading.value = false;
  }
}

function updatePrivacyPolicy() {
  const policy = selectedPrivacyPolicy.value;
  if (!policy) return;
  privacyRetentionDays.value = policy.defaultRetentionDays;
  if (!policy.allowCloudProcessing) privacyProcessingMode.value = "local_only";
}

async function confirmMeetingPrivacy() {
  const policy = selectedPrivacyPolicy.value;
  if (!policy || !privacyAccepted.value || !meetingId.value) {
    privacyError.value = "Chọn policy và xác nhận consent trước khi tiếp tục.";
    return;
  }

  privacyLoading.value = true;
  privacyError.value = "";
  try {
    const providerClass = privacyProcessingMode.value === "local_only" ? "local" : "any";
    const existing = privacyConsents.value.find(
      (consent) =>
        consent.retentionPolicyId === policy.id &&
        consent.providerClass === providerClass &&
        (!consent.sourceEntityId || consent.sourceEntityId === meetingId.value),
    );
    const consent = existing ?? await apiResult<MeetingConsent>("/api/privacy/consents", {
      method: "POST",
      body: JSON.stringify({
        projectId: selectedProjectId.value,
        retentionPolicyId: policy.id,
        purpose: "meeting_action_extraction",
        providerClass,
        sourceType: "meeting",
        sourceEntityId: meetingId.value,
        noticeVersion: privacyNoticeVersion,
        expiresAt: null,
      }),
    });

    privacyContext.value = {
      projectId: selectedProjectId.value,
      consentId: consent.id,
      retentionPolicyId: policy.id,
      processingMode: privacyProcessingMode.value,
      retentionDays: privacyRetentionDays.value,
      noticeVersion: privacyNoticeVersion,
    };
    const pendingAction = privacyAction.value;
    showPrivacyGate.value = false;
    privacyAction.value = null;
    privacyAccepted.value = false;
    if (pendingAction === "speech") speechRec.start();
    if (pendingAction === "checknote") await runGenerateChecknote();
  } catch (error: any) {
    privacyError.value = error?.message || "Không thể ghi nhận consent.";
  } finally {
    privacyLoading.value = false;
  }
}

function closePrivacyGate() {
  showPrivacyGate.value = false;
  privacyAction.value = null;
  privacyAccepted.value = false;
}

function appendLocalTranscript(senderName: string, text: string, timestamp: number) {
  transcriptList.value.push({ senderName, text, timestamp });
  transcriptList.value.sort((a, b) => a.timestamp - b.timestamp);
  if (meetingId.value) {
    saveBuffer(meetingId.value, transcriptList.value);
  }
  // Auto-scroll transcript
  nextTick(() => {
    if (transcriptLogRef.value) {
      transcriptLogRef.value.scrollTop = transcriptLogRef.value.scrollHeight;
    }
  });
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

  if (await ensureMeetingPrivacy("checknote")) await runGenerateChecknote();
}

async function runGenerateChecknote() {
  const context = privacyContext.value;
  if (!selectedProjectId.value || transcriptList.value.length === 0 || !meetingId.value || !context) return;

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
        consentId: context.consentId,
        retentionPolicyId: context.retentionPolicyId,
        processingMode: context.processingMode,
        retentionDays: context.retentionDays,
        noticeVersion: context.noticeVersion,
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
  if (privacyContext.value?.projectId !== newVal) privacyContext.value = null;
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
  <div class="gm" :class="{ 'gm--light': isLightTheme }">
    <div v-if="showPrivacyGate" class="gm-privacy-overlay" role="presentation" @click.self="closePrivacyGate">
      <section class="gm-privacy-dialog" role="dialog" aria-modal="true" aria-labelledby="meeting-privacy-title">
        <header class="gm-privacy-dialog__header">
          <div class="gm-privacy-dialog__title">
            <LockKeyhole :size="19" />
            <div>
              <h2 id="meeting-privacy-title">Xác nhận xử lý dữ liệu cuộc họp</h2>
              <span>{{ privacyAction === 'speech' ? 'Bật phụ đề thời gian thực' : 'Tạo biên bản AI' }}</span>
            </div>
          </div>
          <button class="gm-icon-button" type="button" title="Đóng" @click="closePrivacyGate"><X :size="17" /></button>
        </header>

        <div class="gm-privacy-dialog__body">
          <label class="gm-privacy-field">Dự án
            <select v-model="selectedProjectId" @change="loadMeetingPrivacyOptions">
              <option value="" disabled>Chọn dự án</option>
              <option v-for="proj in projects" :key="proj.id" :value="proj.id">{{ proj.name }}</option>
            </select>
          </label>
          <label class="gm-privacy-field">Retention policy
            <select v-model="privacyPolicyId" :disabled="privacyLoading" @change="updatePrivacyPolicy">
              <option value="" disabled>Chọn policy</option>
              <option v-for="policy in privacyPolicies" :key="policy.id" :value="policy.id">
                {{ policy.name }} · {{ policy.policyVersion }}
              </option>
            </select>
          </label>

          <div class="gm-privacy-field">
            <span>Provider</span>
            <div class="gm-privacy-segmented">
              <button type="button" :class="{ active: privacyProcessingMode === 'local_only' }" @click="privacyProcessingMode = 'local_only'">
                <Server :size="16" /> Local only
              </button>
              <button type="button" :disabled="!selectedPrivacyPolicy?.allowCloudProcessing" :class="{ active: privacyProcessingMode === 'cloud_allowed' }" @click="privacyProcessingMode = 'cloud_allowed'">
                <Cloud :size="16" /> Cloud allowed
              </button>
            </div>
          </div>

          <label class="gm-privacy-field">Thời hạn lưu trữ
            <select v-model="privacyRetentionDays" :disabled="!selectedPrivacyPolicy">
              <option v-for="day in selectedPrivacyPolicy?.allowedRetentionDays || []" :key="day" :value="day">{{ day }} ngày</option>
            </select>
          </label>

          <div v-if="selectedPrivacyPolicy" class="gm-privacy-summary">
            <ShieldCheck :size="17" />
            <span>Dữ liệu được phân loại sensitive collaboration; khi hết hạn sẽ {{ selectedPrivacyPolicy.expiryAction }}. Qaly chỉ tạo task sau khi bạn duyệt draft.</span>
          </div>

          <label class="gm-privacy-consent">
            <input v-model="privacyAccepted" type="checkbox" />
            <span>Tôi đồng ý ghi nhận và xử lý nội dung cuộc họp để tạo phụ đề, tóm tắt và action item theo policy, provider và thời hạn đã chọn.</span>
          </label>

          <div v-if="privacyError" class="gm-privacy-error"><AlertTriangle :size="16" />{{ privacyError }}</div>
        </div>

        <footer class="gm-privacy-dialog__footer">
          <button class="gm-btn-secondary" type="button" @click="closePrivacyGate">Hủy</button>
          <button class="gm-btn-primary" type="button" :disabled="privacyLoading || !privacyPolicyId || !privacyAccepted" @click="confirmMeetingPrivacy">
            <Loader2 v-if="privacyLoading" :size="16" class="gm-spin" />
            <CheckCircle2 v-else :size="16" />
            Xác nhận
          </button>
        </footer>
      </section>
    </div>
    <!-- ── Top Bar ── -->
    <header class="gm-topbar">
      <div class="gm-topbar__left">
        <button class="gm-back" type="button" @click="router.push({ name: 'group-detail', params: { groupId } })">
          <ChevronDown :size="16" style="transform:rotate(90deg)" />
          <span>Trở lại</span>
        </button>
        <div class="gm-brand">
          <span class="gm-brand__badge">QALY MEET</span>
          <span class="gm-brand__room">{{ meetingShortCode }}</span>
        </div>
      </div>

      <div class="gm-topbar__right">
        <div class="gm-status" :class="{ 'gm-status--active': active }">
          <span class="gm-status__dot"></span>
          {{ meetingConnectionLabel }}
        </div>
        <button class="gm-topbar-btn" type="button" @click="toggleTheme" :title="isLightTheme ? 'Giao diện tối' : 'Giao diện sáng'">
          <Moon v-if="isLightTheme" :size="16" />
          <Sun v-else :size="16" />
        </button>
        <button class="gm-topbar-btn" type="button" @click="copyMeetingLink" title="Sao chép link phòng họp">
          <Copy :size="16" />
        </button>
        <button class="gm-topbar-btn" type="button" @click="sidebarOpen = !sidebarOpen" :title="sidebarOpen ? 'Ẩn Sidebar' : 'Hiện Sidebar'">
          <PanelRightClose v-if="sidebarOpen" :size="16" />
          <PanelRightOpen v-else :size="16" />
        </button>
      </div>
    </header>

    <!-- ── Main Body ── -->
    <div class="gm-body" :class="{ 'gm-body--sidebar-closed': !sidebarOpen }">
      <!-- Video Stage -->
      <main class="gm-stage">
        <!-- Video Grid -->
        <div v-if="showMeetingFrame" class="gm-grid" :class="`gm-grid--count-${Math.min(stageTileCount, 4)}`">
          <!-- Local Tile -->
          <article class="gm-tile gm-tile--local">
            <video
              v-show="!cameraMuted"
              ref="localVideoRef"
              class="gm-tile__video gm-tile__video--mirror"
              autoplay playsinline muted
            ></video>
            <div v-if="cameraMuted" class="gm-tile__placeholder">
              <div class="gm-avatar gm-avatar--xl">
                {{ currentUserName.slice(0, 2).toUpperCase() }}
              </div>
            </div>
            <footer class="gm-tile__label">
              <strong>Bạn</strong>
              <span :class="{ 'gm-mic--off': micMuted }">{{ micMuted ? "🔇" : "🎤" }}</span>
            </footer>
          </article>

          <!-- Remote Tiles -->
          <article
            v-for="tile in remoteTiles"
            :key="tile.identity"
            class="gm-tile"
          >
            <video
              v-show="showRemoteVideo(tile)"
              :ref="(el) => setRemoteVideoRef(tile.identity, el as HTMLVideoElement | null)"
              class="gm-tile__video"
              autoplay playsinline
            ></video>
            <audio
              :ref="(el) => setRemoteAudioRef(tile.identity, el as HTMLAudioElement | null)"
              autoplay
            ></audio>
            <div v-if="showRemoteCameraOff(tile)" class="gm-tile__placeholder">
              <div class="gm-avatar gm-avatar--xl">{{ tile.initials }}</div>
            </div>
            <footer class="gm-tile__label">
              <strong>{{ tile.name }}</strong>
              <span :class="{ 'gm-mic--off': !tile.micOn }">{{ tile.micOn ? "🎤" : "🔇" }}</span>
            </footer>
          </article>

          <!-- Overlays -->
          <div class="gm-grid__info-chip">
            <span class="gm-dot gm-dot--green"></span>
            {{ participantCount }} người tham gia
          </div>
          <div v-if="meetingError" class="gm-grid__error">
            <AlertTriangle :size="16" />
            {{ meetingError }}
          </div>
        </div>

        <!-- Pre-join State -->
        <div v-else class="gm-prejoin">
          <div class="gm-prejoin__orbit">
            <div class="gm-avatar gm-avatar--hero">
              {{ currentUserName.slice(0, 2).toUpperCase() }}
            </div>
          </div>
          <span class="gm-prejoin__eyebrow">
            <Sparkles :size="14" /> Không gian họp Qaly
          </span>
          <h2 class="gm-prejoin__heading">
            {{ isStarting ? "Đang chuẩn bị..." : "Sẵn sàng bắt đầu" }}
          </h2>
          <p class="gm-prejoin__copy">Kiểm tra camera, bật micro và vào phòng họp nhóm.</p>
          <button class="gm-prejoin__btn" type="button" @click="startMeeting" :disabled="isStarting">
            Bắt đầu cuộc họp
          </button>
        </div>

        <!-- Live Captions Overlay -->
        <Transition name="caption-fade">
          <div v-if="liveCaptionText" class="gm-captions">
            <Captions :size="14" />
            <span>{{ liveCaptionText }}</span>
          </div>
        </Transition>

        <!-- Speech Error Banner -->
        <Transition name="caption-fade">
          <div v-if="speechError" class="gm-speech-error">
            <AlertTriangle :size="14" />
            <span>{{ speechError }}</span>
            <button type="button" @click="speechRec.stop(); speechRec.start();" class="gm-speech-error__retry">Thử lại</button>
          </div>
        </Transition>

        <!-- Control Dock -->
        <div class="gm-dock-wrapper">
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

      <!-- ── Sidebar ── -->
      <aside v-show="sidebarOpen" class="gm-sidebar">
        <!-- Tab Switcher -->
        <nav class="gm-tabs">
          <button
            class="gm-tab"
            :class="{ 'gm-tab--active': activeSidebarTab === 'participants' }"
            @click="activeSidebarTab = 'participants'"
          >
            <Users :size="15" />
            <span>Thành viên</span>
          </button>
          <button
            class="gm-tab"
            :class="{ 'gm-tab--active': activeSidebarTab === 'transcript' }"
            @click="activeSidebarTab = 'transcript'"
          >
            <Captions :size="15" />
            <span>Phụ đề</span>
            <span v-if="transcriptList.length > 0" class="gm-tab__badge">{{ transcriptList.length }}</span>
          </button>
          <button
            class="gm-tab"
            :class="{ 'gm-tab--active': activeSidebarTab === 'checknote' }"
            @click="activeSidebarTab = 'checknote'"
          >
            <Sparkles :size="15" />
            <span>AI</span>
          </button>
        </nav>

        <!-- ── Tab 1: Participants ── -->
        <div v-if="activeSidebarTab === 'participants'" class="gm-pane">
          <div class="gm-section-card">
            <div class="gm-section-card__icon"><ShieldCheck :size="18" /></div>
            <div>
              <strong>{{ participantCount }} trong phòng</strong>
              <p>Room: {{ groupId.slice(0, 8) }}</p>
            </div>
          </div>

          <ScreenSharePanel ref="screenShareRef" />

          <div class="gm-participant-list">
            <!-- Self -->
            <div class="gm-participant">
              <div class="gm-avatar gm-avatar--sm">{{ currentUserName.slice(0, 2).toUpperCase() }}</div>
              <div class="gm-participant__info">
                <strong>{{ currentUserName }} <span class="gm-you-badge">bạn</span></strong>
                <span>Chủ phòng</span>
              </div>
              <span :class="micMuted ? 'gm-mic-badge gm-mic-badge--off' : 'gm-mic-badge'">
                {{ micMuted ? '🔇' : '🎤' }}
              </span>
            </div>
            <!-- Remotes -->
            <div v-for="tile in remoteTiles" :key="tile.identity" class="gm-participant">
              <div class="gm-avatar gm-avatar--sm gm-avatar--secondary">{{ tile.initials }}</div>
              <div class="gm-participant__info">
                <strong>{{ tile.name }}</strong>
                <span>{{ tile.micOn ? "Mic bật" : "Mic tắt" }} · {{ tile.cameraOn ? "Camera bật" : "Camera tắt" }}</span>
              </div>
              <span :class="tile.micOn ? 'gm-mic-badge' : 'gm-mic-badge gm-mic-badge--off'">
                {{ tile.micOn ? '🎤' : '🔇' }}
              </span>
            </div>
            <div v-if="remoteTiles.length === 0" class="gm-empty-state">
              <Info :size="22" />
              <span>Chưa có thành viên khác.</span>
              <p>Sao chép link phòng họp để mời.</p>
            </div>
          </div>
        </div>

        <!-- ── Tab 2: Transcript ── -->
        <div v-else-if="activeSidebarTab === 'transcript'" class="gm-pane gm-pane--transcript">
          <!-- Transcript Header -->
          <div class="gm-transcript-header">
            <div>
              <strong>Phụ đề thời gian thực</strong>
              <p v-if="isSpeechListening" class="gm-recording-label">
                <span class="gm-rec-dot"></span> Đang ghi nhận...
              </p>
              <p v-else class="gm-recording-label gm-recording-label--off">
                Chưa bật · Nhấn nút <Captions :size="12" /> trên thanh điều khiển
              </p>
            </div>
            <span class="gm-transcript-count">{{ transcriptList.length }}</span>
          </div>

          <!-- Transcript Messages -->
          <div ref="transcriptLogRef" class="gm-transcript-log">
            <TransitionGroup name="bubble">
              <div
                v-for="(log, idx) in transcriptList"
                :key="idx"
                class="gm-bubble"
                :class="{ 'gm-bubble--self': log.senderName === currentUserName }"
              >
                <div class="gm-bubble__meta">
                  <span class="gm-bubble__sender">{{ log.senderName }}</span>
                  <span class="gm-bubble__time">{{ formatTime(log.timestamp) }}</span>
                </div>
                <div
                  class="gm-bubble__text"
                  @dblclick="editTranscriptMessage(idx)"
                  v-if="editingTranscriptIdx !== idx"
                  title="Nhấp đúp để chỉnh sửa"
                >
                  {{ log.text }}
                </div>
                <input
                  v-else
                  type="text"
                  v-model="editingTranscriptText"
                  class="gm-bubble__edit"
                  @blur="saveTranscriptMessage(idx)"
                  @keyup.enter="saveTranscriptMessage(idx)"
                  ref="transcriptEditInputRef"
                />
              </div>
            </TransitionGroup>
            <div v-if="transcriptList.length === 0" class="gm-empty-state gm-empty-state--compact">
              <Captions :size="28" />
              <span>Chưa có phụ đề nào.</span>
              <p>Bật tính năng Phụ đề AI trên thanh điều khiển phía dưới và bắt đầu nói.</p>
            </div>
          </div>
        </div>

        <!-- ── Tab 3: AI Checknote ── -->
        <div v-else-if="activeSidebarTab === 'checknote'" class="gm-pane gm-pane--checknote">
          <!-- Setup View -->
          <div v-if="showChecknoteSetup" class="gm-checknote-setup">
            <div class="gm-checknote-hero">
              <Sparkles :size="32" />
              <h3>Biên bản AI</h3>
              <p>AI sẽ phân tích phụ đề cuộc họp, tóm tắt nội dung chính và gợi ý công việc cần làm.</p>
            </div>

            <label class="gm-field-label">Dự án đích</label>
            <select v-model="selectedProjectId" class="gm-select">
              <option value="">-- Chọn dự án --</option>
              <option v-for="proj in projects" :key="proj.id" :value="proj.id">
                {{ proj.name }}
              </option>
            </select>

            <button
              class="gm-btn-generate"
              @click="generateChecknote"
              :disabled="!selectedProjectId || transcriptList.length === 0"
            >
              <Sparkles :size="16" /> Tạo biên bản AI
            </button>
            <p v-if="showChecknoteHint" class="gm-hint gm-hint--warn">
              <AlertTriangle :size="14" />
              Phụ đề trống, không thể phân tích.
            </p>
          </div>

          <!-- Loading -->
          <div v-else-if="isGeneratingChecknote" class="gm-checknote-loading">
            <div class="gm-pulse-ring"></div>
            <Sparkles :size="24" class="gm-pulse-icon" />
            <span>Đang phân tích cuộc họp bằng AI...</span>
            <p>Quá trình có thể mất 10-30 giây.</p>
          </div>

          <!-- Results -->
          <div v-else class="gm-checknote-results">
            <div class="gm-cn-section">
              <h4><Sparkles :size="14" /> Tóm tắt cuộc họp</h4>
              <div class="gm-cn-summary">{{ checknoteResult.summary }}</div>
            </div>

            <div class="gm-cn-section">
              <h4>📋 Công việc cần làm ({{ checknoteResult.actionItems.length }})</h4>
              <div class="gm-cn-items">
                <div
                  v-for="(item, idx) in checknoteResult.actionItems"
                  :key="idx"
                  class="gm-cn-card"
                  :class="{ 'gm-cn-card--linked': item.mappingStatus === 'Linked' }"
                >
                  <div class="gm-cn-card__head">
                    <input
                      type="text"
                      v-model="item.title"
                      class="gm-cn-card__title"
                      placeholder="Tiêu đề công việc"
                      :disabled="item.mappingStatus === 'Linked'"
                    />
                    <span v-if="item.mappingStatus === 'Linked'" class="gm-cn-linked-badge">✓ Đã tạo</span>
                  </div>
                  <textarea
                    v-model="item.description"
                    class="gm-cn-card__desc"
                    placeholder="Mô tả..."
                    :disabled="item.mappingStatus === 'Linked'"
                  ></textarea>
                  <div class="gm-cn-card__meta">
                    <div class="gm-cn-field">
                      <label>Ưu tiên</label>
                      <select v-model="item.priority" :disabled="item.mappingStatus === 'Linked'">
                        <option value="Low">Thấp</option>
                        <option value="Medium">Trung bình</option>
                        <option value="High">Cao</option>
                      </select>
                    </div>
                    <div class="gm-cn-field">
                      <label>Hạn chót</label>
                      <input type="date" v-model="item.dueDateFormatted" :disabled="item.mappingStatus === 'Linked'" />
                    </div>
                  </div>
                  <div class="gm-cn-card__meta">
                    <div class="gm-cn-field" style="flex:1">
                      <label>Gán cho</label>
                      <select v-model="item.assigneeId" :disabled="item.mappingStatus === 'Linked'">
                        <option value="">-- Chọn --</option>
                        <option v-for="m in projectMembers" :key="m.userId" :value="m.userId">
                          {{ m.fullName }}
                        </option>
                      </select>
                    </div>
                  </div>
                  <div v-if="item.mappingStatus !== 'Linked'" class="gm-cn-card__actions">
                    <button
                      class="gm-btn-create-task"
                      @click="createTaskFromCard(idx)"
                      :disabled="isCreatingTask === idx"
                    >
                      {{ isCreatingTask === idx ? "Đang tạo..." : "➕ Tạo Task" }}
                    </button>
                  </div>
                </div>
              </div>
            </div>

            <button class="gm-btn-reset" @click="resetChecknote">
              Phân tích lại
            </button>
          </div>
        </div>
      </aside>
    </div>
  </div>
</template>

<style scoped>
.gm-privacy-overlay {
  position: fixed;
  inset: 0;
  z-index: 10000;
  display: grid;
  place-items: center;
  padding: 18px;
  background: rgba(2, 6, 23, 0.72);
}

.gm-privacy-dialog {
  width: min(520px, 100%);
  max-height: calc(100dvh - 36px);
  overflow: auto;
  border: 1px solid var(--gm-surface-border);
  border-radius: 8px;
  background: #111827;
  color: var(--gm-text-primary);
  box-shadow: 0 24px 70px rgba(0, 0, 0, 0.36);
}

.gm--light .gm-privacy-dialog { background: #ffffff; }

.gm-privacy-dialog__header,
.gm-privacy-dialog__footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 16px 18px;
}

.gm-privacy-dialog__header { border-bottom: 1px solid var(--gm-surface-border); }
.gm-privacy-dialog__footer { border-top: 1px solid var(--gm-surface-border); justify-content: flex-end; }
.gm-privacy-dialog__title { display: flex; align-items: center; gap: 10px; color: var(--gm-accent); }
.gm-privacy-dialog__title h2 { margin: 0; font-size: 0.98rem; color: var(--gm-text-primary); }
.gm-privacy-dialog__title span { display: block; margin-top: 3px; color: var(--gm-text-muted); font-size: 0.75rem; }
.gm-privacy-dialog__body { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; padding: 18px; }
.gm-privacy-field { display: flex; flex-direction: column; gap: 7px; color: var(--gm-text-secondary); font-size: 0.78rem; font-weight: 700; }
.gm-privacy-field select { min-height: 40px; width: 100%; border: 1px solid var(--gm-surface-border); border-radius: 6px; background: var(--gm-surface); color: var(--gm-text-primary); padding: 8px 10px; }
.gm-privacy-segmented { display: grid; grid-template-columns: 1fr 1fr; border: 1px solid var(--gm-surface-border); border-radius: 6px; overflow: hidden; }
.gm-privacy-segmented button { display: flex; align-items: center; justify-content: center; gap: 6px; min-height: 38px; border: 0; background: transparent; color: var(--gm-text-secondary); cursor: pointer; font-size: 0.74rem; font-weight: 700; }
.gm-privacy-segmented button + button { border-left: 1px solid var(--gm-surface-border); }
.gm-privacy-segmented button.active { background: var(--gm-accent-soft); color: var(--gm-accent); }
.gm-privacy-segmented button:disabled { opacity: 0.42; cursor: not-allowed; }
.gm-privacy-summary,
.gm-privacy-consent,
.gm-privacy-error { grid-column: 1 / -1; display: flex; align-items: flex-start; gap: 9px; padding: 11px 12px; border: 1px solid var(--gm-surface-border); border-radius: 6px; font-size: 0.76rem; line-height: 1.5; }
.gm-privacy-summary { color: var(--gm-text-secondary); }
.gm-privacy-summary svg { flex: 0 0 auto; color: var(--gm-accent); margin-top: 1px; }
.gm-privacy-consent { cursor: pointer; color: var(--gm-text-primary); }
.gm-privacy-consent input { width: 17px; height: 17px; flex: 0 0 auto; margin-top: 2px; accent-color: var(--gm-accent); }
.gm-privacy-error { color: #fca5a5; border-color: rgba(239, 68, 68, 0.45); }
.gm-btn-primary,
.gm-btn-secondary { display: inline-flex; align-items: center; justify-content: center; gap: 7px; min-height: 38px; border-radius: 6px; padding: 8px 14px; font-size: 0.78rem; font-weight: 800; cursor: pointer; }
.gm-btn-primary { border: 1px solid var(--gm-accent); background: var(--gm-accent); color: white; }
.gm-btn-secondary { border: 1px solid var(--gm-surface-border); background: var(--gm-surface); color: var(--gm-text-secondary); }
.gm-btn-primary:disabled { opacity: 0.48; cursor: not-allowed; }
.gm-icon-button { display: grid; place-items: center; width: 34px; height: 34px; flex: 0 0 34px; border: 1px solid var(--gm-surface-border); border-radius: 6px; background: var(--gm-surface); color: var(--gm-text-secondary); cursor: pointer; }
.gm-spin { animation: gm-spin 1s linear infinite; }
@keyframes gm-spin { to { transform: rotate(360deg); } }

@media (max-width: 560px) {
  .gm-privacy-dialog__body { grid-template-columns: 1fr; }
  .gm-privacy-summary, .gm-privacy-consent, .gm-privacy-error { grid-column: 1; }
  .gm-privacy-dialog__footer .gm-btn-primary, .gm-privacy-dialog__footer .gm-btn-secondary { flex: 1; }
}

/* ═══════════════════════════════════════════════
   QALY MEET — Premium Meeting UI
   ═══════════════════════════════════════════════ */

/* ── CSS Variables ── */
.gm {
  --gm-bg: linear-gradient(145deg, #0c1222 0%, #111827 50%, #0f172a 100%);
  --gm-surface: rgba(15, 23, 42, 0.65);
  --gm-surface-border: rgba(255, 255, 255, 0.06);
  --gm-surface-hover: rgba(255, 255, 255, 0.08);
  --gm-text-primary: #f1f5f9;
  --gm-text-secondary: #94a3b8;
  --gm-text-muted: #64748b;
  --gm-accent: #3b82f6;
  --gm-accent-soft: rgba(59, 130, 246, 0.15);
  --gm-radius: 14px;
  --gm-radius-sm: 10px;
  --gm-radius-xs: 8px;
  --gm-shadow: 0 4px 24px rgba(0, 0, 0, 0.3);

  min-height: calc(100dvh - 74px);
  display: grid;
  grid-template-rows: auto minmax(0, 1fr);
  gap: 0;
  background: var(--gm-bg);
  font-family: inherit;
}

.gm--light {
  --gm-bg: linear-gradient(145deg, #f0f4ff 0%, #f8fafc 50%, #eef2ff 100%);
  --gm-surface: rgba(255, 255, 255, 0.75);
  --gm-surface-border: rgba(148, 163, 184, 0.2);
  --gm-surface-hover: rgba(241, 245, 249, 0.9);
  --gm-text-primary: #0f172a;
  --gm-text-secondary: #475569;
  --gm-text-muted: #94a3b8;
  --gm-shadow: 0 4px 24px rgba(0, 0, 0, 0.06);
}

/* ── Top Bar ── */
.gm-topbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 20px;
  border-bottom: 1px solid var(--gm-surface-border);
  background: var(--gm-surface);
  backdrop-filter: blur(16px) saturate(1.4);
  -webkit-backdrop-filter: blur(16px) saturate(1.4);
}

.gm-topbar__left,
.gm-topbar__right {
  display: flex;
  align-items: center;
  gap: 10px;
}

.gm-back {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 7px 14px;
  border: 1px solid var(--gm-surface-border);
  border-radius: 9999px;
  background: transparent;
  color: var(--gm-text-secondary);
  font-weight: 600;
  font-size: 0.82rem;
  cursor: pointer;
  transition: all 150ms ease;
}

.gm-back:hover {
  background: var(--gm-surface-hover);
  color: var(--gm-text-primary);
}

.gm-brand {
  display: flex;
  align-items: center;
  gap: 10px;
}

.gm-brand__badge {
  padding: 4px 10px;
  border-radius: 6px;
  background: linear-gradient(135deg, #3b82f6, #6366f1);
  color: #fff;
  font-size: 0.65rem;
  font-weight: 800;
  letter-spacing: 0.08em;
}

.gm-brand__room {
  color: var(--gm-text-secondary);
  font-size: 0.82rem;
  font-weight: 700;
  font-family: 'SF Mono', 'Fira Code', monospace;
}

.gm-status {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  padding: 6px 14px;
  border-radius: 9999px;
  background: rgba(100, 116, 139, 0.15);
  color: var(--gm-text-secondary);
  font-size: 0.76rem;
  font-weight: 700;
}

.gm-status__dot {
  width: 7px;
  height: 7px;
  border-radius: 9999px;
  background: var(--gm-text-muted);
  transition: background 300ms ease;
}

.gm-status--active {
  background: rgba(34, 197, 94, 0.12);
  color: #22c55e;
}

.gm-status--active .gm-status__dot {
  background: #22c55e;
  box-shadow: 0 0 8px rgba(34, 197, 94, 0.5);
}

.gm-topbar-btn {
  width: 36px;
  height: 36px;
  display: grid;
  place-items: center;
  border: 1px solid var(--gm-surface-border);
  border-radius: 9999px;
  background: transparent;
  color: var(--gm-text-secondary);
  cursor: pointer;
  transition: all 150ms ease;
}

.gm-topbar-btn:hover {
  background: var(--gm-surface-hover);
  color: var(--gm-text-primary);
}

/* ── Body Layout ── */
.gm-body {
  display: grid;
  grid-template-columns: 1fr 380px;
  gap: 0;
  min-height: 0;
  overflow: hidden;
}

.gm-body--sidebar-closed {
  grid-template-columns: 1fr;
}

/* ── Video Stage ── */
.gm-stage {
  position: relative;
  display: flex;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
}

/* ── Video Grid ── */
.gm-grid {
  flex: 1;
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(400px, 100%), 1fr));
  grid-auto-rows: minmax(260px, 1fr);
  gap: 8px;
  padding: 12px;
  position: relative;
}

.gm-grid--count-1 { grid-template-columns: 1fr; }
.gm-grid--count-2 { grid-template-columns: repeat(2, 1fr); }
.gm-grid--count-3,
.gm-grid--count-4 { grid-template-columns: repeat(2, 1fr); }

/* ── Tiles ── */
.gm-tile {
  position: relative;
  overflow: hidden;
  border-radius: var(--gm-radius);
  background:
    radial-gradient(ellipse at 50% 30%, rgba(59, 130, 246, 0.08) 0%, transparent 60%),
    rgba(15, 23, 42, 0.5);
  border: 1px solid rgba(255, 255, 255, 0.05);
  transition: box-shadow 200ms ease;
}

.gm-tile:hover {
  box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.2);
}

.gm--light .gm-tile {
  background: rgba(255, 255, 255, 0.6);
  border-color: rgba(148, 163, 184, 0.18);
}

.gm-tile__video {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

.gm-tile__video--mirror {
  transform: scaleX(-1);
}

.gm-tile__placeholder {
  width: 100%;
  height: 100%;
  min-height: 260px;
  display: grid;
  place-items: center;
}

.gm-tile__label {
  position: absolute;
  left: 10px;
  right: 10px;
  bottom: 10px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 14px;
  border-radius: 9999px;
  background: rgba(0, 0, 0, 0.55);
  backdrop-filter: blur(12px);
  -webkit-backdrop-filter: blur(12px);
  color: #f1f5f9;
  font-size: 0.82rem;
}

.gm--light .gm-tile__label {
  background: rgba(255, 255, 255, 0.8);
  color: #1e293b;
}

.gm-tile__label strong {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.gm-mic--off { opacity: 0.5; }

/* ── Avatars ── */
.gm-avatar {
  display: grid;
  place-items: center;
  border-radius: 9999px;
  background: linear-gradient(135deg, #3b82f6, #6366f1);
  color: #fff;
  font-weight: 800;
}

.gm-avatar--xl { width: 120px; height: 120px; font-size: 2.2rem; }
.gm-avatar--hero { width: 96px; height: 96px; font-size: 1.8rem; }
.gm-avatar--sm { width: 36px; height: 36px; font-size: 0.72rem; min-width: 36px; }
.gm-avatar--secondary { background: linear-gradient(135deg, #475569, #64748b); }

/* ── Grid Overlays ── */
.gm-grid__info-chip {
  position: absolute;
  top: 22px;
  right: 22px;
  display: inline-flex;
  align-items: center;
  gap: 7px;
  padding: 7px 14px;
  border-radius: 9999px;
  background: rgba(0, 0, 0, 0.5);
  backdrop-filter: blur(10px);
  color: #e2e8f0;
  font-size: 0.76rem;
  font-weight: 700;
  z-index: 2;
}

.gm-dot { width: 7px; height: 7px; border-radius: 9999px; }
.gm-dot--green { background: #22c55e; box-shadow: 0 0 6px rgba(34, 197, 94, 0.5); }

.gm-grid__error {
  position: absolute;
  bottom: 22px;
  left: 50%;
  transform: translateX(-50%);
  display: inline-flex;
  align-items: center;
  gap: 8px;
  max-width: min(560px, calc(100% - 40px));
  padding: 10px 18px;
  border-radius: 9999px;
  background: rgba(254, 226, 226, 0.92);
  color: #991b1b;
  font-size: 0.82rem;
  font-weight: 600;
  z-index: 2;
}

/* ── Pre-Join ── */
.gm-prejoin {
  flex: 1;
  display: grid;
  place-items: center;
  align-content: center;
  gap: 16px;
  padding: 60px 28px;
  text-align: center;
}

.gm-prejoin__orbit {
  width: 150px;
  height: 150px;
  display: grid;
  place-items: center;
  border-radius: 9999px;
  background:
    linear-gradient(135deg, rgba(59, 130, 246, 0.25), rgba(99, 102, 241, 0.15)),
    var(--gm-surface);
  box-shadow: var(--gm-shadow);
}

.gm-prejoin__eyebrow {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 14px;
  border-radius: 9999px;
  background: var(--gm-accent-soft);
  color: var(--gm-accent);
  font-size: 0.76rem;
  font-weight: 700;
}

.gm-prejoin__heading {
  margin: 0;
  color: var(--gm-text-primary);
  font-size: clamp(1.6rem, 3vw, 2.8rem);
  font-weight: 800;
  line-height: 1.1;
}

.gm-prejoin__copy {
  margin: 0;
  color: var(--gm-text-secondary);
  font-size: 0.92rem;
  max-width: 400px;
}

.gm-prejoin__btn {
  min-height: 48px;
  padding: 0 28px;
  border: 0;
  border-radius: 9999px;
  background: linear-gradient(135deg, #3b82f6, #6366f1);
  color: #fff;
  font-weight: 700;
  font-size: 0.95rem;
  cursor: pointer;
  box-shadow: 0 4px 20px rgba(59, 130, 246, 0.35);
  transition: all 200ms ease;
}

.gm-prejoin__btn:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 8px 28px rgba(59, 130, 246, 0.45);
}

.gm-prejoin__btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* ── Live Captions ── */
.gm-captions {
  position: absolute;
  bottom: 90px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 5;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  max-width: min(600px, 80%);
  padding: 10px 20px;
  border-radius: 9999px;
  background: rgba(0, 0, 0, 0.72);
  backdrop-filter: blur(12px);
  color: #f1f5f9;
  font-size: 0.88rem;
  font-weight: 500;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.25);
}

.caption-fade-enter-active,
.caption-fade-leave-active {
  transition: all 250ms ease;
}
.caption-fade-enter-from,
.caption-fade-leave-to {
  opacity: 0;
  transform: translateX(-50%) translateY(8px);
}

/* ── Speech Error ── */
.gm-speech-error {
  position: absolute;
  top: 16px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 5;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 8px 18px;
  border-radius: 9999px;
  background: rgba(254, 243, 199, 0.95);
  color: #92400e;
  font-size: 0.82rem;
  font-weight: 600;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

.gm-speech-error__retry {
  padding: 3px 10px;
  border: 0;
  border-radius: 6px;
  background: rgba(146, 64, 14, 0.12);
  color: #92400e;
  font-weight: 700;
  font-size: 0.76rem;
  cursor: pointer;
}

/* ── Dock ── */
.gm-dock-wrapper {
  position: absolute;
  bottom: 16px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 6;
}

/* ═══════════ SIDEBAR ═══════════ */
.gm-sidebar {
  display: flex;
  flex-direction: column;
  border-left: 1px solid var(--gm-surface-border);
  background: var(--gm-surface);
  backdrop-filter: blur(16px) saturate(1.4);
  -webkit-backdrop-filter: blur(16px) saturate(1.4);
  overflow: hidden;
}

/* ── Tabs ── */
.gm-tabs {
  display: flex;
  gap: 2px;
  padding: 8px 8px 0 8px;
  border-bottom: 1px solid var(--gm-surface-border);
  flex-shrink: 0;
}

.gm-tab {
  flex: 1;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 10px 8px;
  border: 0;
  border-bottom: 2px solid transparent;
  border-radius: var(--gm-radius-xs) var(--gm-radius-xs) 0 0;
  background: transparent;
  color: var(--gm-text-muted);
  font-size: 0.78rem;
  font-weight: 700;
  cursor: pointer;
  transition: all 150ms ease;
  position: relative;
}

.gm-tab:hover {
  color: var(--gm-text-secondary);
  background: var(--gm-surface-hover);
}

.gm-tab--active {
  color: var(--gm-accent) !important;
  border-bottom-color: var(--gm-accent);
}

.gm-tab__badge {
  padding: 1px 7px;
  border-radius: 9999px;
  background: var(--gm-accent);
  color: #fff;
  font-size: 0.65rem;
  font-weight: 800;
  min-width: 18px;
  text-align: center;
}

/* ── Pane ── */
.gm-pane {
  flex: 1;
  overflow-y: auto;
  padding: 14px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

/* ── Section Card ── */
.gm-section-card {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 14px;
  border-radius: var(--gm-radius-sm);
  background: var(--gm-accent-soft);
  border: 1px solid rgba(59, 130, 246, 0.1);
}

.gm-section-card__icon {
  width: 38px;
  height: 38px;
  display: grid;
  place-items: center;
  border-radius: var(--gm-radius-xs);
  background: var(--gm-accent);
  color: #fff;
  flex-shrink: 0;
}

.gm-section-card strong {
  display: block;
  color: var(--gm-text-primary);
  font-size: 0.88rem;
}

.gm-section-card p {
  margin: 2px 0 0;
  color: var(--gm-text-muted);
  font-size: 0.76rem;
  font-family: 'SF Mono', 'Fira Code', monospace;
}

/* ── Participant List ── */
.gm-participant-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.gm-participant {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  border-radius: var(--gm-radius-sm);
  border: 1px solid var(--gm-surface-border);
  background: var(--gm-surface-hover);
  transition: all 150ms ease;
}

.gm-participant:hover {
  background: rgba(59, 130, 246, 0.06);
  border-color: rgba(59, 130, 246, 0.12);
}

.gm-participant__info {
  flex: 1;
  min-width: 0;
}

.gm-participant__info strong {
  display: block;
  color: var(--gm-text-primary);
  font-size: 0.86rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.gm-participant__info span {
  color: var(--gm-text-muted);
  font-size: 0.76rem;
}

.gm-you-badge {
  display: inline-block;
  padding: 1px 6px;
  border-radius: 4px;
  background: var(--gm-accent-soft);
  color: var(--gm-accent) !important;
  font-size: 0.65rem !important;
  font-weight: 700;
  vertical-align: middle;
  margin-left: 4px;
}

.gm-mic-badge {
  font-size: 0.9rem;
  flex-shrink: 0;
}

.gm-mic-badge--off {
  opacity: 0.35;
}

/* ── Empty State ── */
.gm-empty-state {
  display: grid;
  place-items: center;
  gap: 6px;
  min-height: 140px;
  padding: 24px 16px;
  border: 1px dashed var(--gm-surface-border);
  border-radius: var(--gm-radius-sm);
  text-align: center;
  color: var(--gm-text-muted);
}

.gm-empty-state span {
  font-weight: 700;
  font-size: 0.88rem;
  color: var(--gm-text-secondary);
}

.gm-empty-state p {
  margin: 0;
  font-size: 0.78rem;
  max-width: 240px;
}

.gm-empty-state--compact {
  min-height: 200px;
}

/* ═══════════ TRANSCRIPT ═══════════ */
.gm-pane--transcript {
  gap: 0;
}

.gm-transcript-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  padding: 0 0 12px 0;
  border-bottom: 1px solid var(--gm-surface-border);
  margin-bottom: 12px;
  flex-shrink: 0;
}

.gm-transcript-header strong {
  color: var(--gm-text-primary);
  font-size: 0.92rem;
}

.gm-recording-label {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  margin: 4px 0 0;
  color: #22c55e;
  font-size: 0.76rem;
  font-weight: 600;
}

.gm-recording-label--off {
  color: var(--gm-text-muted);
}

.gm-rec-dot {
  width: 6px;
  height: 6px;
  border-radius: 9999px;
  background: #ef4444;
  animation: blink-rec 1.2s ease-in-out infinite;
}

@keyframes blink-rec {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.2; }
}

.gm-transcript-count {
  padding: 4px 10px;
  border-radius: 9999px;
  background: var(--gm-surface-hover);
  color: var(--gm-text-secondary);
  font-size: 0.76rem;
  font-weight: 800;
  flex-shrink: 0;
}

.gm-transcript-log {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding-right: 4px;
}

/* ── Chat Bubble ── */
.gm-bubble {
  max-width: 92%;
  padding: 10px 14px;
  border-radius: var(--gm-radius-sm) var(--gm-radius-sm) var(--gm-radius-sm) 4px;
  background: var(--gm-surface-hover);
  border: 1px solid var(--gm-surface-border);
  animation: bubble-in 200ms ease;
}

.gm-bubble--self {
  margin-left: auto;
  border-radius: var(--gm-radius-sm) var(--gm-radius-sm) 4px var(--gm-radius-sm);
  background: var(--gm-accent-soft);
  border-color: rgba(59, 130, 246, 0.12);
}

@keyframes bubble-in {
  from { opacity: 0; transform: translateY(6px); }
  to { opacity: 1; transform: translateY(0); }
}

.bubble-enter-active { animation: bubble-in 200ms ease; }
.bubble-leave-active { transition: opacity 150ms ease; }
.bubble-leave-to { opacity: 0; }

.gm-bubble__meta {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 4px;
}

.gm-bubble__sender {
  font-weight: 700;
  font-size: 0.78rem;
  color: var(--gm-accent);
}

.gm-bubble--self .gm-bubble__sender {
  color: #6366f1;
}

.gm-bubble__time {
  font-size: 0.68rem;
  color: var(--gm-text-muted);
}

.gm-bubble__text {
  color: var(--gm-text-primary);
  font-size: 0.86rem;
  line-height: 1.5;
  white-space: pre-wrap;
  cursor: pointer;
}

.gm-bubble__edit {
  width: 100%;
  padding: 4px 8px;
  border-radius: 6px;
  border: 1px solid var(--gm-accent);
  background: transparent;
  color: var(--gm-text-primary);
  outline: none;
  font-size: 0.86rem;
}

/* ═══════════ AI CHECKNOTE ═══════════ */
.gm-pane--checknote {
  gap: 0;
}

.gm-checknote-setup {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.gm-checknote-hero {
  text-align: center;
  padding: 24px 16px;
  border-radius: var(--gm-radius);
  background:
    radial-gradient(ellipse at 50% 0%, rgba(168, 85, 247, 0.12), transparent 65%),
    var(--gm-surface-hover);
  border: 1px solid var(--gm-surface-border);
}

.gm-checknote-hero h3 {
  margin: 10px 0 6px;
  color: var(--gm-text-primary);
  font-size: 1.1rem;
}

.gm-checknote-hero p {
  margin: 0;
  color: var(--gm-text-secondary);
  font-size: 0.82rem;
  line-height: 1.5;
}

.gm-field-label {
  font-weight: 700;
  font-size: 0.78rem;
  color: var(--gm-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.03em;
}

.gm-select,
.gm-cn-field select,
.gm-cn-field input {
  width: 100%;
  padding: 10px 12px;
  border-radius: var(--gm-radius-xs);
  border: 1px solid var(--gm-surface-border);
  background: var(--gm-surface-hover);
  color: var(--gm-text-primary);
  outline: none;
  font-size: 0.86rem;
  transition: border-color 150ms ease;
}

.gm-select:focus,
.gm-cn-field select:focus,
.gm-cn-field input:focus {
  border-color: var(--gm-accent);
}

.gm-btn-generate {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 12px 16px;
  border: 0;
  border-radius: var(--gm-radius-xs);
  background: linear-gradient(135deg, #7c3aed, #a855f7);
  color: #fff;
  font-weight: 700;
  font-size: 0.88rem;
  cursor: pointer;
  box-shadow: 0 4px 16px rgba(124, 58, 237, 0.3);
  transition: all 200ms ease;
}

.gm-btn-generate:hover:not(:disabled) {
  transform: translateY(-1px);
  box-shadow: 0 6px 24px rgba(124, 58, 237, 0.4);
}

.gm-btn-generate:disabled {
  background: rgba(100, 116, 139, 0.2);
  color: var(--gm-text-muted);
  box-shadow: none;
  cursor: not-allowed;
}

.gm-hint {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 0.78rem;
  color: var(--gm-text-muted);
}

.gm-hint--warn {
  color: #f59e0b;
}

/* ── Loading ── */
.gm-checknote-loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 14px;
  padding: 48px 16px;
  text-align: center;
  position: relative;
}

.gm-pulse-ring {
  width: 64px;
  height: 64px;
  border-radius: 9999px;
  border: 3px solid rgba(168, 85, 247, 0.2);
  animation: pulse-ring 1.8s ease-out infinite;
}

@keyframes pulse-ring {
  0% { transform: scale(0.8); opacity: 1; }
  100% { transform: scale(1.6); opacity: 0; }
}

.gm-pulse-icon {
  position: absolute;
  top: 68px;
  color: #a855f7;
  animation: pulse-glow 1.8s ease-in-out infinite;
}

@keyframes pulse-glow {
  0%, 100% { opacity: 0.6; }
  50% { opacity: 1; }
}

.gm-checknote-loading span {
  font-weight: 700;
  color: var(--gm-text-primary);
  font-size: 0.92rem;
}

.gm-checknote-loading p {
  margin: 0;
  color: var(--gm-text-muted);
  font-size: 0.78rem;
}

/* ── Results ── */
.gm-checknote-results {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.gm-cn-section h4 {
  display: flex;
  align-items: center;
  gap: 6px;
  margin: 0 0 10px;
  color: var(--gm-text-primary);
  font-size: 0.88rem;
  font-weight: 700;
}

.gm-cn-summary {
  padding: 14px;
  border-radius: var(--gm-radius-sm);
  background: var(--gm-surface-hover);
  border: 1px solid var(--gm-surface-border);
  color: var(--gm-text-secondary);
  font-size: 0.86rem;
  line-height: 1.6;
}

.gm-cn-items {
  display: flex;
  flex-direction: column;
  gap: 10px;
  max-height: 400px;
  overflow-y: auto;
  padding-right: 4px;
}

.gm-cn-card {
  padding: 14px;
  border-radius: var(--gm-radius-sm);
  border: 1px solid var(--gm-surface-border);
  background: var(--gm-surface-hover);
  display: flex;
  flex-direction: column;
  gap: 8px;
  transition: all 200ms ease;
}

.gm-cn-card:hover {
  border-color: rgba(59, 130, 246, 0.15);
}

.gm-cn-card--linked {
  border-color: rgba(16, 185, 129, 0.3) !important;
  background: rgba(16, 185, 129, 0.04);
}

.gm-cn-card__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.gm-cn-card__title {
  flex: 1;
  border: 0;
  border-bottom: 1px solid transparent;
  padding: 2px 0;
  background: transparent;
  color: var(--gm-text-primary);
  font-weight: 700;
  font-size: 0.88rem;
  outline: none;
}

.gm-cn-card__title:focus {
  border-bottom-color: var(--gm-accent);
}

.gm-cn-linked-badge {
  padding: 3px 10px;
  border-radius: 9999px;
  background: rgba(16, 185, 129, 0.12);
  color: #10b981;
  font-size: 0.72rem;
  font-weight: 700;
  flex-shrink: 0;
}

.gm-cn-card__desc {
  width: 100%;
  min-height: 36px;
  border: 0;
  resize: vertical;
  background: transparent;
  color: var(--gm-text-secondary);
  font-size: 0.8rem;
  outline: none;
  line-height: 1.5;
}

.gm-cn-card__meta {
  display: flex;
  gap: 10px;
}

.gm-cn-field {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.gm-cn-field label {
  font-size: 0.68rem;
  font-weight: 700;
  text-transform: uppercase;
  color: var(--gm-text-muted);
  letter-spacing: 0.04em;
}

.gm-cn-card__actions {
  display: flex;
  justify-content: flex-end;
  padding-top: 4px;
}

.gm-btn-create-task {
  padding: 7px 16px;
  border: 0;
  border-radius: var(--gm-radius-xs);
  background: var(--gm-accent);
  color: #fff;
  font-weight: 700;
  font-size: 0.8rem;
  cursor: pointer;
  transition: all 150ms ease;
}

.gm-btn-create-task:hover:not(:disabled) {
  background: #2563eb;
  transform: translateY(-1px);
}

.gm-btn-create-task:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.gm-btn-reset {
  width: 100%;
  padding: 10px;
  border: 1px solid var(--gm-surface-border);
  border-radius: var(--gm-radius-xs);
  background: transparent;
  color: var(--gm-text-secondary);
  font-weight: 700;
  font-size: 0.82rem;
  cursor: pointer;
  transition: all 150ms ease;
}

.gm-btn-reset:hover {
  background: var(--gm-surface-hover);
  color: var(--gm-text-primary);
}

/* ═══════════ RESPONSIVE ═══════════ */
@media (max-width: 1200px) {
  .gm-body {
    grid-template-columns: 1fr 340px;
  }
}

@media (max-width: 960px) {
  .gm-body {
    grid-template-columns: 1fr;
    grid-template-rows: 1fr auto;
  }

  .gm-sidebar {
    border-left: none;
    border-top: 1px solid var(--gm-surface-border);
    max-height: 360px;
  }
}

@media (max-width: 640px) {
  .gm-topbar {
    padding: 8px 12px;
    flex-wrap: wrap;
    gap: 8px;
  }

  .gm-brand { display: none; }

  .gm-grid {
    grid-template-columns: 1fr !important;
    padding: 8px;
    gap: 6px;
  }
}
</style>
