# Qaly Release Candidate Governance

> RC window: 28/07–03/08/2026  
> Decision owner: Quang Tuấn (Tech Lead)

## Scope lock

The RC contains stabilization of organization/member management, three-layer
authorization, AI progress/skill features, primary-screen UX and release
recovery. New RAG, PWA, advanced reporting and unrelated features are deferred.

## Merge gates

- [ ] PR identifies priority (`P0`/`P1`/`P2`) and risk (`low`/`medium`/`high`).
- [ ] Acceptance criteria and user-visible behaviour are stated.
- [ ] High-risk changes have Tech Lead plus domain-owner review.
- [ ] Positive and negative authorization tests accompany RBAC/tenant changes.
- [ ] `npm run typecheck` and `npm run build` pass; committed bundle matches.
- [ ] Release build, unit and integration suites pass at their coverage gates.
- [ ] RC Playwright journeys pass on the same commit.
- [ ] Configuration, dependency and container checks pass.
- [ ] Migration includes preflight, postflight, reconciliation and recovery evidence.
- [ ] User-visible change has a release/demo note.

## Daily triage

| Severity | Definition | Response |
|---|---|---|
| P0 | Cross-tenant data access, privilege escalation, secret exposure, data loss, system unavailable | Stop merge/release; assign immediately; retest full affected boundary |
| P1 | Core organization/project/task/AI journey cannot complete or release gate fails | Fix before RC; owner and retest deadline required |
| P2 | Recoverable UX, responsive, accessibility or non-core defect | Fix if capacity permits; otherwise record owner/date |

Every defect records commit, environment, reproduction, expected/actual result,
evidence, owner and retest outcome.

## RC evidence record

| Evidence | Status | Commit/artifact | Owner |
|---|---|---|---|
| Frontend typecheck/build/bundle | Local pass; CI evidence pending | Source baseline `91d3fca0` plus RC working changes; typecheck/build pass, bundle unchanged | Viết Minh |
| Release solution build | Local pass | 0 warnings, 0 errors | Quang Tuấn |
| Unit tests and coverage | Local pass | 373/373; 49.36% ≥ 43% | Đoàn Trung |
| Integration tests and coverage | Local pass | 98/98; 37.07% ≥ 16% | Đoàn Trung |
| RC Playwright journeys | Local pass | 38/38, serial groups against Docker candidate | Đoàn Trung |
| Tenant/RBAC regression | Local pass | Moderator matrix 9/9; full integration 98/98 | Duy Hoàng + Đoàn Trung |
| AI schema/source/budget/failure matrix | Local pass | `docs/16_AI_Failure_Matrix_RC_2026-07-29.md`; AI E2E 14/14 | Quốc Bảo + Chí Khang |
| Configuration/vulnerability scan | Local pass | `91d3fca0`; safety/parity/Compose and NuGet vulnerability scan pass | Gia Long |
| Staging health/degradation test | Local equivalent pass; real target pending | Candidate/rollback health 200; Redis outage gives readiness 503 while login remains 200 | Gia Long |
| Backup/clean restore/rollback rehearsal | Local pass; real target pending | SHA256 backup, `VERIFYONLY`, restore, `DBCC CHECKDB`, smoke and cleanup pass | Gia Long |

## Go/No-Go

Go requires no open P0/P1, all required gates green on one immutable commit,
verified tenant/RBAC/AI budget boundaries, healthy staging, successful recovery
rehearsal and approved release notes/known issues.

No-Go is automatic for cross-tenant access, privilege escalation, unsafe
migration/restore, unbounded AI spend, secret exposure, unreproducible build or
missing verified rollback.

## Decision record

- Candidate commit:
- Image digest/tag:
- Decision time:
- Decision: `GO` / `NO-GO`
- Open P2 issues:
- Known limitations:
- Evidence links:
- Approved by:

## Interim decision — 29/07/2026

**Decision: `NO-GO` (expected during RC preparation).**

Local gates and recovery rehearsal are healthy, but the release cannot be
approved until the changes are captured in one immutable candidate commit, CI
repeats the evidence, and health/degradation/recovery are run on the selected
staging target with its real secret store. See
`docs/15_Tech_Lead_RC_Audit_2026-07-29.md`.
