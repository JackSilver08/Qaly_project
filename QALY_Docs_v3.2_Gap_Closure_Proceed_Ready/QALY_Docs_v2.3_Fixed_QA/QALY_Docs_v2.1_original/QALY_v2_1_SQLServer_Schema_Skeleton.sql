-- QALY v2.1 SQL Server logical schema skeleton
-- Generated from QALY_Table_Catalog.csv; review domain-specific columns before EF Core migration.
-- Primary implementation target: SQL Server 2022 Developer Edition. Roadmap compatible with SQL Server 2025.

CREATE SCHEMA qaly;
GO

IF OBJECT_ID(N'qaly.feature_flags', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.feature_flags (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_feature_flags PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_feature_flags_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_feature_flags_active_created_at' AND object_id = OBJECT_ID(N'qaly.feature_flags')) CREATE INDEX ix_feature_flags_active_created_at ON qaly.feature_flags(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.users', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.users (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_users PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        email NVARCHAR(320) NOT NULL,
        password_hash NVARCHAR(MAX) NULL,
        system_role NVARCHAR(50) NOT NULL DEFAULT N'guest',
        is_active BIT NOT NULL DEFAULT 1,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_users_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_users_tenant_id' AND object_id = OBJECT_ID(N'qaly.users')) CREATE INDEX ix_users_tenant_id ON qaly.users(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_users_active_created_at' AND object_id = OBJECT_ID(N'qaly.users')) CREATE INDEX ix_users_active_created_at ON qaly.users(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.user_profiles', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.user_profiles (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_user_profiles PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_user_profiles_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_profiles_tenant_id' AND object_id = OBJECT_ID(N'qaly.user_profiles')) CREATE INDEX ix_user_profiles_tenant_id ON qaly.user_profiles(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_profiles_active_created_at' AND object_id = OBJECT_ID(N'qaly.user_profiles')) CREATE INDEX ix_user_profiles_active_created_at ON qaly.user_profiles(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.organizations', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.organizations (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_organizations PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NOT NULL,
        code NVARCHAR(80) NOT NULL,
        status NVARCHAR(50) NOT NULL DEFAULT N'active',
        owner_id UNIQUEIDENTIFIER NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_organizations_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organizations_tenant_id' AND object_id = OBJECT_ID(N'qaly.organizations')) CREATE INDEX ix_organizations_tenant_id ON qaly.organizations(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organizations_active_created_at' AND object_id = OBJECT_ID(N'qaly.organizations')) CREATE INDEX ix_organizations_active_created_at ON qaly.organizations(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.organization_members', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.organization_members (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_organization_members PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_organization_members_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_members_tenant_id' AND object_id = OBJECT_ID(N'qaly.organization_members')) CREATE INDEX ix_organization_members_tenant_id ON qaly.organization_members(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_members_active_created_at' AND object_id = OBJECT_ID(N'qaly.organization_members')) CREATE INDEX ix_organization_members_active_created_at ON qaly.organization_members(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.organization_custom_fields', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.organization_custom_fields (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_organization_custom_fields PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_organization_custom_fields_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_custom_fields_tenant_id' AND object_id = OBJECT_ID(N'qaly.organization_custom_fields')) CREATE INDEX ix_organization_custom_fields_tenant_id ON qaly.organization_custom_fields(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_custom_fields_active_created_at' AND object_id = OBJECT_ID(N'qaly.organization_custom_fields')) CREATE INDEX ix_organization_custom_fields_active_created_at ON qaly.organization_custom_fields(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.projects', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.projects (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_projects PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NOT NULL,
        code NVARCHAR(80) NOT NULL,
        description NVARCHAR(MAX) NULL,
        status NVARCHAR(50) NOT NULL DEFAULT N'active',
        owner_id UNIQUEIDENTIFIER NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_projects_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_projects_tenant_id' AND object_id = OBJECT_ID(N'qaly.projects')) CREATE INDEX ix_projects_tenant_id ON qaly.projects(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_projects_active_created_at' AND object_id = OBJECT_ID(N'qaly.projects')) CREATE INDEX ix_projects_active_created_at ON qaly.projects(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.project_settings', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.project_settings (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_project_settings PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_project_settings_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_settings_tenant_id' AND object_id = OBJECT_ID(N'qaly.project_settings')) CREATE INDEX ix_project_settings_tenant_id ON qaly.project_settings(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_settings_active_created_at' AND object_id = OBJECT_ID(N'qaly.project_settings')) CREATE INDEX ix_project_settings_active_created_at ON qaly.project_settings(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.project_members', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.project_members (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_project_members PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_project_members_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_members_tenant_id' AND object_id = OBJECT_ID(N'qaly.project_members')) CREATE INDEX ix_project_members_tenant_id ON qaly.project_members(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_members_active_created_at' AND object_id = OBJECT_ID(N'qaly.project_members')) CREATE INDEX ix_project_members_active_created_at ON qaly.project_members(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.project_labels', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.project_labels (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_project_labels PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_project_labels_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_labels_tenant_id' AND object_id = OBJECT_ID(N'qaly.project_labels')) CREATE INDEX ix_project_labels_tenant_id ON qaly.project_labels(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_labels_active_created_at' AND object_id = OBJECT_ID(N'qaly.project_labels')) CREATE INDEX ix_project_labels_active_created_at ON qaly.project_labels(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.project_custom_fields', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.project_custom_fields (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_project_custom_fields PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_project_custom_fields_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_custom_fields_tenant_id' AND object_id = OBJECT_ID(N'qaly.project_custom_fields')) CREATE INDEX ix_project_custom_fields_tenant_id ON qaly.project_custom_fields(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_custom_fields_active_created_at' AND object_id = OBJECT_ID(N'qaly.project_custom_fields')) CREATE INDEX ix_project_custom_fields_active_created_at ON qaly.project_custom_fields(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.project_template_tasks', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.project_template_tasks (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_project_template_tasks PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_project_template_tasks_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_template_tasks_tenant_id' AND object_id = OBJECT_ID(N'qaly.project_template_tasks')) CREATE INDEX ix_project_template_tasks_tenant_id ON qaly.project_template_tasks(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_template_tasks_active_created_at' AND object_id = OBJECT_ID(N'qaly.project_template_tasks')) CREATE INDEX ix_project_template_tasks_active_created_at ON qaly.project_template_tasks(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.project_status_reports', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.project_status_reports (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_project_status_reports PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_project_status_reports_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_status_reports_tenant_id' AND object_id = OBJECT_ID(N'qaly.project_status_reports')) CREATE INDEX ix_project_status_reports_tenant_id ON qaly.project_status_reports(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_status_reports_active_created_at' AND object_id = OBJECT_ID(N'qaly.project_status_reports')) CREATE INDEX ix_project_status_reports_active_created_at ON qaly.project_status_reports(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.tasks', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.tasks (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_tasks PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        title NVARCHAR(300) NOT NULL,
        description NVARCHAR(MAX) NULL,
        status NVARCHAR(50) NOT NULL DEFAULT N'todo',
        priority NVARCHAR(50) NOT NULL DEFAULT N'medium',
        due_date DATETIMEOFFSET(7) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_tasks_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tasks_tenant_id' AND object_id = OBJECT_ID(N'qaly.tasks')) CREATE INDEX ix_tasks_tenant_id ON qaly.tasks(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tasks_active_created_at' AND object_id = OBJECT_ID(N'qaly.tasks')) CREATE INDEX ix_tasks_active_created_at ON qaly.tasks(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_assignments', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_assignments (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_assignments PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_assignments_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_assignments_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_assignments')) CREATE INDEX ix_task_assignments_tenant_id ON qaly.task_assignments(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_assignments_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_assignments')) CREATE INDEX ix_task_assignments_active_created_at ON qaly.task_assignments(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_labels', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_labels (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_labels PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_labels_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_labels_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_labels')) CREATE INDEX ix_task_labels_tenant_id ON qaly.task_labels(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_labels_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_labels')) CREATE INDEX ix_task_labels_active_created_at ON qaly.task_labels(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_checklists', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_checklists (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_checklists PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_checklists_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_checklists_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_checklists')) CREATE INDEX ix_task_checklists_tenant_id ON qaly.task_checklists(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_checklists_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_checklists')) CREATE INDEX ix_task_checklists_active_created_at ON qaly.task_checklists(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_checklist_items', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_checklist_items (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_checklist_items PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_checklist_items_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_checklist_items_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_checklist_items')) CREATE INDEX ix_task_checklist_items_tenant_id ON qaly.task_checklist_items(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_checklist_items_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_checklist_items')) CREATE INDEX ix_task_checklist_items_active_created_at ON qaly.task_checklist_items(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_time_logs', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_time_logs (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_time_logs PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_time_logs_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_time_logs_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_time_logs')) CREATE INDEX ix_task_time_logs_tenant_id ON qaly.task_time_logs(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_time_logs_created_at' AND object_id = OBJECT_ID(N'qaly.task_time_logs')) CREATE INDEX ix_task_time_logs_created_at ON qaly.task_time_logs(created_at DESC);
GO

IF OBJECT_ID(N'qaly.task_comments_count_cache', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_comments_count_cache (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_comments_count_cache PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_comments_count_cache_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_comments_count_cache_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_comments_count_cache')) CREATE INDEX ix_task_comments_count_cache_tenant_id ON qaly.task_comments_count_cache(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_comments_count_cache_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_comments_count_cache')) CREATE INDEX ix_task_comments_count_cache_active_created_at ON qaly.task_comments_count_cache(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_subtasks', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_subtasks (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_subtasks PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_subtasks_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_subtasks_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_subtasks')) CREATE INDEX ix_task_subtasks_tenant_id ON qaly.task_subtasks(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_subtasks_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_subtasks')) CREATE INDEX ix_task_subtasks_active_created_at ON qaly.task_subtasks(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.workflow_definitions', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.workflow_definitions (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_workflow_definitions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_workflow_definitions_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_workflow_definitions_tenant_id' AND object_id = OBJECT_ID(N'qaly.workflow_definitions')) CREATE INDEX ix_workflow_definitions_tenant_id ON qaly.workflow_definitions(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_workflow_definitions_active_created_at' AND object_id = OBJECT_ID(N'qaly.workflow_definitions')) CREATE INDEX ix_workflow_definitions_active_created_at ON qaly.workflow_definitions(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.workflow_states', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.workflow_states (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_workflow_states PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_workflow_states_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_workflow_states_tenant_id' AND object_id = OBJECT_ID(N'qaly.workflow_states')) CREATE INDEX ix_workflow_states_tenant_id ON qaly.workflow_states(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_workflow_states_active_created_at' AND object_id = OBJECT_ID(N'qaly.workflow_states')) CREATE INDEX ix_workflow_states_active_created_at ON qaly.workflow_states(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.workflow_transitions', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.workflow_transitions (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_workflow_transitions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_workflow_transitions_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_workflow_transitions_tenant_id' AND object_id = OBJECT_ID(N'qaly.workflow_transitions')) CREATE INDEX ix_workflow_transitions_tenant_id ON qaly.workflow_transitions(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_workflow_transitions_active_created_at' AND object_id = OBJECT_ID(N'qaly.workflow_transitions')) CREATE INDEX ix_workflow_transitions_active_created_at ON qaly.workflow_transitions(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.workflow_transition_conditions', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.workflow_transition_conditions (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_workflow_transition_conditions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_workflow_transition_conditions_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_workflow_transition_conditions_tenant_id' AND object_id = OBJECT_ID(N'qaly.workflow_transition_conditions')) CREATE INDEX ix_workflow_transition_conditions_tenant_id ON qaly.workflow_transition_conditions(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_workflow_transition_conditions_active_created_at' AND object_id = OBJECT_ID(N'qaly.workflow_transition_conditions')) CREATE INDEX ix_workflow_transition_conditions_active_created_at ON qaly.workflow_transition_conditions(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.workflow_transition_actions', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.workflow_transition_actions (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_workflow_transition_actions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_workflow_transition_actions_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_workflow_transition_actions_tenant_id' AND object_id = OBJECT_ID(N'qaly.workflow_transition_actions')) CREATE INDEX ix_workflow_transition_actions_tenant_id ON qaly.workflow_transition_actions(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_workflow_transition_actions_active_created_at' AND object_id = OBJECT_ID(N'qaly.workflow_transition_actions')) CREATE INDEX ix_workflow_transition_actions_active_created_at ON qaly.workflow_transition_actions(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_workflow_states', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_workflow_states (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_workflow_states PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_workflow_states_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_workflow_states_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_workflow_states')) CREATE INDEX ix_task_workflow_states_tenant_id ON qaly.task_workflow_states(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_workflow_states_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_workflow_states')) CREATE INDEX ix_task_workflow_states_active_created_at ON qaly.task_workflow_states(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_status_change_requests', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_status_change_requests (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_status_change_requests PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_status_change_requests_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_status_change_requests_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_status_change_requests')) CREATE INDEX ix_task_status_change_requests_tenant_id ON qaly.task_status_change_requests(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_status_change_requests_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_status_change_requests')) CREATE INDEX ix_task_status_change_requests_active_created_at ON qaly.task_status_change_requests(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_status_change_approvals', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_status_change_approvals (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_status_change_approvals PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_status_change_approvals_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_status_change_approvals_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_status_change_approvals')) CREATE INDEX ix_task_status_change_approvals_tenant_id ON qaly.task_status_change_approvals(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_status_change_approvals_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_status_change_approvals')) CREATE INDEX ix_task_status_change_approvals_active_created_at ON qaly.task_status_change_approvals(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_review_cycles', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_review_cycles (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_review_cycles PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_review_cycles_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_review_cycles_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_review_cycles')) CREATE INDEX ix_task_review_cycles_tenant_id ON qaly.task_review_cycles(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_review_cycles_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_review_cycles')) CREATE INDEX ix_task_review_cycles_active_created_at ON qaly.task_review_cycles(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_review_feedbacks', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_review_feedbacks (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_review_feedbacks PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_review_feedbacks_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_review_feedbacks_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_review_feedbacks')) CREATE INDEX ix_task_review_feedbacks_tenant_id ON qaly.task_review_feedbacks(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_review_feedbacks_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_review_feedbacks')) CREATE INDEX ix_task_review_feedbacks_active_created_at ON qaly.task_review_feedbacks(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_evidences', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_evidences (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_evidences PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_evidences_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_evidences_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_evidences')) CREATE INDEX ix_task_evidences_tenant_id ON qaly.task_evidences(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_evidences_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_evidences')) CREATE INDEX ix_task_evidences_active_created_at ON qaly.task_evidences(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_evidence_reviews', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_evidence_reviews (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_evidence_reviews PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_evidence_reviews_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_evidence_reviews_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_evidence_reviews')) CREATE INDEX ix_task_evidence_reviews_tenant_id ON qaly.task_evidence_reviews(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_evidence_reviews_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_evidence_reviews')) CREATE INDEX ix_task_evidence_reviews_active_created_at ON qaly.task_evidence_reviews(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_evidence_review_comments', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_evidence_review_comments (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_evidence_review_comments PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_evidence_review_comments_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_evidence_review_comments_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_evidence_review_comments')) CREATE INDEX ix_task_evidence_review_comments_tenant_id ON qaly.task_evidence_review_comments(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_evidence_review_comments_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_evidence_review_comments')) CREATE INDEX ix_task_evidence_review_comments_active_created_at ON qaly.task_evidence_review_comments(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.evidence_approval_audit_logs', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.evidence_approval_audit_logs (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_evidence_approval_audit_logs PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_evidence_approval_audit_logs_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_evidence_approval_audit_logs_tenant_id' AND object_id = OBJECT_ID(N'qaly.evidence_approval_audit_logs')) CREATE INDEX ix_evidence_approval_audit_logs_tenant_id ON qaly.evidence_approval_audit_logs(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_evidence_approval_audit_logs_created_at' AND object_id = OBJECT_ID(N'qaly.evidence_approval_audit_logs')) CREATE INDEX ix_evidence_approval_audit_logs_created_at ON qaly.evidence_approval_audit_logs(created_at DESC);
GO

IF OBJECT_ID(N'qaly.task_dependencies', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_dependencies (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_dependencies PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_dependencies_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_dependencies_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_dependencies')) CREATE INDEX ix_task_dependencies_tenant_id ON qaly.task_dependencies(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_dependencies_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_dependencies')) CREATE INDEX ix_task_dependencies_active_created_at ON qaly.task_dependencies(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.kanban_columns', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.kanban_columns (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_kanban_columns PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_kanban_columns_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_kanban_columns_tenant_id' AND object_id = OBJECT_ID(N'qaly.kanban_columns')) CREATE INDEX ix_kanban_columns_tenant_id ON qaly.kanban_columns(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_kanban_columns_active_created_at' AND object_id = OBJECT_ID(N'qaly.kanban_columns')) CREATE INDEX ix_kanban_columns_active_created_at ON qaly.kanban_columns(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.sprints', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.sprints (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_sprints PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_sprints_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_sprints_tenant_id' AND object_id = OBJECT_ID(N'qaly.sprints')) CREATE INDEX ix_sprints_tenant_id ON qaly.sprints(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_sprints_active_created_at' AND object_id = OBJECT_ID(N'qaly.sprints')) CREATE INDEX ix_sprints_active_created_at ON qaly.sprints(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.sprint_tasks', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.sprint_tasks (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_sprint_tasks PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_sprint_tasks_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_sprint_tasks_tenant_id' AND object_id = OBJECT_ID(N'qaly.sprint_tasks')) CREATE INDEX ix_sprint_tasks_tenant_id ON qaly.sprint_tasks(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_sprint_tasks_active_created_at' AND object_id = OBJECT_ID(N'qaly.sprint_tasks')) CREATE INDEX ix_sprint_tasks_active_created_at ON qaly.sprint_tasks(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.sprint_review_notes', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.sprint_review_notes (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_sprint_review_notes PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_sprint_review_notes_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_sprint_review_notes_tenant_id' AND object_id = OBJECT_ID(N'qaly.sprint_review_notes')) CREATE INDEX ix_sprint_review_notes_tenant_id ON qaly.sprint_review_notes(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_sprint_review_notes_active_created_at' AND object_id = OBJECT_ID(N'qaly.sprint_review_notes')) CREATE INDEX ix_sprint_review_notes_active_created_at ON qaly.sprint_review_notes(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.comments', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.comments (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_comments PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_comments_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_comments_tenant_id' AND object_id = OBJECT_ID(N'qaly.comments')) CREATE INDEX ix_comments_tenant_id ON qaly.comments(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_comments_active_created_at' AND object_id = OBJECT_ID(N'qaly.comments')) CREATE INDEX ix_comments_active_created_at ON qaly.comments(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.chat_rooms', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.chat_rooms (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_chat_rooms PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_chat_rooms_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_rooms_tenant_id' AND object_id = OBJECT_ID(N'qaly.chat_rooms')) CREATE INDEX ix_chat_rooms_tenant_id ON qaly.chat_rooms(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_rooms_active_created_at' AND object_id = OBJECT_ID(N'qaly.chat_rooms')) CREATE INDEX ix_chat_rooms_active_created_at ON qaly.chat_rooms(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.chat_messages', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.chat_messages (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_chat_messages PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_chat_messages_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_messages_tenant_id' AND object_id = OBJECT_ID(N'qaly.chat_messages')) CREATE INDEX ix_chat_messages_tenant_id ON qaly.chat_messages(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_messages_active_created_at' AND object_id = OBJECT_ID(N'qaly.chat_messages')) CREATE INDEX ix_chat_messages_active_created_at ON qaly.chat_messages(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.meetings', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.meetings (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_meetings PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_meetings_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_meetings_tenant_id' AND object_id = OBJECT_ID(N'qaly.meetings')) CREATE INDEX ix_meetings_tenant_id ON qaly.meetings(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_meetings_active_created_at' AND object_id = OBJECT_ID(N'qaly.meetings')) CREATE INDEX ix_meetings_active_created_at ON qaly.meetings(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.meeting_participants', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.meeting_participants (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_meeting_participants PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_meeting_participants_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_meeting_participants_tenant_id' AND object_id = OBJECT_ID(N'qaly.meeting_participants')) CREATE INDEX ix_meeting_participants_tenant_id ON qaly.meeting_participants(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_meeting_participants_active_created_at' AND object_id = OBJECT_ID(N'qaly.meeting_participants')) CREATE INDEX ix_meeting_participants_active_created_at ON qaly.meeting_participants(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.meeting_agendas', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.meeting_agendas (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_meeting_agendas PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_meeting_agendas_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_meeting_agendas_tenant_id' AND object_id = OBJECT_ID(N'qaly.meeting_agendas')) CREATE INDEX ix_meeting_agendas_tenant_id ON qaly.meeting_agendas(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_meeting_agendas_active_created_at' AND object_id = OBJECT_ID(N'qaly.meeting_agendas')) CREATE INDEX ix_meeting_agendas_active_created_at ON qaly.meeting_agendas(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.meeting_agenda_items', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.meeting_agenda_items (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_meeting_agenda_items PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_meeting_agenda_items_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_meeting_agenda_items_tenant_id' AND object_id = OBJECT_ID(N'qaly.meeting_agenda_items')) CREATE INDEX ix_meeting_agenda_items_tenant_id ON qaly.meeting_agenda_items(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_meeting_agenda_items_active_created_at' AND object_id = OBJECT_ID(N'qaly.meeting_agenda_items')) CREATE INDEX ix_meeting_agenda_items_active_created_at ON qaly.meeting_agenda_items(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.meeting_notes', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.meeting_notes (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_meeting_notes PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_meeting_notes_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_meeting_notes_tenant_id' AND object_id = OBJECT_ID(N'qaly.meeting_notes')) CREATE INDEX ix_meeting_notes_tenant_id ON qaly.meeting_notes(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_meeting_notes_active_created_at' AND object_id = OBJECT_ID(N'qaly.meeting_notes')) CREATE INDEX ix_meeting_notes_active_created_at ON qaly.meeting_notes(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.meeting_note_sections', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.meeting_note_sections (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_meeting_note_sections PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_meeting_note_sections_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_meeting_note_sections_tenant_id' AND object_id = OBJECT_ID(N'qaly.meeting_note_sections')) CREATE INDEX ix_meeting_note_sections_tenant_id ON qaly.meeting_note_sections(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_meeting_note_sections_active_created_at' AND object_id = OBJECT_ID(N'qaly.meeting_note_sections')) CREATE INDEX ix_meeting_note_sections_active_created_at ON qaly.meeting_note_sections(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.meeting_action_items', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.meeting_action_items (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_meeting_action_items PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_meeting_action_items_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_meeting_action_items_tenant_id' AND object_id = OBJECT_ID(N'qaly.meeting_action_items')) CREATE INDEX ix_meeting_action_items_tenant_id ON qaly.meeting_action_items(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_meeting_action_items_active_created_at' AND object_id = OBJECT_ID(N'qaly.meeting_action_items')) CREATE INDEX ix_meeting_action_items_active_created_at ON qaly.meeting_action_items(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.meeting_recordings', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.meeting_recordings (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_meeting_recordings PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_meeting_recordings_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_meeting_recordings_tenant_id' AND object_id = OBJECT_ID(N'qaly.meeting_recordings')) CREATE INDEX ix_meeting_recordings_tenant_id ON qaly.meeting_recordings(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_meeting_recordings_active_created_at' AND object_id = OBJECT_ID(N'qaly.meeting_recordings')) CREATE INDEX ix_meeting_recordings_active_created_at ON qaly.meeting_recordings(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.meeting_ai_summaries', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.meeting_ai_summaries (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_meeting_ai_summaries PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_meeting_ai_summaries_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_meeting_ai_summaries_tenant_id' AND object_id = OBJECT_ID(N'qaly.meeting_ai_summaries')) CREATE INDEX ix_meeting_ai_summaries_tenant_id ON qaly.meeting_ai_summaries(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_meeting_ai_summaries_active_created_at' AND object_id = OBJECT_ID(N'qaly.meeting_ai_summaries')) CREATE INDEX ix_meeting_ai_summaries_active_created_at ON qaly.meeting_ai_summaries(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.meeting_transcripts', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.meeting_transcripts (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_meeting_transcripts PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_meeting_transcripts_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_meeting_transcripts_tenant_id' AND object_id = OBJECT_ID(N'qaly.meeting_transcripts')) CREATE INDEX ix_meeting_transcripts_tenant_id ON qaly.meeting_transcripts(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_meeting_transcripts_active_created_at' AND object_id = OBJECT_ID(N'qaly.meeting_transcripts')) CREATE INDEX ix_meeting_transcripts_active_created_at ON qaly.meeting_transcripts(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.wiki_pages', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.wiki_pages (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_wiki_pages PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_wiki_pages_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_pages_tenant_id' AND object_id = OBJECT_ID(N'qaly.wiki_pages')) CREATE INDEX ix_wiki_pages_tenant_id ON qaly.wiki_pages(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_pages_active_created_at' AND object_id = OBJECT_ID(N'qaly.wiki_pages')) CREATE INDEX ix_wiki_pages_active_created_at ON qaly.wiki_pages(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.wiki_page_comments', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.wiki_page_comments (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_wiki_page_comments PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_wiki_page_comments_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_page_comments_tenant_id' AND object_id = OBJECT_ID(N'qaly.wiki_page_comments')) CREATE INDEX ix_wiki_page_comments_tenant_id ON qaly.wiki_page_comments(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_page_comments_active_created_at' AND object_id = OBJECT_ID(N'qaly.wiki_page_comments')) CREATE INDEX ix_wiki_page_comments_active_created_at ON qaly.wiki_page_comments(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.files', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.files (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_files PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_files_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_files_tenant_id' AND object_id = OBJECT_ID(N'qaly.files')) CREATE INDEX ix_files_tenant_id ON qaly.files(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_files_active_created_at' AND object_id = OBJECT_ID(N'qaly.files')) CREATE INDEX ix_files_active_created_at ON qaly.files(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.notifications', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.notifications (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_notifications PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_notifications_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_notifications_tenant_id' AND object_id = OBJECT_ID(N'qaly.notifications')) CREATE INDEX ix_notifications_tenant_id ON qaly.notifications(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_notifications_active_created_at' AND object_id = OBJECT_ID(N'qaly.notifications')) CREATE INDEX ix_notifications_active_created_at ON qaly.notifications(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.webhooks', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.webhooks (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_webhooks PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_webhooks_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_webhooks_tenant_id' AND object_id = OBJECT_ID(N'qaly.webhooks')) CREATE INDEX ix_webhooks_tenant_id ON qaly.webhooks(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_webhooks_active_created_at' AND object_id = OBJECT_ID(N'qaly.webhooks')) CREATE INDEX ix_webhooks_active_created_at ON qaly.webhooks(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.webhook_events', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.webhook_events (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_webhook_events PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_webhook_events_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_webhook_events_tenant_id' AND object_id = OBJECT_ID(N'qaly.webhook_events')) CREATE INDEX ix_webhook_events_tenant_id ON qaly.webhook_events(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_webhook_events_active_created_at' AND object_id = OBJECT_ID(N'qaly.webhook_events')) CREATE INDEX ix_webhook_events_active_created_at ON qaly.webhook_events(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.webhook_deliveries', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.webhook_deliveries (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_webhook_deliveries PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_webhook_deliveries_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_webhook_deliveries_tenant_id' AND object_id = OBJECT_ID(N'qaly.webhook_deliveries')) CREATE INDEX ix_webhook_deliveries_tenant_id ON qaly.webhook_deliveries(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_webhook_deliveries_created_at' AND object_id = OBJECT_ID(N'qaly.webhook_deliveries')) CREATE INDEX ix_webhook_deliveries_created_at ON qaly.webhook_deliveries(created_at DESC);
GO

IF OBJECT_ID(N'qaly.webhook_delivery_retries', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.webhook_delivery_retries (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_webhook_delivery_retries PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_webhook_delivery_retries_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_webhook_delivery_retries_tenant_id' AND object_id = OBJECT_ID(N'qaly.webhook_delivery_retries')) CREATE INDEX ix_webhook_delivery_retries_tenant_id ON qaly.webhook_delivery_retries(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_webhook_delivery_retries_created_at' AND object_id = OBJECT_ID(N'qaly.webhook_delivery_retries')) CREATE INDEX ix_webhook_delivery_retries_created_at ON qaly.webhook_delivery_retries(created_at DESC);
GO

IF OBJECT_ID(N'qaly.webhook_secrets', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.webhook_secrets (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_webhook_secrets PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_webhook_secrets_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_webhook_secrets_tenant_id' AND object_id = OBJECT_ID(N'qaly.webhook_secrets')) CREATE INDEX ix_webhook_secrets_tenant_id ON qaly.webhook_secrets(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_webhook_secrets_active_created_at' AND object_id = OBJECT_ID(N'qaly.webhook_secrets')) CREATE INDEX ix_webhook_secrets_active_created_at ON qaly.webhook_secrets(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.webhook_ip_whitelist', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.webhook_ip_whitelist (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_webhook_ip_whitelist PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_webhook_ip_whitelist_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_webhook_ip_whitelist_tenant_id' AND object_id = OBJECT_ID(N'qaly.webhook_ip_whitelist')) CREATE INDEX ix_webhook_ip_whitelist_tenant_id ON qaly.webhook_ip_whitelist(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_webhook_ip_whitelist_active_created_at' AND object_id = OBJECT_ID(N'qaly.webhook_ip_whitelist')) CREATE INDEX ix_webhook_ip_whitelist_active_created_at ON qaly.webhook_ip_whitelist(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.integrations', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.integrations (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_integrations PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_integrations_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_integrations_tenant_id' AND object_id = OBJECT_ID(N'qaly.integrations')) CREATE INDEX ix_integrations_tenant_id ON qaly.integrations(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_integrations_active_created_at' AND object_id = OBJECT_ID(N'qaly.integrations')) CREATE INDEX ix_integrations_active_created_at ON qaly.integrations(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.integration_configs', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.integration_configs (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_integration_configs PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_integration_configs_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_integration_configs_tenant_id' AND object_id = OBJECT_ID(N'qaly.integration_configs')) CREATE INDEX ix_integration_configs_tenant_id ON qaly.integration_configs(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_integration_configs_active_created_at' AND object_id = OBJECT_ID(N'qaly.integration_configs')) CREATE INDEX ix_integration_configs_active_created_at ON qaly.integration_configs(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.integration_sync_logs', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.integration_sync_logs (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_integration_sync_logs PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_integration_sync_logs_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_integration_sync_logs_tenant_id' AND object_id = OBJECT_ID(N'qaly.integration_sync_logs')) CREATE INDEX ix_integration_sync_logs_tenant_id ON qaly.integration_sync_logs(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_integration_sync_logs_created_at' AND object_id = OBJECT_ID(N'qaly.integration_sync_logs')) CREATE INDEX ix_integration_sync_logs_created_at ON qaly.integration_sync_logs(created_at DESC);
GO

IF OBJECT_ID(N'qaly.integration_field_mappings', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.integration_field_mappings (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_integration_field_mappings PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_integration_field_mappings_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_integration_field_mappings_tenant_id' AND object_id = OBJECT_ID(N'qaly.integration_field_mappings')) CREATE INDEX ix_integration_field_mappings_tenant_id ON qaly.integration_field_mappings(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_integration_field_mappings_active_created_at' AND object_id = OBJECT_ID(N'qaly.integration_field_mappings')) CREATE INDEX ix_integration_field_mappings_active_created_at ON qaly.integration_field_mappings(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.ai_knowledge_documents', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.ai_knowledge_documents (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_ai_knowledge_documents PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_ai_knowledge_documents_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_ai_knowledge_documents_tenant_id' AND object_id = OBJECT_ID(N'qaly.ai_knowledge_documents')) CREATE INDEX ix_ai_knowledge_documents_tenant_id ON qaly.ai_knowledge_documents(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_ai_knowledge_documents_active_created_at' AND object_id = OBJECT_ID(N'qaly.ai_knowledge_documents')) CREATE INDEX ix_ai_knowledge_documents_active_created_at ON qaly.ai_knowledge_documents(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.ai_knowledge_chunks', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.ai_knowledge_chunks (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_ai_knowledge_chunks PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_ai_knowledge_chunks_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_ai_knowledge_chunks_tenant_id' AND object_id = OBJECT_ID(N'qaly.ai_knowledge_chunks')) CREATE INDEX ix_ai_knowledge_chunks_tenant_id ON qaly.ai_knowledge_chunks(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_ai_knowledge_chunks_active_created_at' AND object_id = OBJECT_ID(N'qaly.ai_knowledge_chunks')) CREATE INDEX ix_ai_knowledge_chunks_active_created_at ON qaly.ai_knowledge_chunks(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.ai_knowledge_embeddings', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.ai_knowledge_embeddings (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_ai_knowledge_embeddings PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_ai_knowledge_embeddings_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_ai_knowledge_embeddings_tenant_id' AND object_id = OBJECT_ID(N'qaly.ai_knowledge_embeddings')) CREATE INDEX ix_ai_knowledge_embeddings_tenant_id ON qaly.ai_knowledge_embeddings(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_ai_knowledge_embeddings_active_created_at' AND object_id = OBJECT_ID(N'qaly.ai_knowledge_embeddings')) CREATE INDEX ix_ai_knowledge_embeddings_active_created_at ON qaly.ai_knowledge_embeddings(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.ai_knowledge_sync_jobs', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.ai_knowledge_sync_jobs (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_ai_knowledge_sync_jobs PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_ai_knowledge_sync_jobs_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_ai_knowledge_sync_jobs_tenant_id' AND object_id = OBJECT_ID(N'qaly.ai_knowledge_sync_jobs')) CREATE INDEX ix_ai_knowledge_sync_jobs_tenant_id ON qaly.ai_knowledge_sync_jobs(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_ai_knowledge_sync_jobs_active_created_at' AND object_id = OBJECT_ID(N'qaly.ai_knowledge_sync_jobs')) CREATE INDEX ix_ai_knowledge_sync_jobs_active_created_at ON qaly.ai_knowledge_sync_jobs(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.ai_knowledge_permissions', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.ai_knowledge_permissions (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_ai_knowledge_permissions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_ai_knowledge_permissions_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_ai_knowledge_permissions_tenant_id' AND object_id = OBJECT_ID(N'qaly.ai_knowledge_permissions')) CREATE INDEX ix_ai_knowledge_permissions_tenant_id ON qaly.ai_knowledge_permissions(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_ai_knowledge_permissions_active_created_at' AND object_id = OBJECT_ID(N'qaly.ai_knowledge_permissions')) CREATE INDEX ix_ai_knowledge_permissions_active_created_at ON qaly.ai_knowledge_permissions(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.document_import_jobs', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.document_import_jobs (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_document_import_jobs PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_document_import_jobs_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_document_import_jobs_tenant_id' AND object_id = OBJECT_ID(N'qaly.document_import_jobs')) CREATE INDEX ix_document_import_jobs_tenant_id ON qaly.document_import_jobs(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_document_import_jobs_active_created_at' AND object_id = OBJECT_ID(N'qaly.document_import_jobs')) CREATE INDEX ix_document_import_jobs_active_created_at ON qaly.document_import_jobs(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.document_import_sources', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.document_import_sources (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_document_import_sources PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_document_import_sources_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_document_import_sources_tenant_id' AND object_id = OBJECT_ID(N'qaly.document_import_sources')) CREATE INDEX ix_document_import_sources_tenant_id ON qaly.document_import_sources(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_document_import_sources_active_created_at' AND object_id = OBJECT_ID(N'qaly.document_import_sources')) CREATE INDEX ix_document_import_sources_active_created_at ON qaly.document_import_sources(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.document_draft_tasks', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.document_draft_tasks (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_document_draft_tasks PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_document_draft_tasks_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_document_draft_tasks_tenant_id' AND object_id = OBJECT_ID(N'qaly.document_draft_tasks')) CREATE INDEX ix_document_draft_tasks_tenant_id ON qaly.document_draft_tasks(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_document_draft_tasks_active_created_at' AND object_id = OBJECT_ID(N'qaly.document_draft_tasks')) CREATE INDEX ix_document_draft_tasks_active_created_at ON qaly.document_draft_tasks(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.document_draft_task_reviews', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.document_draft_task_reviews (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_document_draft_task_reviews PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_document_draft_task_reviews_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_document_draft_task_reviews_tenant_id' AND object_id = OBJECT_ID(N'qaly.document_draft_task_reviews')) CREATE INDEX ix_document_draft_task_reviews_tenant_id ON qaly.document_draft_task_reviews(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_document_draft_task_reviews_active_created_at' AND object_id = OBJECT_ID(N'qaly.document_draft_task_reviews')) CREATE INDEX ix_document_draft_task_reviews_active_created_at ON qaly.document_draft_task_reviews(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.document_import_audit_logs', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.document_import_audit_logs (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_document_import_audit_logs PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_document_import_audit_logs_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_document_import_audit_logs_tenant_id' AND object_id = OBJECT_ID(N'qaly.document_import_audit_logs')) CREATE INDEX ix_document_import_audit_logs_tenant_id ON qaly.document_import_audit_logs(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_document_import_audit_logs_created_at' AND object_id = OBJECT_ID(N'qaly.document_import_audit_logs')) CREATE INDEX ix_document_import_audit_logs_created_at ON qaly.document_import_audit_logs(created_at DESC);
GO

IF OBJECT_ID(N'qaly.document_draft_wiki_pages', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.document_draft_wiki_pages (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_document_draft_wiki_pages PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_document_draft_wiki_pages_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_document_draft_wiki_pages_tenant_id' AND object_id = OBJECT_ID(N'qaly.document_draft_wiki_pages')) CREATE INDEX ix_document_draft_wiki_pages_tenant_id ON qaly.document_draft_wiki_pages(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_document_draft_wiki_pages_active_created_at' AND object_id = OBJECT_ID(N'qaly.document_draft_wiki_pages')) CREATE INDEX ix_document_draft_wiki_pages_active_created_at ON qaly.document_draft_wiki_pages(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.audit_logs', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.audit_logs (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_audit_logs PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_audit_logs_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_audit_logs_tenant_id' AND object_id = OBJECT_ID(N'qaly.audit_logs')) CREATE INDEX ix_audit_logs_tenant_id ON qaly.audit_logs(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_audit_logs_created_at' AND object_id = OBJECT_ID(N'qaly.audit_logs')) CREATE INDEX ix_audit_logs_created_at ON qaly.audit_logs(created_at DESC);
GO

IF OBJECT_ID(N'qaly.compliance_reports', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.compliance_reports (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_compliance_reports PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_compliance_reports_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_compliance_reports_tenant_id' AND object_id = OBJECT_ID(N'qaly.compliance_reports')) CREATE INDEX ix_compliance_reports_tenant_id ON qaly.compliance_reports(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_compliance_reports_active_created_at' AND object_id = OBJECT_ID(N'qaly.compliance_reports')) CREATE INDEX ix_compliance_reports_active_created_at ON qaly.compliance_reports(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.project_analytics_snapshots', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.project_analytics_snapshots (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_project_analytics_snapshots PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_project_analytics_snapshots_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_analytics_snapshots_tenant_id' AND object_id = OBJECT_ID(N'qaly.project_analytics_snapshots')) CREATE INDEX ix_project_analytics_snapshots_tenant_id ON qaly.project_analytics_snapshots(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_analytics_snapshots_created_at' AND object_id = OBJECT_ID(N'qaly.project_analytics_snapshots')) CREATE INDEX ix_project_analytics_snapshots_created_at ON qaly.project_analytics_snapshots(created_at DESC);
GO

IF OBJECT_ID(N'qaly.task_analytics_snapshots', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_analytics_snapshots (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_analytics_snapshots PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_analytics_snapshots_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_analytics_snapshots_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_analytics_snapshots')) CREATE INDEX ix_task_analytics_snapshots_tenant_id ON qaly.task_analytics_snapshots(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_analytics_snapshots_created_at' AND object_id = OBJECT_ID(N'qaly.task_analytics_snapshots')) CREATE INDEX ix_task_analytics_snapshots_created_at ON qaly.task_analytics_snapshots(created_at DESC);
GO

IF OBJECT_ID(N'qaly.sprint_analytics', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.sprint_analytics (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_sprint_analytics PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_sprint_analytics_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_sprint_analytics_tenant_id' AND object_id = OBJECT_ID(N'qaly.sprint_analytics')) CREATE INDEX ix_sprint_analytics_tenant_id ON qaly.sprint_analytics(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_sprint_analytics_active_created_at' AND object_id = OBJECT_ID(N'qaly.sprint_analytics')) CREATE INDEX ix_sprint_analytics_active_created_at ON qaly.sprint_analytics(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.release_checklists', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.release_checklists (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_release_checklists PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_release_checklists_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_release_checklists_tenant_id' AND object_id = OBJECT_ID(N'qaly.release_checklists')) CREATE INDEX ix_release_checklists_tenant_id ON qaly.release_checklists(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_release_checklists_active_created_at' AND object_id = OBJECT_ID(N'qaly.release_checklists')) CREATE INDEX ix_release_checklists_active_created_at ON qaly.release_checklists(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.system_settings', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.system_settings (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_system_settings PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_system_settings_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_system_settings_active_created_at' AND object_id = OBJECT_ID(N'qaly.system_settings')) CREATE INDEX ix_system_settings_active_created_at ON qaly.system_settings(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.system_announcements', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.system_announcements (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_system_announcements PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_system_announcements_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_system_announcements_active_created_at' AND object_id = OBJECT_ID(N'qaly.system_announcements')) CREATE INDEX ix_system_announcements_active_created_at ON qaly.system_announcements(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.maintenance_windows', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.maintenance_windows (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_maintenance_windows PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_maintenance_windows_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_maintenance_windows_active_created_at' AND object_id = OBJECT_ID(N'qaly.maintenance_windows')) CREATE INDEX ix_maintenance_windows_active_created_at ON qaly.maintenance_windows(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.rate_limit_configs', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.rate_limit_configs (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_rate_limit_configs PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_rate_limit_configs_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_rate_limit_configs_active_created_at' AND object_id = OBJECT_ID(N'qaly.rate_limit_configs')) CREATE INDEX ix_rate_limit_configs_active_created_at ON qaly.rate_limit_configs(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.email_templates', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.email_templates (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_email_templates PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_email_templates_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_email_templates_active_created_at' AND object_id = OBJECT_ID(N'qaly.email_templates')) CREATE INDEX ix_email_templates_active_created_at ON qaly.email_templates(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.email_template_versions', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.email_template_versions (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_email_template_versions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_email_template_versions_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_email_template_versions_tenant_id' AND object_id = OBJECT_ID(N'qaly.email_template_versions')) CREATE INDEX ix_email_template_versions_tenant_id ON qaly.email_template_versions(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_email_template_versions_active_created_at' AND object_id = OBJECT_ID(N'qaly.email_template_versions')) CREATE INDEX ix_email_template_versions_active_created_at ON qaly.email_template_versions(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.system_health_logs', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.system_health_logs (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_system_health_logs PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_system_health_logs_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_system_health_logs_created_at' AND object_id = OBJECT_ID(N'qaly.system_health_logs')) CREATE INDEX ix_system_health_logs_created_at ON qaly.system_health_logs(created_at DESC);
GO

IF OBJECT_ID(N'qaly.tenants', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.tenants (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_tenants PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_tenants_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tenants_tenant_id' AND object_id = OBJECT_ID(N'qaly.tenants')) CREATE INDEX ix_tenants_tenant_id ON qaly.tenants(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tenants_active_created_at' AND object_id = OBJECT_ID(N'qaly.tenants')) CREATE INDEX ix_tenants_active_created_at ON qaly.tenants(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.tenant_settings', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.tenant_settings (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_tenant_settings PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_tenant_settings_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tenant_settings_tenant_id' AND object_id = OBJECT_ID(N'qaly.tenant_settings')) CREATE INDEX ix_tenant_settings_tenant_id ON qaly.tenant_settings(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tenant_settings_active_created_at' AND object_id = OBJECT_ID(N'qaly.tenant_settings')) CREATE INDEX ix_tenant_settings_active_created_at ON qaly.tenant_settings(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.tenant_subscriptions', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.tenant_subscriptions (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_tenant_subscriptions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_tenant_subscriptions_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tenant_subscriptions_tenant_id' AND object_id = OBJECT_ID(N'qaly.tenant_subscriptions')) CREATE INDEX ix_tenant_subscriptions_tenant_id ON qaly.tenant_subscriptions(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tenant_subscriptions_active_created_at' AND object_id = OBJECT_ID(N'qaly.tenant_subscriptions')) CREATE INDEX ix_tenant_subscriptions_active_created_at ON qaly.tenant_subscriptions(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.tenant_subscription_plans', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.tenant_subscription_plans (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_tenant_subscription_plans PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_tenant_subscription_plans_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tenant_subscription_plans_tenant_id' AND object_id = OBJECT_ID(N'qaly.tenant_subscription_plans')) CREATE INDEX ix_tenant_subscription_plans_tenant_id ON qaly.tenant_subscription_plans(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tenant_subscription_plans_active_created_at' AND object_id = OBJECT_ID(N'qaly.tenant_subscription_plans')) CREATE INDEX ix_tenant_subscription_plans_active_created_at ON qaly.tenant_subscription_plans(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.tenant_billing_history', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.tenant_billing_history (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_tenant_billing_history PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_tenant_billing_history_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tenant_billing_history_tenant_id' AND object_id = OBJECT_ID(N'qaly.tenant_billing_history')) CREATE INDEX ix_tenant_billing_history_tenant_id ON qaly.tenant_billing_history(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tenant_billing_history_created_at' AND object_id = OBJECT_ID(N'qaly.tenant_billing_history')) CREATE INDEX ix_tenant_billing_history_created_at ON qaly.tenant_billing_history(created_at DESC);
GO

IF OBJECT_ID(N'qaly.tenant_feature_overrides', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.tenant_feature_overrides (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_tenant_feature_overrides PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_tenant_feature_overrides_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tenant_feature_overrides_tenant_id' AND object_id = OBJECT_ID(N'qaly.tenant_feature_overrides')) CREATE INDEX ix_tenant_feature_overrides_tenant_id ON qaly.tenant_feature_overrides(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tenant_feature_overrides_active_created_at' AND object_id = OBJECT_ID(N'qaly.tenant_feature_overrides')) CREATE INDEX ix_tenant_feature_overrides_active_created_at ON qaly.tenant_feature_overrides(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.tenant_storage_quotas', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.tenant_storage_quotas (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_tenant_storage_quotas PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_tenant_storage_quotas_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tenant_storage_quotas_tenant_id' AND object_id = OBJECT_ID(N'qaly.tenant_storage_quotas')) CREATE INDEX ix_tenant_storage_quotas_tenant_id ON qaly.tenant_storage_quotas(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tenant_storage_quotas_active_created_at' AND object_id = OBJECT_ID(N'qaly.tenant_storage_quotas')) CREATE INDEX ix_tenant_storage_quotas_active_created_at ON qaly.tenant_storage_quotas(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.tenant_security_policies', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.tenant_security_policies (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_tenant_security_policies PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_tenant_security_policies_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tenant_security_policies_tenant_id' AND object_id = OBJECT_ID(N'qaly.tenant_security_policies')) CREATE INDEX ix_tenant_security_policies_tenant_id ON qaly.tenant_security_policies(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tenant_security_policies_active_created_at' AND object_id = OBJECT_ID(N'qaly.tenant_security_policies')) CREATE INDEX ix_tenant_security_policies_active_created_at ON qaly.tenant_security_policies(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.user_preferences', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.user_preferences (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_user_preferences PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_user_preferences_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_preferences_tenant_id' AND object_id = OBJECT_ID(N'qaly.user_preferences')) CREATE INDEX ix_user_preferences_tenant_id ON qaly.user_preferences(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_preferences_active_created_at' AND object_id = OBJECT_ID(N'qaly.user_preferences')) CREATE INDEX ix_user_preferences_active_created_at ON qaly.user_preferences(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.user_sessions', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.user_sessions (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_user_sessions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_user_sessions_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_sessions_tenant_id' AND object_id = OBJECT_ID(N'qaly.user_sessions')) CREATE INDEX ix_user_sessions_tenant_id ON qaly.user_sessions(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_sessions_active_created_at' AND object_id = OBJECT_ID(N'qaly.user_sessions')) CREATE INDEX ix_user_sessions_active_created_at ON qaly.user_sessions(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.user_refresh_tokens', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.user_refresh_tokens (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_user_refresh_tokens PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_user_refresh_tokens_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_refresh_tokens_tenant_id' AND object_id = OBJECT_ID(N'qaly.user_refresh_tokens')) CREATE INDEX ix_user_refresh_tokens_tenant_id ON qaly.user_refresh_tokens(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_refresh_tokens_active_created_at' AND object_id = OBJECT_ID(N'qaly.user_refresh_tokens')) CREATE INDEX ix_user_refresh_tokens_active_created_at ON qaly.user_refresh_tokens(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.user_password_history', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.user_password_history (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_user_password_history PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_user_password_history_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_password_history_tenant_id' AND object_id = OBJECT_ID(N'qaly.user_password_history')) CREATE INDEX ix_user_password_history_tenant_id ON qaly.user_password_history(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_password_history_created_at' AND object_id = OBJECT_ID(N'qaly.user_password_history')) CREATE INDEX ix_user_password_history_created_at ON qaly.user_password_history(created_at DESC);
GO

IF OBJECT_ID(N'qaly.user_mfa_configs', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.user_mfa_configs (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_user_mfa_configs PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_user_mfa_configs_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_mfa_configs_tenant_id' AND object_id = OBJECT_ID(N'qaly.user_mfa_configs')) CREATE INDEX ix_user_mfa_configs_tenant_id ON qaly.user_mfa_configs(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_mfa_configs_active_created_at' AND object_id = OBJECT_ID(N'qaly.user_mfa_configs')) CREATE INDEX ix_user_mfa_configs_active_created_at ON qaly.user_mfa_configs(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.user_mfa_backup_codes', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.user_mfa_backup_codes (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_user_mfa_backup_codes PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_user_mfa_backup_codes_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_mfa_backup_codes_tenant_id' AND object_id = OBJECT_ID(N'qaly.user_mfa_backup_codes')) CREATE INDEX ix_user_mfa_backup_codes_tenant_id ON qaly.user_mfa_backup_codes(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_mfa_backup_codes_active_created_at' AND object_id = OBJECT_ID(N'qaly.user_mfa_backup_codes')) CREATE INDEX ix_user_mfa_backup_codes_active_created_at ON qaly.user_mfa_backup_codes(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.user_login_history', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.user_login_history (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_user_login_history PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_user_login_history_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_login_history_tenant_id' AND object_id = OBJECT_ID(N'qaly.user_login_history')) CREATE INDEX ix_user_login_history_tenant_id ON qaly.user_login_history(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_login_history_created_at' AND object_id = OBJECT_ID(N'qaly.user_login_history')) CREATE INDEX ix_user_login_history_created_at ON qaly.user_login_history(created_at DESC);
GO

IF OBJECT_ID(N'qaly.user_email_verifications', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.user_email_verifications (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_user_email_verifications PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_user_email_verifications_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_email_verifications_tenant_id' AND object_id = OBJECT_ID(N'qaly.user_email_verifications')) CREATE INDEX ix_user_email_verifications_tenant_id ON qaly.user_email_verifications(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_email_verifications_active_created_at' AND object_id = OBJECT_ID(N'qaly.user_email_verifications')) CREATE INDEX ix_user_email_verifications_active_created_at ON qaly.user_email_verifications(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.user_password_resets', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.user_password_resets (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_user_password_resets PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_user_password_resets_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_password_resets_tenant_id' AND object_id = OBJECT_ID(N'qaly.user_password_resets')) CREATE INDEX ix_user_password_resets_tenant_id ON qaly.user_password_resets(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_password_resets_active_created_at' AND object_id = OBJECT_ID(N'qaly.user_password_resets')) CREATE INDEX ix_user_password_resets_active_created_at ON qaly.user_password_resets(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.oauth_providers', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.oauth_providers (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_oauth_providers PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_oauth_providers_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_oauth_providers_tenant_id' AND object_id = OBJECT_ID(N'qaly.oauth_providers')) CREATE INDEX ix_oauth_providers_tenant_id ON qaly.oauth_providers(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_oauth_providers_active_created_at' AND object_id = OBJECT_ID(N'qaly.oauth_providers')) CREATE INDEX ix_oauth_providers_active_created_at ON qaly.oauth_providers(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.oauth_connections', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.oauth_connections (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_oauth_connections PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_oauth_connections_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_oauth_connections_tenant_id' AND object_id = OBJECT_ID(N'qaly.oauth_connections')) CREATE INDEX ix_oauth_connections_tenant_id ON qaly.oauth_connections(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_oauth_connections_active_created_at' AND object_id = OBJECT_ID(N'qaly.oauth_connections')) CREATE INDEX ix_oauth_connections_active_created_at ON qaly.oauth_connections(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.api_keys', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.api_keys (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_api_keys PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_api_keys_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_api_keys_tenant_id' AND object_id = OBJECT_ID(N'qaly.api_keys')) CREATE INDEX ix_api_keys_tenant_id ON qaly.api_keys(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_api_keys_active_created_at' AND object_id = OBJECT_ID(N'qaly.api_keys')) CREATE INDEX ix_api_keys_active_created_at ON qaly.api_keys(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.api_key_permissions', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.api_key_permissions (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_api_key_permissions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_api_key_permissions_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_api_key_permissions_tenant_id' AND object_id = OBJECT_ID(N'qaly.api_key_permissions')) CREATE INDEX ix_api_key_permissions_tenant_id ON qaly.api_key_permissions(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_api_key_permissions_active_created_at' AND object_id = OBJECT_ID(N'qaly.api_key_permissions')) CREATE INDEX ix_api_key_permissions_active_created_at ON qaly.api_key_permissions(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.user_devices', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.user_devices (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_user_devices PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_user_devices_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_devices_tenant_id' AND object_id = OBJECT_ID(N'qaly.user_devices')) CREATE INDEX ix_user_devices_tenant_id ON qaly.user_devices(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_devices_active_created_at' AND object_id = OBJECT_ID(N'qaly.user_devices')) CREATE INDEX ix_user_devices_active_created_at ON qaly.user_devices(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.user_notification_tokens', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.user_notification_tokens (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_user_notification_tokens PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_user_notification_tokens_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_notification_tokens_tenant_id' AND object_id = OBJECT_ID(N'qaly.user_notification_tokens')) CREATE INDEX ix_user_notification_tokens_tenant_id ON qaly.user_notification_tokens(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_user_notification_tokens_active_created_at' AND object_id = OBJECT_ID(N'qaly.user_notification_tokens')) CREATE INDEX ix_user_notification_tokens_active_created_at ON qaly.user_notification_tokens(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.organization_settings', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.organization_settings (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_organization_settings PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_organization_settings_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_settings_tenant_id' AND object_id = OBJECT_ID(N'qaly.organization_settings')) CREATE INDEX ix_organization_settings_tenant_id ON qaly.organization_settings(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_settings_active_created_at' AND object_id = OBJECT_ID(N'qaly.organization_settings')) CREATE INDEX ix_organization_settings_active_created_at ON qaly.organization_settings(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.organization_member_invitations', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.organization_member_invitations (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_organization_member_invitations PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_organization_member_invitations_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_member_invitations_tenant_id' AND object_id = OBJECT_ID(N'qaly.organization_member_invitations')) CREATE INDEX ix_organization_member_invitations_tenant_id ON qaly.organization_member_invitations(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_member_invitations_active_created_at' AND object_id = OBJECT_ID(N'qaly.organization_member_invitations')) CREATE INDEX ix_organization_member_invitations_active_created_at ON qaly.organization_member_invitations(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.organization_roles', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.organization_roles (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_organization_roles PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_organization_roles_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_roles_tenant_id' AND object_id = OBJECT_ID(N'qaly.organization_roles')) CREATE INDEX ix_organization_roles_tenant_id ON qaly.organization_roles(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_roles_active_created_at' AND object_id = OBJECT_ID(N'qaly.organization_roles')) CREATE INDEX ix_organization_roles_active_created_at ON qaly.organization_roles(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.organization_role_permissions', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.organization_role_permissions (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_organization_role_permissions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_organization_role_permissions_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_role_permissions_tenant_id' AND object_id = OBJECT_ID(N'qaly.organization_role_permissions')) CREATE INDEX ix_organization_role_permissions_tenant_id ON qaly.organization_role_permissions(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_role_permissions_active_created_at' AND object_id = OBJECT_ID(N'qaly.organization_role_permissions')) CREATE INDEX ix_organization_role_permissions_active_created_at ON qaly.organization_role_permissions(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.organization_departments', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.organization_departments (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_organization_departments PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_organization_departments_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_departments_tenant_id' AND object_id = OBJECT_ID(N'qaly.organization_departments')) CREATE INDEX ix_organization_departments_tenant_id ON qaly.organization_departments(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_departments_active_created_at' AND object_id = OBJECT_ID(N'qaly.organization_departments')) CREATE INDEX ix_organization_departments_active_created_at ON qaly.organization_departments(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.organization_tags', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.organization_tags (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_organization_tags PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_organization_tags_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_tags_tenant_id' AND object_id = OBJECT_ID(N'qaly.organization_tags')) CREATE INDEX ix_organization_tags_tenant_id ON qaly.organization_tags(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_tags_active_created_at' AND object_id = OBJECT_ID(N'qaly.organization_tags')) CREATE INDEX ix_organization_tags_active_created_at ON qaly.organization_tags(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.organization_custom_field_values', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.organization_custom_field_values (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_organization_custom_field_values PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_organization_custom_field_values_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_custom_field_values_tenant_id' AND object_id = OBJECT_ID(N'qaly.organization_custom_field_values')) CREATE INDEX ix_organization_custom_field_values_tenant_id ON qaly.organization_custom_field_values(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_custom_field_values_active_created_at' AND object_id = OBJECT_ID(N'qaly.organization_custom_field_values')) CREATE INDEX ix_organization_custom_field_values_active_created_at ON qaly.organization_custom_field_values(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.organization_working_calendars', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.organization_working_calendars (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_organization_working_calendars PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_organization_working_calendars_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_working_calendars_tenant_id' AND object_id = OBJECT_ID(N'qaly.organization_working_calendars')) CREATE INDEX ix_organization_working_calendars_tenant_id ON qaly.organization_working_calendars(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_working_calendars_active_created_at' AND object_id = OBJECT_ID(N'qaly.organization_working_calendars')) CREATE INDEX ix_organization_working_calendars_active_created_at ON qaly.organization_working_calendars(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.organization_holidays', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.organization_holidays (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_organization_holidays PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_organization_holidays_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_holidays_tenant_id' AND object_id = OBJECT_ID(N'qaly.organization_holidays')) CREATE INDEX ix_organization_holidays_tenant_id ON qaly.organization_holidays(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_organization_holidays_active_created_at' AND object_id = OBJECT_ID(N'qaly.organization_holidays')) CREATE INDEX ix_organization_holidays_active_created_at ON qaly.organization_holidays(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.project_member_roles', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.project_member_roles (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_project_member_roles PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_project_member_roles_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_member_roles_tenant_id' AND object_id = OBJECT_ID(N'qaly.project_member_roles')) CREATE INDEX ix_project_member_roles_tenant_id ON qaly.project_member_roles(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_member_roles_active_created_at' AND object_id = OBJECT_ID(N'qaly.project_member_roles')) CREATE INDEX ix_project_member_roles_active_created_at ON qaly.project_member_roles(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.project_invitations', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.project_invitations (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_project_invitations PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_project_invitations_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_invitations_tenant_id' AND object_id = OBJECT_ID(N'qaly.project_invitations')) CREATE INDEX ix_project_invitations_tenant_id ON qaly.project_invitations(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_invitations_active_created_at' AND object_id = OBJECT_ID(N'qaly.project_invitations')) CREATE INDEX ix_project_invitations_active_created_at ON qaly.project_invitations(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.project_custom_field_values', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.project_custom_field_values (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_project_custom_field_values PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_project_custom_field_values_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_custom_field_values_tenant_id' AND object_id = OBJECT_ID(N'qaly.project_custom_field_values')) CREATE INDEX ix_project_custom_field_values_tenant_id ON qaly.project_custom_field_values(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_custom_field_values_active_created_at' AND object_id = OBJECT_ID(N'qaly.project_custom_field_values')) CREATE INDEX ix_project_custom_field_values_active_created_at ON qaly.project_custom_field_values(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.project_templates', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.project_templates (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_project_templates PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_project_templates_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_templates_tenant_id' AND object_id = OBJECT_ID(N'qaly.project_templates')) CREATE INDEX ix_project_templates_tenant_id ON qaly.project_templates(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_templates_active_created_at' AND object_id = OBJECT_ID(N'qaly.project_templates')) CREATE INDEX ix_project_templates_active_created_at ON qaly.project_templates(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.project_health_snapshots', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.project_health_snapshots (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_project_health_snapshots PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_project_health_snapshots_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_health_snapshots_tenant_id' AND object_id = OBJECT_ID(N'qaly.project_health_snapshots')) CREATE INDEX ix_project_health_snapshots_tenant_id ON qaly.project_health_snapshots(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_health_snapshots_created_at' AND object_id = OBJECT_ID(N'qaly.project_health_snapshots')) CREATE INDEX ix_project_health_snapshots_created_at ON qaly.project_health_snapshots(created_at DESC);
GO

IF OBJECT_ID(N'qaly.project_archived_reasons', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.project_archived_reasons (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_project_archived_reasons PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_project_archived_reasons_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_archived_reasons_tenant_id' AND object_id = OBJECT_ID(N'qaly.project_archived_reasons')) CREATE INDEX ix_project_archived_reasons_tenant_id ON qaly.project_archived_reasons(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_archived_reasons_active_created_at' AND object_id = OBJECT_ID(N'qaly.project_archived_reasons')) CREATE INDEX ix_project_archived_reasons_active_created_at ON qaly.project_archived_reasons(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.project_customer_access_policies', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.project_customer_access_policies (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_project_customer_access_policies PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_project_customer_access_policies_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_customer_access_policies_tenant_id' AND object_id = OBJECT_ID(N'qaly.project_customer_access_policies')) CREATE INDEX ix_project_customer_access_policies_tenant_id ON qaly.project_customer_access_policies(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_customer_access_policies_created_at' AND object_id = OBJECT_ID(N'qaly.project_customer_access_policies')) CREATE INDEX ix_project_customer_access_policies_created_at ON qaly.project_customer_access_policies(created_at DESC);
GO

IF OBJECT_ID(N'qaly.project_source_links', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.project_source_links (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_project_source_links PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_project_source_links_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_source_links_tenant_id' AND object_id = OBJECT_ID(N'qaly.project_source_links')) CREATE INDEX ix_project_source_links_tenant_id ON qaly.project_source_links(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_source_links_active_created_at' AND object_id = OBJECT_ID(N'qaly.project_source_links')) CREATE INDEX ix_project_source_links_active_created_at ON qaly.project_source_links(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.project_change_requests', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.project_change_requests (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_project_change_requests PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_project_change_requests_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_change_requests_tenant_id' AND object_id = OBJECT_ID(N'qaly.project_change_requests')) CREATE INDEX ix_project_change_requests_tenant_id ON qaly.project_change_requests(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_change_requests_active_created_at' AND object_id = OBJECT_ID(N'qaly.project_change_requests')) CREATE INDEX ix_project_change_requests_active_created_at ON qaly.project_change_requests(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.project_decision_logs', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.project_decision_logs (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_project_decision_logs PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_project_decision_logs_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_decision_logs_tenant_id' AND object_id = OBJECT_ID(N'qaly.project_decision_logs')) CREATE INDEX ix_project_decision_logs_tenant_id ON qaly.project_decision_logs(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_project_decision_logs_created_at' AND object_id = OBJECT_ID(N'qaly.project_decision_logs')) CREATE INDEX ix_project_decision_logs_created_at ON qaly.project_decision_logs(created_at DESC);
GO

IF OBJECT_ID(N'qaly.task_watchers', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_watchers (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_watchers PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_watchers_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_watchers_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_watchers')) CREATE INDEX ix_task_watchers_tenant_id ON qaly.task_watchers(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_watchers_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_watchers')) CREATE INDEX ix_task_watchers_active_created_at ON qaly.task_watchers(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_custom_field_values', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_custom_field_values (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_custom_field_values PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_custom_field_values_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_custom_field_values_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_custom_field_values')) CREATE INDEX ix_task_custom_field_values_tenant_id ON qaly.task_custom_field_values(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_custom_field_values_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_custom_field_values')) CREATE INDEX ix_task_custom_field_values_active_created_at ON qaly.task_custom_field_values(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_estimated_times', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_estimated_times (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_estimated_times PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_estimated_times_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_estimated_times_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_estimated_times')) CREATE INDEX ix_task_estimated_times_tenant_id ON qaly.task_estimated_times(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_estimated_times_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_estimated_times')) CREATE INDEX ix_task_estimated_times_active_created_at ON qaly.task_estimated_times(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_actual_times', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_actual_times (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_actual_times PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_actual_times_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_actual_times_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_actual_times')) CREATE INDEX ix_task_actual_times_tenant_id ON qaly.task_actual_times(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_actual_times_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_actual_times')) CREATE INDEX ix_task_actual_times_active_created_at ON qaly.task_actual_times(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_activity_logs', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_activity_logs (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_activity_logs PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_activity_logs_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_activity_logs_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_activity_logs')) CREATE INDEX ix_task_activity_logs_tenant_id ON qaly.task_activity_logs(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_activity_logs_created_at' AND object_id = OBJECT_ID(N'qaly.task_activity_logs')) CREATE INDEX ix_task_activity_logs_created_at ON qaly.task_activity_logs(created_at DESC);
GO

IF OBJECT_ID(N'qaly.task_evidence_attachments', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_evidence_attachments (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_evidence_attachments PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_evidence_attachments_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_evidence_attachments_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_evidence_attachments')) CREATE INDEX ix_task_evidence_attachments_tenant_id ON qaly.task_evidence_attachments(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_evidence_attachments_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_evidence_attachments')) CREATE INDEX ix_task_evidence_attachments_active_created_at ON qaly.task_evidence_attachments(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_dependency_types', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_dependency_types (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_dependency_types PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_dependency_types_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_dependency_types_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_dependency_types')) CREATE INDEX ix_task_dependency_types_tenant_id ON qaly.task_dependency_types(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_dependency_types_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_dependency_types')) CREATE INDEX ix_task_dependency_types_active_created_at ON qaly.task_dependency_types(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.dependency_impact_analysis', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.dependency_impact_analysis (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_dependency_impact_analysis PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_dependency_impact_analysis_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_dependency_impact_analysis_tenant_id' AND object_id = OBJECT_ID(N'qaly.dependency_impact_analysis')) CREATE INDEX ix_dependency_impact_analysis_tenant_id ON qaly.dependency_impact_analysis(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_dependency_impact_analysis_active_created_at' AND object_id = OBJECT_ID(N'qaly.dependency_impact_analysis')) CREATE INDEX ix_dependency_impact_analysis_active_created_at ON qaly.dependency_impact_analysis(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_blockers', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_blockers (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_blockers PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_blockers_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_blockers_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_blockers')) CREATE INDEX ix_task_blockers_tenant_id ON qaly.task_blockers(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_blockers_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_blockers')) CREATE INDEX ix_task_blockers_active_created_at ON qaly.task_blockers(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.task_risk_flags', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.task_risk_flags (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_task_risk_flags PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_task_risk_flags_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_risk_flags_tenant_id' AND object_id = OBJECT_ID(N'qaly.task_risk_flags')) CREATE INDEX ix_task_risk_flags_tenant_id ON qaly.task_risk_flags(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_task_risk_flags_active_created_at' AND object_id = OBJECT_ID(N'qaly.task_risk_flags')) CREATE INDEX ix_task_risk_flags_active_created_at ON qaly.task_risk_flags(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.kanban_boards', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.kanban_boards (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_kanban_boards PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_kanban_boards_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_kanban_boards_tenant_id' AND object_id = OBJECT_ID(N'qaly.kanban_boards')) CREATE INDEX ix_kanban_boards_tenant_id ON qaly.kanban_boards(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_kanban_boards_active_created_at' AND object_id = OBJECT_ID(N'qaly.kanban_boards')) CREATE INDEX ix_kanban_boards_active_created_at ON qaly.kanban_boards(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.kanban_column_limits', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.kanban_column_limits (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_kanban_column_limits PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_kanban_column_limits_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_kanban_column_limits_tenant_id' AND object_id = OBJECT_ID(N'qaly.kanban_column_limits')) CREATE INDEX ix_kanban_column_limits_tenant_id ON qaly.kanban_column_limits(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_kanban_column_limits_active_created_at' AND object_id = OBJECT_ID(N'qaly.kanban_column_limits')) CREATE INDEX ix_kanban_column_limits_active_created_at ON qaly.kanban_column_limits(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.kanban_cards', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.kanban_cards (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_kanban_cards PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_kanban_cards_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_kanban_cards_tenant_id' AND object_id = OBJECT_ID(N'qaly.kanban_cards')) CREATE INDEX ix_kanban_cards_tenant_id ON qaly.kanban_cards(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_kanban_cards_active_created_at' AND object_id = OBJECT_ID(N'qaly.kanban_cards')) CREATE INDEX ix_kanban_cards_active_created_at ON qaly.kanban_cards(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.kanban_card_orders', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.kanban_card_orders (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_kanban_card_orders PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_kanban_card_orders_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_kanban_card_orders_tenant_id' AND object_id = OBJECT_ID(N'qaly.kanban_card_orders')) CREATE INDEX ix_kanban_card_orders_tenant_id ON qaly.kanban_card_orders(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_kanban_card_orders_active_created_at' AND object_id = OBJECT_ID(N'qaly.kanban_card_orders')) CREATE INDEX ix_kanban_card_orders_active_created_at ON qaly.kanban_card_orders(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.kanban_swimlanes', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.kanban_swimlanes (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_kanban_swimlanes PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_kanban_swimlanes_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_kanban_swimlanes_tenant_id' AND object_id = OBJECT_ID(N'qaly.kanban_swimlanes')) CREATE INDEX ix_kanban_swimlanes_tenant_id ON qaly.kanban_swimlanes(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_kanban_swimlanes_active_created_at' AND object_id = OBJECT_ID(N'qaly.kanban_swimlanes')) CREATE INDEX ix_kanban_swimlanes_active_created_at ON qaly.kanban_swimlanes(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.kanban_filters', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.kanban_filters (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_kanban_filters PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_kanban_filters_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_kanban_filters_tenant_id' AND object_id = OBJECT_ID(N'qaly.kanban_filters')) CREATE INDEX ix_kanban_filters_tenant_id ON qaly.kanban_filters(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_kanban_filters_active_created_at' AND object_id = OBJECT_ID(N'qaly.kanban_filters')) CREATE INDEX ix_kanban_filters_active_created_at ON qaly.kanban_filters(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.kanban_saved_filters', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.kanban_saved_filters (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_kanban_saved_filters PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_kanban_saved_filters_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_kanban_saved_filters_tenant_id' AND object_id = OBJECT_ID(N'qaly.kanban_saved_filters')) CREATE INDEX ix_kanban_saved_filters_tenant_id ON qaly.kanban_saved_filters(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_kanban_saved_filters_active_created_at' AND object_id = OBJECT_ID(N'qaly.kanban_saved_filters')) CREATE INDEX ix_kanban_saved_filters_active_created_at ON qaly.kanban_saved_filters(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.sprint_goals', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.sprint_goals (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_sprint_goals PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_sprint_goals_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_sprint_goals_tenant_id' AND object_id = OBJECT_ID(N'qaly.sprint_goals')) CREATE INDEX ix_sprint_goals_tenant_id ON qaly.sprint_goals(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_sprint_goals_active_created_at' AND object_id = OBJECT_ID(N'qaly.sprint_goals')) CREATE INDEX ix_sprint_goals_active_created_at ON qaly.sprint_goals(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.sprint_capacity_plans', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.sprint_capacity_plans (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_sprint_capacity_plans PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_sprint_capacity_plans_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_sprint_capacity_plans_tenant_id' AND object_id = OBJECT_ID(N'qaly.sprint_capacity_plans')) CREATE INDEX ix_sprint_capacity_plans_tenant_id ON qaly.sprint_capacity_plans(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_sprint_capacity_plans_active_created_at' AND object_id = OBJECT_ID(N'qaly.sprint_capacity_plans')) CREATE INDEX ix_sprint_capacity_plans_active_created_at ON qaly.sprint_capacity_plans(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.sprint_member_capacities', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.sprint_member_capacities (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_sprint_member_capacities PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_sprint_member_capacities_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_sprint_member_capacities_tenant_id' AND object_id = OBJECT_ID(N'qaly.sprint_member_capacities')) CREATE INDEX ix_sprint_member_capacities_tenant_id ON qaly.sprint_member_capacities(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_sprint_member_capacities_active_created_at' AND object_id = OBJECT_ID(N'qaly.sprint_member_capacities')) CREATE INDEX ix_sprint_member_capacities_active_created_at ON qaly.sprint_member_capacities(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.sprint_velocity_history', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.sprint_velocity_history (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_sprint_velocity_history PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_sprint_velocity_history_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_sprint_velocity_history_tenant_id' AND object_id = OBJECT_ID(N'qaly.sprint_velocity_history')) CREATE INDEX ix_sprint_velocity_history_tenant_id ON qaly.sprint_velocity_history(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_sprint_velocity_history_created_at' AND object_id = OBJECT_ID(N'qaly.sprint_velocity_history')) CREATE INDEX ix_sprint_velocity_history_created_at ON qaly.sprint_velocity_history(created_at DESC);
GO

IF OBJECT_ID(N'qaly.sprint_retrospectives', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.sprint_retrospectives (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_sprint_retrospectives PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_sprint_retrospectives_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_sprint_retrospectives_tenant_id' AND object_id = OBJECT_ID(N'qaly.sprint_retrospectives')) CREATE INDEX ix_sprint_retrospectives_tenant_id ON qaly.sprint_retrospectives(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_sprint_retrospectives_active_created_at' AND object_id = OBJECT_ID(N'qaly.sprint_retrospectives')) CREATE INDEX ix_sprint_retrospectives_active_created_at ON qaly.sprint_retrospectives(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.timeline_views', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.timeline_views (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_timeline_views PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_timeline_views_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_timeline_views_tenant_id' AND object_id = OBJECT_ID(N'qaly.timeline_views')) CREATE INDEX ix_timeline_views_tenant_id ON qaly.timeline_views(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_timeline_views_active_created_at' AND object_id = OBJECT_ID(N'qaly.timeline_views')) CREATE INDEX ix_timeline_views_active_created_at ON qaly.timeline_views(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.timeline_milestones', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.timeline_milestones (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_timeline_milestones PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_timeline_milestones_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_timeline_milestones_tenant_id' AND object_id = OBJECT_ID(N'qaly.timeline_milestones')) CREATE INDEX ix_timeline_milestones_tenant_id ON qaly.timeline_milestones(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_timeline_milestones_active_created_at' AND object_id = OBJECT_ID(N'qaly.timeline_milestones')) CREATE INDEX ix_timeline_milestones_active_created_at ON qaly.timeline_milestones(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.timeline_baseline_snapshots', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.timeline_baseline_snapshots (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_timeline_baseline_snapshots PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_timeline_baseline_snapshots_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_timeline_baseline_snapshots_tenant_id' AND object_id = OBJECT_ID(N'qaly.timeline_baseline_snapshots')) CREATE INDEX ix_timeline_baseline_snapshots_tenant_id ON qaly.timeline_baseline_snapshots(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_timeline_baseline_snapshots_created_at' AND object_id = OBJECT_ID(N'qaly.timeline_baseline_snapshots')) CREATE INDEX ix_timeline_baseline_snapshots_created_at ON qaly.timeline_baseline_snapshots(created_at DESC);
GO

IF OBJECT_ID(N'qaly.timeline_critical_paths', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.timeline_critical_paths (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_timeline_critical_paths PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_timeline_critical_paths_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_timeline_critical_paths_tenant_id' AND object_id = OBJECT_ID(N'qaly.timeline_critical_paths')) CREATE INDEX ix_timeline_critical_paths_tenant_id ON qaly.timeline_critical_paths(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_timeline_critical_paths_active_created_at' AND object_id = OBJECT_ID(N'qaly.timeline_critical_paths')) CREATE INDEX ix_timeline_critical_paths_active_created_at ON qaly.timeline_critical_paths(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.timeline_buffer_times', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.timeline_buffer_times (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_timeline_buffer_times PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_timeline_buffer_times_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_timeline_buffer_times_tenant_id' AND object_id = OBJECT_ID(N'qaly.timeline_buffer_times')) CREATE INDEX ix_timeline_buffer_times_tenant_id ON qaly.timeline_buffer_times(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_timeline_buffer_times_active_created_at' AND object_id = OBJECT_ID(N'qaly.timeline_buffer_times')) CREATE INDEX ix_timeline_buffer_times_active_created_at ON qaly.timeline_buffer_times(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.comment_reactions', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.comment_reactions (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_comment_reactions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_comment_reactions_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_comment_reactions_tenant_id' AND object_id = OBJECT_ID(N'qaly.comment_reactions')) CREATE INDEX ix_comment_reactions_tenant_id ON qaly.comment_reactions(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_comment_reactions_active_created_at' AND object_id = OBJECT_ID(N'qaly.comment_reactions')) CREATE INDEX ix_comment_reactions_active_created_at ON qaly.comment_reactions(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.comment_mentions', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.comment_mentions (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_comment_mentions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_comment_mentions_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_comment_mentions_tenant_id' AND object_id = OBJECT_ID(N'qaly.comment_mentions')) CREATE INDEX ix_comment_mentions_tenant_id ON qaly.comment_mentions(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_comment_mentions_active_created_at' AND object_id = OBJECT_ID(N'qaly.comment_mentions')) CREATE INDEX ix_comment_mentions_active_created_at ON qaly.comment_mentions(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.comment_attachments', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.comment_attachments (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_comment_attachments PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_comment_attachments_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_comment_attachments_tenant_id' AND object_id = OBJECT_ID(N'qaly.comment_attachments')) CREATE INDEX ix_comment_attachments_tenant_id ON qaly.comment_attachments(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_comment_attachments_active_created_at' AND object_id = OBJECT_ID(N'qaly.comment_attachments')) CREATE INDEX ix_comment_attachments_active_created_at ON qaly.comment_attachments(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.comment_edit_history', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.comment_edit_history (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_comment_edit_history PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_comment_edit_history_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_comment_edit_history_tenant_id' AND object_id = OBJECT_ID(N'qaly.comment_edit_history')) CREATE INDEX ix_comment_edit_history_tenant_id ON qaly.comment_edit_history(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_comment_edit_history_created_at' AND object_id = OBJECT_ID(N'qaly.comment_edit_history')) CREATE INDEX ix_comment_edit_history_created_at ON qaly.comment_edit_history(created_at DESC);
GO

IF OBJECT_ID(N'qaly.chat_room_members', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.chat_room_members (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_chat_room_members PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_chat_room_members_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_room_members_tenant_id' AND object_id = OBJECT_ID(N'qaly.chat_room_members')) CREATE INDEX ix_chat_room_members_tenant_id ON qaly.chat_room_members(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_room_members_active_created_at' AND object_id = OBJECT_ID(N'qaly.chat_room_members')) CREATE INDEX ix_chat_room_members_active_created_at ON qaly.chat_room_members(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.chat_room_settings', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.chat_room_settings (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_chat_room_settings PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_chat_room_settings_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_room_settings_tenant_id' AND object_id = OBJECT_ID(N'qaly.chat_room_settings')) CREATE INDEX ix_chat_room_settings_tenant_id ON qaly.chat_room_settings(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_room_settings_active_created_at' AND object_id = OBJECT_ID(N'qaly.chat_room_settings')) CREATE INDEX ix_chat_room_settings_active_created_at ON qaly.chat_room_settings(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.chat_room_pinned_messages', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.chat_room_pinned_messages (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_chat_room_pinned_messages PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_chat_room_pinned_messages_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_room_pinned_messages_tenant_id' AND object_id = OBJECT_ID(N'qaly.chat_room_pinned_messages')) CREATE INDEX ix_chat_room_pinned_messages_tenant_id ON qaly.chat_room_pinned_messages(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_room_pinned_messages_active_created_at' AND object_id = OBJECT_ID(N'qaly.chat_room_pinned_messages')) CREATE INDEX ix_chat_room_pinned_messages_active_created_at ON qaly.chat_room_pinned_messages(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.chat_message_reactions', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.chat_message_reactions (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_chat_message_reactions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_chat_message_reactions_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_message_reactions_tenant_id' AND object_id = OBJECT_ID(N'qaly.chat_message_reactions')) CREATE INDEX ix_chat_message_reactions_tenant_id ON qaly.chat_message_reactions(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_message_reactions_active_created_at' AND object_id = OBJECT_ID(N'qaly.chat_message_reactions')) CREATE INDEX ix_chat_message_reactions_active_created_at ON qaly.chat_message_reactions(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.chat_message_mentions', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.chat_message_mentions (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_chat_message_mentions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_chat_message_mentions_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_message_mentions_tenant_id' AND object_id = OBJECT_ID(N'qaly.chat_message_mentions')) CREATE INDEX ix_chat_message_mentions_tenant_id ON qaly.chat_message_mentions(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_message_mentions_active_created_at' AND object_id = OBJECT_ID(N'qaly.chat_message_mentions')) CREATE INDEX ix_chat_message_mentions_active_created_at ON qaly.chat_message_mentions(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.chat_message_attachments', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.chat_message_attachments (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_chat_message_attachments PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_chat_message_attachments_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_message_attachments_tenant_id' AND object_id = OBJECT_ID(N'qaly.chat_message_attachments')) CREATE INDEX ix_chat_message_attachments_tenant_id ON qaly.chat_message_attachments(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_message_attachments_active_created_at' AND object_id = OBJECT_ID(N'qaly.chat_message_attachments')) CREATE INDEX ix_chat_message_attachments_active_created_at ON qaly.chat_message_attachments(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.chat_message_task_links', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.chat_message_task_links (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_chat_message_task_links PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_chat_message_task_links_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_message_task_links_tenant_id' AND object_id = OBJECT_ID(N'qaly.chat_message_task_links')) CREATE INDEX ix_chat_message_task_links_tenant_id ON qaly.chat_message_task_links(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_message_task_links_active_created_at' AND object_id = OBJECT_ID(N'qaly.chat_message_task_links')) CREATE INDEX ix_chat_message_task_links_active_created_at ON qaly.chat_message_task_links(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.chat_message_edit_history', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.chat_message_edit_history (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_chat_message_edit_history PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_chat_message_edit_history_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_message_edit_history_tenant_id' AND object_id = OBJECT_ID(N'qaly.chat_message_edit_history')) CREATE INDEX ix_chat_message_edit_history_tenant_id ON qaly.chat_message_edit_history(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_message_edit_history_created_at' AND object_id = OBJECT_ID(N'qaly.chat_message_edit_history')) CREATE INDEX ix_chat_message_edit_history_created_at ON qaly.chat_message_edit_history(created_at DESC);
GO

IF OBJECT_ID(N'qaly.chat_message_read_receipts', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.chat_message_read_receipts (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_chat_message_read_receipts PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_chat_message_read_receipts_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_message_read_receipts_tenant_id' AND object_id = OBJECT_ID(N'qaly.chat_message_read_receipts')) CREATE INDEX ix_chat_message_read_receipts_tenant_id ON qaly.chat_message_read_receipts(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_message_read_receipts_active_created_at' AND object_id = OBJECT_ID(N'qaly.chat_message_read_receipts')) CREATE INDEX ix_chat_message_read_receipts_active_created_at ON qaly.chat_message_read_receipts(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.chat_direct_messages', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.chat_direct_messages (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_chat_direct_messages PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_chat_direct_messages_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_direct_messages_tenant_id' AND object_id = OBJECT_ID(N'qaly.chat_direct_messages')) CREATE INDEX ix_chat_direct_messages_tenant_id ON qaly.chat_direct_messages(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_direct_messages_active_created_at' AND object_id = OBJECT_ID(N'qaly.chat_direct_messages')) CREATE INDEX ix_chat_direct_messages_active_created_at ON qaly.chat_direct_messages(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.chat_dm_participants', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.chat_dm_participants (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_chat_dm_participants PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_chat_dm_participants_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_dm_participants_tenant_id' AND object_id = OBJECT_ID(N'qaly.chat_dm_participants')) CREATE INDEX ix_chat_dm_participants_tenant_id ON qaly.chat_dm_participants(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_chat_dm_participants_active_created_at' AND object_id = OBJECT_ID(N'qaly.chat_dm_participants')) CREATE INDEX ix_chat_dm_participants_active_created_at ON qaly.chat_dm_participants(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.realtime_presence_sessions', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.realtime_presence_sessions (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_realtime_presence_sessions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_realtime_presence_sessions_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_realtime_presence_sessions_tenant_id' AND object_id = OBJECT_ID(N'qaly.realtime_presence_sessions')) CREATE INDEX ix_realtime_presence_sessions_tenant_id ON qaly.realtime_presence_sessions(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_realtime_presence_sessions_active_created_at' AND object_id = OBJECT_ID(N'qaly.realtime_presence_sessions')) CREATE INDEX ix_realtime_presence_sessions_active_created_at ON qaly.realtime_presence_sessions(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.realtime_typing_indicators', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.realtime_typing_indicators (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_realtime_typing_indicators PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_realtime_typing_indicators_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_realtime_typing_indicators_tenant_id' AND object_id = OBJECT_ID(N'qaly.realtime_typing_indicators')) CREATE INDEX ix_realtime_typing_indicators_tenant_id ON qaly.realtime_typing_indicators(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_realtime_typing_indicators_active_created_at' AND object_id = OBJECT_ID(N'qaly.realtime_typing_indicators')) CREATE INDEX ix_realtime_typing_indicators_active_created_at ON qaly.realtime_typing_indicators(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.wiki_spaces', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.wiki_spaces (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_wiki_spaces PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        wiki_page_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_wiki_spaces_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_spaces_tenant_id' AND object_id = OBJECT_ID(N'qaly.wiki_spaces')) CREATE INDEX ix_wiki_spaces_tenant_id ON qaly.wiki_spaces(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_spaces_active_created_at' AND object_id = OBJECT_ID(N'qaly.wiki_spaces')) CREATE INDEX ix_wiki_spaces_active_created_at ON qaly.wiki_spaces(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.wiki_page_versions', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.wiki_page_versions (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_wiki_page_versions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_wiki_page_versions_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_page_versions_tenant_id' AND object_id = OBJECT_ID(N'qaly.wiki_page_versions')) CREATE INDEX ix_wiki_page_versions_tenant_id ON qaly.wiki_page_versions(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_page_versions_active_created_at' AND object_id = OBJECT_ID(N'qaly.wiki_page_versions')) CREATE INDEX ix_wiki_page_versions_active_created_at ON qaly.wiki_page_versions(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.wiki_page_contents', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.wiki_page_contents (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_wiki_page_contents PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_wiki_page_contents_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_page_contents_tenant_id' AND object_id = OBJECT_ID(N'qaly.wiki_page_contents')) CREATE INDEX ix_wiki_page_contents_tenant_id ON qaly.wiki_page_contents(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_page_contents_active_created_at' AND object_id = OBJECT_ID(N'qaly.wiki_page_contents')) CREATE INDEX ix_wiki_page_contents_active_created_at ON qaly.wiki_page_contents(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.wiki_page_permissions', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.wiki_page_permissions (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_wiki_page_permissions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_wiki_page_permissions_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_page_permissions_tenant_id' AND object_id = OBJECT_ID(N'qaly.wiki_page_permissions')) CREATE INDEX ix_wiki_page_permissions_tenant_id ON qaly.wiki_page_permissions(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_page_permissions_active_created_at' AND object_id = OBJECT_ID(N'qaly.wiki_page_permissions')) CREATE INDEX ix_wiki_page_permissions_active_created_at ON qaly.wiki_page_permissions(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.wiki_page_watchers', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.wiki_page_watchers (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_wiki_page_watchers PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_wiki_page_watchers_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_page_watchers_tenant_id' AND object_id = OBJECT_ID(N'qaly.wiki_page_watchers')) CREATE INDEX ix_wiki_page_watchers_tenant_id ON qaly.wiki_page_watchers(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_page_watchers_active_created_at' AND object_id = OBJECT_ID(N'qaly.wiki_page_watchers')) CREATE INDEX ix_wiki_page_watchers_active_created_at ON qaly.wiki_page_watchers(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.wiki_page_links', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.wiki_page_links (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_wiki_page_links PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_wiki_page_links_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_page_links_tenant_id' AND object_id = OBJECT_ID(N'qaly.wiki_page_links')) CREATE INDEX ix_wiki_page_links_tenant_id ON qaly.wiki_page_links(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_page_links_active_created_at' AND object_id = OBJECT_ID(N'qaly.wiki_page_links')) CREATE INDEX ix_wiki_page_links_active_created_at ON qaly.wiki_page_links(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.wiki_page_attachments', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.wiki_page_attachments (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_wiki_page_attachments PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_wiki_page_attachments_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_page_attachments_tenant_id' AND object_id = OBJECT_ID(N'qaly.wiki_page_attachments')) CREATE INDEX ix_wiki_page_attachments_tenant_id ON qaly.wiki_page_attachments(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_page_attachments_active_created_at' AND object_id = OBJECT_ID(N'qaly.wiki_page_attachments')) CREATE INDEX ix_wiki_page_attachments_active_created_at ON qaly.wiki_page_attachments(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.wiki_page_templates', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.wiki_page_templates (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_wiki_page_templates PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_wiki_page_templates_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_page_templates_tenant_id' AND object_id = OBJECT_ID(N'qaly.wiki_page_templates')) CREATE INDEX ix_wiki_page_templates_tenant_id ON qaly.wiki_page_templates(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_page_templates_active_created_at' AND object_id = OBJECT_ID(N'qaly.wiki_page_templates')) CREATE INDEX ix_wiki_page_templates_active_created_at ON qaly.wiki_page_templates(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.wiki_page_tags', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.wiki_page_tags (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_wiki_page_tags PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_wiki_page_tags_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_page_tags_tenant_id' AND object_id = OBJECT_ID(N'qaly.wiki_page_tags')) CREATE INDEX ix_wiki_page_tags_tenant_id ON qaly.wiki_page_tags(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_page_tags_active_created_at' AND object_id = OBJECT_ID(N'qaly.wiki_page_tags')) CREATE INDEX ix_wiki_page_tags_active_created_at ON qaly.wiki_page_tags(created_at DESC) WHERE deleted_at IS NULL;
GO

IF OBJECT_ID(N'qaly.wiki_page_export_history', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.wiki_page_export_history (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_wiki_page_export_history PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        archived_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_wiki_page_export_history_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_page_export_history_tenant_id' AND object_id = OBJECT_ID(N'qaly.wiki_page_export_history')) CREATE INDEX ix_wiki_page_export_history_tenant_id ON qaly.wiki_page_export_history(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_wiki_page_export_history_created_at' AND object_id = OBJECT_ID(N'qaly.wiki_page_export_history')) CREATE INDEX ix_wiki_page_export_history_created_at ON qaly.wiki_page_export_history(created_at DESC);
GO

IF OBJECT_ID(N'qaly.file_versions', N'U') IS NULL
BEGIN
    CREATE TABLE qaly.file_versions (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_file_versions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        tenant_id UNIQUEIDENTIFIER NULL,
        name NVARCHAR(200) NULL,
        code NVARCHAR(100) NULL,
        status NVARCHAR(50) NULL,
        metadata_json NVARCHAR(MAX) NULL,
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        deleted_at DATETIMEOFFSET(7) NULL,
        CONSTRAINT ck_file_versions_metadata_json_is_json CHECK (metadata_json IS NULL OR ISJSON(metadata_json) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_file_versions_tenant_id' AND object_id = OBJECT_ID(N'qaly.file_versions')) CREATE INDEX ix_file_versions_tenant_id ON qaly.file_versions(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_file_versions_active_created_at' AND object_id = OBJECT_ID(N'qaly.file_versions')) CREATE INDEX ix_file_versions_active_created_at ON qaly.file_versions(created_at DESC) WHERE deleted_at IS NULL;
GO
