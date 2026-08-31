import assert from 'node:assert/strict'
import test from 'node:test'

import {
  normalizeAssistantProjectTarget,
  resolveAssistantRouteEntity,
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

test('shell-mounted assistant recovers Project and Task context from the visible URL', () => {
  assert.deepEqual(
    resolveAssistantRouteEntity('/projects/project-123/tasks/task-456'),
    { projectId: 'project-123', taskId: 'task-456', wikiId: null, groupId: null },
  )
})

test('shell-mounted assistant recovers Wiki and Group context from the visible URL', () => {
  assert.deepEqual(
    resolveAssistantRouteEntity('/projects/project-123/wiki/wiki-456'),
    { projectId: 'project-123', taskId: null, wikiId: 'wiki-456', groupId: null },
  )
  assert.deepEqual(
    resolveAssistantRouteEntity('/groups/group-789'),
    { projectId: null, taskId: null, wikiId: null, groupId: 'group-789' },
  )
})
