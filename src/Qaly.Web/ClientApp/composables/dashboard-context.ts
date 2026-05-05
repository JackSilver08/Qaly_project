import { inject, type InjectionKey } from 'vue'

export type DashboardContext = Record<string, any>

export const dashboardContextKey: InjectionKey<DashboardContext> = Symbol('dashboard-context')

export function useDashboardContext() {
  const context = inject(dashboardContextKey)

  if (!context) {
    throw new Error('Dashboard context is not available.')
  }

  return context
}
