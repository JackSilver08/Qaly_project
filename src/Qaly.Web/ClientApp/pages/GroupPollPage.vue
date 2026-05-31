<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount } from "vue";
import {
  HubConnectionBuilder,
  type HubConnection,
  HubConnectionState,
} from "@microsoft/signalr";
import { useRoute } from "vue-router";
import { apiResult, apiCommand, errorMessage } from "../utils/api-client";
import { showError, showSuccess } from "../composables/use-toast";

const route = useRoute();
const groupId = route.params.groupId as string;
const pollId = route.params.pollId as string | undefined;

const question = ref("");
const options = ref(["", ""]);
const results = ref<any | null>(null);
let hubConnection: HubConnection | null = null;

async function createPoll() {
  try {
    const payload = {
      question: question.value,
      options: options.value
        .map((option) => option.trim())
        .filter(Boolean)
        .map((content) => ({ content })),
      allowMultiple: false,
    };
    await apiResult(`/api/groups/${groupId}/polls`, {
      method: "POST",
      body: JSON.stringify(payload),
    });
    showSuccess("Poll created");
  } catch (e) {
    showError(errorMessage(e, "Không thể tạo poll"));
  }
}

async function loadResults() {
  if (!pollId) return;
  try {
    results.value = await apiResult(
      `/api/groups/${groupId}/polls/${pollId}/results`,
    );
  } catch (e) {
    showError(errorMessage(e, "Không thể tải kết quả"));
  }
}

async function vote(optionId: string) {
  if (!pollId) return;
  try {
    await apiResult(`/api/groups/${groupId}/polls/${pollId}/vote`, {
      method: "POST",
      body: JSON.stringify({ optionIds: [optionId] }),
    });
    showSuccess("Đã bỏ phiếu");
    await loadResults();
  } catch (e) {
    showError(errorMessage(e, "Không thể bỏ phiếu"));
  }
}

async function closePoll() {
  if (!pollId) return;
  try {
    await apiCommand(`/api/groups/${groupId}/polls/${pollId}/close`, {
      method: "PUT",
    });
    showSuccess("Poll đã được đóng");
    await loadResults();
  } catch (e) {
    showError(errorMessage(e, "Không thể đóng poll"));
  }
}

onMounted(async () => {
  if (pollId) void loadResults();

  hubConnection = new HubConnectionBuilder()
    .withUrl("/hubs/groups")
    .withAutomaticReconnect()
    .build();

  hubConnection.on("PollUpdated", (payload: any) => {
    try {
      if (
        payload &&
        payload.pollId &&
        payload.groupId === groupId &&
        payload.pollId === pollId
      ) {
        results.value = payload.results;
      }
    } catch {
      // ignore
    }
  });

  try {
    await hubConnection.start();
    if (hubConnection.state === HubConnectionState.Connected) {
      await hubConnection.invoke("JoinGroup", groupId).catch(() => undefined);
    }
  } catch (e) {
    console.warn("Could not start group hub for polls", e);
  }
});

onBeforeUnmount(async () => {
  if (hubConnection) {
    try {
      if (hubConnection.state === HubConnectionState.Connected)
        await hubConnection
          .invoke("LeaveGroup", groupId)
          .catch(() => undefined);
      await hubConnection.stop();
    } catch {}
    hubConnection = null;
  }
});
</script>

<template>
  <div class="p-4">
    <h1 class="text-2xl font-semibold mb-4">Poll — Nhóm</h1>
    <p class="text-sm text-slate-500 mb-6">Group: {{ groupId }}</p>

    <section class="glass-card p-4 mb-6">
      <h3 class="font-medium">Tạo poll</h3>
      <input
        v-model="question"
        placeholder="Câu hỏi"
        class="w-full mt-2 p-2 border rounded"
      />
      <div class="mt-2" v-for="(opt, idx) in options" :key="idx">
        <input
          v-model="options[idx]"
          :placeholder="`Lựa chọn ${idx + 1}`"
          class="w-full p-2 border rounded mt-1"
        />
      </div>
      <button class="text-button mt-3" @click="options.push('')">
        Thêm lựa chọn
      </button>
      <div class="mt-3">
        <button class="primary-button" @click="createPoll">Tạo poll</button>
      </div>
    </section>

    <section v-if="results" class="glass-card p-4">
      <h3 class="font-medium">Kết quả</h3>
      <div
        v-for="opt in results.options"
        :key="opt.id || opt.optionId || opt.optionId"
        class="mt-2"
      >
        <div class="flex justify-between items-center">
          <div>{{ opt.content || opt.Content || opt.content }}</div>
          <div class="flex items-center gap-3">
            <div>{{ opt.voteCount ?? opt.voteCount ?? 0 }} votes</div>
            <button
              v-if="
                results.currentUserOptionIds &&
                !results.currentUserOptionIds.length
              "
              class="primary-button"
              @click="vote(opt.optionId || opt.id || opt.optionId)"
            >
              Vote
            </button>
          </div>
        </div>
      </div>
      <div class="mt-4">
        <button class="text-button" @click="closePoll">Đóng poll</button>
      </div>
    </section>
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
  padding: 6px 10px;
  border-radius: 6px;
}
</style>
