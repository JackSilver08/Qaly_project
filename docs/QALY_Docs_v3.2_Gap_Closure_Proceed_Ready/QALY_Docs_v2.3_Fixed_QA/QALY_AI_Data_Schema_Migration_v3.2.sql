/*
QALY AI Data Schema Migration v3.2
DB: SQL Server oriented.
Purpose: close v3.1 gaps: FK-ready columns, tenant/project/user indexes, retention/consent/sensitive controls,
data subject requests, provider budget, schema_id, job lifecycle, and audit trail.

Note:
- Core table names may differ in implementation. This script uses conservative nullable FK columns and indexes.
- Add actual FOREIGN KEY constraints after confirming core table names: users, projects, tenants/organizations, tasks, chat_messages, meetings.
*/

-- 1. Provider config and budget
IF OBJECT_ID('dbo.ai_provider_config', 'U') IS NULL
CREATE TABLE dbo.ai_provider_config (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    tenant_id BIGINT NULL,
    provider_name NVARCHAR(50) NOT NULL,
    model_name NVARCHAR(100) NOT NULL,
    purpose NVARCHAR(100) NOT NULL,
    is_enabled BIT NOT NULL DEFAULT 1,
    priority_order INT NOT NULL DEFAULT 100,
    max_input_tokens INT NOT NULL DEFAULT 8000,
    max_output_tokens INT NOT NULL DEFAULT 1500,
    cost_input_per_1m_usd DECIMAL(12,6) NULL,
    cost_output_per_1m_usd DECIMAL(12,6) NULL,
    data_policy NVARCHAR(50) NOT NULL DEFAULT 'standard', -- standard|no_cloud_sensitive|mock_only
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2 NULL
);
GO

IF OBJECT_ID('dbo.ai_budget_policy', 'U') IS NULL
CREATE TABLE dbo.ai_budget_policy (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    tenant_id BIGINT NULL,
    project_id BIGINT NULL,
    daily_budget_usd DECIMAL(12,4) NOT NULL DEFAULT 2.0000,
    monthly_budget_usd DECIMAL(12,4) NOT NULL DEFAULT 30.0000,
    warn_at_percent INT NOT NULL DEFAULT 80,
    hard_stop_enabled BIT NOT NULL DEFAULT 1,
    allow_cloud_for_sensitive BIT NOT NULL DEFAULT 0,
    created_by BIGINT NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2 NULL
);
GO
CREATE INDEX ix_ai_budget_policy_scope ON dbo.ai_budget_policy(tenant_id, project_id);
GO

-- 2. Usage ledger and cache
IF OBJECT_ID('dbo.ai_usage_ledger', 'U') IS NULL
CREATE TABLE dbo.ai_usage_ledger (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    tenant_id BIGINT NULL,
    project_id BIGINT NULL,
    user_id BIGINT NULL,
    job_id BIGINT NULL,
    job_type NVARCHAR(100) NOT NULL,
    provider_name NVARCHAR(50) NOT NULL,
    model_name NVARCHAR(100) NOT NULL,
    input_tokens INT NOT NULL DEFAULT 0,
    output_tokens INT NOT NULL DEFAULT 0,
    estimated_cost_usd DECIMAL(12,6) NOT NULL DEFAULT 0,
    latency_ms INT NULL,
    status NVARCHAR(30) NOT NULL,
    cache_hit BIT NOT NULL DEFAULT 0,
    prompt_hash CHAR(64) NULL,
    response_hash CHAR(64) NULL,
    error_code NVARCHAR(100) NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
CREATE INDEX ix_ai_usage_ledger_project_time ON dbo.ai_usage_ledger(tenant_id, project_id, created_at);
CREATE INDEX ix_ai_usage_ledger_user_time ON dbo.ai_usage_ledger(tenant_id, user_id, created_at);
CREATE INDEX ix_ai_usage_ledger_job ON dbo.ai_usage_ledger(job_id);
GO

IF OBJECT_ID('dbo.ai_prompt_cache', 'U') IS NULL
CREATE TABLE dbo.ai_prompt_cache (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    tenant_id BIGINT NULL,
    project_id BIGINT NULL,
    cache_key CHAR(64) NOT NULL,
    job_type NVARCHAR(100) NOT NULL,
    schema_id NVARCHAR(100) NOT NULL,
    provider_name NVARCHAR(50) NULL,
    model_name NVARCHAR(100) NULL,
    request_hash CHAR(64) NOT NULL,
    response_json NVARCHAR(MAX) NOT NULL,
    hit_count INT NOT NULL DEFAULT 0,
    expires_at DATETIME2 NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2 NULL,
    CONSTRAINT uq_ai_prompt_cache_scope UNIQUE(tenant_id, project_id, cache_key)
);
GO
CREATE INDEX ix_ai_prompt_cache_expiry ON dbo.ai_prompt_cache(expires_at);
GO

-- 3. Job queue
IF OBJECT_ID('dbo.ai_job_queue', 'U') IS NULL
CREATE TABLE dbo.ai_job_queue (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    tenant_id BIGINT NULL,
    project_id BIGINT NULL,
    requested_by BIGINT NULL,
    job_type NVARCHAR(100) NOT NULL,
    schema_id NVARCHAR(100) NOT NULL,
    status NVARCHAR(30) NOT NULL DEFAULT 'queued', -- queued|running|succeeded|failed|retrying|canceled
    priority INT NOT NULL DEFAULT 100,
    sensitive BIT NOT NULL DEFAULT 0,
    consent_id BIGINT NULL,
    provider_hint NVARCHAR(50) NULL,
    input_ref_type NVARCHAR(50) NULL,
    input_ref_id BIGINT NULL,
    payload_json NVARCHAR(MAX) NULL,
    result_json NVARCHAR(MAX) NULL,
    retry_count INT NOT NULL DEFAULT 0,
    max_retry INT NOT NULL DEFAULT 1,
    error_code NVARCHAR(100) NULL,
    error_message NVARCHAR(MAX) NULL,
    estimated_cost_usd DECIMAL(12,6) NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    started_at DATETIME2 NULL,
    finished_at DATETIME2 NULL,
    canceled_at DATETIME2 NULL,
    canceled_by BIGINT NULL,
    CONSTRAINT ck_ai_job_queue_status CHECK (status IN ('queued','running','succeeded','failed','retrying','canceled'))
);
GO
CREATE INDEX ix_ai_job_queue_status_priority ON dbo.ai_job_queue(status, priority, created_at);
CREATE INDEX ix_ai_job_queue_project_status ON dbo.ai_job_queue(tenant_id, project_id, status, created_at);
GO

-- 4. Privacy consent and data subject requests
IF OBJECT_ID('dbo.privacy_consents', 'U') IS NULL
CREATE TABLE dbo.privacy_consents (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    tenant_id BIGINT NULL,
    project_id BIGINT NULL,
    user_id BIGINT NULL,
    consent_type NVARCHAR(50) NOT NULL, -- meeting_import|ai_cloud_processing|transcript_storage
    purpose NVARCHAR(255) NOT NULL,
    scope_json NVARCHAR(MAX) NULL,
    status NVARCHAR(30) NOT NULL DEFAULT 'granted', -- granted|revoked|expired
    granted_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    revoked_at DATETIME2 NULL,
    ip_address NVARCHAR(64) NULL,
    user_agent NVARCHAR(512) NULL
);
GO
CREATE INDEX ix_privacy_consents_scope ON dbo.privacy_consents(tenant_id, project_id, user_id, consent_type, status);
GO

IF OBJECT_ID('dbo.data_subject_requests', 'U') IS NULL
CREATE TABLE dbo.data_subject_requests (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    tenant_id BIGINT NULL,
    project_id BIGINT NULL,
    requester_user_id BIGINT NULL,
    request_type NVARCHAR(30) NOT NULL, -- export|delete|anonymize
    scope_json NVARCHAR(MAX) NOT NULL,
    status NVARCHAR(30) NOT NULL DEFAULT 'pending', -- pending|approved|processing|completed|rejected
    requested_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    approved_by BIGINT NULL,
    completed_at DATETIME2 NULL,
    rejection_reason NVARCHAR(MAX) NULL,
    evidence_uri NVARCHAR(512) NULL,
    CONSTRAINT ck_dsr_type CHECK (request_type IN ('export','delete','anonymize'))
);
GO
CREATE INDEX ix_dsr_status_time ON dbo.data_subject_requests(status, requested_at);
GO

-- 5. Meetily import and meeting intelligence
IF OBJECT_ID('dbo.meetily_import_sessions', 'U') IS NULL
CREATE TABLE dbo.meetily_import_sessions (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    tenant_id BIGINT NULL,
    project_id BIGINT NULL,
    meeting_id BIGINT NULL,
    imported_by BIGINT NULL,
    consent_id BIGINT NULL,
    source_type NVARCHAR(30) NOT NULL DEFAULT 'meetily',
    source_file_name NVARCHAR(255) NULL,
    source_hash CHAR(64) NULL,
    import_status NVARCHAR(30) NOT NULL DEFAULT 'pending',
    sensitive BIT NOT NULL DEFAULT 0,
    transcript_text NVARCHAR(MAX) NULL,
    summary_text NVARCHAR(MAX) NULL,
    language NVARCHAR(20) NULL DEFAULT 'vi',
    metadata_json NVARCHAR(MAX) NULL,
    retention_days INT NOT NULL DEFAULT 180,
    delete_after DATETIME2 NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    completed_at DATETIME2 NULL,
    CONSTRAINT uq_meetily_import_hash UNIQUE(project_id, source_hash)
);
GO
CREATE INDEX ix_meetily_import_project_time ON dbo.meetily_import_sessions(tenant_id, project_id, created_at);
CREATE INDEX ix_meetily_import_delete_after ON dbo.meetily_import_sessions(delete_after);
GO

-- 6. Generated draft workflow
IF OBJECT_ID('dbo.ai_generated_drafts', 'U') IS NULL
CREATE TABLE dbo.ai_generated_drafts (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    tenant_id BIGINT NULL,
    project_id BIGINT NULL,
    job_id BIGINT NULL,
    source_type NVARCHAR(50) NOT NULL,
    source_id BIGINT NULL,
    draft_type NVARCHAR(50) NOT NULL,
    schema_id NVARCHAR(100) NOT NULL,
    draft_json NVARCHAR(MAX) NOT NULL,
    confidence DECIMAL(5,4) NULL,
    status NVARCHAR(30) NOT NULL DEFAULT 'pending_review', -- pending_review|confirmed|rejected|expired
    reviewed_by BIGINT NULL,
    reviewed_at DATETIME2 NULL,
    confirmed_entity_type NVARCHAR(50) NULL,
    confirmed_entity_id BIGINT NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    expires_at DATETIME2 NULL,
    CONSTRAINT ck_ai_generated_drafts_status CHECK (status IN ('pending_review','confirmed','rejected','expired'))
);
GO
CREATE INDEX ix_ai_generated_drafts_project_status ON dbo.ai_generated_drafts(tenant_id, project_id, status, created_at);
CREATE INDEX ix_ai_generated_drafts_job ON dbo.ai_generated_drafts(job_id);
GO

-- 7. AI audit events
IF OBJECT_ID('dbo.ai_audit_events', 'U') IS NULL
CREATE TABLE dbo.ai_audit_events (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    tenant_id BIGINT NULL,
    project_id BIGINT NULL,
    actor_user_id BIGINT NULL,
    event_type NVARCHAR(100) NOT NULL,
    entity_type NVARCHAR(50) NULL,
    entity_id BIGINT NULL,
    job_id BIGINT NULL,
    before_json NVARCHAR(MAX) NULL,
    after_json NVARCHAR(MAX) NULL,
    ip_address NVARCHAR(64) NULL,
    user_agent NVARCHAR(512) NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
CREATE INDEX ix_ai_audit_events_entity ON dbo.ai_audit_events(tenant_id, entity_type, entity_id, created_at);
CREATE INDEX ix_ai_audit_events_job ON dbo.ai_audit_events(job_id);
GO

-- 8. Optional vector sync state for P1
IF OBJECT_ID('dbo.vector_sync_state', 'U') IS NULL
CREATE TABLE dbo.vector_sync_state (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    tenant_id BIGINT NULL,
    project_id BIGINT NULL,
    source_type NVARCHAR(50) NOT NULL,
    source_id BIGINT NOT NULL,
    source_updated_at DATETIME2 NULL,
    vector_collection NVARCHAR(100) NULL,
    vector_point_id NVARCHAR(100) NULL,
    sync_status NVARCHAR(30) NOT NULL DEFAULT 'pending',
    error_message NVARCHAR(MAX) NULL,
    synced_at DATETIME2 NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT uq_vector_sync_source UNIQUE(tenant_id, project_id, source_type, source_id)
);
GO
CREATE INDEX ix_vector_sync_status ON dbo.vector_sync_state(sync_status, created_at);
GO

-- 9. Suggested FK constraints to add after core table confirmation
-- ALTER TABLE dbo.ai_usage_ledger ADD CONSTRAINT fk_ai_usage_project FOREIGN KEY(project_id) REFERENCES dbo.projects(id);
-- ALTER TABLE dbo.ai_usage_ledger ADD CONSTRAINT fk_ai_usage_user FOREIGN KEY(user_id) REFERENCES dbo.users(id);
-- ALTER TABLE dbo.ai_job_queue ADD CONSTRAINT fk_ai_job_project FOREIGN KEY(project_id) REFERENCES dbo.projects(id);
-- ALTER TABLE dbo.ai_job_queue ADD CONSTRAINT fk_ai_job_requested_by FOREIGN KEY(requested_by) REFERENCES dbo.users(id);
-- ALTER TABLE dbo.meetily_import_sessions ADD CONSTRAINT fk_meetily_project FOREIGN KEY(project_id) REFERENCES dbo.projects(id);
-- ALTER TABLE dbo.meetily_import_sessions ADD CONSTRAINT fk_meetily_consent FOREIGN KEY(consent_id) REFERENCES dbo.privacy_consents(id);
-- ALTER TABLE dbo.ai_generated_drafts ADD CONSTRAINT fk_ai_draft_job FOREIGN KEY(job_id) REFERENCES dbo.ai_job_queue(id);

-- 10. Seed provider examples - update exact pricing before real API purchase
IF NOT EXISTS (SELECT 1 FROM dbo.ai_provider_config WHERE provider_name='mock' AND model_name='mock-json-v3.2')
INSERT INTO dbo.ai_provider_config(provider_name, model_name, purpose, priority_order, max_input_tokens, max_output_tokens, cost_input_per_1m_usd, cost_output_per_1m_usd, data_policy)
VALUES ('mock','mock-json-v3.2','ci-demo-fallback',1,32000,4000,0,0,'mock_only');

IF NOT EXISTS (SELECT 1 FROM dbo.ai_provider_config WHERE provider_name='openai' AND model_name='gpt-5.4-mini')
INSERT INTO dbo.ai_provider_config(provider_name, model_name, purpose, priority_order, max_input_tokens, max_output_tokens, cost_input_per_1m_usd, cost_output_per_1m_usd, data_policy)
VALUES ('openai','gpt-5.4-mini','primary-demo-quality',10,32000,4000,0.750000,4.500000,'standard');

IF NOT EXISTS (SELECT 1 FROM dbo.ai_provider_config WHERE provider_name='gemini' AND model_name='gemini-2.5-flash-lite')
INSERT INTO dbo.ai_provider_config(provider_name, model_name, purpose, priority_order, max_input_tokens, max_output_tokens, cost_input_per_1m_usd, cost_output_per_1m_usd, data_policy)
VALUES ('gemini','gemini-2.5-flash-lite','low-cost-summary-extraction',20,32000,4000,0.100000,0.400000,'standard');
GO
