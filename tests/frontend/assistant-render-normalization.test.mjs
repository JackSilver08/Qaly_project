import assert from 'node:assert/strict'
import test from 'node:test'

import { cloneAssistantJson } from '../../src/Qaly.Web/ClientApp/components/chat/assistant-render-normalization.ts'

test('P06 clones a reactive-like Launch Brief payload without DataCloneError', () => {
  const nested = new Proxy([{ title: 'Đặt dịch vụ' }], {})
  const reactiveLikeBrief = new Proxy({
    proposedProjectName: 'E2E-AI-SPA-Dịch-vụ',
    features: nested,
  }, {})

  const cloned = cloneAssistantJson(reactiveLikeBrief)

  assert.deepEqual(cloned, {
    proposedProjectName: 'E2E-AI-SPA-Dịch-vụ',
    features: [{ title: 'Đặt dịch vụ' }],
  })
  assert.notEqual(cloned, reactiveLikeBrief)
  assert.notEqual(cloned.features, nested)
})
