SET NOCOUNT ON;
SET XACT_ABORT ON;

SELECT
    PrivacyState,
    DataClassification,
    ProviderClass,
    COUNT_BIG(*) AS [RowCount],
    SUM(CASE WHEN ConsentId IS NULL THEN 1 ELSE 0 END) AS [MissingConsentCount],
    SUM(CASE WHEN RetentionPolicyId IS NULL THEN 1 ELSE 0 END) AS [MissingPolicyCount],
    SUM(CASE WHEN RetentionExpiresAt IS NULL THEN 1 ELSE 0 END) AS [MissingExpiryCount]
FROM dbo.MeetingImports
GROUP BY PrivacyState, DataClassification, ProviderClass
ORDER BY PrivacyState, DataClassification, ProviderClass;

SELECT
    Status,
    ProviderClass,
    PolicyVersion,
    COUNT_BIG(*) AS [RowCount],
    SUM(CASE WHEN RetentionPolicyId IS NULL THEN 1 ELSE 0 END) AS [MissingPolicyCount]
FROM dbo.PrivacyConsents
GROUP BY Status, ProviderClass, PolicyVersion
ORDER BY Status, ProviderClass, PolicyVersion;

SELECT
    Status,
    RequestType,
    COUNT_BIG(*) AS [RowCount],
    SUM(CASE WHEN SubjectUserId IS NULL THEN 1 ELSE 0 END) AS [MissingSubjectCount],
    SUM(CASE WHEN LeaseOwner IS NOT NULL OR LeaseExpiresAt IS NOT NULL THEN 1 ELSE 0 END) AS [LeasedCount]
FROM dbo.DataSubjectRequests
GROUP BY Status, RequestType
ORDER BY Status, RequestType;

SELECT
    Status,
    ActionType,
    COUNT_BIG(*) AS [RowCount],
    MIN(DueAt) AS [OldestDueAt],
    MAX(AttemptCount) AS [MaximumAttemptCount]
FROM dbo.PrivacyRetentionActions
GROUP BY Status, ActionType
ORDER BY Status, ActionType;
