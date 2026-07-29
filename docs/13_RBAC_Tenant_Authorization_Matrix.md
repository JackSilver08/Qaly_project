# Qaly RC Authorization Matrix

> Owner: Quang Tuấn (Tech Lead)  
> Baseline date: 29/07/2026  
> Scope: Release Candidate 28/07–03/08/2026

This document is the normative authorization baseline for backend, frontend and
test implementations. The backend is the enforcement boundary. Hiding or
disabling a frontend control is not an authorization control.

## 1. Roles and boundaries

| Boundary | Roles/capabilities | Meaning |
|---|---|---|
| System | `Admin`, `Moderator`, `Member` | Global account role. Only `Admin` has unrestricted administration. |
| Organization | `Owner`, `OrganizationAdmin`, `PrivacyOperator`, `BillingAdmin`, `Member` | Membership role valid only inside one organization. |
| Project | `Owner`, `Manager`, `ScrumMaster`, `Developer`, `Tester`, `Reviewer`, `Member`, `Viewer`, `Customer` | Membership role valid only inside one project. |
| Delegated organization support | `organization.users.view`, `.invite`, `.update_role`, `.remove` | Time-bound, revocable capability for a system `Moderator` and one organization. |

Legacy organization roles `Admin` and `Manager` normalize to
`OrganizationAdmin`. Legacy project manager aliases normalize to `Manager`.
No API may use a role from one boundary as authority in another boundary.

## 2. System administration

| Operation | Admin | Moderator | Member |
|---|:---:|:---:|:---:|
| List/read system users | Allow | Deny | Deny |
| Create/update/lock/unlock user | Allow | Deny | Deny |
| Revoke user sessions | Allow | Deny | Deny |
| Assign system role (`Moderator`/`Member`) | Allow | Deny | Deny |
| Assign or create another `Admin` | Deny by business rule | Deny | Deny |
| Grant/revoke moderator capability | Allow | Deny | Deny |
| List all organizations | Allow | Assigned/member only | Member/owner only |
| Transfer organization ownership | Allow | Deny | Deny |

The database filtered unique index `UX_Users_SingleAdmin` is a second line of
defence for the single-Admin invariant.

## 3. Organization access

| Operation | System Admin | Owner | OrganizationAdmin | PrivacyOperator | BillingAdmin | Member |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| Read organization | Allow | Allow | Allow | Allow | Allow | Allow |
| List organization members | Allow | Allow | Allow | Allow | Allow | Allow |
| Update organization profile | Allow | Allow | Allow | Deny | Deny | Deny |
| Activate/deactivate organization | Allow | Allow | Allow | Deny | Deny | Deny |
| Add member | Allow | Allow | Allow | Deny | Deny | Deny |
| Change assignable organization role | Allow | Allow | Allow | Deny | Deny | Deny |
| Remove non-owner member | Allow | Allow | Allow | Deny | Deny | Deny |
| Remove/downgrade Owner | Deny | Deny | Deny | Deny | Deny | Deny |
| Transfer ownership | Allow | Deny | Deny | Deny | Deny | Deny |
| Manage AI budget | Allow | Allow | Allow | Deny | Allow | Deny |
| Operate privacy workflows | Per endpoint policy | Per endpoint policy | Per endpoint policy | Allow | Deny | Deny |

An authenticated user may create an organization owned by themselves. Only a
system Admin may create one for another active user.

## 4. Delegated Moderator access

A Moderator has no global user-management authority. A capability is effective
only when all conditions are true:

- the account system role is exactly `Moderator`;
- the assignment organization matches the requested organization;
- `IsActive` is true and `RevokedAt` is null;
- `ExpiresAt` is null or later than current UTC time;
- the exact operation capability is assigned.

| Operation | Required capability |
|---|---|
| List members of assigned organization | `organization.users.view` |
| Add member by ID/email | `organization.users.invite` |
| Update existing member role | `organization.users.update_role` |
| Remove non-owner member | `organization.users.remove` |
| Update/deactivate organization or transfer owner | Never delegated |

Capability checks do not imply another capability. For example, `invite` does
not grant `view` or `update_role`.

## 5. Project baseline

| Operation class | Owner/Manager/ScrumMaster | Developer/Tester/Reviewer | Member | Viewer/Customer |
|---|:---:|:---:|:---:|:---:|
| Read project | Allow when project member | Allow when project member | Allow | Allow, restricted views |
| Manage project/membership | Allow | Deny | Deny | Deny |
| General project write | Allow | Endpoint/task-policy decision | Allow under current compatibility rule | Deny |
| View-only operation | Allow | Allow | Allow | Allow |

Task and AI operations must additionally pass `ITaskAccessPolicy`; a project
role alone must not authorize access to a task belonging to another project or
organization.

## 6. Required response and isolation behaviour

| Situation | Required result |
|---|---|
| Missing/invalid authentication | `401` |
| Authenticated but not authorized | `403` |
| Authorized lookup of absent object | `404` |
| Concurrency/version conflict | `409` |
| Invalid role/capability/request | `400` |

List endpoints must filter inaccessible records rather than return them with
redacted fields. Detail/mutation endpoints must perform authorization before
returning sensitive data. Jobs, exports, AI requests, notifications and realtime
hubs must preserve the same organization/project boundary as the initiating
request.

## 7. Mandatory regression evidence

| Invariant | Automated evidence |
|---|---|
| Only Admin manages system users | `AdminUsersAuthorizationTests`, `SystemRoleRulesTests` |
| Member cannot read another organization's users | `OrganizationUsersAuthorizationTests.OrganizationUsers_RejectsMemberFromAnotherOrganization` |
| Moderator capability is organization-scoped | `OrganizationUsersAuthorizationTests.OrganizationUsers_AllowsModeratorWithActiveViewCapabilityOnlyForAssignedOrganization` |
| Organization roles do not become project/system roles | `OrganizationRoleRulesTests`, `ProjectRoleRulesTests` |
| Tasks remain tenant/project scoped | `TaskAccessPolicyIsolationTests`, `TaskConcurrencyTests` |
| Auth boundary and session resilience | `AuthBoundaryIntegrationTests`, `AuthSessionResilienceTests` |
| AI source/budget/security boundaries | `AiSourceGuardTests`, `AiBudgetApiTests`, `AiSecurityGuardTests` |

Before RC approval, QA must add evidence for expired/revoked Moderator
assignments and each delegated mutation (`invite`, `update_role`, `remove`).

## 8. Change-control rule

Any change to a role constant, normalization rule, authorization policy,
tenant-scoped query, membership mutation, moderator capability, task access,
AI budget/source rule or migration is high risk. It requires:

1. Tech Lead and domain-owner review.
2. A positive and a negative authorization test.
3. Tenant-isolation evidence when organization/project data is involved.
4. Migration recovery evidence when persisted authorization data changes.
5. An update to this matrix if behaviour changes.
