<script setup lang="ts">
import { computed, ref } from 'vue'
import ChatSidebar from '../components/chat/ChatSidebar.vue'
import ChatWindow from '../components/chat/ChatWindow.vue'
import type { ChatGroupModel, TeamChatAttachment, TeamChatMessage, TeamChatPoll } from '../components/chat/chat-types'
import { useDashboardContext } from '../composables/dashboard-context'

const { currentUser } = useDashboardContext()

const groups = ref<ChatGroupModel[]>([
  { id: 'core', name: 'Qaly Core', description: 'Điều phối triển khai hằng ngày', unreadCount: 2 },
  { id: 'design', name: 'Design System', description: 'UI, component và guideline', unreadCount: 0 },
  { id: 'release', name: 'Release Room', description: 'Checklist trước khi phát hành', unreadCount: 1 },
])

const activeGroupId = ref(groups.value[0]?.id ?? '')

const messages = ref<TeamChatMessage[]>([
  {
    id: 'm1',
    groupId: 'core',
    senderId: 'admin',
    senderName: 'Quản trị viên',
    senderInitials: 'QT',
    text: 'Mọi người cập nhật tiến độ API dashboard trước 16:00 nhé.',
    createdAt: '09:12',
    pinned: true,
    attachments: [],
  },
  {
    id: 'm2',
    groupId: 'core',
    senderId: 'me',
    senderName: 'Bạn',
    senderInitials: 'BU',
    text: 'Đã xong phần routing, đang kiểm tra responsive.',
    createdAt: '09:24',
    pinned: false,
    attachments: [],
  },
  {
    id: 'm3',
    groupId: 'design',
    senderId: 'design',
    senderName: 'Trần Thị B',
    senderInitials: 'TB',
    text: 'Poll nhanh cho layout project card.',
    createdAt: '10:05',
    pinned: false,
    attachments: [],
    poll: {
      question: 'Dùng card hay table cho danh sách dự án?',
      options: ['Card', 'Table', 'Hybrid'],
    },
  },
])

const currentUserId = computed(() => currentUser.value?.id ?? 'me')
const activeGroup = computed(() => groups.value.find((group) => group.id === activeGroupId.value) ?? null)
const activeMessages = computed(() => messages.value.filter((message) => message.groupId === activeGroupId.value))

function createGroup() {
  const name = window.prompt('Tên nhóm chat mới')
  if (!name?.trim()) return

  const group = {
    id: `group-${Date.now()}`,
    name: name.trim(),
    description: 'Nhóm chat mới',
    unreadCount: 0,
  }

  groups.value = [group, ...groups.value]
  activeGroupId.value = group.id
}

function sendMessage(payload: { text: string; attachments: TeamChatAttachment[]; poll?: TeamChatPoll }) {
  if (!activeGroupId.value) return

  messages.value.push({
    id: `message-${Date.now()}`,
    groupId: activeGroupId.value,
    senderId: currentUserId.value,
    senderName: currentUser.value?.fullName ?? 'Bạn',
    senderInitials: (currentUser.value?.fullName ?? 'Bạn')
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map((part: string) => part[0]?.toUpperCase() ?? '')
      .join(''),
    text: payload.text,
    createdAt: new Intl.DateTimeFormat('vi', { hour: '2-digit', minute: '2-digit' }).format(new Date()),
    pinned: false,
    attachments: payload.attachments,
    poll: payload.poll,
  })
}

function togglePin(messageId: string) {
  messages.value = messages.value.map((message) =>
    message.id === messageId ? { ...message, pinned: !message.pinned } : message,
  )
}
</script>

<template>
  <div class="dashboard-scroll dashboard-scroll--embedded no-scrollbar">
    <div class="dashboard-main project-home-main no-scrollbar">
      <section class="team-chat-page">
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
      </section>
    </div>
  </div>
</template>
