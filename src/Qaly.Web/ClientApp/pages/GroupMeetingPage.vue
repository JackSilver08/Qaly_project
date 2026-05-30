<script setup lang="ts">
import { ref, onBeforeUnmount } from "vue";
import {
  HubConnectionBuilder,
  type HubConnection,
  HubConnectionState,
} from "@microsoft/signalr";
import ScreenSharePanel from "../components/meeting/ScreenSharePanel.vue";
import MeetingControls from "../components/meeting/MeetingControls.vue";
import { useRoute } from "vue-router";

const route = useRoute();
const groupId = route.params.groupId as string;

const active = ref(false);
let hubConnection: HubConnection | null = null;
const participants = ref<{ connectionId: string; lastSeen: string }[]>([]);

async function startMeeting() {
  active.value = true;
  hubConnection = new HubConnectionBuilder()
    .withUrl("/hubs/groups")
    .withAutomaticReconnect()
    .build();
  hubConnection.on("peerSignal", (payload: any) => {
    try {
      const from = payload?.from;
      if (!from) return;
      const existing = participants.value.find((p) => p.connectionId === from);
      if (!existing)
        participants.value.push({
          connectionId: from,
          lastSeen: new Date().toISOString(),
        });
      else existing.lastSeen = new Date().toISOString();
    } catch (e) {
      console.warn("peerSignal", e);
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
  if (hubConnection && hubConnection.state === HubConnectionState.Connected) {
    try {
      await hubConnection.invoke("LeaveGroup", groupId).catch(() => undefined);
      await hubConnection.stop();
    } catch {}
    hubConnection = null;
  }
  active.value = false;
  participants.value = [];
}

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
    <h1 class="text-2xl font-semibold mb-4">Meeting — Nhóm</h1>
    <p class="text-sm text-slate-500 mb-6">Group: {{ groupId }}</p>

    <MeetingControls :active="active" @start="startMeeting" @end="endMeeting" />

    <div v-if="active" class="mt-6 grid grid-cols-1 md:grid-cols-2 gap-4">
      <ScreenSharePanel />
      <div class="glass-card p-4">
        <h3 class="font-medium">Participants</h3>
        <div v-if="participants.length === 0" class="text-sm text-slate-500">
          (Chưa có người tham gia)
        </div>
        <ul v-else>
          <li v-for="p in participants" :key="p.connectionId">
            {{ p.connectionId }} · <small>{{ p.lastSeen }}</small>
          </li>
        </ul>
      </div>
    </div>
  </div>
</template>

<style scoped>
.glass-card {
  background: white;
  border-radius: 8px;
  padding: 12px;
  box-shadow: 0 6px 20px rgba(2, 6, 23, 0.06);
}
</style>
