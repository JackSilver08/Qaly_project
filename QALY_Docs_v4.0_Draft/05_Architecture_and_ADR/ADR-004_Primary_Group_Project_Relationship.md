# ADR-004: Primary Group to Project Relationship

Status: Accepted for P0 target; implementation partial

## Context

Qaly has rich group chat and can create a project from a group. Project currently carries a source-group relationship, but users cannot consistently discover and navigate linked projects from the group or the primary group from a project.

A fully general many-to-many model would add membership, notification, privacy, and navigation ambiguity before a validated need exists.

## Decision

- A Project has zero or one PrimaryGroupId in P0.
- A Group may be the primary group for multiple projects.
- The existing SourceGroupId is migrated or aliased to PrimaryGroupId with explicit semantics.
- Creating a project from a group sets its primary group.
- Linking an existing project requires authority over both project and group.
- Project and group expose reciprocal canonical links.
- Membership is not silently synchronized. Qaly previews differences and asks an authorized user to resolve them.

## Navigation and policy

- Project header and collaboration tab open the primary group.
- Group project tool lists linked projects and their permission-filtered status.
- Notifications, messages, meetings, and AI sources can reference both IDs when relevant.
- Removing a primary link does not delete either entity or its history.
- Dissolving a group leaves linked projects active and records an action-required state.

## Deferred option

Multiple groups per project are P2. A future join entity requires concrete workflows, role semantics, notification ownership, and migration evidence.

## Acceptance evidence

- Integration tests cover create, link, unlink, dissolve, membership mismatch, and authorization.
- E2E proves reciprocal navigation, refresh, shareable URL, and back behavior.
- Migration reconciles every existing source-group relationship.
