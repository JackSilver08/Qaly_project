# Qaly v4.0 Architecture Decision Records

These ADRs define target architecture for v4.0-draft. They do not claim that every decision is already implemented.

| ADR | Decision | Draft status |
|---|---|---|
| [ADR-001](ADR-001_Modular_Monolith.md) | Modular monolith and internal module boundaries | Accepted for v4.0 target |
| [ADR-002](ADR-002_Cookie_Session_Authentication.md) | Same-origin cookie session authentication | Accepted for v4.0 target |
| [ADR-003](ADR-003_Asynchronous_AI_Jobs.md) | Canonical asynchronous AI job lifecycle | Accepted; locally implemented in P0-02; hosted rollout pending |
| [ADR-004](ADR-004_Primary_Group_Project_Relationship.md) | Primary Group to Project relationship | Accepted for P0 target; implementation partial |
| [ADR-005](ADR-005_Meeting_Privacy_and_Retention.md) | Sensitive meeting data, consent, and retention | Accepted; locally implemented in P0-03; hosted rollout pending |
| [ADR-006](ADR-006_Constraint_Scheduling_and_Assignment.md) | Deterministic scheduling with AI explanation | Accepted for P1 target; implementation pending |
| [ADR-007](ADR-007_Entity_Versioning_and_Rollback.md) | Entity-specific snapshots and compensating rollback | Accepted for P1 target; implementation pending |

An ADR can be superseded only by a new ADR that lists affected requirements, migration, compatibility, acceptance evidence, and rollback.
