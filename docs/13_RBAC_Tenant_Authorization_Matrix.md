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
| Create task | Allow | Allow | Deny | Deny |
| Update own task / comment / track time | Allow | Allow | Allow | Deny |
| Review evidence | Allow | Tester, Reviewer only | Deny | Deny |
| Read internal wiki | Allow | Allow | Allow | Viewer allow, Customer deny |
| View-only operation | Allow | Allow | Allow | Allow |

Task and AI operations must additionally pass `ITaskAccessPolicy`; a project
role alone must not authorize access to a task belonging to another project or
organization.

`ProjectPermissionRules.Resolve` is the single mapping from project role to this
table and is returned to clients on the project payload. It is a rendering aid
only; each endpoint still authorizes independently.

Adding a project member to a project owned by an organization also grants the
lowest organization role when the user has none. Project reads are filtered by
organization membership, so without that row the member cannot see the project
at all. The grant never overwrites an existing organization role, and it does not
change who may add members.

### 5.1 Organization-defined project roles

An organization owner or `OrganizationAdmin` may define additional project roles
(`ProjectRoleDefinition`). A custom role is a label plus an inherited built-in
role (`BaseRole`); authorization reads only `BaseRole`, so a custom role can
never grant more than an existing built-in role. Rules:

- The key is unique per organization and never resolves across organizations.
- A custom role may not shadow a built-in role name.
- An inactive definition is not assignable, and existing assignments keep working.
- A definition still assigned to project members cannot be deleted, only deactivated.

### 5.2 AI capability tiers

`AiCapabilityRules` maps a project role to one ordered tier. Both the chat tool
filter and the AI job endpoints read from it, so they cannot diverge.

| Tier | Roles | Reach |
|---|---|---|
| `Full` | Owner, Manager, ScrumMaster, system Admin, organization managers | All AI capabilities |
| `Specialist` | Developer, Tester, Reviewer | Team analysis, task shaping |
| `Contributor` | Member | Own work only |
| `ReadOnly` | Viewer, Customer | Progress and summary, no writes |
| `None` | Not a project member | Nothing |

Progress and summary are the floor: every project member may read them. Staffing
and replanning require `Full`. Read-only roles get no write tools even when they
appear as a task assignee.

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
| A project member can actually see the project | `ProjectServiceTests.AddMemberAsync_WhenProjectBelongsToOrganization_GrantsOrganizationMembership` |
| Membership backfill does not escalate or downgrade | `ProjectServiceTests.AddMemberAsync_WhenUserAlreadyHasElevatedOrganizationRole_DoesNotDowngradeIt`, `..._WhenCallerIsPlainMember_IsStillForbidden` |
| Unknown role is rejected, not downgraded to Member | `ProjectRoleRulesTests.TryNormalizeAssignableRole_ShouldRejectUnknownRoleInsteadOfDowngradingToMember` |
| Custom roles stay inside their organization | `ProjectServiceTests.AddMemberAsync_WithRoleFromAnotherOrganization_IsRejected`, `..._WithDeactivatedCustomRole_IsRejected` |
| Custom role inherits only its base role's reach | `ProjectServiceTests.AddMemberAsync_WithOrganizationDefinedRole_StoresKeyAndInheritsBaseRolePermissions` |
| AI tiers are ordered and role-correct | `AiCapabilityRulesTests` |
| Every project role may read progress; outsiders may not | `AiProgressSummaryApiTests.Enqueue_AnyProjectMember_CanReadProgressSummary`, `..._Enqueue_UserOutsideProject_IsDenied` |
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
