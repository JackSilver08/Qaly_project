<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, watch } from "vue";
import {
  HubConnectionBuilder,
  type HubConnection,
  HubConnectionState,
} from "@microsoft/signalr";
import { apiResult, apiCommand } from "../../utils/api-client";
import type { TeamChatPoll } from "./chat-types";

const props = defineProps<{
  groupId: string;
  poll: TeamChatPoll;
}>();

const results = ref<any | null>(null);
const loading = ref(false);
let hubConnection: HubConnection | null = null;

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
      <strong>{{ props.poll.question }}</strong>
    </div>

    <div v-if="results" class="poll-results">
      <div
        v-for="opt in results.options"
        :key="opt.optionId || opt.id"
        class="poll-option"
      >
        <div class="option-label">
          {{ opt.content || opt.Content || opt.content }}
        </div>
        <div class="option-stats">
          <div class="votes">
            {{ opt.voteCount ?? opt.voteCount ?? 0 }} votes
          </div>
          <div class="percent">
            <div
              class="bar"
              :style="{
                width:
                  ((opt.voteCount || 0) / (results.totalVotes || 1)) * 100 +
                  '%',
              }"
            ></div>
          </div>
        </div>
        <div>
          <button
            v-if="
              results.currentUserOptionIds &&
              !results.currentUserOptionIds.length
            "
            class="primary-button"
            @click="vote(opt.optionId || opt.id || opt.optionId)"
            :disabled="loading"
          >
            Vote
          </button>
        </div>
      </div>
    </div>

    <div v-else class="poll-options">
      <div
        v-for="(opt, idx) in props.poll.options"
        :key="idx"
        class="poll-option"
      >
        <button class="primary-button" disabled>{{ opt }}</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.poll-card {
  padding: 8px;
  border-radius: 8px;
  background: #fff;
  box-shadow: 0 4px 12px rgba(2, 6, 23, 0.06);
}
.poll-option {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-top: 8px;
}
.option-stats {
  flex: 1;
}
.percent {
  background: #e6eefc;
  height: 8px;
  border-radius: 4px;
  overflow: hidden;
}
.bar {
  background: #2563eb;
  height: 8px;
}
.primary-button {
  background: #2563eb;
  color: #fff;
  padding: 6px 10px;
  border-radius: 6px;
}
</style>
