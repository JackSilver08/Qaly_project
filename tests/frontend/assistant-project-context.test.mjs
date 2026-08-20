import assert from 'node:assert/strict'
import test from 'node:test'

import {
  normalizeAssistantProjectTarget,
  resolveAssistantProjectId,
} from '../../src/Qaly.Web/ClientApp/components/chat/assistant-project-context.ts'

test('P03 uses the Project currently selected in the composer', () => {
  assert.equal(resolveAssistantProjectId(null, 'qaly-release-4'), 'qaly-release-4')
})

test('a concrete Project route remains authoritative', () => {
  assert.equal(
    resolveAssistantProjectId('route-project', 'composer-project'),
    'route-project',
  )
})

test('workspace selection does not leak a previous Project', () => {
  assert.equal(resolveAssistantProjectId(null, 'workspace'), null)
  assert.equal(normalizeAssistantProjectTarget('  workspace  '), null)
})
