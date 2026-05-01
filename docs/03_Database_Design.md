# 📊 QALY PROJECT – DATABASE DESIGN (SQL Server)

> **Ngày tạo:** 01/05/2026 | **Phiên bản:** 1.0

---

## I. MAPPING POSTGRES → SQL SERVER

| Postgres | SQL Server | EF Core Config |
|---|---|---|
| `UUID` | `UNIQUEIDENTIFIER` | `.HasDefaultValueSql("NEWID()")` |
| `SERIAL` | `INT IDENTITY(1,1)` | `.ValueGeneratedOnAdd()` |
| `JSONB` | `NVARCHAR(MAX)` | `.HasColumnType("nvarchar(max)")` |
| `TIMESTAMPTZ` | `DATETIMEOFFSET` | Mặc định |
| `TEXT` | `NVARCHAR(MAX)` | Mặc định |
| `VARCHAR(n)` | `NVARCHAR(n)` | `.HasMaxLength(n)` |
| `BOOLEAN` | `BIT` | Mặc định |
| `GIN INDEX` | `LIKE` / `FULLTEXT` | Phase 1: `.Contains()` |
| `xmin` | `ROWVERSION` | `.IsRowVersion()` |

---

## II. BẢNG DỮ LIỆU

### Users
| Cột | Kiểu | Constraint |
|---|---|---|
| Id | UNIQUEIDENTIFIER | PK, DEFAULT NEWID() |
| FullName | NVARCHAR(100) | NOT NULL |
| Email | NVARCHAR(256) | UNIQUE, NOT NULL |
| PasswordHash | NVARCHAR(MAX) | NOT NULL |
| Role | NVARCHAR(50) | NOT NULL |
| IsActive | BIT | DEFAULT 1 |
| AvatarUrl | NVARCHAR(MAX) | NULL |
| CreatedAt | DATETIMEOFFSET | DEFAULT SYSDATETIMEOFFSET() |
| UpdatedAt | DATETIMEOFFSET | NULL |

### Projects
| Cột | Kiểu | Constraint |
|---|---|---|
| Id | UNIQUEIDENTIFIER | PK, DEFAULT NEWID() |
| Name | NVARCHAR(200) | NOT NULL |
| Description | NVARCHAR(MAX) | NULL |
| Status | NVARCHAR(20) | NOT NULL |
| OwnerId | UNIQUEIDENTIFIER | FK → Users.Id |
| StartDate | DATETIMEOFFSET | NULL |
| EndDate | DATETIMEOFFSET | NULL |
| CreatedAt | DATETIMEOFFSET | DEFAULT SYSDATETIMEOFFSET() |
| UpdatedAt | DATETIMEOFFSET | NULL |

### ProjectMembers
| Cột | Kiểu | Constraint |
|---|---|---|
| Id | UNIQUEIDENTIFIER | PK, DEFAULT NEWID() |
| ProjectId | UNIQUEIDENTIFIER | FK → Projects.Id |
| UserId | UNIQUEIDENTIFIER | FK → Users.Id |
| Role | NVARCHAR(20) | NOT NULL |
| JoinedAt | DATETIMEOFFSET | DEFAULT SYSDATETIMEOFFSET() |

### TaskItems
| Cột | Kiểu | Constraint |
|---|---|---|
| Id | UNIQUEIDENTIFIER | PK, DEFAULT NEWID() |
| Title | NVARCHAR(300) | NOT NULL |
| Description | NVARCHAR(MAX) | NULL |
| Status | NVARCHAR(20) | NOT NULL, DEFAULT 'Todo' |
| Priority | NVARCHAR(20) | NOT NULL, DEFAULT 'Medium' |
| ProjectId | UNIQUEIDENTIFIER | FK → Projects.Id |
| AssigneeId | UNIQUEIDENTIFIER | FK → Users.Id, NULL |
| ReporterId | UNIQUEIDENTIFIER | FK → Users.Id |
| DueDate | DATETIMEOFFSET | NULL |
| EstimatedHours | INT | NULL |
| ActualHours | INT | NULL |
| IsPrivate | BIT | DEFAULT 0 |
| CreatedAt | DATETIMEOFFSET | DEFAULT SYSDATETIMEOFFSET() |
| UpdatedAt | DATETIMEOFFSET | NULL |

### TaskComments
| Cột | Kiểu | Constraint |
|---|---|---|
| Id | UNIQUEIDENTIFIER | PK, DEFAULT NEWID() |
| Content | NVARCHAR(MAX) | NOT NULL |
| TaskItemId | UNIQUEIDENTIFIER | FK → TaskItems.Id |
| AuthorId | UNIQUEIDENTIFIER | FK → Users.Id |
| CreatedAt | DATETIMEOFFSET | DEFAULT SYSDATETIMEOFFSET() |
| UpdatedAt | DATETIMEOFFSET | NULL |

### TaskAttachments
| Cột | Kiểu | Constraint |
|---|---|---|
| Id | UNIQUEIDENTIFIER | PK, DEFAULT NEWID() |
| FileName | NVARCHAR(500) | NOT NULL |
| FilePath | NVARCHAR(MAX) | NOT NULL |
| FileSize | BIGINT | NOT NULL |
| ContentType | NVARCHAR(100) | NULL |
| TaskItemId | UNIQUEIDENTIFIER | FK → TaskItems.Id |
| UploadedById | UNIQUEIDENTIFIER | FK → Users.Id |
| UploadedAt | DATETIMEOFFSET | DEFAULT SYSDATETIMEOFFSET() |

### Notifications
| Cột | Kiểu | Constraint |
|---|---|---|
| Id | UNIQUEIDENTIFIER | PK, DEFAULT NEWID() |
| Message | NVARCHAR(500) | NOT NULL |
| Type | NVARCHAR(50) | NOT NULL |
| IsRead | BIT | DEFAULT 0 |
| UserId | UNIQUEIDENTIFIER | FK → Users.Id |
| RelatedEntityId | UNIQUEIDENTIFIER | NULL |
| RelatedEntityType | NVARCHAR(50) | NULL |
| CreatedAt | DATETIMEOFFSET | DEFAULT SYSDATETIMEOFFSET() |

### AuditLogs
| Cột | Kiểu | Constraint |
|---|---|---|
| Id | BIGINT | PK, IDENTITY(1,1) |
| Action | NVARCHAR(50) | NOT NULL |
| EntityType | NVARCHAR(100) | NOT NULL |
| EntityId | NVARCHAR(100) | NOT NULL |
| ChangesJson | NVARCHAR(MAX) | NULL *(thay JSONB)* |
| UserId | UNIQUEIDENTIFIER | FK → Users.Id |
| IpAddress | NVARCHAR(45) | NULL |
| Timestamp | DATETIMEOFFSET | DEFAULT SYSDATETIMEOFFSET() |

---

## III. INDEXES

```sql
-- Performance indexes
CREATE INDEX IX_TaskItems_ProjectId ON TaskItems(ProjectId);
CREATE INDEX IX_TaskItems_AssigneeId ON TaskItems(AssigneeId);
CREATE INDEX IX_TaskItems_Status ON TaskItems(Status);
CREATE INDEX IX_TaskComments_TaskItemId ON TaskComments(TaskItemId);
CREATE INDEX IX_Notifications_UserId_IsRead ON Notifications(UserId, IsRead);
CREATE INDEX IX_AuditLogs_EntityType_EntityId ON AuditLogs(EntityType, EntityId);
CREATE INDEX IX_AuditLogs_Timestamp ON AuditLogs(Timestamp DESC);
CREATE INDEX IX_ProjectMembers_UserId ON ProjectMembers(UserId);

-- Unique constraints
CREATE UNIQUE INDEX IX_ProjectMembers_ProjectId_UserId ON ProjectMembers(ProjectId, UserId);
```

---

*Cập nhật khi có thay đổi schema.*
