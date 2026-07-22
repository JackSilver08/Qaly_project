<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import { AlertTriangle, HelpCircle, ShieldAlert, X } from 'lucide-vue-next'
import { confirmDialogState, resolveConfirmDialog, type PromptDialogOptions } from '../composables/use-confirm-dialog'

const panel = ref<HTMLElement | null>(null)
const cancelButton = ref<HTMLButtonElement | null>(null)
const input = ref<HTMLInputElement | HTMLTextAreaElement | null>(null)
const value = ref('')
const error = ref('')
let previousFocus: HTMLElement | null = null

const options = computed(() => confirmDialogState.options)
const tone = computed(() => options.value?.tone ?? 'default')
const isPrompt = computed(() => confirmDialogState.mode === 'prompt')
const promptOptions = computed(() => options.value as PromptDialogOptions | null)
const expectedText = computed(() => options.value?.requireText?.trim() ?? '')
const icon = computed(() => tone.value === 'critical' ? ShieldAlert : tone.value === 'default' ? HelpCircle : AlertTriangle)

watch(() => confirmDialogState.open, async (open) => {
  if (!open) return
  previousFocus = document.activeElement as HTMLElement | null
  value.value = ''
  error.value = ''
  document.body.classList.add('dialog-open')
  await nextTick()
  if (isPrompt.value || expectedText.value) input.value?.focus()
  else cancelButton.value?.focus()
})

function cancel() {
  resolveConfirmDialog(isPrompt.value ? null : false)
  closeCleanup()
}

function submit() {
  const trimmed = value.value.trim()
  if (expectedText.value && trimmed !== expectedText.value) {
    error.value = `Nhập chính xác “${expectedText.value}” để tiếp tục.`
    input.value?.focus()
    return
  }
  if (isPrompt.value && promptOptions.value?.required !== false && !trimmed) {
    error.value = 'Vui lòng nhập nội dung trước khi tiếp tục.'
    input.value?.focus()
    return
  }
  resolveConfirmDialog(isPrompt.value ? trimmed : true)
  closeCleanup()
}

function closeCleanup() {
  document.body.classList.remove('dialog-open')
  requestAnimationFrame(() => previousFocus?.focus())
}

function onKeydown(event: KeyboardEvent) {
  if (!confirmDialogState.open) return
  if (event.key === 'Escape') {
    event.preventDefault()
    cancel()
    return
  }
  if (event.key !== 'Tab' || !panel.value) return
  const focusable = Array.from(panel.value.querySelectorAll<HTMLElement>('button:not([disabled]), input:not([disabled]), textarea:not([disabled])'))
  if (!focusable.length) return
  const first = focusable[0], last = focusable[focusable.length - 1]
  if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
  else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
}

onBeforeUnmount(() => document.body.classList.remove('dialog-open'))
</script>

<template>
  <Teleport to="body">
    <Transition name="confirm-dialog">
      <div v-if="confirmDialogState.open && options" class="confirm-backdrop" @keydown="onKeydown">
        <section ref="panel" class="confirm-panel" :data-tone="tone" :role="tone === 'danger' || tone === 'critical' ? 'alertdialog' : 'dialog'" aria-modal="true" aria-labelledby="confirm-title" aria-describedby="confirm-message">
          <button class="confirm-close" type="button" aria-label="Đóng" @click="cancel"><X :size="18" /></button>
          <div class="confirm-icon" aria-hidden="true"><component :is="icon" :size="23" /></div>
          <div class="confirm-copy">
            <h2 id="confirm-title">{{ options.title }}</h2>
            <p v-if="options.message" id="confirm-message">{{ options.message }}</p>
            <strong v-if="options.subject" class="confirm-subject">{{ options.subject }}</strong>
          </div>
          <label v-if="isPrompt || expectedText" class="confirm-field">
            <span>{{ isPrompt ? promptOptions?.inputLabel : `Nhập “${expectedText}” để xác nhận` }}</span>
            <textarea v-if="isPrompt" ref="input" v-model="value" rows="3" :maxlength="promptOptions?.maxLength ?? 500" :placeholder="promptOptions?.placeholder" @input="error = ''" />
            <input v-else ref="input" v-model="value" autocomplete="off" @input="error = ''" />
            <small v-if="isPrompt && promptOptions?.maxLength">{{ value.length }}/{{ promptOptions.maxLength }}</small>
            <em v-if="error" role="alert">{{ error }}</em>
          </label>
          <footer class="confirm-actions">
            <button ref="cancelButton" class="confirm-cancel" type="button" @click="cancel">{{ options.cancelLabel ?? 'Hủy' }}</button>
            <button class="confirm-submit" type="button" @click="submit">{{ options.confirmLabel ?? 'Xác nhận' }}</button>
          </footer>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.confirm-backdrop{position:fixed;inset:0;z-index:3000;display:grid;place-items:center;padding:20px;background:rgb(15 23 42/.54);backdrop-filter:blur(3px)}.confirm-panel{position:relative;width:min(440px,100%);border:1px solid var(--line-light,#dce2ea);border-radius:16px;background:var(--surface,#fff);color:var(--text-primary,#172033);box-shadow:0 24px 70px rgb(15 23 42/.24);padding:24px}.confirm-close{position:absolute;top:14px;right:14px;width:34px;height:34px;display:grid;place-items:center;border:0;border-radius:9px;background:transparent;color:var(--muted,#667085);cursor:pointer}.confirm-icon{width:44px;height:44px;display:grid;place-items:center;border-radius:12px;margin-bottom:16px;background:#e8f1ff;color:#2457a7}.confirm-panel[data-tone=warning] .confirm-icon{background:#fff4d6;color:#9a6400}.confirm-panel[data-tone=danger] .confirm-icon,.confirm-panel[data-tone=critical] .confirm-icon{background:#fee7e7;color:#b42318}.confirm-copy h2{margin:0 40px 8px 0;font-size:20px}.confirm-copy p{margin:0;color:var(--muted,#667085);line-height:1.55}.confirm-subject{display:block;margin-top:12px;padding:10px 12px;border-radius:10px;background:var(--surface-muted,#f5f7fa);overflow-wrap:anywhere}.confirm-field{display:grid;gap:7px;margin-top:18px;font-size:13px;font-weight:700}.confirm-field input,.confirm-field textarea{width:100%;box-sizing:border-box;border:1px solid var(--line-light,#cfd7e3);border-radius:10px;background:var(--surface,#fff);color:inherit;padding:10px 12px;font:inherit;resize:vertical}.confirm-field input:focus,.confirm-field textarea:focus{outline:3px solid rgb(37 99 235/.18);border-color:#2563eb}.confirm-field small{text-align:right;color:var(--muted,#667085);font-weight:500}.confirm-field em{color:#b42318;font-style:normal;font-weight:600}.confirm-actions{display:flex;justify-content:flex-end;gap:10px;margin-top:24px}.confirm-actions button{min-height:42px;border-radius:10px;padding:0 16px;font-weight:750;cursor:pointer}.confirm-cancel{border:1px solid var(--line-light,#d5dce6);background:var(--surface,#fff);color:inherit}.confirm-submit{border:1px solid #2457a7;background:#2457a7;color:#fff}.confirm-panel[data-tone=warning] .confirm-submit{border-color:#9a6400;background:#9a6400}.confirm-panel[data-tone=danger] .confirm-submit,.confirm-panel[data-tone=critical] .confirm-submit{border-color:#b42318;background:#b42318}.confirm-dialog-enter-active,.confirm-dialog-leave-active{transition:opacity .16s ease}.confirm-dialog-enter-active .confirm-panel,.confirm-dialog-leave-active .confirm-panel{transition:transform .16s ease,opacity .16s ease}.confirm-dialog-enter-from,.confirm-dialog-leave-to{opacity:0}.confirm-dialog-enter-from .confirm-panel,.confirm-dialog-leave-to .confirm-panel{transform:translateY(10px) scale(.985);opacity:0}:global(:root[data-theme=dark]) .confirm-panel,:global(:root[data-theme=dark]) .confirm-field input,:global(:root[data-theme=dark]) .confirm-field textarea,:global(:root[data-theme=dark]) .confirm-cancel{background:#151b27;border-color:#344054}:global(:root[data-theme=dark]) .confirm-subject{background:#202838}:global(body.dialog-open){overflow:hidden}@media(max-width:560px){.confirm-backdrop{place-items:end center;padding:0}.confirm-panel{width:100%;border-radius:18px 18px 0 0;padding:22px 18px calc(18px + env(safe-area-inset-bottom))}.confirm-actions{display:grid;grid-template-columns:1fr 1fr}}@media(prefers-reduced-motion:reduce){.confirm-dialog-enter-active,.confirm-dialog-leave-active,.confirm-dialog-enter-active .confirm-panel,.confirm-dialog-leave-active .confirm-panel{transition:none}}
</style>
