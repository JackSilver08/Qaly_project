<script setup lang="ts">
import { computed, ref, onMounted, onBeforeUnmount, watch } from "vue";
import {
  HubConnectionBuilder,
  type HubConnection,
  HubConnectionState,
} from "@microsoft/signalr";
import { ApiError, apiResult, apiCommand } from "../../utils/api-client";
import { showError, showSuccess } from "../../composables/use-toast";
import type { TeamChatPoll } from "./chat-types";
import { Check, CheckCircle2, Trash2, Edit2, Plus, Users, Vote, X } from "lucide-vue-next";

const props = defineProps<{
  groupId: string;
  poll: TeamChatPoll;
  currentUserId?: string;
  creatorId?: string;
}>();

const results = ref<any | null>(null);
const loading = ref(false);
const selectedOptionIds = ref<string[]>([]);
const expandedOptionId = ref<string | null>(null);
let hubConnection: HubConnection | null = null;
const isDeleted = ref(false);

const isEditing = ref(false);
const editQuestion = ref("");
const editOptions = ref<{ id: string, text: string }[]>([]);

const canEdit = computed(() => {
  return props.currentUserId && props.creatorId && props.currentUserId === props.creatorId;
});

const hasVoted = computed(() =>
  Boolean(results.value?.currentUserOptionIds?.length),
);

const isOpen = computed(() => (results.value?.status ?? "Open") === "Open");

watch(
  () => results.value?.currentUserOptionIds,
  (optionIds) => {
    selectedOptionIds.value = [...(optionIds ?? [])];
  },
  { immediate: true },
);

function optionPercent(voteCount: number) {
  const total = results.value?.totalVotes || 0;
  if (!total) return 0;
  return Math.round((voteCount / total) * 100);
}

function initials(name: string) {
  return name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");
}

function voterNames(voters: any[] = []) {
  if (!voters.length) return "Chưa ai chọn";
  const names = voters.map((voter) => voter.fullName || voter.FullName).filter(Boolean);
  if (names.length <= 2) return names.join(", ");
  return `${names.slice(0, 2).join(", ")} +${names.length - 2}`;
}

async function loadResults() {
  if (!props.poll?.id) return;
  try {
    results.value = await apiResult(
      `/api/groups/${props.groupId}/polls/${props.poll.id}/results`,
    );
  } catch (err: any) {
    if (err instanceof ApiError && err.status === 404) {
      isDeleted.value = true;
      return;
    }

    console.warn("Không thể tải kết quả bình chọn", err);
  }
}

async function submitVote(optionIds: string[]) {
  if (!props.poll?.id || !optionIds.length || !isOpen.value) return;
  const hadVotedBefore = hasVoted.value;
  loading.value = true;
  try {
    results.value = await apiResult(
      `/api/groups/${props.groupId}/polls/${props.poll.id}/vote`,
      {
        method: "POST",
        body: JSON.stringify({ optionIds }),
      },
    );
    showSuccess(hadVotedBefore ? "Đã cập nhật lựa chọn" : "Đã ghi nhận bình chọn");
  } catch (e: unknown) {
    if (e instanceof ApiError && e.status === 404) {
      isDeleted.value = true;
      return;
    }

    selectedOptionIds.value = [...(results.value?.currentUserOptionIds ?? [])];
    showError(e instanceof Error ? e.message : "Không thể gửi bình chọn.");
  } finally {
    loading.value = false;
  }
}

async function selectOption(optionId: string) {
  if (!optionId || loading.value || !isOpen.value) return;

  if (!results.value?.allowMultiple) {
    selectedOptionIds.value = [optionId];
    await submitVote([optionId]);
    return;
  }

  selectedOptionIds.value = selectedOptionIds.value.includes(optionId)
    ? selectedOptionIds.value.filter((id) => id !== optionId)
    : [...selectedOptionIds.value, optionId];
}

function toggleVoters(optionId: string) {
  expandedOptionId.value = expandedOptionId.value === optionId ? null : optionId;
}

function startEditing() {
  editQuestion.value = results.value?.question || props.poll.question;
  const opts = results.value?.options?.map((o: any) => o.content) || props.poll.options;
  editOptions.value = opts.map((o: string) => ({ id: Math.random().toString(), text: o }));
  isEditing.value = true;
}

function addOption() {
  editOptions.value.push({ id: Math.random().toString(), text: "" });
}

function removeOption(index: number) {
  editOptions.value.splice(index, 1);
}

async function savePoll() {
  if (!props.poll?.id) return;
  const validOpts = editOptions.value.filter(o => o.text.trim());
  if (validOpts.length < 2) {
    alert("Cần ít nhất 2 lựa chọn");
    return;
  }
  if (!editQuestion.value.trim()) {
    alert("Câu hỏi không được để trống");
    return;
  }

  loading.value = true;
  try {
    await apiResult(`/api/groups/${props.groupId}/polls/${props.poll.id}`, {
      method: "PUT",
      body: JSON.stringify({
        question: editQuestion.value,
        options: validOpts.map(o => ({ content: o.text })),
        allowMultiple: false
      })
    });
    isEditing.value = false;
    await loadResults();
  } catch (e) {
    if (e instanceof ApiError && e.status === 404) {
      isDeleted.value = true;
      return;
    }

    console.error("Lỗi cập nhật bình chọn", e);
  } finally {
    loading.value = false;
  }
}

async function deletePoll() {
  if (!props.poll?.id) return;
  if (!confirm("Bạn có chắc muốn xóa bình chọn này?")) return;
  loading.value = true;
  try {
    await apiCommand(`/api/groups/${props.groupId}/polls/${props.poll.id}`, {
      method: "DELETE"
    });
    isDeleted.value = true;
  } catch (e) {
    if (e instanceof ApiError && e.status === 404) {
      isDeleted.value = true;
      return;
    }

    console.error("Lỗi xóa bình chọn", e);
  } finally {
    loading.value = false;
  }
}

onMounted(async () => {
  if (props.poll?.id) await loadResults();

  hubConnection = new HubConnectionBuilder()
    .withUrl("/hubs/groups")
    .withAutomaticReconnect()
    .build();

  hubConnection.on("PollUpdated", (payload: any) => {
    try {
      if (
        payload &&
        payload.groupId === props.groupId &&
        payload.pollId === props.poll.id
      ) {
        results.value = payload.results;
      }
    } catch {}
  });

  hubConnection.on("PollDeleted", (payload: any) => {
    try {
      if (
        payload &&
        payload.groupId === props.groupId &&
        payload.pollId === props.poll.id
      ) {
        isDeleted.value = true;
      }
    } catch {}
  });

  try {
    await hubConnection.start();
    if (hubConnection.state === HubConnectionState.Connected) {
      await hubConnection
        .invoke("JoinGroup", props.groupId)
        .catch(() => undefined);
    }
  } catch (e) {
    console.warn("Không thể kết nối realtime bình chọn", e);
  }
});

onBeforeUnmount(async () => {
  if (hubConnection) {
    try {
      if (hubConnection.state === HubConnectionState.Connected)
        await hubConnection
          .invoke("LeaveGroup", props.groupId)
          .catch(() => undefined);
      await hubConnection.stop();
    } catch {}
    hubConnection = null;
  }
});
</script>

<template>
  <div class="poll-card" v-if="!isDeleted">
    <div v-if="isEditing" class="poll-edit-mode">
      <div class="edit-header">
        <strong>Sửa bình chọn</strong>
        <button type="button" @click="isEditing = false" class="btn-cancel">Hủy</button>
      </div>
      
      <input v-model="editQuestion" type="text" class="input-text" placeholder="Câu hỏi" />
      
      <div class="edit-options">
        <div v-for="(opt, idx) in editOptions" :key="opt.id" class="edit-option-row">
          <input v-model="opt.text" type="text" class="input-text" placeholder="Lựa chọn" />
          <button type="button" @click="removeOption(idx)" class="btn-icon">
            <X :size="16" />
          </button>
        </div>
      </div>
      
      <button type="button" @click="addOption" class="btn-add-option">
        <Plus :size="16" /> Thêm lựa chọn
      </button>
      
      <button type="button" @click="savePoll" class="btn-save" :disabled="loading">
        {{ loading ? 'Đang lưu...' : 'Lưu thay đổi' }}
      </button>
    </div>

    <template v-else>
      <div class="poll-header">
        <div class="poll-header-top">
          <span><Vote :size="13" /> Bình chọn nhóm</span>
          <div class="poll-actions" v-if="canEdit">
            <button type="button" @click="startEditing" title="Sửa"><Edit2 :size="14" /></button>
            <button type="button" @click="deletePoll" title="Xóa" class="text-danger"><Trash2 :size="14" /></button>
          </div>
        </div>
        <strong>{{ results?.question || props.poll.question }}</strong>
        <div v-if="results" class="poll-summary">
          <span><Users :size="14" /> {{ results.totalVoters ?? 0 }} người tham gia</span>
          <span>{{ results.allowMultiple ? "Chọn nhiều đáp án" : "Chọn một đáp án" }}</span>
        </div>
      </div>

      <div v-if="results" class="poll-results">
        <button
          v-for="opt in results.options"
          :key="opt.optionId || opt.id"
          class="poll-option"
          type="button"
          :class="{
            'is-selected': selectedOptionIds.includes(opt.optionId || opt.id),
          }"
          :disabled="loading || !isOpen"
          @click="selectOption(opt.optionId || opt.id)"
        >
          <div class="option-row">
            <span class="option-choice">
              <span class="option-check">
                <Check v-if="selectedOptionIds.includes(opt.optionId || opt.id)" :size="14" />
              </span>
              <span class="option-label">{{ opt.content || opt.Content }}</span>
            </span>
            <strong>{{ opt.voteCount ?? 0 }} chọn · {{ optionPercent(opt.voteCount || 0) }}%</strong>
          </div>
          <div class="percent">
            <div class="bar" :style="{ width: optionPercent(opt.voteCount || 0) + '%' }"></div>
          </div>
          <div
            class="poll-voters"
            role="button"
            tabindex="0"
            @click.stop="toggleVoters(opt.optionId || opt.id)"
            @keydown.enter.stop="toggleVoters(opt.optionId || opt.id)"
          >
            <span
              v-for="voter in (opt.voters || opt.Voters || []).slice(0, 4)"
              :key="voter.userId || voter.UserId"
              :title="(voter.fullName || voter.FullName) + ' - ' + (voter.email || voter.Email)"
            >
              {{ initials(voter.fullName || voter.FullName || "") }}
            </span>
            <small>{{ voterNames(opt.voters || opt.Voters || []) }}</small>
          </div>
          <div v-if="expandedOptionId === (opt.optionId || opt.id)" class="poll-voter-list">
            <div v-for="voter in (opt.voters || opt.Voters || [])" :key="voter.userId || voter.UserId">
              <span>{{ initials(voter.fullName || voter.FullName || "") }}</span>
              <p>
                <strong>{{ voter.fullName || voter.FullName }}</strong>
                <small>{{ voter.email || voter.Email }}</small>
              </p>
              <CheckCircle2 :size="16" />
            </div>
            <small v-if="!(opt.voters || opt.Voters || []).length">Chưa có thành viên chọn đáp án này.</small>
          </div>
        </button>
      </div>

      <button
        v-if="results?.allowMultiple && isOpen"
        class="poll-submit"
        type="button"
        :disabled="loading || selectedOptionIds.length === 0"
        @click="submitVote(selectedOptionIds)"
      >
        <Vote :size="16" />
        {{ loading ? "Đang gửi..." : hasVoted ? "Cập nhật bình chọn" : "Gửi bình chọn" }}
      </button>
      <div v-if="!isOpen" class="poll-closed">Bình chọn đã kết thúc</div>

      <div v-else class="poll-options">
        <button
          v-for="(opt, idx) in props.poll.options"
          :key="idx"
          class="poll-option"
          type="button"
          disabled
        >
          <span class="option-label">{{ opt }}</span>
        </button>
      </div>
    </template>
  </div>
  <div v-else class="poll-card deleted-poll">
    <span>Bình chọn đã bị xóa</span>
  </div>
</template>

<style scoped>
.poll-card {
  width: 100%;
  min-width: min(100%, 340px);
  padding: 14px;
  border: 1px solid rgba(203, 213, 225, 0.82);
  border-radius: var(--qaly-radius-lg);
  color: #0f172a;
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(248, 250, 252, 0.96));
  box-shadow: var(--qaly-shadow-md);
}

.poll-card button {
  color: #0f172a !important;
}
.deleted-poll {
  display: flex;
  justify-content: center;
  align-items: center;
  color: #94a3b8;
  font-size: 0.9rem;
  font-style: italic;
  padding: 20px;
  background: #f8fafc;
}
.poll-header-top {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
}
.poll-actions {
  display: flex;
  gap: 6px;
}
.poll-actions button {
  background: none;
  border: none;
  cursor: pointer;
  color: #64748b;
  padding: 4px;
  border-radius: 4px;
  transition: all 0.2s;
}
.poll-actions button:hover {
  background: #e2e8f0;
  color: #0f172a;
}
.poll-actions button.text-danger:hover {
  background: #fee2e2;
  color: #ef4444;
}
.poll-edit-mode {
  display: grid;
  gap: 12px;
}
.edit-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.edit-header strong {
  color: #0f172a;
  font-size: 1rem;
}
.btn-cancel {
  background: none;
  border: none;
  color: #64748b;
  cursor: pointer;
}
.input-text {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid #cbd5e1;
  border-radius: var(--qaly-radius-lg);
  font-size: 0.9rem;
  color: #0f172a;
  background: #fff;
}
.edit-options {
  display: grid;
  gap: 8px;
}
.edit-option-row {
  display: flex;
  gap: 8px;
  align-items: center;
}
.btn-icon {
  background: none;
  border: none;
  color: #ef4444;
  cursor: pointer;
  padding: 4px;
}
.btn-add-option {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  background: #f1f5f9;
  border: 1px dashed #cbd5e1;
  padding: 8px;
  border-radius: var(--qaly-radius-lg);
  color: #3b82f6;
  cursor: pointer;
}
.btn-add-option:hover {
  background: #e2e8f0;
}
.btn-save {
  background: #2563eb;
  color: #fff;
  border: none;
  padding: 10px;
  border-radius: var(--qaly-radius-lg);
  font-weight: 600;
  cursor: pointer;
}
.btn-save:disabled {
  opacity: 0.7;
  cursor: not-allowed;
}
.poll-header {
  display: grid;
  gap: 4px;
  margin-bottom: 12px;
}

.poll-header span {
  width: max-content;
  border-radius: 999px;
  padding: 4px 8px;
  background: #eff6ff;
  color: #1d4ed8;
  font-size: 0.7rem;
  font-weight: 900;
  text-transform: uppercase;
  display: inline-flex;
  align-items: center;
  gap: 5px;
}

.poll-header strong {
  color: #0f172a;
  font-size: 0.98rem;
  line-height: 1.35;
}

.poll-header small,
.poll-option small {
  color: #64748b;
  font-size: 0.75rem;
  font-weight: 700;
}

.poll-summary {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  color: #64748b;
  font-size: 0.74rem;
  font-weight: 700;
}

.poll-summary span {
  padding: 0;
  background: transparent;
  color: inherit;
  font-size: inherit;
  font-weight: inherit;
  text-transform: none;
}

.poll-results,
.poll-options {
  display: grid;
  gap: 9px;
}

.poll-option {
  width: 100%;
  display: grid;
  gap: 7px;
  border: 1px solid rgba(203, 213, 225, 0.9);
  border-radius: var(--qaly-radius-lg) !important;
  padding: 10px;
  background: #ffffff !important;
  color: #0f172a !important;
  text-align: left;
  cursor: pointer;
  transition: transform 160ms ease, border-color 160ms ease, box-shadow 160ms ease;
}

.poll-option:hover:not(:disabled) {
  transform: translateY(-1px);
  border-color: rgba(37, 99, 235, 0.42);
  box-shadow: var(--qaly-shadow-md);
}

.poll-option.is-selected {
  border-color: rgba(37, 99, 235, 0.64);
  background:
    linear-gradient(135deg, rgba(239, 246, 255, 0.98), rgba(219, 234, 254, 0.72));
  box-shadow: var(--qaly-shadow-md);
}

.option-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.option-choice {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 9px;
}

.option-check {
  width: 20px;
  height: 20px;
  flex: 0 0 20px;
  display: grid;
  place-items: center;
  border: 1.5px solid #cbd5e1;
  border-radius: 50%;
  background: #fff;
  color: #fff;
}

.poll-option.is-selected .option-check {
  border-color: #2563eb;
  background: #2563eb;
}

.option-row strong {
  color: #1d4ed8;
  font-size: 0.78rem;
  white-space: nowrap;
}

.option-label {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  color: #0f172a !important;
  font-weight: 800;
}

.percent {
  width: 100%;
  background: #e2e8f0;
  height: 8px;
  border-radius: 999px;
  overflow: hidden;
}

.bar {
  min-width: 3px;
  background: linear-gradient(90deg, #2563eb, #60a5fa);
  height: 100%;
  border-radius: inherit;
  transition: width 220ms ease;
}

.poll-voters {
  min-height: 26px;
  display: flex;
  align-items: center;
  gap: 6px;
  cursor: pointer;
}

.poll-voters span {
  width: 24px;
  height: 24px;
  display: grid;
  place-items: center;
  margin-right: -10px;
  border: 2px solid #fff;
  border-radius: 999px;
  background: linear-gradient(135deg, #1d4ed8, #60a5fa);
  color: #fff;
  font-size: 0.62rem;
  font-weight: 900;
}

.poll-voters small {
  margin-left: 10px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.poll-voter-list {
  display: grid;
  gap: 7px;
  padding-top: 8px;
  border-top: 1px solid #e2e8f0;
}

.poll-voter-list > div {
  display: grid;
  grid-template-columns: 30px minmax(0, 1fr) 18px;
  align-items: center;
  gap: 8px;
}

.poll-voter-list > div > span {
  width: 30px;
  height: 30px;
  display: grid;
  place-items: center;
  border-radius: 50%;
  background: #dbeafe;
  color: #1d4ed8;
  font-size: 0.68rem;
  font-weight: 900;
}

.poll-voter-list p {
  min-width: 0;
  display: grid;
  margin: 0;
}

.poll-voter-list p strong,
.poll-voter-list p small {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.poll-voter-list > div > svg {
  color: #2563eb;
}

.poll-card .poll-submit {
  width: 100%;
  min-height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 7px;
  margin-top: 11px;
  border: 0;
  border-radius: var(--qaly-radius-lg);
  background: #1769e0;
  color: #fff !important;
  font-weight: 800;
  cursor: pointer;
}

.poll-submit:disabled {
  opacity: 0.55;
}

.poll-closed {
  margin-top: 10px;
  padding: 9px;
  border-radius: var(--qaly-radius-lg);
  background: #f1f5f9;
  color: #64748b;
  text-align: center;
  font-size: 0.78rem;
  font-weight: 800;
}

button:disabled {
  cursor: default;
}
</style>
