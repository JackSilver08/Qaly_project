/**
 * Assistant responses live inside Vue reactive state. Browser
 * structuredClone() rejects Vue Proxy objects, while JSON serialization is
 * the assistant API transport contract and safely unwraps those proxies.
 */
export function cloneAssistantJson<T>(value: T): T {
  const serialized = JSON.stringify(value)
  if (serialized === undefined) throw new TypeError('Assistant payload is not JSON serializable.')
  return JSON.parse(serialized) as T
}
