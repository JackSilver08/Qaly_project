-- QALY v2.0 PostgreSQL logical schema skeleton
-- Generated for specification; review constraints/types before production migration.
CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS feature_flags (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_feature_flags_created_at ON feature_flags(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_feature_flags_status ON feature_flags(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS users (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  organization_id UUID,
  user_id UUID,
  email VARCHAR(320) NOT NULL,
  password_hash TEXT NOT NULL,
  system_role VARCHAR(30) NOT NULL DEFAULT 'guest',
  is_active BOOLEAN NOT NULL DEFAULT TRUE,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_users_tenant ON users(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_users_organization ON users(organization_id) — load dữ liệu theo organization.
-- INDEX RECOMMENDATION: idx_users_user ON users(user_id) — load dữ liệu theo user.

CREATE TABLE IF NOT EXISTS user_profiles (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  user_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_user_profiles_tenant ON user_profiles(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_user_profiles_user ON user_profiles(user_id) — load dữ liệu theo user.
-- INDEX RECOMMENDATION: idx_user_profiles_created_at ON user_profiles(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS organizations (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200) NOT NULL,
  code VARCHAR(80) NOT NULL,
  status VARCHAR(30) NOT NULL DEFAULT 'active',
  owner_id UUID NOT NULL,
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_organizations_tenant ON organizations(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_organizations_created_at ON organizations(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_organizations_status ON organizations(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS organization_members (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  organization_id UUID,
  user_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_organization_members_tenant ON organization_members(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_organization_members_organization ON organization_members(organization_id) — load dữ liệu theo organization.
-- INDEX RECOMMENDATION: idx_organization_members_user ON organization_members(user_id) — load dữ liệu theo user.

CREATE TABLE IF NOT EXISTS organization_custom_fields (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  organization_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_organization_custom_fields_tenant ON organization_custom_fields(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_organization_custom_fields_organization ON organization_custom_fields(organization_id) — load dữ liệu theo organization.
-- INDEX RECOMMENDATION: idx_organization_custom_fields_created_at ON organization_custom_fields(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS projects (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  organization_id UUID,
  name VARCHAR(200) NOT NULL,
  code VARCHAR(80) NOT NULL,
  description TEXT,
  status VARCHAR(30) NOT NULL DEFAULT 'active',
  owner_id UUID NOT NULL,
  start_date DATE,
  end_date DATE,
  visibility VARCHAR(30) NOT NULL DEFAULT 'internal',
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_projects_tenant ON projects(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_projects_organization ON projects(organization_id) — load dữ liệu theo organization.
-- INDEX RECOMMENDATION: idx_projects_created_at ON projects(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS project_settings (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_project_settings_tenant ON project_settings(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_project_settings_project ON project_settings(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_project_settings_created_at ON project_settings(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS project_members (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  user_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_project_members_tenant ON project_members(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_project_members_project ON project_members(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_project_members_user ON project_members(user_id) — load dữ liệu theo user.

CREATE TABLE IF NOT EXISTS project_labels (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_project_labels_tenant ON project_labels(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_project_labels_project ON project_labels(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_project_labels_created_at ON project_labels(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS project_custom_fields (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_project_custom_fields_tenant ON project_custom_fields(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_project_custom_fields_project ON project_custom_fields(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_project_custom_fields_created_at ON project_custom_fields(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS project_template_tasks (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  organization_id UUID,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_project_template_tasks_tenant ON project_template_tasks(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_project_template_tasks_organization ON project_template_tasks(organization_id) — load dữ liệu theo organization.
-- INDEX RECOMMENDATION: idx_project_template_tasks_project ON project_template_tasks(project_id) — load dữ liệu theo project.

CREATE TABLE IF NOT EXISTS project_status_reports (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_project_status_reports_tenant ON project_status_reports(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_project_status_reports_project ON project_status_reports(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_project_status_reports_created_at ON project_status_reports(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS tasks (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  title VARCHAR(300) NOT NULL,
  description_md TEXT,
  status VARCHAR(40) NOT NULL DEFAULT 'todo',
  priority VARCHAR(20) NOT NULL DEFAULT 'medium',
  visibility VARCHAR(30) NOT NULL DEFAULT 'internal',
  due_at TIMESTAMPTZ,
  start_at TIMESTAMPTZ,
  estimated_hours NUMERIC(8,2),
  actual_hours NUMERIC(8,2),
  weight NUMERIC(8,2) NOT NULL DEFAULT 1,
  contributes_to_progress BOOLEAN NOT NULL DEFAULT TRUE,
  parent_task_id UUID,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_tasks_tenant ON tasks(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_tasks_project ON tasks(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_tasks_created_at ON tasks(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS task_assignments (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_assignments_tenant ON task_assignments(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_assignments_project ON task_assignments(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_assignments_task ON task_assignments(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS task_labels (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_labels_tenant ON task_labels(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_labels_project ON task_labels(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_labels_task ON task_labels(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS task_checklists (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_checklists_tenant ON task_checklists(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_checklists_project ON task_checklists(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_checklists_task ON task_checklists(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS task_checklist_items (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_checklist_items_tenant ON task_checklist_items(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_checklist_items_project ON task_checklist_items(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_checklist_items_task ON task_checklist_items(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS task_time_logs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_time_logs_tenant ON task_time_logs(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_time_logs_project ON task_time_logs(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_time_logs_task ON task_time_logs(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS task_comments_count_cache (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_comments_count_cache_tenant ON task_comments_count_cache(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_comments_count_cache_project ON task_comments_count_cache(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_comments_count_cache_task ON task_comments_count_cache(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS task_subtasks (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_subtasks_tenant ON task_subtasks(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_subtasks_project ON task_subtasks(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_subtasks_task ON task_subtasks(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS workflow_definitions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_workflow_definitions_tenant ON workflow_definitions(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_workflow_definitions_created_at ON workflow_definitions(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_workflow_definitions_status ON workflow_definitions(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS workflow_states (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_workflow_states_tenant ON workflow_states(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_workflow_states_created_at ON workflow_states(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_workflow_states_status ON workflow_states(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS workflow_transitions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_workflow_transitions_tenant ON workflow_transitions(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_workflow_transitions_created_at ON workflow_transitions(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_workflow_transitions_status ON workflow_transitions(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS workflow_transition_conditions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_workflow_transition_conditions_tenant ON workflow_transition_conditions(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_workflow_transition_conditions_created_at ON workflow_transition_conditions(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_workflow_transition_conditions_status ON workflow_transition_conditions(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS workflow_transition_actions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_workflow_transition_actions_tenant ON workflow_transition_actions(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_workflow_transition_actions_created_at ON workflow_transition_actions(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_workflow_transition_actions_status ON workflow_transition_actions(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS task_workflow_states (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_workflow_states_tenant ON task_workflow_states(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_workflow_states_project ON task_workflow_states(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_workflow_states_task ON task_workflow_states(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS task_status_change_requests (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_status_change_requests_tenant ON task_status_change_requests(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_status_change_requests_project ON task_status_change_requests(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_status_change_requests_task ON task_status_change_requests(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS task_status_change_approvals (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_status_change_approvals_tenant ON task_status_change_approvals(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_status_change_approvals_project ON task_status_change_approvals(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_status_change_approvals_task ON task_status_change_approvals(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS task_review_cycles (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_review_cycles_tenant ON task_review_cycles(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_review_cycles_project ON task_review_cycles(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_review_cycles_task ON task_review_cycles(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS task_review_feedbacks (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_review_feedbacks_tenant ON task_review_feedbacks(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_review_feedbacks_project ON task_review_feedbacks(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_review_feedbacks_task ON task_review_feedbacks(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS task_evidences (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_evidences_tenant ON task_evidences(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_evidences_project ON task_evidences(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_evidences_task ON task_evidences(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS task_evidence_reviews (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_evidence_reviews_tenant ON task_evidence_reviews(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_evidence_reviews_project ON task_evidence_reviews(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_evidence_reviews_task ON task_evidence_reviews(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS task_evidence_review_comments (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_evidence_review_comments_tenant ON task_evidence_review_comments(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_evidence_review_comments_project ON task_evidence_review_comments(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_evidence_review_comments_task ON task_evidence_review_comments(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS evidence_approval_audit_logs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  archived_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_evidence_approval_audit_logs_tenant ON evidence_approval_audit_logs(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_evidence_approval_audit_logs_task ON evidence_approval_audit_logs(task_id) — load dữ liệu theo task.
-- INDEX RECOMMENDATION: idx_evidence_approval_audit_logs_created_at ON evidence_approval_audit_logs(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS task_dependencies (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_dependencies_tenant ON task_dependencies(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_dependencies_project ON task_dependencies(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_dependencies_task ON task_dependencies(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS kanban_columns (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_kanban_columns_tenant ON kanban_columns(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_kanban_columns_project ON kanban_columns(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_kanban_columns_created_at ON kanban_columns(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS sprints (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_sprints_tenant ON sprints(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_sprints_project ON sprints(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_sprints_created_at ON sprints(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS sprint_tasks (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_sprint_tasks_tenant ON sprint_tasks(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_sprint_tasks_project ON sprint_tasks(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_sprint_tasks_task ON sprint_tasks(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS sprint_review_notes (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_sprint_review_notes_tenant ON sprint_review_notes(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_sprint_review_notes_project ON sprint_review_notes(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_sprint_review_notes_task ON sprint_review_notes(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS comments (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_comments_tenant ON comments(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_comments_task ON comments(task_id) — load dữ liệu theo task.
-- INDEX RECOMMENDATION: idx_comments_created_at ON comments(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS chat_rooms (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_chat_rooms_tenant ON chat_rooms(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_chat_rooms_project ON chat_rooms(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_chat_rooms_created_at ON chat_rooms(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS chat_messages (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  room_id UUID NOT NULL,
  sender_id UUID NOT NULL,
  content_md TEXT,
  message_type VARCHAR(30) NOT NULL DEFAULT 'text',
  visibility VARCHAR(30) NOT NULL DEFAULT 'internal',
  pinned_at TIMESTAMPTZ,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_chat_messages_tenant ON chat_messages(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_chat_messages_project ON chat_messages(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_chat_messages_created_at ON chat_messages(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS meetings (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_meetings_tenant ON meetings(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_meetings_project ON meetings(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_meetings_created_at ON meetings(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS meeting_participants (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  user_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_meeting_participants_tenant ON meeting_participants(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_meeting_participants_project ON meeting_participants(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_meeting_participants_user ON meeting_participants(user_id) — load dữ liệu theo user.

CREATE TABLE IF NOT EXISTS meeting_agendas (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_meeting_agendas_tenant ON meeting_agendas(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_meeting_agendas_project ON meeting_agendas(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_meeting_agendas_created_at ON meeting_agendas(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS meeting_agenda_items (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_meeting_agenda_items_tenant ON meeting_agenda_items(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_meeting_agenda_items_project ON meeting_agenda_items(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_meeting_agenda_items_created_at ON meeting_agenda_items(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS meeting_notes (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_meeting_notes_tenant ON meeting_notes(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_meeting_notes_project ON meeting_notes(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_meeting_notes_created_at ON meeting_notes(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS meeting_note_sections (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_meeting_note_sections_tenant ON meeting_note_sections(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_meeting_note_sections_project ON meeting_note_sections(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_meeting_note_sections_created_at ON meeting_note_sections(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS meeting_action_items (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_meeting_action_items_tenant ON meeting_action_items(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_meeting_action_items_project ON meeting_action_items(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_meeting_action_items_created_at ON meeting_action_items(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS meeting_recordings (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_meeting_recordings_tenant ON meeting_recordings(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_meeting_recordings_project ON meeting_recordings(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_meeting_recordings_created_at ON meeting_recordings(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS meeting_ai_summaries (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_meeting_ai_summaries_tenant ON meeting_ai_summaries(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_meeting_ai_summaries_project ON meeting_ai_summaries(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_meeting_ai_summaries_created_at ON meeting_ai_summaries(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS meeting_transcripts (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_meeting_transcripts_tenant ON meeting_transcripts(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_meeting_transcripts_project ON meeting_transcripts(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_meeting_transcripts_created_at ON meeting_transcripts(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS wiki_pages (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  space_id UUID,
  parent_page_id UUID,
  title VARCHAR(250) NOT NULL,
  slug VARCHAR(250) NOT NULL,
  visibility VARCHAR(30) NOT NULL DEFAULT 'internal',
  current_version_no INTEGER NOT NULL DEFAULT 1,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_wiki_pages_tenant ON wiki_pages(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_wiki_pages_project ON wiki_pages(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_wiki_pages_created_at ON wiki_pages(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS wiki_page_comments (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_wiki_page_comments_tenant ON wiki_page_comments(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_wiki_page_comments_project ON wiki_page_comments(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_wiki_page_comments_task ON wiki_page_comments(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS files (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  storage_provider VARCHAR(30) NOT NULL DEFAULT 'minio',
  bucket_name VARCHAR(120) NOT NULL,
  object_key VARCHAR(700) NOT NULL,
  original_name VARCHAR(255) NOT NULL,
  mime_type VARCHAR(150) NOT NULL,
  size_bytes BIGINT NOT NULL DEFAULT 0,
  visibility VARCHAR(30) NOT NULL DEFAULT 'internal',
  checksum_sha256 CHAR(64),
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_files_tenant ON files(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_files_created_at ON files(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS notifications (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_notifications_tenant ON notifications(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_notifications_created_at ON notifications(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_notifications_status ON notifications(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS webhooks (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_webhooks_tenant ON webhooks(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_webhooks_project ON webhooks(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_webhooks_created_at ON webhooks(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS webhook_events (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_webhook_events_tenant ON webhook_events(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_webhook_events_project ON webhook_events(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_webhook_events_created_at ON webhook_events(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS webhook_deliveries (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  archived_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_webhook_deliveries_tenant ON webhook_deliveries(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_webhook_deliveries_project ON webhook_deliveries(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_webhook_deliveries_created_at ON webhook_deliveries(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS webhook_delivery_retries (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_webhook_delivery_retries_tenant ON webhook_delivery_retries(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_webhook_delivery_retries_project ON webhook_delivery_retries(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_webhook_delivery_retries_created_at ON webhook_delivery_retries(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS webhook_secrets (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_webhook_secrets_tenant ON webhook_secrets(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_webhook_secrets_project ON webhook_secrets(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_webhook_secrets_created_at ON webhook_secrets(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS webhook_ip_whitelist (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_webhook_ip_whitelist_tenant ON webhook_ip_whitelist(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_webhook_ip_whitelist_project ON webhook_ip_whitelist(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_webhook_ip_whitelist_created_at ON webhook_ip_whitelist(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS integrations (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_integrations_tenant ON integrations(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_integrations_created_at ON integrations(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_integrations_status ON integrations(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS integration_configs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_integration_configs_tenant ON integration_configs(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_integration_configs_created_at ON integration_configs(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_integration_configs_status ON integration_configs(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS integration_sync_logs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_integration_sync_logs_tenant ON integration_sync_logs(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_integration_sync_logs_created_at ON integration_sync_logs(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_integration_sync_logs_status ON integration_sync_logs(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS integration_field_mappings (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_integration_field_mappings_tenant ON integration_field_mappings(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_integration_field_mappings_created_at ON integration_field_mappings(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_integration_field_mappings_status ON integration_field_mappings(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS ai_knowledge_documents (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_ai_knowledge_documents_tenant ON ai_knowledge_documents(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_ai_knowledge_documents_created_at ON ai_knowledge_documents(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_ai_knowledge_documents_status ON ai_knowledge_documents(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS ai_knowledge_chunks (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_ai_knowledge_chunks_tenant ON ai_knowledge_chunks(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_ai_knowledge_chunks_created_at ON ai_knowledge_chunks(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_ai_knowledge_chunks_status ON ai_knowledge_chunks(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS ai_knowledge_embeddings (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_ai_knowledge_embeddings_tenant ON ai_knowledge_embeddings(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_ai_knowledge_embeddings_created_at ON ai_knowledge_embeddings(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_ai_knowledge_embeddings_status ON ai_knowledge_embeddings(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS ai_knowledge_sync_jobs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_ai_knowledge_sync_jobs_tenant ON ai_knowledge_sync_jobs(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_ai_knowledge_sync_jobs_created_at ON ai_knowledge_sync_jobs(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_ai_knowledge_sync_jobs_status ON ai_knowledge_sync_jobs(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS ai_knowledge_permissions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_ai_knowledge_permissions_tenant ON ai_knowledge_permissions(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_ai_knowledge_permissions_created_at ON ai_knowledge_permissions(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_ai_knowledge_permissions_status ON ai_knowledge_permissions(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS document_import_jobs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  source_file_id UUID,
  status VARCHAR(40) NOT NULL DEFAULT 'uploaded',
  import_mode VARCHAR(30) NOT NULL DEFAULT 'preview',
  progress_percent INTEGER NOT NULL DEFAULT 0,
  error_message TEXT,
  name VARCHAR(200),
  code VARCHAR(100),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_document_import_jobs_tenant ON document_import_jobs(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_document_import_jobs_created_at ON document_import_jobs(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_document_import_jobs_status ON document_import_jobs(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS document_import_sources (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_document_import_sources_tenant ON document_import_sources(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_document_import_sources_created_at ON document_import_sources(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_document_import_sources_status ON document_import_sources(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS document_draft_tasks (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_document_draft_tasks_tenant ON document_draft_tasks(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_document_draft_tasks_project ON document_draft_tasks(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_document_draft_tasks_task ON document_draft_tasks(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS document_draft_task_reviews (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_document_draft_task_reviews_tenant ON document_draft_task_reviews(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_document_draft_task_reviews_project ON document_draft_task_reviews(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_document_draft_task_reviews_task ON document_draft_task_reviews(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS document_import_audit_logs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  archived_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_document_import_audit_logs_tenant ON document_import_audit_logs(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_document_import_audit_logs_created_at ON document_import_audit_logs(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_document_import_audit_logs_status ON document_import_audit_logs(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS document_draft_wiki_pages (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_document_draft_wiki_pages_tenant ON document_draft_wiki_pages(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_document_draft_wiki_pages_project ON document_draft_wiki_pages(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_document_draft_wiki_pages_created_at ON document_draft_wiki_pages(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS audit_logs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  actor_id UUID,
  action_type VARCHAR(80) NOT NULL,
  entity_type VARCHAR(80) NOT NULL,
  entity_id UUID,
  correlation_id VARCHAR(80),
  ip_address INET,
  user_agent TEXT,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  archived_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_audit_logs_tenant ON audit_logs(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_audit_logs_created_at ON audit_logs(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS compliance_reports (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_compliance_reports_tenant ON compliance_reports(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_compliance_reports_project ON compliance_reports(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_compliance_reports_created_at ON compliance_reports(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS project_analytics_snapshots (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_project_analytics_snapshots_tenant ON project_analytics_snapshots(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_project_analytics_snapshots_project ON project_analytics_snapshots(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_project_analytics_snapshots_created_at ON project_analytics_snapshots(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS task_analytics_snapshots (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_analytics_snapshots_tenant ON task_analytics_snapshots(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_analytics_snapshots_project ON task_analytics_snapshots(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_analytics_snapshots_task ON task_analytics_snapshots(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS sprint_analytics (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_sprint_analytics_tenant ON sprint_analytics(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_sprint_analytics_project ON sprint_analytics(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_sprint_analytics_created_at ON sprint_analytics(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS release_checklists (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_release_checklists_tenant ON release_checklists(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_release_checklists_task ON release_checklists(task_id) — load dữ liệu theo task.
-- INDEX RECOMMENDATION: idx_release_checklists_created_at ON release_checklists(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS system_settings (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_system_settings_created_at ON system_settings(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_system_settings_status ON system_settings(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS system_announcements (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_system_announcements_created_at ON system_announcements(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_system_announcements_status ON system_announcements(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS maintenance_windows (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_maintenance_windows_created_at ON maintenance_windows(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_maintenance_windows_status ON maintenance_windows(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS rate_limit_configs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_rate_limit_configs_created_at ON rate_limit_configs(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_rate_limit_configs_status ON rate_limit_configs(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS email_templates (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_email_templates_created_at ON email_templates(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_email_templates_status ON email_templates(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS email_template_versions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_email_template_versions_created_at ON email_template_versions(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_email_template_versions_status ON email_template_versions(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS system_health_logs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  archived_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_system_health_logs_created_at ON system_health_logs(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_system_health_logs_status ON system_health_logs(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS tenants (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_tenants_tenant ON tenants(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_tenants_created_at ON tenants(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_tenants_status ON tenants(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS tenant_settings (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_tenant_settings_tenant ON tenant_settings(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_tenant_settings_created_at ON tenant_settings(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_tenant_settings_status ON tenant_settings(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS tenant_subscriptions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_tenant_subscriptions_tenant ON tenant_subscriptions(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_tenant_subscriptions_created_at ON tenant_subscriptions(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_tenant_subscriptions_status ON tenant_subscriptions(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS tenant_subscription_plans (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_tenant_subscription_plans_tenant ON tenant_subscription_plans(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_tenant_subscription_plans_created_at ON tenant_subscription_plans(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_tenant_subscription_plans_status ON tenant_subscription_plans(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS tenant_billing_history (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_tenant_billing_history_tenant ON tenant_billing_history(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_tenant_billing_history_created_at ON tenant_billing_history(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_tenant_billing_history_status ON tenant_billing_history(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS tenant_feature_overrides (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_tenant_feature_overrides_tenant ON tenant_feature_overrides(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_tenant_feature_overrides_created_at ON tenant_feature_overrides(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_tenant_feature_overrides_status ON tenant_feature_overrides(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS tenant_storage_quotas (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_tenant_storage_quotas_tenant ON tenant_storage_quotas(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_tenant_storage_quotas_created_at ON tenant_storage_quotas(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_tenant_storage_quotas_status ON tenant_storage_quotas(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS tenant_security_policies (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_tenant_security_policies_tenant ON tenant_security_policies(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_tenant_security_policies_created_at ON tenant_security_policies(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_tenant_security_policies_status ON tenant_security_policies(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS user_preferences (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  user_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_user_preferences_tenant ON user_preferences(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_user_preferences_user ON user_preferences(user_id) — load dữ liệu theo user.
-- INDEX RECOMMENDATION: idx_user_preferences_created_at ON user_preferences(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS user_sessions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  user_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_user_sessions_tenant ON user_sessions(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_user_sessions_user ON user_sessions(user_id) — load dữ liệu theo user.
-- INDEX RECOMMENDATION: idx_user_sessions_created_at ON user_sessions(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS user_refresh_tokens (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  user_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_user_refresh_tokens_tenant ON user_refresh_tokens(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_user_refresh_tokens_user ON user_refresh_tokens(user_id) — load dữ liệu theo user.
-- INDEX RECOMMENDATION: idx_user_refresh_tokens_created_at ON user_refresh_tokens(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS user_password_history (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  user_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_user_password_history_tenant ON user_password_history(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_user_password_history_user ON user_password_history(user_id) — load dữ liệu theo user.
-- INDEX RECOMMENDATION: idx_user_password_history_created_at ON user_password_history(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS user_mfa_configs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  user_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_user_mfa_configs_tenant ON user_mfa_configs(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_user_mfa_configs_user ON user_mfa_configs(user_id) — load dữ liệu theo user.
-- INDEX RECOMMENDATION: idx_user_mfa_configs_created_at ON user_mfa_configs(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS user_mfa_backup_codes (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  user_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_user_mfa_backup_codes_tenant ON user_mfa_backup_codes(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_user_mfa_backup_codes_user ON user_mfa_backup_codes(user_id) — load dữ liệu theo user.
-- INDEX RECOMMENDATION: idx_user_mfa_backup_codes_created_at ON user_mfa_backup_codes(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS user_login_history (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  user_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  archived_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_user_login_history_tenant ON user_login_history(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_user_login_history_user ON user_login_history(user_id) — load dữ liệu theo user.
-- INDEX RECOMMENDATION: idx_user_login_history_created_at ON user_login_history(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS user_email_verifications (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  user_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_user_email_verifications_tenant ON user_email_verifications(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_user_email_verifications_user ON user_email_verifications(user_id) — load dữ liệu theo user.
-- INDEX RECOMMENDATION: idx_user_email_verifications_created_at ON user_email_verifications(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS user_password_resets (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  user_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_user_password_resets_tenant ON user_password_resets(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_user_password_resets_user ON user_password_resets(user_id) — load dữ liệu theo user.
-- INDEX RECOMMENDATION: idx_user_password_resets_created_at ON user_password_resets(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS oauth_providers (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_oauth_providers_tenant ON oauth_providers(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_oauth_providers_created_at ON oauth_providers(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_oauth_providers_status ON oauth_providers(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS oauth_connections (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_oauth_connections_tenant ON oauth_connections(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_oauth_connections_created_at ON oauth_connections(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_oauth_connections_status ON oauth_connections(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS api_keys (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_api_keys_tenant ON api_keys(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_api_keys_created_at ON api_keys(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_api_keys_status ON api_keys(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS api_key_permissions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_api_key_permissions_tenant ON api_key_permissions(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_api_key_permissions_created_at ON api_key_permissions(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_api_key_permissions_status ON api_key_permissions(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS user_devices (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  user_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_user_devices_tenant ON user_devices(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_user_devices_user ON user_devices(user_id) — load dữ liệu theo user.
-- INDEX RECOMMENDATION: idx_user_devices_created_at ON user_devices(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS user_notification_tokens (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  user_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_user_notification_tokens_tenant ON user_notification_tokens(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_user_notification_tokens_user ON user_notification_tokens(user_id) — load dữ liệu theo user.
-- INDEX RECOMMENDATION: idx_user_notification_tokens_created_at ON user_notification_tokens(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS organization_settings (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  organization_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_organization_settings_tenant ON organization_settings(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_organization_settings_organization ON organization_settings(organization_id) — load dữ liệu theo organization.
-- INDEX RECOMMENDATION: idx_organization_settings_created_at ON organization_settings(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS organization_member_invitations (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  organization_id UUID,
  user_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_organization_member_invitations_tenant ON organization_member_invitations(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_organization_member_invitations_organization ON organization_member_invitations(organization_id) — load dữ liệu theo organization.
-- INDEX RECOMMENDATION: idx_organization_member_invitations_user ON organization_member_invitations(user_id) — load dữ liệu theo user.

CREATE TABLE IF NOT EXISTS organization_roles (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  organization_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_organization_roles_tenant ON organization_roles(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_organization_roles_organization ON organization_roles(organization_id) — load dữ liệu theo organization.
-- INDEX RECOMMENDATION: idx_organization_roles_created_at ON organization_roles(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS organization_role_permissions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  organization_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_organization_role_permissions_tenant ON organization_role_permissions(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_organization_role_permissions_organization ON organization_role_permissions(organization_id) — load dữ liệu theo organization.
-- INDEX RECOMMENDATION: idx_organization_role_permissions_created_at ON organization_role_permissions(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS organization_departments (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  organization_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_organization_departments_tenant ON organization_departments(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_organization_departments_organization ON organization_departments(organization_id) — load dữ liệu theo organization.
-- INDEX RECOMMENDATION: idx_organization_departments_created_at ON organization_departments(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS organization_tags (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  organization_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_organization_tags_tenant ON organization_tags(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_organization_tags_organization ON organization_tags(organization_id) — load dữ liệu theo organization.
-- INDEX RECOMMENDATION: idx_organization_tags_created_at ON organization_tags(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS organization_custom_field_values (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  organization_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_organization_custom_field_values_tenant ON organization_custom_field_values(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_organization_custom_field_values_organization ON organization_custom_field_values(organization_id) — load dữ liệu theo organization.
-- INDEX RECOMMENDATION: idx_organization_custom_field_values_created_at ON organization_custom_field_values(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS organization_working_calendars (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  organization_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_organization_working_calendars_tenant ON organization_working_calendars(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_organization_working_calendars_organization ON organization_working_calendars(organization_id) — load dữ liệu theo organization.
-- INDEX RECOMMENDATION: idx_organization_working_calendars_created_at ON organization_working_calendars(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS organization_holidays (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  organization_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_organization_holidays_tenant ON organization_holidays(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_organization_holidays_organization ON organization_holidays(organization_id) — load dữ liệu theo organization.
-- INDEX RECOMMENDATION: idx_organization_holidays_created_at ON organization_holidays(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS project_member_roles (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  user_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_project_member_roles_tenant ON project_member_roles(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_project_member_roles_project ON project_member_roles(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_project_member_roles_user ON project_member_roles(user_id) — load dữ liệu theo user.

CREATE TABLE IF NOT EXISTS project_invitations (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_project_invitations_tenant ON project_invitations(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_project_invitations_project ON project_invitations(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_project_invitations_created_at ON project_invitations(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS project_custom_field_values (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_project_custom_field_values_tenant ON project_custom_field_values(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_project_custom_field_values_project ON project_custom_field_values(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_project_custom_field_values_created_at ON project_custom_field_values(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS project_templates (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  organization_id UUID,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_project_templates_tenant ON project_templates(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_project_templates_organization ON project_templates(organization_id) — load dữ liệu theo organization.
-- INDEX RECOMMENDATION: idx_project_templates_project ON project_templates(project_id) — load dữ liệu theo project.

CREATE TABLE IF NOT EXISTS project_health_snapshots (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_project_health_snapshots_tenant ON project_health_snapshots(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_project_health_snapshots_project ON project_health_snapshots(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_project_health_snapshots_created_at ON project_health_snapshots(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS project_archived_reasons (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_project_archived_reasons_tenant ON project_archived_reasons(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_project_archived_reasons_project ON project_archived_reasons(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_project_archived_reasons_created_at ON project_archived_reasons(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS project_customer_access_policies (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_project_customer_access_policies_tenant ON project_customer_access_policies(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_project_customer_access_policies_project ON project_customer_access_policies(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_project_customer_access_policies_created_at ON project_customer_access_policies(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS project_source_links (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_project_source_links_tenant ON project_source_links(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_project_source_links_project ON project_source_links(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_project_source_links_created_at ON project_source_links(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS project_change_requests (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_project_change_requests_tenant ON project_change_requests(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_project_change_requests_project ON project_change_requests(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_project_change_requests_created_at ON project_change_requests(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS project_decision_logs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_project_decision_logs_tenant ON project_decision_logs(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_project_decision_logs_project ON project_decision_logs(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_project_decision_logs_created_at ON project_decision_logs(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS task_watchers (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_watchers_tenant ON task_watchers(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_watchers_project ON task_watchers(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_watchers_task ON task_watchers(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS task_custom_field_values (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_custom_field_values_tenant ON task_custom_field_values(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_custom_field_values_project ON task_custom_field_values(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_custom_field_values_task ON task_custom_field_values(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS task_estimated_times (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_estimated_times_tenant ON task_estimated_times(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_estimated_times_project ON task_estimated_times(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_estimated_times_task ON task_estimated_times(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS task_actual_times (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_actual_times_tenant ON task_actual_times(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_actual_times_project ON task_actual_times(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_actual_times_task ON task_actual_times(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS task_activity_logs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_activity_logs_tenant ON task_activity_logs(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_activity_logs_project ON task_activity_logs(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_activity_logs_task ON task_activity_logs(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS task_evidence_attachments (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_evidence_attachments_tenant ON task_evidence_attachments(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_evidence_attachments_project ON task_evidence_attachments(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_evidence_attachments_task ON task_evidence_attachments(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS task_dependency_types (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_dependency_types_tenant ON task_dependency_types(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_dependency_types_project ON task_dependency_types(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_dependency_types_task ON task_dependency_types(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS dependency_impact_analysis (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_dependency_impact_analysis_tenant ON dependency_impact_analysis(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_dependency_impact_analysis_task ON dependency_impact_analysis(task_id) — load dữ liệu theo task.
-- INDEX RECOMMENDATION: idx_dependency_impact_analysis_created_at ON dependency_impact_analysis(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS task_blockers (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_blockers_tenant ON task_blockers(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_blockers_project ON task_blockers(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_blockers_task ON task_blockers(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS task_risk_flags (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_task_risk_flags_tenant ON task_risk_flags(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_task_risk_flags_project ON task_risk_flags(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_task_risk_flags_task ON task_risk_flags(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS kanban_boards (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_kanban_boards_tenant ON kanban_boards(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_kanban_boards_project ON kanban_boards(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_kanban_boards_created_at ON kanban_boards(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS kanban_column_limits (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_kanban_column_limits_tenant ON kanban_column_limits(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_kanban_column_limits_project ON kanban_column_limits(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_kanban_column_limits_created_at ON kanban_column_limits(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS kanban_cards (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_kanban_cards_tenant ON kanban_cards(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_kanban_cards_project ON kanban_cards(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_kanban_cards_created_at ON kanban_cards(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS kanban_card_orders (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_kanban_card_orders_tenant ON kanban_card_orders(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_kanban_card_orders_project ON kanban_card_orders(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_kanban_card_orders_created_at ON kanban_card_orders(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS kanban_swimlanes (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_kanban_swimlanes_tenant ON kanban_swimlanes(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_kanban_swimlanes_project ON kanban_swimlanes(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_kanban_swimlanes_created_at ON kanban_swimlanes(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS kanban_filters (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_kanban_filters_tenant ON kanban_filters(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_kanban_filters_project ON kanban_filters(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_kanban_filters_created_at ON kanban_filters(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS kanban_saved_filters (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_kanban_saved_filters_tenant ON kanban_saved_filters(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_kanban_saved_filters_project ON kanban_saved_filters(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_kanban_saved_filters_created_at ON kanban_saved_filters(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS sprint_goals (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_sprint_goals_tenant ON sprint_goals(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_sprint_goals_project ON sprint_goals(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_sprint_goals_created_at ON sprint_goals(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS sprint_capacity_plans (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_sprint_capacity_plans_tenant ON sprint_capacity_plans(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_sprint_capacity_plans_project ON sprint_capacity_plans(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_sprint_capacity_plans_created_at ON sprint_capacity_plans(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS sprint_member_capacities (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  user_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_sprint_member_capacities_tenant ON sprint_member_capacities(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_sprint_member_capacities_project ON sprint_member_capacities(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_sprint_member_capacities_user ON sprint_member_capacities(user_id) — load dữ liệu theo user.

CREATE TABLE IF NOT EXISTS sprint_velocity_history (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_sprint_velocity_history_tenant ON sprint_velocity_history(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_sprint_velocity_history_project ON sprint_velocity_history(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_sprint_velocity_history_created_at ON sprint_velocity_history(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS sprint_retrospectives (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_sprint_retrospectives_tenant ON sprint_retrospectives(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_sprint_retrospectives_project ON sprint_retrospectives(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_sprint_retrospectives_created_at ON sprint_retrospectives(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS timeline_views (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_timeline_views_tenant ON timeline_views(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_timeline_views_project ON timeline_views(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_timeline_views_created_at ON timeline_views(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS timeline_milestones (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_timeline_milestones_tenant ON timeline_milestones(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_timeline_milestones_project ON timeline_milestones(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_timeline_milestones_created_at ON timeline_milestones(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS timeline_baseline_snapshots (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_timeline_baseline_snapshots_tenant ON timeline_baseline_snapshots(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_timeline_baseline_snapshots_project ON timeline_baseline_snapshots(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_timeline_baseline_snapshots_created_at ON timeline_baseline_snapshots(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS timeline_critical_paths (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_timeline_critical_paths_tenant ON timeline_critical_paths(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_timeline_critical_paths_project ON timeline_critical_paths(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_timeline_critical_paths_created_at ON timeline_critical_paths(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS timeline_buffer_times (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_timeline_buffer_times_tenant ON timeline_buffer_times(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_timeline_buffer_times_project ON timeline_buffer_times(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_timeline_buffer_times_created_at ON timeline_buffer_times(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS comment_reactions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_comment_reactions_tenant ON comment_reactions(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_comment_reactions_task ON comment_reactions(task_id) — load dữ liệu theo task.
-- INDEX RECOMMENDATION: idx_comment_reactions_created_at ON comment_reactions(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS comment_mentions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_comment_mentions_tenant ON comment_mentions(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_comment_mentions_task ON comment_mentions(task_id) — load dữ liệu theo task.
-- INDEX RECOMMENDATION: idx_comment_mentions_created_at ON comment_mentions(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS comment_attachments (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_comment_attachments_tenant ON comment_attachments(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_comment_attachments_task ON comment_attachments(task_id) — load dữ liệu theo task.
-- INDEX RECOMMENDATION: idx_comment_attachments_created_at ON comment_attachments(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS comment_edit_history (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_comment_edit_history_tenant ON comment_edit_history(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_comment_edit_history_task ON comment_edit_history(task_id) — load dữ liệu theo task.
-- INDEX RECOMMENDATION: idx_comment_edit_history_created_at ON comment_edit_history(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS chat_room_members (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  user_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_chat_room_members_tenant ON chat_room_members(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_chat_room_members_project ON chat_room_members(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_chat_room_members_user ON chat_room_members(user_id) — load dữ liệu theo user.

CREATE TABLE IF NOT EXISTS chat_room_settings (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_chat_room_settings_tenant ON chat_room_settings(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_chat_room_settings_project ON chat_room_settings(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_chat_room_settings_created_at ON chat_room_settings(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS chat_room_pinned_messages (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_chat_room_pinned_messages_tenant ON chat_room_pinned_messages(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_chat_room_pinned_messages_project ON chat_room_pinned_messages(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_chat_room_pinned_messages_created_at ON chat_room_pinned_messages(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS chat_message_reactions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_chat_message_reactions_tenant ON chat_message_reactions(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_chat_message_reactions_project ON chat_message_reactions(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_chat_message_reactions_created_at ON chat_message_reactions(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS chat_message_mentions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_chat_message_mentions_tenant ON chat_message_mentions(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_chat_message_mentions_project ON chat_message_mentions(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_chat_message_mentions_created_at ON chat_message_mentions(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS chat_message_attachments (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_chat_message_attachments_tenant ON chat_message_attachments(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_chat_message_attachments_project ON chat_message_attachments(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_chat_message_attachments_created_at ON chat_message_attachments(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS chat_message_task_links (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  task_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_chat_message_task_links_tenant ON chat_message_task_links(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_chat_message_task_links_project ON chat_message_task_links(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_chat_message_task_links_task ON chat_message_task_links(task_id) — load dữ liệu theo task.

CREATE TABLE IF NOT EXISTS chat_message_edit_history (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_chat_message_edit_history_tenant ON chat_message_edit_history(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_chat_message_edit_history_project ON chat_message_edit_history(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_chat_message_edit_history_created_at ON chat_message_edit_history(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS chat_message_read_receipts (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_chat_message_read_receipts_tenant ON chat_message_read_receipts(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_chat_message_read_receipts_project ON chat_message_read_receipts(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_chat_message_read_receipts_created_at ON chat_message_read_receipts(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS chat_direct_messages (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_chat_direct_messages_tenant ON chat_direct_messages(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_chat_direct_messages_project ON chat_direct_messages(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_chat_direct_messages_created_at ON chat_direct_messages(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS chat_dm_participants (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  user_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_chat_dm_participants_tenant ON chat_dm_participants(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_chat_dm_participants_project ON chat_dm_participants(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_chat_dm_participants_user ON chat_dm_participants(user_id) — load dữ liệu theo user.

CREATE TABLE IF NOT EXISTS realtime_presence_sessions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_realtime_presence_sessions_tenant ON realtime_presence_sessions(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_realtime_presence_sessions_created_at ON realtime_presence_sessions(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_realtime_presence_sessions_status ON realtime_presence_sessions(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS realtime_typing_indicators (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_realtime_typing_indicators_tenant ON realtime_typing_indicators(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_realtime_typing_indicators_created_at ON realtime_typing_indicators(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_realtime_typing_indicators_status ON realtime_typing_indicators(status) — filter theo trạng thái.

CREATE TABLE IF NOT EXISTS wiki_spaces (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_wiki_spaces_tenant ON wiki_spaces(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_wiki_spaces_project ON wiki_spaces(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_wiki_spaces_created_at ON wiki_spaces(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS wiki_page_versions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_wiki_page_versions_tenant ON wiki_page_versions(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_wiki_page_versions_project ON wiki_page_versions(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_wiki_page_versions_created_at ON wiki_page_versions(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS wiki_page_contents (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_wiki_page_contents_tenant ON wiki_page_contents(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_wiki_page_contents_project ON wiki_page_contents(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_wiki_page_contents_created_at ON wiki_page_contents(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS wiki_page_permissions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_wiki_page_permissions_tenant ON wiki_page_permissions(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_wiki_page_permissions_project ON wiki_page_permissions(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_wiki_page_permissions_created_at ON wiki_page_permissions(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS wiki_page_watchers (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_wiki_page_watchers_tenant ON wiki_page_watchers(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_wiki_page_watchers_project ON wiki_page_watchers(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_wiki_page_watchers_created_at ON wiki_page_watchers(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS wiki_page_links (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_wiki_page_links_tenant ON wiki_page_links(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_wiki_page_links_project ON wiki_page_links(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_wiki_page_links_created_at ON wiki_page_links(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS wiki_page_attachments (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_wiki_page_attachments_tenant ON wiki_page_attachments(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_wiki_page_attachments_project ON wiki_page_attachments(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_wiki_page_attachments_created_at ON wiki_page_attachments(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS wiki_page_templates (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_wiki_page_templates_tenant ON wiki_page_templates(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_wiki_page_templates_project ON wiki_page_templates(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_wiki_page_templates_created_at ON wiki_page_templates(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS wiki_page_tags (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_wiki_page_tags_tenant ON wiki_page_tags(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_wiki_page_tags_project ON wiki_page_tags(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_wiki_page_tags_created_at ON wiki_page_tags(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS wiki_page_export_history (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  project_id UUID,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_wiki_page_export_history_tenant ON wiki_page_export_history(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_wiki_page_export_history_project ON wiki_page_export_history(project_id) — load dữ liệu theo project.
-- INDEX RECOMMENDATION: idx_wiki_page_export_history_created_at ON wiki_page_export_history(created_at DESC) — phân trang/audit/log.

CREATE TABLE IF NOT EXISTS file_versions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  name VARCHAR(200),
  code VARCHAR(100),
  status VARCHAR(40),
  metadata_json JSONB,
  created_by UUID,
  updated_by UUID,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ DEFAULT NULL
);
-- INDEX RECOMMENDATION: idx_file_versions_tenant ON file_versions(tenant_id) — lọc theo tenant, chống cross-tenant access.
-- INDEX RECOMMENDATION: idx_file_versions_created_at ON file_versions(created_at DESC) — phân trang/audit/log.
-- INDEX RECOMMENDATION: idx_file_versions_status ON file_versions(status) — filter theo trạng thái.

