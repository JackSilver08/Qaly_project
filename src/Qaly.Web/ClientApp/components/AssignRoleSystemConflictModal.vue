<script setup lang="ts">
import { ShieldAlert, X, Check } from 'lucide-vue-next'

const props = defineProps<{
  show: boolean
  memberName: string
  newRoleName: string
  systemRoleName: string
  systemAiTier: string
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'confirm'): void
}>()
</script>

<template>
  <Teleport to="body">
    <Transition name="conflict-modal">
      <div
        v-if="show"
        class="conflict-modal-backdrop"
        role="presentation"
        @click.self="emit('close')"
      >
        <section
          class="conflict-modal"
          role="alertdialog"
          aria-modal="true"
          aria-labelledby="role-conflict-title"
          aria-describedby="role-conflict-description"
        >
          <header class="conflict-modal__header">
            <div class="conflict-modal__heading">
              <span class="conflict-modal__icon" aria-hidden="true">
                <ShieldAlert :size="20" />
              </span>
              <div>
                <span class="conflict-modal__eyebrow">Cảnh báo giới hạn hệ thống</span>
                <h2 id="role-conflict-title">Role mới có xung đột quyền AI</h2>
              </div>
            </div>
            <button
              type="button"
              class="conflict-modal__close"
              aria-label="Đóng hộp thoại"
              @click="emit('close')"
            >
              <X :size="19" />
            </button>
          </header>

          <div class="conflict-modal__body">
            <p id="role-conflict-description" class="conflict-modal__description">
              Bạn đang gán role <span class="conflict-role-token">{{ newRoleName }}</span> cho thành viên
              <strong>{{ memberName }}</strong>.
            </p>

            <div class="permission-comparison" aria-label="So sánh quyền role dự án và giới hạn hệ thống">
              <div class="permission-comparison__item">
                <span>Project role</span>
                <strong>{{ newRoleName }}</strong>
                <small>Yêu cầu quyền AI đầy đủ</small>
              </div>
              <span class="permission-comparison__operator" aria-hidden="true">≠</span>
              <div class="permission-comparison__item permission-comparison__item--restricted">
                <span>System role</span>
                <strong>{{ systemRoleName }}</strong>
                <small>AI tier: {{ systemAiTier }}</small>
              </div>
            </div>

            <div class="conflict-modal__notice">
              <strong>Explicit System Deny luôn được ưu tiên</strong>
              <p>
                Bạn vẫn có thể gán role dự án này, nhưng các tính năng AI nâng cao làm thay đổi dữ liệu sẽ tiếp tục
                bị chặn đối với tài khoản trên.
              </p>
              <span>Role dự án không thể nâng quyền vượt quá giới hạn của role hệ thống.</span>
            </div>
          </div>

          <footer class="conflict-modal__footer">
            <button type="button" class="conflict-modal__button conflict-modal__button--secondary" @click="emit('close')">
              Quay lại
            </button>
            <button type="button" class="conflict-modal__button conflict-modal__button--confirm" @click="emit('confirm')">
              <Check :size="17" />
              <span>Vẫn tiếp tục gán role</span>
            </button>
          </footer>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.conflict-modal-backdrop {
  position: fixed;
  inset: 0;
  z-index: 1410;
  display: grid;
  place-items: center;
  overflow-y: auto;
  padding: 24px;
  background: rgba(15, 23, 42, 0.62);
  backdrop-filter: blur(7px);
}

.conflict-modal {
  position: relative;
  overflow: hidden;
  width: min(100%, 580px);
  max-height: calc(100dvh - 32px);
  display: grid;
  grid-template-rows: auto minmax(0, 1fr) auto;
  border: 1px solid color-mix(in srgb, var(--danger) 32%, var(--line));
  border-radius: 16px;
  color: var(--text-strong);
  background: var(--panel);
  box-shadow: 0 24px 70px rgba(15, 23, 42, 0.3);
}

.conflict-modal::before {
  content: '';
  position: absolute;
  inset: 0 0 auto;
  height: 4px;
  background: linear-gradient(90deg, #dc2626, #e11d48);
}

.conflict-modal__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 22px 24px 18px;
  border-bottom: 1px solid var(--line);
}

.conflict-modal__heading {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 12px;
}

.conflict-modal__icon {
  width: 42px;
  height: 42px;
  flex: 0 0 auto;
  display: grid;
  place-items: center;
  border: 1px solid color-mix(in srgb, var(--danger) 32%, var(--line));
  border-radius: 12px;
  color: var(--danger);
  background: var(--danger-soft);
}

.conflict-modal__eyebrow {
  display: block;
  margin-bottom: 2px;
  color: var(--danger);
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

.conflict-modal__heading h2 {
  margin: 0;
  color: var(--text-strong);
  font-size: 20px;
  font-weight: 850;
  line-height: 1.25;
}

.conflict-modal__close {
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

.conflict-modal__close:hover,
.conflict-modal__close:focus-visible {
  border-color: color-mix(in srgb, var(--danger) 32%, var(--line));
  color: var(--danger);
  background: var(--danger-soft);
  outline: 0;
}

.conflict-modal__body {
  overflow-y: auto;
  display: grid;
  gap: 18px;
  padding: 22px 24px;
}

.conflict-modal__description,
.conflict-modal__notice p {
  margin: 0;
  color: var(--muted);
  font-size: 13.5px;
  line-height: 1.65;
}

.conflict-modal__description strong {
  color: var(--text-strong);
}

.conflict-role-token {
  display: inline-flex;
  align-items: center;
  padding: 2px 7px;
  border-radius: 6px;
  color: #7e22ce;
  background: rgba(168, 85, 247, 0.12);
  font-size: 12px;
  font-weight: 800;
  white-space: nowrap;
}

.permission-comparison {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto minmax(0, 1fr);
  align-items: center;
  gap: 12px;
}

.permission-comparison__item {
  min-width: 0;
  display: grid;
  gap: 4px;
  padding: 13px 14px;
  border: 1px solid rgba(168, 85, 247, 0.28);
  border-radius: 12px;
  background: color-mix(in srgb, rgba(168, 85, 247, 0.1) 72%, var(--panel));
}

.permission-comparison__item--restricted {
  border-color: color-mix(in srgb, var(--danger) 32%, var(--line));
  background: var(--danger-soft);
}

.permission-comparison__item > span,
.permission-comparison__item small {
  color: var(--muted);
  font-size: 11px;
  font-weight: 650;
}

.permission-comparison__item strong {
  overflow: hidden;
  color: var(--text-strong);
  font-size: 14px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.permission-comparison__operator {
  color: var(--danger);
  font-size: 20px;
  font-weight: 850;
}

.conflict-modal__notice {
  display: grid;
  gap: 8px;
  padding: 14px 16px;
  border: 1px solid color-mix(in srgb, var(--danger) 34%, var(--line));
  border-radius: 12px;
  background: var(--danger-soft);
}

.conflict-modal__notice > strong {
  color: var(--danger);
  font-size: 13px;
}

.conflict-modal__notice > span {
  padding-top: 8px;
  border-top: 1px solid color-mix(in srgb, var(--danger) 18%, var(--line));
  color: var(--muted);
  font-size: 12px;
  font-weight: 650;
}

.conflict-modal__footer {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 10px;
  padding: 16px 24px 20px;
  border-top: 1px solid var(--line);
  background: var(--bg-soft);
}

.conflict-modal__button {
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

.conflict-modal__button--secondary {
  border: 1px solid var(--line);
  color: var(--text-strong);
  background: var(--panel);
}

.conflict-modal__button--secondary:hover,
.conflict-modal__button--secondary:focus-visible {
  border-color: color-mix(in srgb, var(--primary) 32%, var(--line));
  background: var(--primary-soft);
  outline: 0;
}

.conflict-modal__button--confirm {
  border: 1px solid #dc2626;
  color: #fff;
  background: linear-gradient(135deg, #dc2626, #be123c);
  box-shadow: 0 8px 18px rgba(220, 38, 38, 0.22);
}

.conflict-modal__button--confirm:hover,
.conflict-modal__button--confirm:focus-visible {
  background: linear-gradient(135deg, #b91c1c, #9f1239);
  outline: 0;
  transform: translateY(-1px);
}

.conflict-modal-enter-active,
.conflict-modal-leave-active {
  transition: opacity 180ms ease;
}

.conflict-modal-enter-active .conflict-modal,
.conflict-modal-leave-active .conflict-modal {
  transition: transform 180ms ease, opacity 180ms ease;
}

.conflict-modal-enter-from,
.conflict-modal-leave-to {
  opacity: 0;
}

.conflict-modal-enter-from .conflict-modal,
.conflict-modal-leave-to .conflict-modal {
  opacity: 0;
  transform: translateY(10px) scale(0.98);
}

@media (max-width: 640px) {
  .conflict-modal-backdrop {
    align-items: end;
    padding: 12px;
  }

  .conflict-modal {
    max-height: calc(100dvh - 24px);
    border-radius: 16px 16px 12px 12px;
  }

  .conflict-modal__header,
  .conflict-modal__body,
  .conflict-modal__footer {
    padding-left: 16px;
    padding-right: 16px;
  }

  .permission-comparison {
    grid-template-columns: 1fr;
  }

  .permission-comparison__operator {
    justify-self: center;
  }

  .conflict-modal__footer {
    align-items: stretch;
    flex-direction: column-reverse;
  }

  .conflict-modal__button {
    width: 100%;
  }
}

@media (prefers-reduced-motion: reduce) {
  .conflict-modal-enter-active,
  .conflict-modal-leave-active,
  .conflict-modal-enter-active .conflict-modal,
  .conflict-modal-leave-active .conflict-modal {
    transition: none;
  }
}
</style>
