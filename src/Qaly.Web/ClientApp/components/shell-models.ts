import type { Component } from 'vue'

export interface ShellNavItem {
  label: string
  to: string
  icon: Component
}

export interface SimulationUser {
  id: string
  fullName: string
  role: string
}
