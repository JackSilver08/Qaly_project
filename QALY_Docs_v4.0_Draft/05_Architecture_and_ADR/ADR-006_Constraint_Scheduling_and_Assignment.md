# ADR-006: Deterministic Scheduling with AI Explanation

Status: Accepted for P1 target; implementation pending

## Context

Current assignment insight uses explainable workload, task history, and text signals. ProjectMember does not model explicit skills, proficiency, availability, weekly capacity, leave, or timezone. Existing Gantt and critical-path behavior is useful but cannot reliably perform resource-constrained scheduling.

Using a language model as the scheduling engine would make feasibility, repeatability, and audit difficult.

## Decision

Scheduling and assignment use a hybrid design:

1. SQL-derived facts and validated user-maintained capability data provide inputs.
2. A deterministic rules or constraint engine creates feasible ranked scenarios.
3. AI explains tradeoffs, missing data, and suggested follow-up questions.
4. An authorized user reviews a visible diff and confirms selected changes.

Required inputs include skills, proficiency, availability, weekly capacity, leave, timezone, task effort, required skills, dependencies, fixed dates, deadlines, project calendar, and policy constraints.

## Fairness and privacy

- Do not infer sensitive traits, personality, health, or performance from chat.
- Explain every ranking factor and show missing or stale data.
- Allow opt-out and correction of capability data.
- Prevent systematic overload with explicit capacity and fairness constraints.
- Keep historical performance signals bounded, purpose-specific, and reviewable.

## Output

Each scenario contains assignments, dates, unresolved conflicts, capacity utilization, critical dependencies, confidence, assumptions, and change diff. No scenario writes domain state before confirmation.

## Acceptance evidence

- Deterministic repeatability for identical inputs.
- No over-allocation beyond approved policy without a visible exception.
- Dependency and calendar feasibility tests.
- Permission, privacy, fairness, stale-data, explanation, and human-confirmation tests.
- Feature flag and rollback to read-only recommendations.
