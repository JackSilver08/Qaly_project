<script setup lang="ts">
import { computed, ref, onMounted, onBeforeUnmount } from "vue";
import {
  HubConnectionBuilder,
  type HubConnection,
  HubConnectionState,
} from "@microsoft/signalr";
import { apiResult } from "../../utils/api-client";
import type { TeamChatPoll } from "./chat-types";

const props = defineProps<{
  groupId: string;
  poll: TeamChatPoll;
}>();

const results = ref<any | null>(null);
const loading = ref(false);
let hubConnection: HubConnection | null = null;

const hasVoted = computed(() =>
  Boolean(results.value?.currentUserOptionIds?.length),
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
  } catch {
    // ignore
  }
}

async function vote(optionId: string) {
  if (!props.poll?.id) return;
  loading.value = true;
  try {
    await apiResult(
      `/api/groups/${props.groupId}/polls/${props.poll.id}/vote`,
      {
        method: "POST",
        body: JSON.stringify({ optionIds: [optionId] }),
      },
    );
    await loadResults();
  } catch (e) {
    console.warn("Vote failed", e);
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

  try {
    await hubConnection.start();
    if (hubConnection.state === HubConnectionState.Connected) {
      await hubConnection
        .invoke("JoinGroup", props.groupId)
        .catch(() => undefined);
    }
  } catch (e) {
    console.warn("Could not connect poll hub", e);
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
  <div class="poll-card">
    <div class="poll-header">
      <span>Poll</span>
      <strong>{{ props.poll.question }}</strong>
      <small v-if="results">{{ results.totalVoters ?? 0 }} người tham gia</small>
    </div>

    <div v-if="results" class="poll-results">
      <button
        v-for="opt in results.options"
        :key="opt.optionId || opt.id"
        class="poll-option"
        type="button"
        :class="{
          'is-selected': results.currentUserOptionIds?.includes(opt.optionId || opt.id),
        }"
        :disabled="loading"
        @click="vote(opt.optionId || opt.id || opt.optionId)"
      >
        <div class="option-row">
          <span class="option-label">{{ opt.content || opt.Content }}</span>
          <strong>{{ opt.voteCount ?? 0 }} chọn · {{ optionPercent(opt.voteCount || 0) }}%</strong>
        </div>
        <div class="percent">
          <div class="bar" :style="{ width: `${optionPercent(opt.voteCount || 0)}%` }"></div>
        </div>
        <div class="poll-voters">
          <span
            v-for="voter in (opt.voters || opt.Voters || []).slice(0, 4)"
            :key="voter.userId || voter.UserId"
            :title="`${voter.fullName || voter.FullName} - ${voter.email || voter.Email}`"
          >
            {{ initials(voter.fullName || voter.FullName || "") }}
          </span>
          <small>{{ voterNames(opt.voters || opt.Voters || []) }}</small>
        </div>
      </button>
    </div>

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
  </div>
</template>

<style scoped>
.poll-card {
  min-width: min(100%, 340px);
  padding: 14px;
  border: 1px solid rgba(203, 213, 225, 0.82);
  border-radius: 18px;
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(248, 250, 252, 0.96));
  box-shadow: 0 18px 34px rgba(15, 23, 42, 0.08);
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
  border-radius: 14px;
  padding: 10px;
  background: #ffffff;
  color: #0f172a;
  text-align: left;
  cursor: pointer;
  transition: transform 160ms ease, border-color 160ms ease, box-shadow 160ms ease;
}

.poll-option:hover:not(:disabled) {
  transform: translateY(-1px);
  border-color: rgba(37, 99, 235, 0.42);
  box-shadow: 0 12px 22px rgba(37, 99, 235, 0.1);
}

.poll-option.is-selected {
  border-color: rgba(37, 99, 235, 0.64);
  background:
    linear-gradient(135deg, rgba(239, 246, 255, 0.98), rgba(219, 234, 254, 0.72));
  box-shadow: 0 14px 28px rgba(37, 99, 235, 0.12);
}

.option-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
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
  color: #0f172a;
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

button:disabled {
  cursor: default;
}
</style>
