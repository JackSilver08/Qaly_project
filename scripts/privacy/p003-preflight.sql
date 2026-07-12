SET NOCOUNT ON;
SET XACT_ABORT ON;

SELECT N'MeetingImports' AS [Entity], COUNT_BIG(*) AS [RowCount]
FROM dbo.MeetingImports
UNION ALL
SELECT N'PrivacyConsents', COUNT_BIG(*)
FROM dbo.PrivacyConsents
UNION ALL
SELECT N'DataSubjectRequests', COUNT_BIG(*)
FROM dbo.DataSubjectRequests;

SELECT
    SourceProvider,
    COUNT_BIG(*) AS [RowCount],
    SUM(CASE WHEN LEN(TranscriptText) > 80000 THEN 1 ELSE 0 END) AS [OversizedTranscriptCount],
    SUM(CASE WHEN NULLIF(LTRIM(RTRIM(TranscriptText)), N'') IS NOT NULL THEN 1 ELSE 0 END) AS [TranscriptPresentCount]
FROM dbo.MeetingImports
GROUP BY SourceProvider
ORDER BY SourceProvider;

SELECT Status, ConsentType, Purpose, COUNT_BIG(*) AS [RowCount]
FROM dbo.PrivacyConsents
GROUP BY Status, ConsentType, Purpose
ORDER BY Status, ConsentType, Purpose;

SELECT Status, RequestType, COUNT_BIG(*) AS [RowCount]
FROM dbo.DataSubjectRequests
GROUP BY Status, RequestType
ORDER BY Status, RequestType;
