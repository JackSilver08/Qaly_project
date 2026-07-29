# Tech Lead RC Audit — 29/07/2026

**Owner:** Quang Tuấn  
**Source baseline:** `91d3fca03c33f28ad0c683828717223d4021d4c0`  
**Decision:** `NO-GO` while RC preparation is in progress

## Completed Tech Lead work

- Locked RC scope and explicit deferred scope.
- Published the normative system/organization/project/capability matrix.
- Added merge, evidence, severity and Go/No-Go rules.
- Expanded CODEOWNERS across auth, tenant, moderator, task access and AI policy.
- Extended the PR template with risk, tenant, negative-test and recovery evidence.
- Reviewed current organization/moderator authorization implementation and tests.

## Local evidence

| Gate | Result |
|---|---|
| `git diff --check` for authored changes | Pass |
| Frontend typecheck | Pass after deterministic `npm ci` |
| Frontend production build | Pass; committed bundle unchanged |
| Release solution build | Pass; 0 warnings, 0 errors |
| Focused RBAC/tenant unit tests | 53/53 pass |
| Full unit tests | 373/373 pass |
| Full integration tests | 91/91 pass |
| Configuration safety | Pass |
| Configuration parity | Pass |
| Docker Compose validation | Pass |
| NuGet vulnerability scan | Pass |
| Unit coverage | 49.36% (gate 43%) |
| Integration coverage | 37.07% (gate 16%) |
| Full Playwright RC | 38/38 pass |
| Production image build | Pass, non-root UID 1654 |
| Candidate/rollback smoke | Both return HTTP 200 |
| Clean backup/restore | `VERIFYONLY`, restore, `DBCC CHECKDB`, smoke and cleanup pass |

The local workstation is running Node 24 while CI locks Node 22. The pre-existing
installed dependency tree was incomplete and typecheck initially failed inside
`@vue/language-core`; a clean `npm ci` from the committed lockfile fixed it.
No package manifest or lockfile change was required.

## Triage

### P0

No P0 was reproduced during this audit.

### P1 release blockers

| ID | Blocker | Owner | Acceptance evidence |
|---|---|---|---|
| RC-P1-01 | Closed locally: RC Playwright | Đoàn Trung | 38/38 pass |
| RC-P1-02 | Closed locally: delegated Moderator matrix | Duy Hoàng + Đoàn Trung | 9/9 focused tests; 98/98 full integration |
| RC-P1-03 | Closed locally: AI failure matrix | Quốc Bảo + Chí Khang | `docs/16_AI_Failure_Matrix_RC_2026-07-29.md`; AI E2E 14/14 |
| RC-P1-04 | Real staging deploy/degradation evidence still missing | Gia Long | Immutable image healthy on target; Redis/provider degradation recorded |
| RC-P1-05 | Real target backup/clean restore/rollback still missing | Gia Long | Local rehearsal passed; repeat against target storage/database |
| RC-P1-06 | Closed locally: coverage gates | Đoàn Trung | Unit 49.36%; integration 37.07% |
| RC-P1-07 | Working changes are not yet an immutable CI candidate | Quang Tuấn | Commit/tag/image digest and all CI gates on that exact source |

### P2 / follow-up

| ID | Item | Owner |
|---|---|---|
| RC-P2-01 | Standardize local development on Node 22 to match CI | Gia Long |
| RC-P2-02 | Replace deprecated `lucide-vue-next@1.0.0` package when UI freeze permits | Viết Minh |
| RC-P2-03 | Add second domain reviewer accounts to CODEOWNERS when GitHub usernames are confirmed | Quang Tuấn |

## Go conditions

Re-evaluate after RC-P1-04, RC-P1-05 and RC-P1-07 are closed. Any cross-tenant
access, privilege escalation, unsafe restore, secret exposure or unbounded AI
spend discovered during closure changes the decision to an immediate release
stop.
