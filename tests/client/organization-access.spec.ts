import { describe, expect, it } from 'vitest'
import {
  canManageOrganizationUsers,
  canUseOrganizationCapability,
  canViewProfessionalProfiles,
} from '../../src/Qaly.Web/ClientApp/utils/organization-access'

describe('organization access policy', () => {
  it('fails closed for a member and a Moderator without delegated scopes', () => {
    expect(canManageOrganizationUsers({ systemRole: 'Member', hasMembership: true })).toBe(false)
    expect(canManageOrganizationUsers({ systemRole: 'Moderator', moderatorCapabilities: [] })).toBe(false)
    expect(canUseOrganizationCapability(
      { systemRole: 'Moderator', moderatorCapabilities: [] },
      'organization.users.invite',
    )).toBe(false)
  })

  it('grants a Moderator only the exact delegated capability', () => {
    const context = {
      systemRole: 'Moderator',
      moderatorCapabilities: ['organization.users.update_role'],
    }
    expect(canUseOrganizationCapability(context, 'organization.users.update_role')).toBe(true)
    expect(canUseOrganizationCapability(context, 'organization.users.invite')).toBe(false)
    expect(canManageOrganizationUsers(context)).toBe(true)
  })

  it('keeps native organization managers independent from Moderator assignments', () => {
    const context = { systemRole: 'Member', membershipRole: 'OrganizationAdmin' }
    expect(canUseOrganizationCapability(context, 'organization.users.remove')).toBe(true)
  })

  it('separates professional-profile viewing from user mutation scopes', () => {
    expect(canViewProfessionalProfiles({ systemRole: 'Member', hasMembership: true })).toBe(true)
    expect(canViewProfessionalProfiles({
      systemRole: 'Moderator',
      moderatorCapabilities: ['organization.users.view'],
    })).toBe(false)
    expect(canViewProfessionalProfiles({
      systemRole: 'Moderator',
      moderatorCapabilities: ['organization.professional_profiles.view'],
    })).toBe(true)
  })
})
