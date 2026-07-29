# AI Failure Matrix — RC Evidence 29/07/2026

**Status:** Local evidence complete  
**Scope:** Gateway, schema, provider routing, job lifecycle, budget, source and UI

| Failure mode | Expected behaviour | Evidence |
|---|---|---|
| Malformed/invalid schema | Retry within policy, then fail closed without applying data | `AiGatewayRouterTests.ExecuteAsync_WhenSchemaValidationFails_RetriesAndReturnsSchemaError`, `AiJobProcessorTests.ProcessAsync_TaskSkillInvalidSemanticSchema_FailsTerminalWithoutDraft` |
| Schema succeeds on retry | Stop retrying after first valid response | `AiGatewayRouterTests.ExecuteAsync_WhenSchemaValidationFailsFirstAttemptButSucceedsOnSecondAttempt_DoesNotRetryAgain` |
| Preferred provider unavailable | Use next configured provider only when fallback policy permits | `AiGatewayRouterTests.ExecuteAsync_WhenPreferredProviderFails_UsesNextConfiguredProvider` |
| Strict provider unavailable | Do not silently use another provider | `AiGatewayRouterTests.ExecuteAsync_WithStrictDeepSeekFailure_DoesNotUseFallbackProvider` |
| All providers unavailable | Return explicit unavailable/degraded result; never present mock as real | `AiGatewayEvidenceTests.ExecuteAsync_WhenProviderFailsWithoutDegradedMockPolicy_ReturnsProviderUnavailable`, Project/Sprint AI Playwright provider-failure cases |
| Timeout/retry policy | Apply configured timeout and bounded retry | `AiGatewayEvidenceTests.ExecuteAsync_WithCustomTimeoutAndRetry_AppliesConfiguration` |
| Retryable job failure | Release lease and schedule bounded backoff | `AiJobProcessorTests.ProcessAsync_RetryableFailure_SchedulesBackoffAndReleasesLease`, `AiJobDispatchStoreSqlServerTests.AbandonLeaseAsync_SchedulesBackoffAndPreventsEarlyReclaim` |
| Duplicate request/delivery | Replay same idempotency key; persist one result/provider call | `AiJobApiTests.CreateJob_WithSameIdempotencyKey_ReplaysAndRejectsDifferentPayload`, `AiJobDispatchStoreSqlServerTests.ProcessAsync_DuplicateDelivery_PersistsOneDraftAndOneProviderCall` |
| Cancellation during work | Discard provider output and complete attempt safely | `AiJobProcessorTests.ProcessAsync_CanceledJob_DiscardsProviderWorkAndCompletesAttempt`, `AiJobDispatchStoreSqlServerTests.ProcessAsync_WhenJobCanceledAfterClaim_CompletesAttemptWithoutGatewayMutation` |
| Hard budget stop | Reject before provider call with explicit budget error | `AiGatewayEvidenceTests.ExecuteAsync_WhenDailyBudgetExceeded_ReturnsExplicitBudgetError`, `AiCostServiceTests.TenantPolicy_HardStopUsesUsageAcrossAllTenantProjects` |
| Budget concurrency/stale edit | Reject stale version and require renewed confirmation | `AiBudgetApiTests.UpdateBudget_RequiresCsrfConfirmationAndCurrentVersionThenAuditsReadBack`, Playwright stale-confirmation case |
| Cross-tenant budget/source | Return not found/forbidden without ledger/source disclosure | `AiBudgetApiTests.CrossTenantAndMismatchedScope_ReturnNotFoundWithoutLedgerDisclosure`, `TaskSkillAiApiTests.CrossTenantAndViewerMutations_AreHiddenAndForeignSkillsNeverEnterSnapshot` |
| Missing/stale source | Fail closed or label historical result stale | `AiSourceGuardTests`, `AiProgressSummaryApiTests.ResultReadBack_WhenSourceChanged_ReturnsHistoricalResultWithStaleFlag` |
| Empty authoritative data | Return deterministic empty result without fabricated narrative | `AiJobProcessorTests.ProcessAsync_EmptyProjectProgress_CompletesDeterministicallyWithoutProviderOrDraft`, Project/Sprint/Task Skill Playwright empty-state cases |
| Sensitive data without policy | Reject with explicit compliance result and audit | `AiGatewayEvidenceTests.ExecuteAsync_SensitiveRequestWithoutConsent_ReturnsExplicitComplianceErrorAndAuditEvent` |

## UI evidence

The following Playwright groups passed locally against the Docker candidate:

- AI activity and review: 2/2.
- AI usage/budget: 4/4.
- Task skill AI: 2/2.
- Project progress AI: 3/3.
- Sprint progress AI: 3/3.

AI-specific total: **14/14 passed**. Full Playwright RC total: **38/38 passed**.

## Acceptance

The local AI failure-matrix blocker is closed. Production approval still
requires the same evidence on an immutable CI candidate and the chosen staging
environment; cloud-provider credential smoke is environment-owned evidence.
