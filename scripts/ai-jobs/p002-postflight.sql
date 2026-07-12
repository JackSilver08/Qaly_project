SET NOCOUNT ON;
SET XACT_ABORT ON;

IF COL_LENGTH(N'dbo.AiJobs', N'IdempotencyKey') IS NULL
    THROW 51000, 'P0-02 postflight failed: canonical AiJob columns are missing.', 1;

IF OBJECT_ID(N'dbo.AiJobDispatches', N'U') IS NULL OR
   OBJECT_ID(N'dbo.AiJobSources', N'U') IS NULL OR
   OBJECT_ID(N'dbo.AiProviderAttempts', N'U') IS NULL OR
   OBJECT_ID(N'dbo.AiJobMigrationRecords', N'U') IS NULL
    THROW 51000, 'P0-02 postflight failed: one or more canonical AI tables are missing.', 1;

DECLARE @Issues TABLE (
    CheckName nvarchar(120) NOT NULL,
    FailureCount bigint NOT NULL,
    Blocking bit NOT NULL
);

INSERT INTO @Issues
SELECT N'non_canonical_job_status', COUNT_BIG(*), 1
FROM dbo.AiJobs
WHERE Status NOT IN (N'queued', N'running', N'retrying', N'succeeded', N'failed', N'canceled');

INSERT INTO @Issues
SELECT N'missing_canonical_job_identity', COUNT_BIG(*), 1
FROM dbo.AiJobs
WHERE NULLIF(LTRIM(RTRIM(JobType)), N'') IS NULL
   OR NULLIF(LTRIM(RTRIM(SchemaId)), N'') IS NULL
   OR NULLIF(LTRIM(RTRIM(SchemaVersion)), N'') IS NULL
   OR NULLIF(LTRIM(RTRIM(RequestHash)), N'') IS NULL
   OR NULLIF(LTRIM(RTRIM(IdempotencyKey)), N'') IS NULL
   OR NULLIF(LTRIM(RTRIM(CacheKey)), N'') IS NULL;

INSERT INTO @Issues
SELECT N'active_job_without_one_dispatch', COUNT_BIG(*), 1
FROM dbo.AiJobs AS job
OUTER APPLY (
    SELECT COUNT_BIG(*) AS DispatchCount
    FROM dbo.AiJobDispatches AS dispatch
    WHERE dispatch.AiJobId = job.Id AND dispatch.CompletedAt IS NULL
) AS dispatches
WHERE job.Status IN (N'queued', N'running', N'retrying')
  AND dispatches.DispatchCount <> 1;

INSERT INTO @Issues
SELECT N'duplicate_dispatch', COUNT_BIG(*), 1
FROM (
    SELECT AiJobId
    FROM dbo.AiJobDispatches
    GROUP BY AiJobId
    HAVING COUNT_BIG(*) > 1
) AS duplicate;

INSERT INTO @Issues
SELECT N'duplicate_provider_attempt_number', COUNT_BIG(*), 1
FROM (
    SELECT AiJobId, AttemptNumber
    FROM dbo.AiProviderAttempts
    GROUP BY AiJobId, AttemptNumber
    HAVING COUNT_BIG(*) > 1
) AS duplicate;

INSERT INTO @Issues
SELECT N'invalid_draft_status', COUNT_BIG(*), 1
FROM dbo.AiGeneratedDrafts
WHERE Status NOT IN (N'pending_review', N'confirmed', N'rejected', N'expired');

INSERT INTO @Issues
SELECT N'legacy_queue_without_migration_record', COUNT_BIG(*), 1
FROM dbo.AiJobQueue AS queueItem
LEFT JOIN dbo.AiJobMigrationRecords AS record ON record.LegacyQueueItemId = queueItem.Id
WHERE record.Id IS NULL;

INSERT INTO @Issues
SELECT N'active_legacy_queue_not_quarantined', COUNT_BIG(*), 1
FROM dbo.AiJobQueue AS queueItem
INNER JOIN dbo.AiJobMigrationRecords AS record ON record.LegacyQueueItemId = queueItem.Id
WHERE LOWER(LTRIM(RTRIM(queueItem.Status))) IN (N'queued', N'running', N'retrying')
  AND record.Classification <> N'quarantined';

INSERT INTO @Issues
SELECT N'terminal_legacy_queue_not_historical', COUNT_BIG(*), 1
FROM dbo.AiJobQueue AS queueItem
INNER JOIN dbo.AiJobMigrationRecords AS record ON record.LegacyQueueItemId = queueItem.Id
WHERE LOWER(LTRIM(RTRIM(queueItem.Status))) NOT IN (N'queued', N'running', N'retrying')
  AND record.Classification <> N'historical_unlinked';

SELECT CheckName, FailureCount, Blocking
FROM @Issues
ORDER BY Blocking DESC, CheckName;

IF EXISTS (SELECT 1 FROM @Issues WHERE Blocking = 1 AND FailureCount > 0)
    THROW 51000, 'P0-02 postflight failed: blocking reconciliation checks did not pass.', 1;

SELECT
    N'P0-02 postflight passed' AS Result,
    (SELECT COUNT_BIG(*) FROM dbo.AiJobs) AS CanonicalJobs,
    (SELECT COUNT_BIG(*) FROM dbo.AiJobDispatches) AS Dispatches,
    (SELECT COUNT_BIG(*) FROM dbo.AiJobSources) AS Sources,
    (SELECT COUNT_BIG(*) FROM dbo.AiProviderAttempts) AS ProviderAttempts,
    (SELECT COUNT_BIG(*) FROM dbo.AiJobMigrationRecords) AS LegacyQueueRecords;
