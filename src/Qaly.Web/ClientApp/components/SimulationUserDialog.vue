<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { Eye, Search, ShieldCheck, X } from 'lucide-vue-next'
import type { SimulationUser } from './shell-models'

const props = defineProps<{
  open: boolean
  users: SimulationUser[]
}>()

const emit = defineEmits<{
  close: []
  select: [user: SimulationUser]
}>()

const query = ref('')
const searchInput = ref<HTMLInputElement | null>(null)
const dialogPanel = ref<HTMLElement | null>(null)

const availableUsers = computed(() => props.users
  .filter(user => user.role.toLowerCase() !== 'admin')
  .sort((left, right) => left.fullName.localeCompare(right.fullName, 'vi')))

const filteredUsers = computed(() => {
  const normalizedQuery = query.value.trim().toLocaleLowerCase('vi')
  if (!normalizedQuery) return availableUsers.value
  return availableUsers.value.filter(user =>
    `${user.fullName} ${user.role}`.toLocaleLowerCase('vi').includes(normalizedQuery),
  )
})

function initials(name: string) {
  return name
    .trim()
    .split(/\s+/)
    .slice(-2)
    .map(part => part[0]?.toUpperCase() ?? '')
    .join('') || 'U'
}

function roleLabel(role: string) {
  const labels: Record<string, string> = {
    moderator: 'Điều phối viên',
    manager: 'Quản lý',
    member: 'Thành viên',
  }
  return labels[role.toLowerCase()] ?? role
}

function selectUser(user: SimulationUser) {
  emit('select', user)
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    event.preventDefault()
    emit('close')
    return
  }

  if (event.key !== 'Tab' || !dialogPanel.value) return
  const focusable = Array.from(dialogPanel.value.querySelectorAll<HTMLElement>(
    'button:not([disabled]), input:not([disabled]), [tabindex]:not([tabindex="-1"])',
  ))
  if (!focusable.length) return

  const first = focusable[0]
  const last = focusable[focusable.length - 1]
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault()
    first.focus()
  }
}

watch(
  () => props.open,
  async open => {
    if (!open) return
    query.value = ''
    await nextTick()
    searchInput.value?.focus()
  },
)
</script>

<template>
  <Teleport to="body">
    <Transition name="simulation-dialog">
      <div
        v-if="open"
        class="simulation-dialog-backdrop"
        @pointerdown.self="$emit('close')"
        @keydown="handleKeydown"
      >
        <section
          ref="dialogPanel"
          class="simulation-dialog"
          role="dialog"
          aria-modal="true"
          aria-labelledby="simulation-dialog-title"
          aria-describedby="simulation-dialog-description"
        >
          <header class="simulation-dialog__header">
            <div class="simulation-dialog__icon" aria-hidden="true">
              <Eye :size="22" />
            </div>
            <div>
              <span class="simulation-dialog__eyebrow">Công cụ quản trị</span>
              <h2 id="simulation-dialog-title">Xem với vai trò người dùng</h2>
            </div>
            <button class="simulation-dialog__close" type="button" aria-label="Đóng" @click="$emit('close')">
              <X :size="19" />
            </button>
          </header>

          <p id="simulation-dialog-description" class="simulation-dialog__description">
            Kiểm tra chính xác những gì một thành viên có thể nhìn thấy trong không gian làm việc.
          </p>

          <div class="simulation-dialog__safety" role="note">
            <ShieldCheck :size="19" aria-hidden="true" />
            <div>
              <strong>Chế độ chỉ đọc</strong>
              <span>Mọi thao tác tạo, sửa hoặc xóa dữ liệu đều bị máy chủ chặn.</span>
            </div>
          </div>

          <label class="simulation-dialog__search">
            <Search :size="18" aria-hidden="true" />
            <input
              ref="searchInput"
              v-model="query"
              type="search"
              placeholder="Tìm theo tên hoặc vai trò..."
              aria-label="Tìm người dùng để xem giao diện"
            />
          </label>

          <div class="simulation-dialog__list" aria-live="polite">
            <button
              v-for="user in filteredUsers"
              :key="user.id"
              class="simulation-user"
              type="button"
              @click="selectUser(user)"
            >
              <span class="simulation-user__avatar" aria-hidden="true">{{ initials(user.fullName) }}</span>
              <span class="simulation-user__identity">
                <strong>{{ user.fullName }}</strong>
                <small>{{ roleLabel(user.role) }}</small>
              </span>
              <span class="simulation-user__action">Xem giao diện</span>
            </button>

            <div v-if="!filteredUsers.length" class="simulation-dialog__empty">
              Không tìm thấy người dùng phù hợp.
            </div>
          </div>

          <footer class="simulation-dialog__footer">
            <span>Trạng thái giả lập sẽ luôn hiển thị trên thanh đầu trang.</span>
            <button type="button" @click="$emit('close')">Hủy</button>
          </footer>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.simulation-dialog-backdrop {
  position: fixed;
  inset: 0;
  z-index: 240;
  display: grid;
  place-items: center;
  padding: 20px;
  background: rgba(15, 23, 42, 0.42);
  backdrop-filter: blur(5px);
}

.simulation-dialog {
  width: min(560px, 100%);
  max-height: min(720px, calc(100dvh - 40px));
  display: grid;
  grid-template-rows: auto auto auto auto minmax(120px, 1fr) auto;
  overflow: hidden;
  border: 1px solid rgba(148, 163, 184, 0.28);
  border-radius: 22px;
  color: var(--text);
  background: rgba(255, 255, 255, 0.98);
  box-shadow: 0 28px 80px rgba(15, 23, 42, 0.24);
}

.simulation-dialog__header {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 13px;
  padding: 22px 22px 12px;
}

.simulation-dialog__icon {
  width: 44px;
  height: 44px;
  display: grid;
  place-items: center;
  border: 1px solid #bfdbfe;
  border-radius: 14px;
  color: #1d4ed8;
  background: linear-gradient(145deg, #eff6ff, #dbeafe);
  box-shadow: 0 10px 24px rgba(37, 99, 235, 0.12);
}

.simulation-dialog__eyebrow {
  color: #2563eb;
  font-size: 11px;
  font-weight: 900;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.simulation-dialog h2 {
  margin: 3px 0 0;
  color: var(--text-strong);
  font-size: 20px;
  line-height: 1.25;
}

.simulation-dialog__close {
  width: 36px;
  height: 36px;
  display: grid;
  place-items: center;
  border: 1px solid var(--line);
  border-radius: 11px;
  color: var(--muted);
  background: var(--panel-soft);
  cursor: pointer;
}

.simulation-dialog__description {
  margin: 0;
  padding: 0 22px 16px;
  color: var(--muted);
  font-size: 14px;
  line-height: 1.55;
}

.simulation-dialog__safety {
  display: flex;
  align-items: flex-start;
  gap: 11px;
  margin: 0 22px 16px;
  padding: 13px 14px;
  border: 1px solid #fde68a;
  border-radius: 14px;
  color: #92400e;
  background: #fffbeb;
}

.simulation-dialog__safety svg { flex: 0 0 auto; margin-top: 1px; color: #d97706; }
.simulation-dialog__safety div { display: grid; gap: 2px; }
.simulation-dialog__safety strong { font-size: 13px; }
.simulation-dialog__safety span { font-size: 12px; line-height: 1.45; }

.simulation-dialog__search {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr);
  align-items: center;
  gap: 10px;
  margin: 0 22px 12px;
  padding: 0 13px;
  border: 1px solid var(--line);
  border-radius: 13px;
  color: var(--muted);
  background: var(--panel-soft);
  transition: border-color 200ms ease-out, box-shadow 200ms ease-out, background 200ms ease-out;
}

.simulation-dialog__search:focus-within {
  border-color: #60a5fa;
  background: var(--panel);
  box-shadow: 0 0 0 4px rgba(59, 130, 246, 0.12);
}

.simulation-dialog__search input {
  width: 100%;
  height: 42px;
  padding: 0;
  border: 0 !important;
  outline: 0 !important;
  color: var(--text-strong) !important;
  background: transparent !important;
  box-shadow: none !important;
  font-size: 14px;
}

.simulation-dialog__search input:focus-visible {
  outline: 0 !important;
  outline-offset: 0 !important;
}

.simulation-dialog__list {
  min-height: 0;
  display: grid;
  align-content: start;
  gap: 7px;
  overflow-y: auto;
  padding: 2px 14px 14px 22px;
  scrollbar-gutter: stable;
}

.simulation-user {
  width: 100%;
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  border: 1px solid transparent;
  border-radius: 14px;
  color: var(--text);
  background: transparent;
  text-align: left;
  cursor: pointer;
  transition: transform 200ms ease-out, border-color 200ms ease-out, background 200ms ease-out;
}

.simulation-user:hover,
.simulation-user:focus-visible {
  transform: translateY(-1px);
  border-color: #bfdbfe;
  background: #f8fbff;
  outline: none;
}

.simulation-user__avatar {
  width: 38px;
  height: 38px;
  display: grid;
  place-items: center;
  border-radius: 12px;
  color: #1e40af;
  background: #dbeafe;
  font-size: 11px;
  font-weight: 900;
}

.simulation-user__identity { min-width: 0; display: grid; gap: 2px; }
.simulation-user__identity strong { overflow: hidden; color: var(--text-strong); font-size: 14px; text-overflow: ellipsis; white-space: nowrap; }
.simulation-user__identity small { color: var(--muted); font-size: 12px; }
.simulation-user__action { color: #2563eb; font-size: 12px; font-weight: 800; }

.simulation-dialog__empty {
  padding: 30px 18px;
  border: 1px dashed var(--line);
  border-radius: 14px;
  color: var(--muted);
  background: var(--panel-soft);
  text-align: center;
  font-size: 13px;
  font-weight: 700;
}

.simulation-dialog__footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 14px 22px;
  border-top: 1px solid var(--line);
  background: var(--panel-soft);
}

.simulation-dialog__footer span { color: var(--muted); font-size: 11px; line-height: 1.4; }
.simulation-dialog__footer button {
  flex: 0 0 auto;
  min-height: 36px;
  padding: 0 15px;
  border: 1px solid var(--line);
  border-radius: 11px;
  color: var(--text);
  background: var(--panel);
  font-weight: 800;
  cursor: pointer;
}

.simulation-dialog__close:hover,
.simulation-dialog__close:focus-visible,
.simulation-dialog__footer button:hover,
.simulation-dialog__footer button:focus-visible {
  border-color: #93c5fd;
  color: #1d4ed8;
  outline: none;
}

.simulation-dialog-enter-active,
.simulation-dialog-leave-active { transition: opacity 200ms ease-out; }
.simulation-dialog-enter-active .simulation-dialog,
.simulation-dialog-leave-active .simulation-dialog { transition: transform 240ms ease-out, opacity 200ms ease-out; }
.simulation-dialog-enter-from,
.simulation-dialog-leave-to { opacity: 0; }
.simulation-dialog-enter-from .simulation-dialog,
.simulation-dialog-leave-to .simulation-dialog { opacity: 0; transform: translateY(12px) scale(0.98); }

:global(:root[data-theme='dark'] .simulation-dialog) {
  border-color: rgba(148, 163, 184, 0.24);
  background: rgba(15, 23, 42, 0.98);
}
:global(:root[data-theme='dark'] .simulation-dialog__safety) {
  border-color: rgba(245, 158, 11, 0.35);
  color: #fde68a;
  background: rgba(120, 53, 15, 0.28);
}
:global(:root[data-theme='dark'] .simulation-user:hover),
:global(:root[data-theme='dark'] .simulation-user:focus-visible) {
  border-color: rgba(96, 165, 250, 0.38);
  background: rgba(30, 64, 175, 0.18);
}

@media (max-width: 560px) {
  .simulation-dialog-backdrop { align-items: end; padding: 0; }
  .simulation-dialog { width: 100%; max-height: 92dvh; border-radius: 22px 22px 0 0; }
  .simulation-user__action { display: none; }
  .simulation-dialog__footer span { display: none; }
  .simulation-dialog__footer { justify-content: flex-end; }
}

@media (prefers-reduced-motion: reduce) {
  .simulation-dialog-enter-active,
  .simulation-dialog-leave-active,
  .simulation-dialog-enter-active .simulation-dialog,
  .simulation-dialog-leave-active .simulation-dialog,
  .simulation-user { transition: none; }
}
</style>
