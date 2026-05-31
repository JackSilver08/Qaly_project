# QALY UML/Diagram Specification v3.2 - Gap Closure Diagrams

## 1. Existing v3.1 diagrams retained

| Diagram | File |
|---|---|
| ARCH_AI_01 | `docs/diagrams/mermaid/ARCH_AI_01_Meetily_API_Primary_v3_1.mmd` |
| DEPLOY_AI_01 | `docs/diagrams/mermaid/DEPLOY_AI_01_VPS8GB_LaptopWorker_v3_1.mmd` |
| SEQ_AI_01 | `docs/diagrams/mermaid/SEQ_AI_01_Meetily_Import_Action_Items_v3_1.mmd` |
| SEQ_AI_02 | `docs/diagrams/mermaid/SEQ_AI_02_Task_Recommendation_HITL_v3_1.mmd` |
| SEQ_AI_03 | `docs/diagrams/mermaid/SEQ_AI_03_Provider_Fallback_Cache_Budget_v3_1.mmd` |
| DATA_AI_01 | `docs/diagrams/mermaid/DATA_AI_01_AI_Audit_Cost_ERD_v3_1.mmd` |
| DFD_AI_01 | `docs/diagrams/mermaid/DFD_AI_01_Compliance_Data_Flow_v3_1.mmd` |

## 2. New v3.2 diagrams

| Diagram | File | Gap closed |
|---|---|---|
| SEQ_AI_04 | `docs/diagrams/mermaid/SEQ_AI_04_AI_Job_Status_Retry_Cancel_v3_2.mmd` | job lifecycle/status/retry/cancel |
| SEC_AI_01 | `docs/diagrams/mermaid/SEC_AI_01_Privacy_Consent_Delete_Export_v3_2.mmd` | compliance UI/API flow |
| OPS_AI_02 | `docs/diagrams/mermaid/OPS_AI_02_Backup_Restore_Health_v3_2.mmd` | backup/restore/healthcheck |
| DATA_AI_02 | `docs/diagrams/mermaid/DATA_AI_02_FK_Retention_Consent_ERD_v3_2.mmd` | DB retention/consent/FK-ready structure |

## 3. Rendering

Use Mermaid CLI or Markdown editor with Mermaid support. `.mmd` files are source of truth for diagrams.
