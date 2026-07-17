# ADR-001: Retain a Modular Monolith

Status: Accepted for v4.0 target
Decision date: 2026-07-11

## Context

The original ProjectHub concept described a simpler application. The implementation is now split into Domain, Application, Infrastructure, and Web projects and supports realtime collaboration, meetings, AI, imports, and operations.

Several source files have grown beyond two thousand lines, but there is no measured scaling, deployment, or ownership evidence requiring distributed services. A microservice split would add network failure, distributed transactions, deployment coordination, observability, and versioning cost before internal boundaries are stable.

## Decision

Qaly remains one deployable modular monolith for v4.0.

Logical modules are:

- Identity and Organization;
- Project and Membership;
- Task, Sprint, Schedule, Time, and Evidence;
- Collaboration Group, Message, Poll, and Notification;
- Meeting, Transcript, Action Item, and Import;
- Knowledge and Wiki;
- AI Gateway, Job, Draft, Cost, and Compliance;
- Analytics and Reporting;
- Operations, Audit, Search, Storage, and Recovery.

Each module owns its use cases, policies, DTOs, and persistence access. Cross-module actions use application contracts or domain events rather than direct UI coupling or unrestricted repository access.

## Guardrails

1. Controllers remain thin and delegate to one use case.
2. Application services are split by cohesive workflow before they exceed a reviewable ownership boundary.
3. Frontend pages compose feature components and composables; they do not become alternate application services.
4. Cross-module DTOs are explicit and versioned where external behavior is affected.
5. Database transactions remain local to the monolith; outbox processing is used for asynchronous side effects.
6. Module ownership is recorded in CODEOWNERS and the architecture map.
7. Architecture tests prevent forbidden dependency directions.

## Consequences

Benefits include simpler deployment, transaction consistency, lower operational cost, and incremental refactoring. The cost is that module boundaries require active code review and architecture tests rather than process isolation.

## Reconsideration criteria

A service extraction requires measured independent scaling, separate data ownership, a stable contract, a responsible team, fault-isolation benefit, and an operational plan that exceeds the value of in-process integration.

## Acceptance evidence

- Module map and ownership approved.
- Oversized services and pages have decomposition plans.
- Architecture dependency tests run in CI.
- Existing build and regression behavior remains green after each extraction.
