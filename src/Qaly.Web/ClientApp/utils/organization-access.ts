export interface OrganizationActorContext {
  systemRole?: string | null
  actorId?: string | null
  ownerId?: string | null
  membershipRole?: string | null
  hasMembership?: boolean
  moderatorCapabilities?: readonly string[]
}

const managerRoles = new Set(['Owner', 'OrganizationAdmin', 'Admin', 'Manager'])

export function hasNativeOrganizationManagement(context: OrganizationActorContext) {
  return context.systemRole === 'Admin' ||
    (!!context.actorId && context.actorId === context.ownerId) ||
    managerRoles.has(context.membershipRole ?? '')
}

export function canUseOrganizationCapability(context: OrganizationActorContext, capability: string) {
  if (hasNativeOrganizationManagement(context)) return true
  return context.systemRole === 'Moderator' &&
    (context.moderatorCapabilities ?? []).includes(capability)
}

export function canManageOrganizationUsers(context: OrganizationActorContext) {
  return ['organization.users.invite', 'organization.users.update_role', 'organization.users.remove']
    .some((capability) => canUseOrganizationCapability(context, capability))
}

export function canViewProfessionalProfiles(context: OrganizationActorContext) {
  if (context.systemRole === 'Admin' ||
    (!!context.actorId && context.actorId === context.ownerId) ||
    context.hasMembership) return true
  return canUseOrganizationCapability(context, 'organization.professional_profiles.view') ||
    canUseOrganizationCapability(context, 'organization.professional_profiles.manage')
}
