<script setup lang="ts">
import { AlertTriangle, Clock, X, Check } from 'lucide-vue-next'

const props = defineProps<{
  show: boolean
  memberName: string
  activeRoleName: string
  activeRoleStartDate: string
  newRoleName: string
  newRoleStartDate: string
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'confirm'): void
}>()
</script>

<template>
  <Teleport to="body">
    <Transition name="role-modal">
      <div
        v-if="show"
        class="role-modal-backdrop"
        role="presentation"
        @click.self="emit('close')"
      >
        <section
          class="role-modal role-modal--warning"
          role="dialog"
          aria-modal="true"
          aria-labelledby="role-overlap-title"
          aria-describedby="role-overlap-description"
        >
          <header class="role-modal__header">
            <div class="role-modal__heading">
              <span class="role-modal__icon" aria-hidden="true">
                <AlertTriangle :size="20" />
              </span>
              <div>
                <span class="role-modal__eyebrow">Xác nhận thay đổi vai trò</span>
                <h2 id="role-overlap-title">Kết thúc role hiện tại?</h2>
              </div>
            </div>
            <button
              type="button"
              class="role-modal__close"
              aria-label="Đóng hộp thoại"
              @click="emit('close')"
            >
              <X :size="19" />
            </button>
          </header>

          <div class="role-modal__body">
            <p id="role-overlap-description" class="role-modal__description">
              Thành viên <strong>{{ memberName }}</strong> đang giữ role
              <span class="role-token role-token--current">{{ activeRoleName }}</span>
              từ ngày <span class="role-date">{{ activeRoleStartDate }}</span>.
            </p>

            <div class="role-change-flow" aria-label="Thay đổi vai trò">
              <div class="role-change-flow__item">
                <span>Role hiện tại</span>
                <strong>{{ activeRoleName }}</strong>
                <small>Kết thúc: {{ newRoleStartDate }}</small>
              </div>
              <span class="role-change-flow__arrow" aria-hidden="true">→</span>
              <div class="role-change-flow__item role-change-flow__item--new">
                <span>Role mới</span>
                <strong>{{ newRoleName }}</strong>
                <small>Bắt đầu: {{ newRoleStartDate }}</small>
              </div>
            </div>

            <div class="role-modal__notice">
              <div class="role-modal__notice-title">
                <Clock :size="17" />
                <strong>Hệ thống sẽ tự động chốt role cũ</strong>
              </div>
              <p>
                Khi xác nhận, role <span class="role-token role-token--current">{{ activeRoleName }}</span>
                sẽ kết thúc vào ngày <span class="role-date">{{ newRoleStartDate }}</span> và role
                <span class="role-token role-token--new">{{ newRoleName }}</span> sẽ có hiệu lực cùng ngày.
              </p>
              <p class="role-modal__principle">
                Chỉ duy trì <strong>1 Single Active Role</strong> tại mỗi thời điểm.
              </p>
            </div>
          </div>

          <footer class="role-modal__footer">
            <button type="button" class="role-modal__button role-modal__button--secondary" @click="emit('close')">
              Hủy bỏ
            </button>
            <button type="button" class="role-modal__button role-modal__button--confirm" @click="emit('confirm')">
              <Check :size="17" />
              <span>Xác nhận chuyển role</span>
            </button>
          </footer>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.role-modal-backdrop {
  position: fixed;
  inset: 0;
  z-index: 1400;
  display: grid;
  place-items: center;
  overflow-y: auto;
  padding: 24px;
  background: rgba(15, 23, 42, 0.58);
  backdrop-filter: blur(7px);
}

.role-modal {
  position: relative;
  overflow: hidden;
  width: min(100%, 580px);
  max-height: calc(100dvh - 32px);
  display: grid;
  grid-template-rows: auto minmax(0, 1fr) auto;
  border: 1px solid var(--line);
  border-radius: 16px;
  color: var(--text-strong);
  background: var(--panel);
  box-shadow: 0 24px 70px rgba(15, 23, 42, 0.28);
}

.role-modal::before {
  content: '';
  position: absolute;
  inset: 0 0 auto;
  height: 4px;
  background: linear-gradient(90deg, #f59e0b, #f97316);
}

.role-modal__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 22px 24px 18px;
  border-bottom: 1px solid var(--line);
}

.role-modal__heading {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 12px;
}

.role-modal__icon {
  width: 42px;
  height: 42px;
  flex: 0 0 auto;
  display: grid;
  place-items: center;
  border: 1px solid rgba(245, 158, 11, 0.3);
  border-radius: 12px;
  color: #b45309;
  background: rgba(245, 158, 11, 0.12);
}

.role-modal__eyebrow {
  display: block;
  margin-bottom: 2px;
  color: #b45309;
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

.role-modal__heading h2 {
  margin: 0;
  color: var(--text-strong);
  font-size: 20px;
  font-weight: 850;
  line-height: 1.25;
}

.role-modal__close {
  width: 36px;
  height: 36px;
  flex: 0 0 auto;
  display: grid;
  place-items: center;
  padding: 0;
  border: 1px solid var(--line);
  border-radius: 10px;
  color: var(--muted);
  background: var(--bg-soft);
}

.role-modal__close:hover,
.role-modal__close:focus-visible {
  border-color: color-mix(in srgb, var(--primary) 36%, var(--line));
  color: var(--text-strong);
  background: var(--primary-soft);
  outline: 0;
}

.role-modal__body {
  overflow-y: auto;
  display: grid;
  gap: 18px;
  padding: 22px 24px;
}

.role-modal__description,
.role-modal__notice p {
  margin: 0;
  color: var(--muted);
  font-size: 13.5px;
  line-height: 1.65;
}

.role-modal__description strong {
  color: var(--text-strong);
}

.role-token,
.role-date {
  display: inline-flex;
  align-items: center;
  padding: 2px 7px;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 800;
  white-space: nowrap;
}

.role-token--current {
  color: var(--primary-strong);
  background: var(--primary-soft);
}

.role-token--new {
  color: #7e22ce;
  background: rgba(168, 85, 247, 0.12);
}

.role-date {
  color: var(--text-strong);
  background: var(--bg-soft);
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}

.role-change-flow {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto minmax(0, 1fr);
  align-items: center;
  gap: 12px;
}

.role-change-flow__item {
  min-width: 0;
  display: grid;
  gap: 4px;
  padding: 13px 14px;
  border: 1px solid var(--line);
  border-radius: 12px;
  background: var(--bg-soft);
}

.role-change-flow__item--new {
  border-color: rgba(168, 85, 247, 0.3);
  background: color-mix(in srgb, rgba(168, 85, 247, 0.12) 75%, var(--panel));
}

.role-change-flow__item > span,
.role-change-flow__item small {
  color: var(--muted);
  font-size: 11px;
  font-weight: 650;
}

.role-change-flow__item strong {
  overflow: hidden;
  color: var(--text-strong);
  font-size: 14px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.role-change-flow__arrow {
  color: #b45309;
  font-size: 20px;
  font-weight: 800;
}

.role-modal__notice {
  display: grid;
  gap: 8px;
  padding: 14px 16px;
  border: 1px solid rgba(245, 158, 11, 0.32);
  border-radius: 12px;
  background: color-mix(in srgb, rgba(245, 158, 11, 0.12) 78%, var(--panel));
}

.role-modal__notice-title {
  display: flex;
  align-items: center;
  gap: 7px;
  color: #b45309;
  font-size: 13px;
}

.role-modal__principle {
  padding-top: 8px;
  border-top: 1px solid rgba(245, 158, 11, 0.2);
  font-size: 12px !important;
}

.role-modal__principle strong {
  color: var(--text-strong);
}

.role-modal__footer {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 10px;
  padding: 16px 24px 20px;
  border-top: 1px solid var(--line);
  background: var(--bg-soft);
}

.role-modal__button {
  min-height: 40px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 7px;
  padding: 0 15px;
  border-radius: 10px;
  font-size: 13px;
  font-weight: 750;
}

.role-modal__button--secondary {
  border: 1px solid var(--line);
  color: var(--text-strong);
  background: var(--panel);
}

.role-modal__button--secondary:hover,
.role-modal__button--secondary:focus-visible {
  border-color: color-mix(in srgb, var(--primary) 32%, var(--line));
  background: var(--primary-soft);
  outline: 0;
}

.role-modal__button--confirm {
  border: 1px solid #d97706;
  color: #fff;
  background: linear-gradient(135deg, #d97706, #ea580c);
  box-shadow: 0 8px 18px rgba(217, 119, 6, 0.22);
}

.role-modal__button--confirm:hover,
.role-modal__button--confirm:focus-visible {
  background: linear-gradient(135deg, #b45309, #c2410c);
  outline: 0;
  transform: translateY(-1px);
}

.role-modal-enter-active,
.role-modal-leave-active {
  transition: opacity 180ms ease;
}

.role-modal-enter-active .role-modal,
.role-modal-leave-active .role-modal {
  transition: transform 180ms ease, opacity 180ms ease;
}

.role-modal-enter-from,
.role-modal-leave-to {
  opacity: 0;
}

.role-modal-enter-from .role-modal,
.role-modal-leave-to .role-modal {
  opacity: 0;
  transform: translateY(10px) scale(0.98);
}

@media (max-width: 640px) {
  .role-modal-backdrop {
    align-items: end;
    padding: 12px;
  }

  .role-modal {
    max-height: calc(100dvh - 24px);
    border-radius: 16px 16px 12px 12px;
  }

  .role-modal__header,
  .role-modal__body,
  .role-modal__footer {
    padding-left: 16px;
    padding-right: 16px;
  }

  .role-change-flow {
    grid-template-columns: 1fr;
  }

  .role-change-flow__arrow {
    justify-self: center;
    line-height: 1;
    transform: rotate(90deg);
  }

  .role-modal__footer {
    align-items: stretch;
    flex-direction: column-reverse;
  }

  .role-modal__button {
    width: 100%;
  }
}

@media (prefers-reduced-motion: reduce) {
  .role-modal-enter-active,
  .role-modal-leave-active,
  .role-modal-enter-active .role-modal,
  .role-modal-leave-active .role-modal {
    transition: none;
  }
}
</style>
