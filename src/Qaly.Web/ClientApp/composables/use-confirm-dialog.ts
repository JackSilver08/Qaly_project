import { reactive } from 'vue'

export type DialogTone = 'default' | 'warning' | 'danger' | 'critical'

export interface ConfirmDialogOptions {
  title: string
  message?: string
  subject?: string
  confirmLabel?: string
  cancelLabel?: string
  tone?: DialogTone
  requireText?: string
}

export interface PromptDialogOptions extends ConfirmDialogOptions {
  inputLabel: string
  placeholder?: string
  required?: boolean
  maxLength?: number
}

type DialogResult = boolean | string | null

export const confirmDialogState = reactive({
  open: false,
  mode: 'confirm' as 'confirm' | 'prompt',
  options: null as ConfirmDialogOptions | PromptDialogOptions | null,
  resolve: null as ((result: DialogResult) => void) | null,
})

function openDialog(mode: 'confirm' | 'prompt', options: ConfirmDialogOptions | PromptDialogOptions) {
  if (confirmDialogState.resolve) confirmDialogState.resolve(mode === 'confirm' ? false : null)
  return new Promise<DialogResult>((resolve) => {
    confirmDialogState.mode = mode
    confirmDialogState.options = options
    confirmDialogState.resolve = resolve
    confirmDialogState.open = true
  })
}

export async function confirmDialog(options: ConfirmDialogOptions) {
  return await openDialog('confirm', options) === true
}

export async function promptDialog(options: PromptDialogOptions) {
  const result = await openDialog('prompt', options)
  return typeof result === 'string' ? result : null
}

export function resolveConfirmDialog(result: DialogResult) {
  const resolve = confirmDialogState.resolve
  confirmDialogState.open = false
  confirmDialogState.resolve = null
  confirmDialogState.options = null
  resolve?.(result)
}
