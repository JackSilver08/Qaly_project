export type LaunchStaffingDraftMember = {
  userId: string
  proposedRole: string
  proposedHours: number
  included: boolean
  manager: boolean
}

// The manager radio is the single source of truth for the draft's Manager
// role. A role copied from a saved scenario must not survive demotion.
// This does not validate eligibility or accept privileged/unknown roles;
// those checks remain authoritative on the server.
export function normalizeLaunchStaffing(staffing: readonly LaunchStaffingDraftMember[]): LaunchStaffingDraftMember[] {
  return staffing.map(member => {
    const manager = member.included && member.manager
    return {
      ...member,
      manager,
      proposedRole: manager ? 'Manager'
        : member.proposedRole.trim().toLowerCase() === 'manager' ? 'Member' : member.proposedRole,
    }
  })
}

export function selectLaunchStaffing(
  staffing: readonly LaunchStaffingDraftMember[],
  includedIds: ReadonlySet<string>,
  managerId?: string | null,
): LaunchStaffingDraftMember[] {
  return normalizeLaunchStaffing(staffing.map(member => ({
    ...member,
    included: includedIds.has(member.userId),
    manager: includedIds.has(member.userId) && member.userId === managerId,
  })))
}

export function editLaunchStaffing(
  staffing: readonly LaunchStaffingDraftMember[],
  userId: string,
  field: 'included' | 'manager' | 'proposedRole' | 'proposedHours',
  value: boolean | string | number,
): LaunchStaffingDraftMember[] {
  return normalizeLaunchStaffing(staffing.map(member => {
    if (member.userId !== userId) {
      return field === 'manager' && value === true ? { ...member, manager: false } : member
    }
    if (field === 'included') return { ...member, included: Boolean(value) }
    if (field === 'manager') return { ...member, manager: Boolean(value), included: value === true || member.included }
    if (field === 'proposedHours') return { ...member, proposedHours: Number(value) }
    return { ...member, proposedRole: String(value) }
  }))
}
