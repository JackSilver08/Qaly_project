export function normalizeAssistantProjectTarget(selectedTarget: string): string | null {
  const normalized = selectedTarget.trim()
  return normalized && normalized !== 'workspace' ? normalized : null
}

export function resolveAssistantProjectId(
  routeProjectId: string | null | undefined,
  selectedTarget: string,
): string | null {
  const normalizedRouteProjectId = routeProjectId?.trim()
  return normalizedRouteProjectId || normalizeAssistantProjectTarget(selectedTarget)
}

export type AssistantRouteEntity = {
  projectId: string | null
  taskId: string | null
  wikiId: string | null
  groupId: string | null
}

function decodedSegment(value: string | undefined): string | null {
  if (!value?.trim()) return null
  try {
    return decodeURIComponent(value.trim())
  } catch {
    return value.trim()
  }
}

/**
 * The assistant panel is mounted at the application shell, so Vue Router params
 * may be empty even while the browser is on a concrete Project/Task/Wiki page.
 * Resolve that visible route as a fallback so request context matches the page.
 */
export function resolveAssistantRouteEntity(pathname: string): AssistantRouteEntity {
  const segments = pathname.split('/').filter(Boolean)
  if (segments[0] === 'projects' && segments[1]) {
    const projectId = decodedSegment(segments[1])
    if (segments[2] === 'tasks' && segments[3]) {
      return { projectId, taskId: decodedSegment(segments[3]), wikiId: null, groupId: null }
    }
    if (segments[2] === 'wiki' && segments[3]) {
      return { projectId, taskId: null, wikiId: decodedSegment(segments[3]), groupId: null }
    }
    return { projectId, taskId: null, wikiId: null, groupId: null }
  }

  if (segments[0] === 'groups' && segments[1]) {
    return { projectId: null, taskId: null, wikiId: null, groupId: decodedSegment(segments[1]) }
  }

  return { projectId: null, taskId: null, wikiId: null, groupId: null }
}
