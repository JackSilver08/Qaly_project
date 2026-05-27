<script setup lang="ts">
import { HubConnectionBuilder, HubConnectionState, type HubConnection } from '@microsoft/signalr'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import ChatSidebar from '../components/chat/ChatSidebar.vue'
import ChatWindow from '../components/chat/ChatWindow.vue'
import GroupAiPanel from '../components/chat/GroupAiPanel.vue'
import type { ChatGroupModel, TeamChatAttachment, TeamChatMessage, TeamChatPoll } from '../components/chat/chat-types'
import { useDashboardContext } from '../composables/dashboard-context'
import { showError, showSuccess } from '../composables/use-toast'
import { apiResult, errorMessage } from '../utils/api-client'
import type { PagedResult } from '../types'

interface GroupDto {
  id: string
  name: string
  description: string | null
  messageCount: number
}

interface GroupMessageDto {
  id: string
  workGroupId: string
  userId: string
  senderName: string
  content: string
  messageType: string
  createdAt: string
}

const { currentUser } = useDashboardContext()

const groups = ref<ChatGroupModel[]>([])
const messages = ref<TeamChatMessage[]>([])
const activeGroupId = ref('')
const isLoadingGroups = ref(false)
const isLoadingMessages = ref(false)
const loadError = ref<string | null>(null)
const realtimeState = ref<'connecting' | 'connected' | 'offline'>('offline')
let hubConnection: HubConnection | null = null

const currentUserId = computed(() => currentUser.value?.id ?? 'me')
const activeGroup = computed(() => groups.value.find((group) => group.id === activeGroupId.value) ?? null)
const activeMessages = computed(() => messages.value.filter((message) => message.groupId === activeGroupId.value))

onMounted(async () => {
  await loadGroups()
  await connectRealtime()
})

onBeforeUnmount(async () => {
  if (hubConnection) {
    await hubConnection.stop()
    hubConnection = null
  }
})

watch(activeGroupId, async (next, previous) => {
  if (previous && hubConnection?.state === HubConnectionState.Connected) {
    await hubConnection.invoke('LeaveGroup', previous).catch(() => undefined)
  }

  if (!next) return
  await loadMessages(next)

  if (hubConnection?.state === HubConnectionState.Connected) {
    await hubConnection.invoke('JoinGroup', next).catch(() => undefined)
  }
})

async function loadGroups() {
  isLoadingGroups.value = true
  loadError.value = null

  try {
    const result = await apiResult<PagedResult<GroupDto>>('/api/groups?pageSize=50')
    groups.value = result.items.map(toGroupModel)

    if (!activeGroupId.value && groups.value.length > 0) {
      activeGroupId.value = groups.value[0].id
    }
  } catch (error) {
    loadError.value = errorMessage(error, 'Khong the tai danh sach nhom chat.')
    showError(loadError.value)
  } finally {
    isLoadingGroups.value = false
  }
}

async function loadMessages(groupId: string) {
  isLoadingMessages.value = true

  try {
    const result = await apiResult<PagedResult<GroupMessageDto>>(`/api/groups/${groupId}/messages?pageSize=100`)
    const mapped = result.items.map(toMessageModel)
    messages.value = [
      ...messages.value.filter((message) => message.groupId !== groupId),
      ...mapped,
    ]
  } catch (error) {
    showError(errorMessage(error, 'Khong the tai tin nhan nhom.'))
  } finally {
    isLoadingMessages.value = false
  }
}

async function connectRealtime() {
  realtimeState.value = 'connecting'
  hubConnection = new HubConnectionBuilder()
    .withUrl('/hubs/groups')
    .withAutomaticReconnect()
    .build()

  hubConnection.onreconnecting(() => {
    realtimeState.value = 'connecting'
  })
  hubConnection.onreconnected(async () => {
    realtimeState.value = 'connected'
    if (activeGroupId.value) await hubConnection?.invoke('JoinGroup', activeGroupId.value).catch(() => undefined)
  })
  hubConnection.onclose(() => {
    realtimeState.value = 'offline'
  })
  hubConnection.on('groupMessageReceived', (message: GroupMessageDto) => {
    upsertMessage(toMessageModel(message))
  })

  try {
    await hubConnection.start()
    realtimeState.value = 'connected'
    if (activeGroupId.value) await hubConnection.invoke('JoinGroup', activeGroupId.value).catch(() => undefined)
  } catch {
    realtimeState.value = 'offline'
  }
}

async function createGroup() {
  const name = window.prompt('Ten nhom chat moi')
  if (!name?.trim()) return

  try {
    const group = await apiResult<GroupDto>('/api/groups', {
      method: 'POST',
      body: JSON.stringify({
        name: name.trim(),
        description: 'Nhom chat moi',
      }),
    })

    const mapped = toGroupModel(group)
    groups.value = [mapped, ...groups.value.filter((item) => item.id !== mapped.id)]
    activeGroupId.value = mapped.id
    showSuccess(`Tao nhom "${mapped.name}" thanh cong`)
  } catch (error) {
    showError(errorMessage(error, 'Khong the tao nhom chat.'))
  }
}

async function sendMessage(payload: { text: string; attachments: TeamChatAttachment[]; poll?: TeamChatPoll }) {
  if (!activeGroupId.value) return

  const content = serializeMessagePayload(payload)
  const messageType = payload.poll ? 'Poll' : 'Text'

  try {
    if (hubConnection?.state === HubConnectionState.Connected) {
      await hubConnection.invoke('SendMessage', activeGroupId.value, content, messageType)
      return
    }

    const saved = await apiResult<GroupMessageDto>(`/api/groups/${activeGroupId.value}/messages`, {
      method: 'POST',
      body: JSON.stringify({ content, messageType }),
    })
    upsertMessage(toMessageModel(saved))
  } catch (error) {
    showError(errorMessage(error, 'Khong the gui tin nhan.'))
  }
}

function togglePin(messageId: string) {
  const target = messages.value.find((message) => message.id === messageId)
  messages.value = messages.value.map((message) =>
    message.id === messageId ? { ...message, pinned: !message.pinned } : message,
  )
  if (target) showSuccess(target.pinned ? 'Da bo ghim tin nhan' : 'Da ghim tin nhan')
}

function upsertMessage(message: TeamChatMessage) {
  messages.value = [
    ...messages.value.filter((item) => item.id !== message.id),
    message,
  ]

  groups.value = groups.value.map((group) =>
    group.id === message.groupId
      ? { ...group, unreadCount: group.id === activeGroupId.value ? 0 : group.unreadCount + 1 }
      : group,
  )
}

function toGroupModel(group: GroupDto): ChatGroupModel {
  return {
    id: group.id,
    name: group.name,
    description: group.description ?? `${group.messageCount} tin nhan`,
    unreadCount: 0,
  }
}

function toMessageModel(message: GroupMessageDto): TeamChatMessage {
  return {
    id: message.id,
    groupId: message.workGroupId,
    senderId: message.userId,
    senderName: message.senderName,
    senderInitials: initials(message.senderName),
    text: message.content,
    createdAt: formatMessageTime(message.createdAt),
    pinned: false,
    attachments: [],
    poll: parsePoll(message),
  }
}

function serializeMessagePayload(payload: { text: string; attachments: TeamChatAttachment[]; poll?: TeamChatPoll }) {
  const lines = [payload.text]

  if (payload.poll) {
    lines.push(`[poll] ${payload.poll.question}`)
    payload.poll.options.forEach((option, index) => lines.push(`${index + 1}. ${option}`))
  }

  if (payload.attachments.length > 0) {
    lines.push(`[attachments] ${payload.attachments.map((file) => file.name).join(', ')}`)
  }

  return lines.filter(Boolean).join('\n')
}

function parsePoll(message: GroupMessageDto): TeamChatPoll | undefined {
  if (message.messageType !== 'Poll') return undefined

  const lines = message.content.split('\n').map((line) => line.trim()).filter(Boolean)
  const question = lines.find((line) => line.startsWith('[poll] '))?.replace('[poll] ', '') ?? lines[0]
  const options = lines
    .filter((line) => /^\d+\.\s+/.test(line))
    .map((line) => line.replace(/^\d+\.\s+/, ''))

  return question && options.length >= 2 ? { question, options } : undefined
}

function initials(name: string) {
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')
}

function formatMessageTime(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return new Intl.DateTimeFormat('vi', { hour: '2-digit', minute: '2-digit' }).format(date)
}
</script>

<template>
  <div class="dashboard-scroll dashboard-scroll--embedded no-scrollbar">
    <div class="dashboard-main project-home-main no-scrollbar">
      <section class="team-chat-page">
        <div v-if="loadError" class="team-chat-banner team-chat-banner--error">{{ loadError }}</div>
        <div v-else-if="isLoadingGroups" class="team-chat-banner">Dang tai nhom chat...</div>
        <div v-else-if="groups.length === 0" class="team-chat-banner">
          Chua co nhom chat. Tao nhom moi de bat dau trao doi.
        </div>

        <ChatSidebar
          :groups="groups"
          :active-group-id="activeGroupId"
          @select="activeGroupId = $event"
          @create="createGroup"
        />
        <ChatWindow
          :group="activeGroup"
          :messages="activeMessages"
          :current-user-id="currentUserId"
          @send="sendMessage"
          @pin="togglePin"
        />
        <GroupAiPanel :group-id="activeGroupId" />

        <div class="team-chat-status">
          <span :class="`team-chat-status__dot team-chat-status__dot--${realtimeState}`"></span>
          {{ realtimeState === 'connected' ? 'Realtime dang bat' : realtimeState === 'connecting' ? 'Dang noi realtime' : 'Realtime tam thoi offline' }}
          <span v-if="isLoadingMessages"> · Dang tai tin nhan...</span>
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.team-chat-page {
  position: relative;
  grid-template-columns: 280px minmax(0, 1fr) 320px;
}

.team-chat-banner {
  position: absolute;
  inset: 16px 24px auto 24px;
  z-index: 2;
  border: 1px solid rgba(59, 130, 246, 0.18);
  border-radius: 10px;
  background: rgba(239, 246, 255, 0.92);
  color: #1e3a8a;
  padding: 10px 14px;
  font-size: 0.88rem;
  font-weight: 600;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.08);
}

.team-chat-banner--error {
  border-color: rgba(239, 68, 68, 0.22);
  background: rgba(254, 242, 242, 0.94);
  color: #991b1b;
}

.team-chat-status {
  position: absolute;
  right: 28px;
  bottom: 16px;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.88);
  color: #475569;
  padding: 7px 12px;
  font-size: 0.78rem;
  box-shadow: 0 8px 20px rgba(15, 23, 42, 0.08);
}

.team-chat-status__dot {
  width: 8px;
  height: 8px;
  border-radius: 999px;
  background: #94a3b8;
}

.team-chat-status__dot--connected {
  background: #22c55e;
}

.team-chat-status__dot--connecting {
  background: #f59e0b;
}

.team-chat-status__dot--offline {
  background: #ef4444;
}

@media (max-width: 1180px) {
  .team-chat-page {
    grid-template-columns: 240px minmax(0, 1fr);
  }

  .group-ai-panel {
    grid-column: 1 / -1;
  }
}

@media (max-width: 980px) {
  .team-chat-page {
    grid-template-columns: 1fr;
  }
}
</style>
