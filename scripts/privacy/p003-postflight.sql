SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.RetentionPolicies', N'U') IS NULL
    THROW 51000, 'P0-03 postflight failed: RetentionPolicies is missing.', 1;
IF OBJECT_ID(N'dbo.PrivacyRetentionActions', N'U') IS NULL
    THROW 51000, 'P0-03 postflight failed: PrivacyRetentionActions is missing.', 1;
IF OBJECT_ID(N'dbo.PrivacyLegalHolds', N'U') IS NULL
    THROW 51000, 'P0-03 postflight failed: PrivacyLegalHolds is missing.', 1;
IF COL_LENGTH(N'dbo.MeetingImports', N'DataClassification') IS NULL
    THROW 51000, 'P0-03 postflight failed: MeetingImports.DataClassification is missing.', 1;
IF COL_LENGTH(N'dbo.MeetingImports', N'PrivacyState') IS NULL
    THROW 51000, 'P0-03 postflight failed: MeetingImports.PrivacyState is missing.', 1;
IF COL_LENGTH(N'dbo.PrivacyConsents', N'PolicyVersion') IS NULL
    THROW 51000, 'P0-03 postflight failed: PrivacyConsents.PolicyVersion is missing.', 1;
IF COL_LENGTH(N'dbo.DataSubjectRequests', N'SubjectUserId') IS NULL
    THROW 51000, 'P0-03 postflight failed: DataSubjectRequests.SubjectUserId is missing.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.MeetingImports
    WHERE DataClassification <> N'unknown_sensitive'
       OR PrivacyState <> N'migration_review'
       OR RetentionPolicyId IS NOT NULL
       OR ConsentId IS NOT NULL
       OR RetentionExpiresAt IS NOT NULL)
    THROW 51000, 'P0-03 postflight failed: existing meeting rows were silently activated or scheduled.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.PrivacyConsents
    WHERE PolicyVersion <> N'legacy-unknown'
       OR NoticeVersion <> N'legacy-unknown'
       OR ProviderClass <> N'unknown'
       OR SourceType <> N'legacy'
       OR RetentionPolicyId IS NOT NULL)
    THROW 51000, 'P0-03 postflight failed: legacy consent was made effective.', 1;

SELECT
    (SELECT COUNT_BIG(*) FROM dbo.MeetingImports) AS [MeetingImportCount],
    (SELECT COUNT_BIG(*) FROM dbo.PrivacyConsents) AS [PrivacyConsentCount],
    (SELECT COUNT_BIG(*) FROM dbo.DataSubjectRequests) AS [DataSubjectRequestCount],
    (SELECT COUNT_BIG(*) FROM dbo.RetentionPolicies) AS [RetentionPolicyCount],
    (SELECT COUNT_BIG(*) FROM dbo.PrivacyRetentionActions) AS [RetentionActionCount],
    (SELECT COUNT_BIG(*) FROM dbo.PrivacyLegalHolds) AS [LegalHoldCount];
