# Qaly V4 — Security Threat Model

> **Status:** `SOURCE_REVIEWED — INDEPENDENT_SIGNOFF_PENDING`
>
> **Updated:** 02/09/2026
> This model describes the current working tree. It is not a penetration-test certificate and does not make an unconfigured external adapter production-ready.

## 1. Scope and production assumptions

In scope: browser/Razor/Vue entry points, cookie and API-key authentication, three-layer RBAC, view-as, AI source/action authorization, SQL/Redis state, imports, realtime, workers and configured external adapters.

Production assumptions that must be verified on the deployed target:

- TLS terminates at Kestrel or at a declared trusted proxy; `ReverseProxy:Mode`, concrete proxy IPs and hop count match the real topology.
- Credentials come from the target secret store, never tracked appsettings, demo seed or CI test values.
- SQL Server, Redis and Data Protection storage are durable, access-controlled, backed up and monitored.
- Only adapters with a real credential/endpoint and canonical receipt are presented as enabled.
- Edge/WAF, network policy, log access and backup access are owned outside this repository and require independent review.

## 2. Assets and trust boundaries

Primary assets are tenant/project content, private Wiki and meeting data, identities and role assignments, professional/skill evidence, availability/workload, AI prompts/sources/drafts, API keys/integration secrets, audit trails and mutation receipts.

Trust boundaries:

1. untrusted browser/client → public HTTP boundary;
2. reverse proxy/load balancer → Qaly Web, only when its concrete address is trusted;
3. authenticated principal → System, Organization, Project and resource authorization;
4. Qaly Web/workers → SQL Server, Redis and Data Protection storage;
5. authorized Qaly source → AI provider/vector retrieval;
6. Qaly outbox → webhook, SMTP, GitHub, WebPush and other external systems;
7. uploaded/imported content → parsers, storage and canonical entities.

## 3. Threat and control matrix

| ID | Threat / abuse path | Current preventive or detective control | Current evidence | Residual production work |
|---|---|---|---|---|
| TM-01 | Cross-tenant/foreign object ID exposes or mutates Project, Task, Wiki, Group or Meeting data | service-level resource checks, deny-wins scope order, private/customer rules and zero-side-effect denial | focused boundary Integration `34/34`, authorization Unit `54/54`, AI P06–P28 Integration `72/72` | independent endpoint inventory and authenticated DAST |
| TM-02 | Access role, custom role or professional profile widens privilege unexpectedly | separate System/Organization/Project/resource layers; professional profiles never grant access; unknown/deleted role fails closed | role/profile focused Unit `58/58`, Organization/Profile Integration `20/20`, client permission gates | exhaustive production persona/browser certification |
| TM-03 | API key inherits the owner's full role or reaches an unpublished route | bearer forwarding plus exact route/method scope middleware; scope only narrows canonical RBAC; API-key view-as denied | API-key persistence and boundary tests, full regression | target secret rotation, revocation drill and independent review |
| TM-04 | Cross-site request forges cookie-authenticated mutation | antiforgery middleware/filter on normal cookie mutation; API-key path is bearer-aware | CSRF-positive and negative WebFeature/Integration coverage | authenticated DAST on exact public origin/CORS topology |
| TM-05 | Admin view-as keeps Admin privilege or performs mutation | full principal replacement happens before authorization; only active real users; non-safe methods and API keys are denied | Simulation Integration `3/3`, auth boundary `10/10`, P25 read-only UI replay | exhaustive production persona matrix and audit review |
| TM-06 | AI provider/prompt injects foreign source or invents authorization | selected scope and server source registry are authoritative; capability router/domain policy re-authorize; provider cannot grant controls | P01–P27 evidence, AI source/action positive/negative suites | live-provider privacy review, DAST and provider-contract acceptance |
| TM-07 | Stale/duplicate AI or Project Launch confirmation writes twice or writes changed state | source/version hash, rowversion/revision, explicit confirmation, payload-bound idempotency and canonical read-back | native action Integration `9/9`, Project Launch and retry/reload evidence | multi-instance/load/soak qualification on target |
| TM-08 | Webhook URL performs SSRF, DNS rebinding or targets private infrastructure | scheme/user-info validation, public-IP-only DNS policy and connection-time address guard | `WebhookEndpointPolicyTests`, webhook service/publisher tests | deployed egress allowlist/proxy policy and external endpoint acceptance |
| TM-09 | Webhook event is lost, replayed or falsely reported successful | Task mutation and occurrence-scoped outbox commit atomically; lease/retry/dead-letter; signed payload; redacted receipt and audited replay | outbox/retention focused Unit `6/6`, operator/health WebFeature evidence | real endpoint signature/read-back, operator alert route and incident drill |
| TM-10 | Stolen/expired cookie remains valid after user disable or role change | HttpOnly, Secure production cookie, SameSite Lax, server-side ticket store, eight-hour sliding lifetime and per-request active-role validation | auth/session Integration and production config validator | target Redis/Data Protection rotation and session revocation drill |
| TM-11 | Spoofed forwarded IP/protocol bypasses HTTPS or rate limiting | forwarded headers processed only in `trusted-proxy` mode with concrete IP/hop; direct mode ignores spoofed headers; account limiters return 429/Retry-After | validator `13/13`, spoofed-forwarded-IP WebFeature negative test | edge/WAF policy and deployed topology test |
| TM-12 | Credentials or personal email leak through repository/config/logs | production config fail-closed; tracked `.env` forbidden; high-confidence token/private-key scan; raw email log templates removed in favor of entity IDs/general events | `check-configuration.ps1`, `check-security-hygiene.ps1`, build and focused Unit/WebFeature | secret-store scan, log-sink access/retention and independent log sample review |
| TM-13 | Malicious/oversized import causes traversal, parser abuse or partial canonical write | controlled import preview/confirm/undo, validation and authorization boundaries | import Unit/Integration/E2E evidence in master plan | fuzzing, malware/content scanning and target storage policy |
| TM-14 | Dependency or build-chain compromise reaches release image | lock files, clean install/restore, transitive vulnerability checks, CodeQL workflow, exact-SHA CD and provenance/SBOM | local NuGet/npm scans, workflow YAML/source gates | successful CodeQL/CI run on commit, dependency/license approval and registry policy |
| TM-15 | External provider outage is represented as success | disabled/degraded/external-deferred states, actual provider/model metadata and receipt requirement | P26 fallback evidence and P28 boundary | live write/read-back acceptance for every enabled adapter |
| TM-16 | Queue/dependency pressure causes silent loss or restart loop | liveness/readiness split, backlog/dead-letter thresholds, durable workers, retention preserving dead-letters, optional OTLP request/dependency plus durable AI progress/answer/terminal metrics and traces | health/operator WebFeature, worker/outbox tests, telemetry validator/registration/bounded-tag tests and local restore rehearsal | target collector/alerting, baseline calibration, load/soak, graceful shutdown and chaos drills |

## 4. Logging and data-minimization contract

- Never log passwords, bearer/API keys, invitation tokens, cookies, request/response bodies or raw AI/provider payloads by default.
- Do not log raw email/full name/phone/address when an entity, subscription, invitation, Project or correlation ID is sufficient.
- Request logging records route/status/latency; query strings, headers and bodies are not promoted into the default message template.
- Operational receipts exposed to users/operators are redacted and authorization-scoped.
- Exception/log sink sampling on the deployed target remains mandatory because third-party exception text can contain provider-controlled detail.

`scripts/check-security-hygiene.ps1` is a narrow fail-closed source gate, not a general secret scanner. It rejects high-confidence private-key/token patterns and sensitive placeholders in application log templates while allowing declared local/CI test credentials. Repository-host secret scanning and target secret-store review remain separate production gates.

## 5. Acceptance and ownership

| Decision | Required owner/evidence | State |
|---|---|---|
| Source threat model reviewed against current routes/services | Engineering owner; this document plus current automated suites | `PASS_LOCAL` |
| No high-confidence tracked credential or explicit raw-PII log template | CI security-hygiene/config gates | `PASS_LOCAL` |
| Deployed trust boundaries match this model | Platform/security owner; exact-SHA deployment, TLS/proxy/network evidence | `BLOCKED_EXTERNAL` |
| Security effectiveness independently tested | Security reviewer; CodeQL, authenticated DAST, penetration report and remediation rerun | `BLOCKED_EXTERNAL` |
| Residual risks accepted for commercial use | Product/security/legal owners | `BLOCKED_EXTERNAL` |

Any material new route, authentication scheme, external adapter, file parser, AI mutation capability or tenant-visible entity must update this model and add a positive plus negative boundary test before release.
