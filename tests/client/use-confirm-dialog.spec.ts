import { beforeEach, describe, expect, it, vi } from 'vitest'

/**
 * The dialog host is a module-level reactive singleton shared by every page, so each test
 * re-imports it to start from a closed dialog.
 */
async function loadDialog() {
  vi.resetModules()
  return import('@/composables/use-confirm-dialog')
}

let dialog: Awaited<ReturnType<typeof loadDialog>>

beforeEach(async () => {
  dialog = await loadDialog()
})

describe('confirmDialog', () => {
  it('opens with the supplied options and stays pending until resolved', async () => {
    const { confirmDialog, confirmDialogState } = dialog

    const pending = confirmDialog({ title: 'Xoá dự án?', message: 'Không thể hoàn tác.', tone: 'danger' })

    expect(confirmDialogState.open).toBe(true)
    expect(confirmDialogState.mode).toBe('confirm')
    expect(confirmDialogState.options).toMatchObject({ title: 'Xoá dự án?', tone: 'danger' })

    dialog.resolveConfirmDialog(true)
    await expect(pending).resolves.toBe(true)
  })

  it('resolves false when the user cancels', async () => {
    const { confirmDialog, resolveConfirmDialog } = dialog

    const pending = confirmDialog({ title: 'Xoá dự án?' })
    resolveConfirmDialog(false)

    await expect(pending).resolves.toBe(false)
  })

  it('treats a null result as a refusal rather than a confirmation', async () => {
    const { confirmDialog, resolveConfirmDialog } = dialog

    const pending = confirmDialog({ title: 'Xoá dự án?' })
    resolveConfirmDialog(null)

    await expect(pending).resolves.toBe(false)
  })

  it('resets the shared state after resolving so the next dialog starts clean', async () => {
    const { confirmDialog, confirmDialogState, resolveConfirmDialog } = dialog

    const pending = confirmDialog({ title: 'Xoá dự án?' })
    resolveConfirmDialog(true)
    await pending

    expect(confirmDialogState.open).toBe(false)
    expect(confirmDialogState.options).toBeNull()
    expect(confirmDialogState.resolve).toBeNull()
  })
})

describe('promptDialog', () => {
  it('returns the typed text', async () => {
    const { promptDialog, confirmDialogState, resolveConfirmDialog } = dialog

    const pending = promptDialog({ title: 'Lý do huỷ?', inputLabel: 'Lý do', required: true })
    expect(confirmDialogState.mode).toBe('prompt')

    resolveConfirmDialog('Khách hàng đổi phạm vi')
    await expect(pending).resolves.toBe('Khách hàng đổi phạm vi')
  })

  it('returns null when dismissed', async () => {
    const { promptDialog, resolveConfirmDialog } = dialog

    const pending = promptDialog({ title: 'Lý do huỷ?', inputLabel: 'Lý do' })
    resolveConfirmDialog(null)

    await expect(pending).resolves.toBeNull()
  })

  it('returns null when a boolean leaks through instead of text', async () => {
    const { promptDialog, resolveConfirmDialog } = dialog

    const pending = promptDialog({ title: 'Lý do huỷ?', inputLabel: 'Lý do' })
    resolveConfirmDialog(true)

    await expect(pending).resolves.toBeNull()
  })

  it('preserves an empty string typed by the user', async () => {
    const { promptDialog, resolveConfirmDialog } = dialog

    const pending = promptDialog({ title: 'Ghi chú', inputLabel: 'Nội dung' })
    resolveConfirmDialog('')

    await expect(pending).resolves.toBe('')
  })
})

describe('overlapping dialogs', () => {
  it('settles the previous confirm as a refusal instead of leaving it pending forever', async () => {
    const { confirmDialog, resolveConfirmDialog } = dialog

    const first = confirmDialog({ title: 'Xoá dự án?' })
    const second = confirmDialog({ title: 'Lưu trữ dự án?' })

    await expect(first).resolves.toBe(false)

    resolveConfirmDialog(true)
    await expect(second).resolves.toBe(true)
  })

  it('settles a superseded prompt as null', async () => {
    const { confirmDialog, promptDialog, resolveConfirmDialog } = dialog

    const prompt = promptDialog({ title: 'Lý do?', inputLabel: 'Lý do' })
    const confirm = confirmDialog({ title: 'Chắc chắn?' })

    await expect(prompt).resolves.toBeNull()

    resolveConfirmDialog(true)
    await expect(confirm).resolves.toBe(true)
  })

  it('shows the newest dialog options after being superseded', async () => {
    const { confirmDialog, confirmDialogState, resolveConfirmDialog } = dialog

    void confirmDialog({ title: 'Xoá dự án?' })
    const second = confirmDialog({ title: 'Lưu trữ dự án?' })

    expect(confirmDialogState.options).toMatchObject({ title: 'Lưu trữ dự án?' })

    resolveConfirmDialog(false)
    await second
  })
})

describe('resolveConfirmDialog', () => {
  it('is safe to call when no dialog is open', async () => {
    const { confirmDialogState, resolveConfirmDialog } = dialog

    expect(() => resolveConfirmDialog(true)).not.toThrow()
    expect(confirmDialogState.open).toBe(false)
  })
})
