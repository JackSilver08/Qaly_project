/*
QALY AI Data Schema Migration v3.1
Purpose: support Meetily import, AI Gateway, cost control, audit, draft workflow, optional semantic memory.
DB: SQL Server oriented. Adjust naming/schema to match existing project conventions.
*/

CREATE TABLE ai_provider_config (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    provider_name NVARCHAR(50) NOT NULL,
    model_name NVARCHAR(100) NOT NULL,
    purpose NVARCHAR(100) NOT NULL,
    is_enabled BIT NOT NULL DEFAULT 1,
    priority_order INT NOT NULL DEFAULT 100,
    max_input_tokens INT NOT NULL DEFAULT 8000,
    max_output_tokens INT NOT NULL DEFAULT 1500,
    cost_input_per_1m_usd DECIMAL(12,6) NULL,
    cost_output_per_1m_usd DECIMAL(12,6) NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2 NULL
);

CREATE TABLE ai_usage_ledger (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    tenant_id BIGINT NULL,
    project_id BIGINT NULL,
    user_id BIGINT NULL,
    job_type NVARCHAR(100) NOT NULL,
    provider_name NVARCHAR(50) NOT NULL,
    model_name NVARCHAR(100) NOT NULL,
    input_tokens INT NOT NULL DEFAULT 0,
    output_tokens INT NOT NULL DEFAULT 0,
    estimated_cost_usd DECIMAL(12,6) NOT NULL DEFAULT 0,
    latency_ms INT NULL,
    status NVARCHAR(30) NOT NULL,
    prompt_hash CHAR(64) NULL,
    response_hash CHAR(64) NULL,
    error_code NVARCHAR(100) NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX ix_ai_usage_ledger_project_time ON ai_usage_ledger(project_id, created_at);
CREATE INDEX ix_ai_usage_ledger_user_time ON ai_usage_ledger(user_id, created_at);
CREATE INDEX ix_ai_usage_ledger_cost_time ON ai_usage_ledger(created_at, estimated_cost_usd);

CREATE TABLE ai_prompt_cache (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    cache_key CHAR(64) NOT NULL UNIQUE,
    job_type NVARCHAR(100) NOT NULL,
    provider_name NVARCHAR(50) NULL,
    model_name NVARCHAR(100) NULL,
    request_schema_version NVARCHAR(30) NOT NULL,
    response_json NVARCHAR(MAX) NOT NULL,
    hit_count INT NOT NULL DEFAULT 0,
    expires_at DATETIME2 NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2 NULL
);

CREATE TABLE ai_job_queue (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    tenant_id BIGINT NULL,
    project_id BIGINT NULL,
    requested_by BIGINT NULL,
    job_type NVARCHAR(100) NOT NULL,
    status NVARCHAR(30) NOT NULL DEFAULT 'queued',
    priority INT NOT NULL DEFAULT 100,
    input_ref_type NVARCHAR(50) NULL,
    input_ref_id BIGINT NULL,
    payload_json NVARCHAR(MAX) NULL,
    result_json NVARCHAR(MAX) NULL,
    retry_count INT NOT NULL DEFAULT 0,
    max_retry INT NOT NULL DEFAULT 1,
    error_message NVARCHAR(MAX) NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    started_at DATETIME2 NULL,
    finished_at DATETIME2 NULL
);

CREATE INDEX ix_ai_job_queue_status_priority ON ai_job_queue(status, priority, created_at);

CREATE TABLE meetily_import_sessions (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    tenant_id BIGINT NULL,
    project_id BIGINT NULL,
    meeting_id BIGINT NULL,
    imported_by BIGINT NULL,
    source_type NVARCHAR(30) NOT NULL DEFAULT 'meetily',
    source_file_name NVARCHAR(255) NULL,
    source_hash CHAR(64) NULL,
    import_status NVARCHAR(30) NOT NULL DEFAULT 'pending',
    transcript_text NVARCHAR(MAX) NULL,
    summary_text NVARCHAR(MAX) NULL,
    metadata_json NVARCHAR(MAX) NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    completed_at DATETIME2 NULL
);

CREATE INDEX ix_meetily_import_project_time ON meetily_import_sessions(project_id, created_at);

CREATE TABLE ai_generated_drafts (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    tenant_id BIGINT NULL,
    project_id BIGINT NULL,
    source_type NVARCHAR(50) NOT NULL,
    source_id BIGINT NULL,
    draft_type NVARCHAR(50) NOT NULL,
    draft_json NVARCHAR(MAX) NOT NULL,
    confidence DECIMAL(5,4) NULL,
    status NVARCHAR(30) NOT NULL DEFAULT 'pending_review',
    reviewed_by BIGINT NULL,
    reviewed_at DATETIME2 NULL,
    confirmed_entity_type NVARCHAR(50) NULL,
    confirmed_entity_id BIGINT NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX ix_ai_generated_drafts_project_status ON ai_generated_drafts(project_id, status, created_at);

CREATE TABLE ai_audit_events (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    tenant_id BIGINT NULL,
    actor_user_id BIGINT NULL,
    event_type NVARCHAR(100) NOT NULL,
    entity_type NVARCHAR(50) NULL,
    entity_id BIGINT NULL,
    before_json NVARCHAR(MAX) NULL,
    after_json NVARCHAR(MAX) NULL,
    ip_address NVARCHAR(64) NULL,
    user_agent NVARCHAR(512) NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX ix_ai_audit_events_entity ON ai_audit_events(entity_type, entity_id, created_at);

CREATE TABLE vector_sync_state (
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
    CONSTRAINT uq_vector_sync_source UNIQUE(source_type, source_id)
);
