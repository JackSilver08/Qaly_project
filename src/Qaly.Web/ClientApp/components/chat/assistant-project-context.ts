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
