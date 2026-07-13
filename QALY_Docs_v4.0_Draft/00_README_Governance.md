# Qaly Documentation v4.0 Draft

Status: Draft for product and engineering review
Evidence date: 2026-07-13
Evidence baseline commit: b9fda76c510988c46b7a3bcceb0d7209f44397aa
Evidence state: P0-01 through P0-03 feature-branch evidence; hosted CI pending
Historical baselines: ProjectHub.docx and QALY Docs v3.2

## 1. Purpose

This package re-baselines Qaly as an integrated project collaboration and operations platform. It connects the original ProjectHub scope, the locked v3.2 MVP requirements, the current implementation, and the target v4.0 design.

The package is not a production-readiness declaration. It records verified behavior, known gaps, target requirements, acceptance gates, and the migration sequence required before a production claim can be made.

## 2. Evidence and precedence

For claims about current implementation, use this order:

1. Executed test or runtime evidence at the evidence commit.
2. Source code and configuration at the evidence commit.
3. Current v4.0 traceability and acceptance records.
4. v3.2 specifications.
5. ProjectHub.docx and older status reports.

For target behavior inside this draft, use this order:

1. 02_Product_Vision_and_Scope_Lock.md.
2. Approved ADRs under 05_Architecture_and_ADR.
3. 03_SRS_Master_v4.0_Draft.md.
4. Domain contracts in documents 06 through 09.
5. Roadmap, acceptance, and risk records.

Source and tests always override a document when deciding whether something is already implemented. Target documents never override current runtime truth.

## 3. Relationship to historical documents

- ProjectHub.docx remains the immutable original product reference.
- QALY_Docs_v3.2_Gap_Closure_Proceed_Ready remains the immutable locked MVP reference.
- docs/13_Project_Status_2026-06-08.md remains a historical snapshot. Its percentages, test counts, and vulnerability claims are superseded by dated evidence in this package.
- No historical file is modified or silently reclassified.
- v4.0-draft becomes an approved baseline only after Product Owner and engineering review.

## 4. Status vocabulary

| Status | Meaning |
|---|---|
| Verified | Behavior is supported by current source plus executed test or runtime evidence. |
| Implemented-Unverified | A coherent implementation exists, but the required end-to-end evidence was not executed or is incomplete. |
| Partial | Part of the requirement works, but a required path, policy, contract, or acceptance condition is missing. |
| Spec-only | The target is documented but no reliable implementation evidence exists. |
| Missing | Required behavior is absent from the product surface or runtime path. |
| Out-of-scope | Deliberately excluded from the current release scope. |
| Deprecated | Retained only for compatibility or historical reference and must not guide new work. |

Percentages must not be published unless the numerator, denominator, traceability query, and evidence date are included.

## 5. Locked product decisions

1. Qaly is an integrated project collaboration and operations platform, not a Jira-lite clone.
2. The 20 v3.2 P0 use cases and 8 locked AI functions remain the minimum release gate.
3. Same-origin cookie session is the target authentication architecture for the current web product.
4. A project has zero or one primary group in P0; a group may be primary for multiple projects.
5. Meeting transcript data is sensitive by default and requires explicit consent and retention policy.
6. AI scheduling and assignment are advisory. Human confirmation is mandatory for mutations.
7. P1 versioning covers Wiki, project settings, and task fields before any broader version platform.
8. Qaly remains a modular monolith. Internal module boundaries are improved before microservices are considered.

## 6. Package index

| Document | Purpose |
|---|---|
| [01_Executive_Audit_and_ChangeLog.md](01_Executive_Audit_and_ChangeLog.md) | Direction verdict, current evidence, changes, and impact. |
| [02_Product_Vision_and_Scope_Lock.md](02_Product_Vision_and_Scope_Lock.md) | Product positioning, personas, scope, and freeze rules. |
| [03_SRS_Master_v4.0_Draft.md](03_SRS_Master_v4.0_Draft.md) | Functional and non-functional requirements. |
| [04_Traceability_Matrix_v4.0.csv](04_Traceability_Matrix_v4.0.csv) | Source-to-implementation-to-v4 traceability. |
| [05_Architecture_and_ADR](05_Architecture_and_ADR/README.md) | Architecture decisions and consequences. |
| [06_AI_Native_and_Endpoint_Contract_v4.0.md](06_AI_Native_and_Endpoint_Contract_v4.0.md) | AI capabilities, jobs, errors, providers, and review contracts. |
| [07_Data_Privacy_Retention_and_Versioning.md](07_Data_Privacy_Retention_and_Versioning.md) | Data classification, consent, retention, DSAR, and rollback. |
| [08_UX_Entity_Link_and_QoL_Map.md](08_UX_Entity_Link_and_QoL_Map.md) | Canonical navigation and cross-module workflow rules. |
| [09_NFR_Operations_Security_and_Recovery.md](09_NFR_Operations_Security_and_Recovery.md) | Performance, resilience, security, recovery, and Git governance. |
| [10_Acceptance_Checklist_v4.0.csv](10_Acceptance_Checklist_v4.0.csv) | Executable release gates and ownership. |
| [11_Risk_Register_v4.0.csv](11_Risk_Register_v4.0.csv) | Risk severity, mitigation, owner, and residual exposure. |
| [12_Roadmap_and_Migration_Plan.md](12_Roadmap_and_Migration_Plan.md) | Dependency-ordered implementation and rollout plan. |
| [13_P0-01_CI_Security_Evidence.md](13_P0-01_CI_Security_Evidence.md) | Dated P0-01 typecheck, dependency, regression, and route-smoke evidence. |
| [14_P0-02_Canonical_AI_Job_Evidence.md](14_P0-02_Canonical_AI_Job_Evidence.md) | Dated P0-02 domain, migration, worker, API, UI, SQL, E2E, runtime, and rollback evidence. |
| [15_P0-03_Privacy_Retention_DSAR_Evidence.md](15_P0-03_Privacy_Retention_DSAR_Evidence.md) | Dated P0-03 privacy policy, consent, retention, DSAR, migration, SQL, E2E, runtime, and rollback evidence. |

## 7. Current evidence snapshot

The bullets below preserve the original Phase A evidence baseline. P0-01 working-tree remediation supersedes its typecheck and dependency findings; P0-02 supersedes the canonical AI platform findings; P0-03 supersedes the privacy consent, retention, and DSAR implementation findings. See evidence documents 13 through 15. Hosted CI and staged target verification remain pending.

- Release backend build passed with a known Microsoft.OpenApi high-severity advisory warning.
- Frontend production build passed.
- Unit tests: 226 of 226 passed.
- Integration tests: 24 of 24 passed.
- Web-feature tests: 9 of 9 passed.
- Frontend typecheck failed at src/Qaly.Web/ClientApp/App.vue line 508.
- npm production audit found 5 vulnerabilities: 3 High and 2 Moderate.
- Microsoft.OpenApi 2.0.0 is a vulnerable transitive dependency in Qaly.Web and two test projects.
- Local UI was inspected with an in-memory database and offline AI fallback.
- Redis-unavailable behavior remained functional but introduced approximately ten-second authenticated request latency.
- Production infrastructure, branch protection, live provider behavior, LiveKit, and disaster recovery were not verified.

## 8. Change control

Every scope change must identify:

- affected requirement and traceability IDs;
- priority and release impact;
- data or API migration;
- security and privacy impact;
- acceptance evidence to add or retire;
- rollback strategy;
- approving Product Owner and technical owner.

No new P0 AI capability may be added until the locked AI-01 through AI-08 gates are green or an explicit scope-change ADR is approved.

## 9. Review and approval

Draft approval requires:

- Product Owner approval of product scope and human-in-the-loop policy;
- engineering approval of ADRs and migration sequence;
- QA approval of traceability and acceptance testability;
- DevSecOps approval of release, dependency, privacy, and recovery gates.

Approval of this package does not approve a production release. Production requires the blocking acceptance records to become Verified with current evidence.
