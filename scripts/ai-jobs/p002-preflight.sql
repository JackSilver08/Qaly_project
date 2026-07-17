SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.AiJobs', N'U') IS NULL
    THROW 51000, 'P0-02 preflight failed: dbo.AiJobs is missing.', 1;

IF OBJECT_ID(N'dbo.AiJobQueue', N'U') IS NULL
    THROW 51000, 'P0-02 preflight failed: dbo.AiJobQueue is missing.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.AiJobs AS job
    LEFT JOIN dbo.Users AS requestedBy ON requestedBy.Id = job.RequestedById
    WHERE requestedBy.Id IS NULL
)
    THROW 51000, 'P0-02 preflight failed: AiJobs contains an orphan RequestedById.', 1;

SELECT N'AiJobsByLegacyStatus' AS Report, job.Status, COUNT_BIG(*) AS [Count]
FROM dbo.AiJobs AS job
GROUP BY job.Status
ORDER BY job.Status;

SELECT N'AiJobQueueByLegacyStatus' AS Report, queueItem.Status, COUNT_BIG(*) AS [Count]
FROM dbo.AiJobQueue AS queueItem
GROUP BY queueItem.Status
ORDER BY queueItem.Status;

SELECT
    N'LegacyQueueInventory' AS Report,
    COUNT_BIG(*) AS TotalRows,
    SUM(CASE WHEN LOWER(LTRIM(RTRIM(Status))) IN (N'queued', N'running', N'retrying') THEN 1 ELSE 0 END) AS ActiveRows,
    SUM(CASE WHEN PayloadJson IS NOT NULL THEN 1 ELSE 0 END) AS RowsWithPayload,
    SUM(CASE WHEN ResultJson IS NOT NULL THEN 1 ELSE 0 END) AS RowsWithResult
FROM dbo.AiJobQueue;

SELECT
    N'LegacyAiJobInventory' AS Report,
    COUNT_BIG(*) AS TotalRows,
    SUM(CASE WHEN NULLIF(LTRIM(RTRIM(SourceId)), N'') IS NULL THEN 1 ELSE 0 END) AS RowsWithoutSourceId,
    SUM(CASE WHEN LOWER(LTRIM(RTRIM(Status))) NOT IN (
        N'queued', N'running', N'retrying', N'failed', N'canceled', N'cancelled',
        N'draftready', N'confirmed', N'rejected', N'succeeded', N'success'
    ) THEN 1 ELSE 0 END) AS UnknownStatusRows
FROM dbo.AiJobs;
