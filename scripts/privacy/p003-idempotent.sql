BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [DeniedAt] datetimeoffset NULL;
END;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [EvidenceJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [ExpiresAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [GrantedById] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [NoticeVersion] nvarchar(80) NOT NULL DEFAULT N'legacy-unknown';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [PolicyVersion] nvarchar(80) NOT NULL DEFAULT N'legacy-unknown';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [ProviderClass] nvarchar(30) NOT NULL DEFAULT N'unknown';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [RequestId] nvarchar(120) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [RetentionPolicyId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [RevokedById] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [RowVersion] rowversion NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [SourceEntityId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [SourceType] nvarchar(50) NOT NULL DEFAULT N'legacy';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [ConsentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [ContentDeletedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [ContentRedactedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [DataClassification] nvarchar(50) NOT NULL DEFAULT N'unknown_sensitive';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [PolicyVersion] nvarchar(80) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [PrivacyState] nvarchar(40) NOT NULL DEFAULT N'migration_review';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [ProcessingPurpose] nvarchar(80) NOT NULL DEFAULT N'meeting_action_extraction';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [ProviderClass] nvarchar(30) NOT NULL DEFAULT N'unknown';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [RetentionExpiresAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [RetentionPolicyId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [TenantId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [AcceptedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [AttemptCount] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [AvailableAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [DeadlineAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [DownloadCount] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [EncryptedResultPayload] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [IdempotencyKey] nvarchar(160) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [IdentityVerifiedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [IdentityVerifiedById] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [LastDownloadedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [LastErrorCode] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [LastErrorMessage] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [LeaseExpiresAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [LeaseOwner] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [LegalHoldDetected] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [LegalHoldReason] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [MaxAttempts] int NOT NULL DEFAULT 5;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [PolicyVersion] nvarchar(80) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [RequestHash] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [ResultContentType] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [ResultExpiresAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [ResultFileName] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [ResultSummaryJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [RowVersion] rowversion NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [StartedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [SubjectUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [DataClassification] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [DataSubjectRequestId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [FailureCode] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [Outcome] nvarchar(40) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [PolicyVersion] nvarchar(80) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [PrivacyConsentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [ProviderClass] nvarchar(30) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [Purpose] nvarchar(80) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [RequestId] nvarchar(120) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [RetentionPolicyId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE TABLE [PrivacyLegalHolds] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [SubjectUserId] uniqueidentifier NULL,
        [EntityType] nvarchar(80) NULL,
        [EntityId] uniqueidentifier NULL,
        [Status] nvarchar(30) NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [HeldAt] datetimeoffset NOT NULL,
        [HeldById] uniqueidentifier NOT NULL,
        [ReleasedAt] datetimeoffset NULL,
        [ReleasedById] uniqueidentifier NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_PrivacyLegalHolds] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE TABLE [RetentionPolicies] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [Name] nvarchar(120) NOT NULL,
        [DataClassification] nvarchar(50) NOT NULL,
        [Purpose] nvarchar(80) NOT NULL,
        [AllowedRetentionDaysJson] nvarchar(200) NOT NULL,
        [DefaultRetentionDays] int NOT NULL,
        [ExpiryAction] nvarchar(30) NOT NULL,
        [LegalHoldBehavior] nvarchar(40) NOT NULL,
        [AllowCloudProcessing] bit NOT NULL,
        [AllowLocalProcessing] bit NOT NULL,
        [RequireExplicitConsent] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [PolicyVersion] nvarchar(80) NOT NULL,
        [CreatedById] uniqueidentifier NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [ApprovalOwnerUserId] uniqueidentifier NULL,
        [EffectiveFrom] datetimeoffset NOT NULL,
        [EffectiveUntil] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_RetentionPolicies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RetentionPolicies_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE TABLE [PrivacyRetentionActions] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [RetentionPolicyId] uniqueidentifier NOT NULL,
        [EntityType] nvarchar(80) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [ActionType] nvarchar(30) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [DueAt] datetimeoffset NOT NULL,
        [AvailableAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [AttemptCount] int NOT NULL,
        [MaxAttempts] int NOT NULL DEFAULT 5,
        [LeaseOwner] nvarchar(200) NULL,
        [LeaseExpiresAt] datetimeoffset NULL,
        [StartedAt] datetimeoffset NULL,
        [CompletedAt] datetimeoffset NULL,
        [LastErrorCode] nvarchar(100) NULL,
        [LastErrorMessage] nvarchar(max) NULL,
        [EvidenceJson] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_PrivacyRetentionActions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PrivacyRetentionActions_RetentionPolicies_RetentionPolicyId] FOREIGN KEY ([RetentionPolicyId]) REFERENCES [RetentionPolicies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    UPDATE consent
    SET consent.[PolicyVersion] = N'legacy-unknown',
        consent.[NoticeVersion] = N'legacy-unknown',
        consent.[ProviderClass] = N'unknown',
        consent.[SourceType] = N'legacy'
    FROM [PrivacyConsents] AS consent;

    UPDATE meetingImport
    SET meetingImport.[TenantId] = COALESCE(project.[OrganizationId], meetingImport.[ProjectId]),
        meetingImport.[DataClassification] = N'unknown_sensitive',
        meetingImport.[PrivacyState] = N'migration_review',
        meetingImport.[ProcessingPurpose] = N'meeting_action_extraction',
        meetingImport.[ProviderClass] = N'unknown'
    FROM [MeetingImports] AS meetingImport
    INNER JOIN [Projects] AS project ON project.[Id] = meetingImport.[ProjectId];

    UPDATE request
    SET request.[SubjectUserId] = COALESCE(request.[SubjectUserId], request.[RequesterUserId]),
        request.[AvailableAt] = COALESCE(request.[RequestedAt], SYSDATETIMEOFFSET()),
        request.[MaxAttempts] = 5,
        request.[Status] = CASE LOWER(request.[Status])
            WHEN N'pending' THEN N'submitted'
            WHEN N'approved' THEN N'accepted'
            WHEN N'processing' THEN N'collecting'
            ELSE LOWER(request.[Status])
        END
    FROM [DataSubjectRequests] AS request;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_PrivacyConsents_RetentionPolicyId] ON [PrivacyConsents] ([RetentionPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_PrivacyConsents_SourceType_SourceEntityId] ON [PrivacyConsents] ([SourceType], [SourceEntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_PrivacyConsents_TenantId_ProjectId_UserId] ON [PrivacyConsents] ([TenantId], [ProjectId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_MeetingImports_ConsentId] ON [MeetingImports] ([ConsentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_MeetingImports_PrivacyState_RetentionExpiresAt] ON [MeetingImports] ([PrivacyState], [RetentionExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_MeetingImports_RetentionPolicyId] ON [MeetingImports] ([RetentionPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_DataSubjectRequests_AvailableAt_LeaseExpiresAt] ON [DataSubjectRequests] ([AvailableAt], [LeaseExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DataSubjectRequests_TenantId_RequesterUserId_IdempotencyKey] ON [DataSubjectRequests] ([TenantId], [RequesterUserId], [IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_DataSubjectRequests_TenantId_SubjectUserId_RequestedAt] ON [DataSubjectRequests] ([TenantId], [SubjectUserId], [RequestedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_AiAuditEvents_DataSubjectRequestId] ON [AiAuditEvents] ([DataSubjectRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_AiAuditEvents_PrivacyConsentId] ON [AiAuditEvents] ([PrivacyConsentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_AiAuditEvents_RetentionPolicyId] ON [AiAuditEvents] ([RetentionPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_PrivacyLegalHolds_EntityType_EntityId_Status] ON [PrivacyLegalHolds] ([EntityType], [EntityId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_PrivacyLegalHolds_TenantId_ProjectId_SubjectUserId_Status] ON [PrivacyLegalHolds] ([TenantId], [ProjectId], [SubjectUserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PrivacyRetentionActions_EntityType_EntityId_ActionType] ON [PrivacyRetentionActions] ([EntityType], [EntityId], [ActionType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_PrivacyRetentionActions_RetentionPolicyId] ON [PrivacyRetentionActions] ([RetentionPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_PrivacyRetentionActions_Status_AvailableAt_LeaseExpiresAt] ON [PrivacyRetentionActions] ([Status], [AvailableAt], [LeaseExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_PrivacyRetentionActions_TenantId_ProjectId_DueAt] ON [PrivacyRetentionActions] ([TenantId], [ProjectId], [DueAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_RetentionPolicies_ProjectId] ON [RetentionPolicies] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RetentionPolicies_TenantId_PolicyVersion] ON [RetentionPolicies] ([TenantId], [PolicyVersion]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_RetentionPolicies_TenantId_ProjectId_DataClassification_Purpose_IsActive] ON [RetentionPolicies] ([TenantId], [ProjectId], [DataClassification], [Purpose], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD CONSTRAINT [FK_MeetingImports_PrivacyConsents_ConsentId] FOREIGN KEY ([ConsentId]) REFERENCES [PrivacyConsents] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD CONSTRAINT [FK_MeetingImports_RetentionPolicies_RetentionPolicyId] FOREIGN KEY ([RetentionPolicyId]) REFERENCES [RetentionPolicies] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD CONSTRAINT [FK_PrivacyConsents_RetentionPolicies_RetentionPolicyId] FOREIGN KEY ([RetentionPolicyId]) REFERENCES [RetentionPolicies] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260712185807_P003PrivacyRetentionDsar', N'10.0.7');
END;

COMMIT;
GO
