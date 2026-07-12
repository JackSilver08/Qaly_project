SET NOCOUNT ON;

SELECT Status, COUNT_BIG(*) AS JobCount
FROM dbo.AiJobs
GROUP BY Status
ORDER BY Status;

SELECT Classification, COUNT_BIG(*) AS LegacyQueueCount
FROM dbo.AiJobMigrationRecords
GROUP BY Classification
ORDER BY Classification;

SELECT
    job.Id AS JobId,
    job.Status,
    job.AttemptCount,
    COUNT(DISTINCT attempt.Id) AS ProviderAttemptRows,
    COUNT(DISTINCT usage.Id) AS UsageRows,
    COUNT(DISTINCT draft.Id) AS DraftRows,
    COUNT(DISTINCT source.Id) AS SourceRows,
    MAX(dispatch.DeliveryCount) AS DeliveryCount,
    MAX(dispatch.CompletedAt) AS DispatchCompletedAt
FROM dbo.AiJobs AS job
LEFT JOIN dbo.AiProviderAttempts AS attempt ON attempt.AiJobId = job.Id
LEFT JOIN dbo.AiUsageLedger AS usage ON usage.AiJobId = job.Id
LEFT JOIN dbo.AiGeneratedDrafts AS draft ON draft.AiJobId = job.Id
LEFT JOIN dbo.AiJobSources AS source ON source.AiJobId = job.Id
LEFT JOIN dbo.AiJobDispatches AS dispatch ON dispatch.AiJobId = job.Id
GROUP BY job.Id, job.Status, job.AttemptCount
ORDER BY job.Id;

SELECT queueItem.Id, queueItem.Status, record.Classification, record.Reason, record.ReconciledAt
FROM dbo.AiJobQueue AS queueItem
LEFT JOIN dbo.AiJobMigrationRecords AS record ON record.LegacyQueueItemId = queueItem.Id
ORDER BY queueItem.CreatedAt, queueItem.Id;
