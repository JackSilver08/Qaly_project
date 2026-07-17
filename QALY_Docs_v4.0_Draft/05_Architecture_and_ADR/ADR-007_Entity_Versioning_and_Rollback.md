# ADR-007: Entity-Specific Snapshots and Compensating Rollback

Status: Accepted for P1 target; implementation pending

## Context

Qaly has audit logs, task RowVersion concurrency, import undo, soft-delete restore, and archived-project restore. These mechanisms solve different problems and do not provide complete object version history.

Generic event sourcing would impose broad migration and reconstruction complexity without a proven need.

## Decision

Implement immutable snapshots for three P1 domains:

- Wiki title and content;
- Project workflow and operational settings;
- approved Task fields such as title, description, priority, dates, estimate, assignees, and workflow state.

Each snapshot records entity ID, version number, schema version, normalized payload, actor, timestamp, reason, source version, and correlation ID.

## Rollback semantics

Rollback does not delete history. It:

1. loads the requested historical snapshot;
2. computes a permission-aware diff against current state;
3. validates dependencies, current row version, and business rules;
4. requires an authorized user to confirm impact;
5. issues a normal compensating update;
6. creates a new snapshot and audit record referencing the restored version.

Audit events, notifications, completed external side effects, and deleted files are not rewritten. Irreversible effects are listed before confirmation.

## Distinctions

- Audit trail records who did what.
- RowVersion detects concurrent writers.
- Undo reverses a bounded recent workflow.
- Soft-delete restore changes lifecycle state.
- Version rollback creates a new current state from a historical snapshot.

## Retention and access

Snapshot retention follows entity and tenant policy. Private or sensitive versions retain the original access restrictions. Export and deletion workflows include snapshot data where policy requires.

## Acceptance evidence

- Snapshot creation, schema migration, diff, permission, stale-version conflict, restore, irreversible-impact warning, audit, and retention tests.
- Recovery verifies snapshots remain consistent across application and database rollback.
