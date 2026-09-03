using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations;

[DbContext(typeof(QalyDbContext))]
[Migration("20260903130000_P026VectorSyncOutboxReliability")]
public sealed class P026VectorSyncOutboxReliability : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateSequence<long>(
            name: "VectorSyncOutboxSequence");

        // The old interceptor emitted these names while the old worker expected
        // Task*/Comment* names, then falsely marked the unknown event processed.
        // Requeue them before normalization so the canonical vector state heals.
        migrationBuilder.Sql(
            """
            UPDATE [VectorSyncOutbox]
            SET [ProcessedAt] = NULL,
                [RetryCount] = 0,
                [ErrorMessage] = N'Requeued by P026 after legacy event-contract mismatch.'
            WHERE [EventType] IN (
                N'TaskItemCreated', N'TaskItemUpdated', N'TaskItemDeleted',
                N'TaskCommentCreated', N'TaskCommentUpdated', N'TaskCommentDeleted');
            """);

        migrationBuilder.Sql(
            "UPDATE [VectorSyncOutbox] SET [ErrorMessage] = LEFT([ErrorMessage], 2000) WHERE LEN([ErrorMessage]) > 2000;");

        migrationBuilder.AlterColumn<string>(
            name: "EventType",
            table: "VectorSyncOutbox",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)");

        migrationBuilder.AlterColumn<string>(
            name: "ErrorMessage",
            table: "VectorSyncOutbox",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldNullable: true);

        migrationBuilder.AlterColumn<DateTimeOffset>(
            name: "CreatedAt",
            table: "VectorSyncOutbox",
            type: "datetimeoffset",
            nullable: false,
            defaultValueSql: "SYSDATETIMEOFFSET()",
            oldClrType: typeof(DateTimeOffset),
            oldType: "datetimeoffset");

        migrationBuilder.AlterColumn<Guid>(
            name: "Id",
            table: "VectorSyncOutbox",
            type: "uniqueidentifier",
            nullable: false,
            defaultValueSql: "NEWID()",
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");

        migrationBuilder.AddColumn<Guid>(
            name: "AggregateId",
            table: "VectorSyncOutbox",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: Guid.Empty);

        migrationBuilder.AddColumn<string>(
            name: "AggregateType",
            table: "VectorSyncOutbox",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: string.Empty);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeadLetteredAt",
            table: "VectorSyncOutbox",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LeaseExpiresAt",
            table: "VectorSyncOutbox",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LeaseOwner",
            table: "VectorSyncOutbox",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "NextAttemptAt",
            table: "VectorSyncOutbox",
            type: "datetimeoffset",
            nullable: false,
            defaultValueSql: "SYSDATETIMEOFFSET()");

        migrationBuilder.AddColumn<long>(
            name: "SequenceNumber",
            table: "VectorSyncOutbox",
            type: "bigint",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE [VectorSyncOutbox]
            SET [EventType] = CASE [EventType]
                    WHEN N'TaskItemCreated' THEN N'TaskCreated'
                    WHEN N'TaskItemUpdated' THEN N'TaskUpdated'
                    WHEN N'TaskItemDeleted' THEN N'TaskDeleted'
                    WHEN N'TaskCommentCreated' THEN N'CommentAdded'
                    WHEN N'TaskCommentUpdated' THEN N'CommentUpdated'
                    WHEN N'TaskCommentDeleted' THEN N'CommentDeleted'
                    ELSE [EventType]
                END;

            UPDATE [VectorSyncOutbox]
            SET [AggregateType] = CASE
                    WHEN [EventType] LIKE N'Project%' THEN N'Project'
                    WHEN [EventType] LIKE N'TaskAttachment%' THEN N'Attachment'
                    WHEN [EventType] LIKE N'Task%' THEN N'Task'
                    WHEN [EventType] LIKE N'Comment%' THEN N'Comment'
                    ELSE N''
                END,
                [AggregateId] = COALESCE(
                    TRY_CONVERT(uniqueidentifier,
                        CASE WHEN ISJSON([Payload]) = 1 THEN JSON_VALUE([Payload], '$.Id') END),
                    '00000000-0000-0000-0000-000000000000');

            UPDATE [VectorSyncOutbox]
            SET [ProcessedAt] = NULL,
                [DeadLetteredAt] = SYSDATETIMEOFFSET(),
                [AggregateType] = CASE WHEN [AggregateType] = N'' THEN N'Invalid' ELSE [AggregateType] END,
                [AggregateId] = CASE
                    WHEN [AggregateId] = '00000000-0000-0000-0000-000000000000' THEN [Id]
                    ELSE [AggregateId]
                END,
                [ErrorMessage] = N'Legacy vector sync event has an invalid event type or aggregate identity.'
            WHERE [EventType] NOT IN (
                    N'ProjectCreated', N'ProjectUpdated', N'ProjectDeleted',
                    N'TaskCreated', N'TaskUpdated', N'TaskDeleted',
                    N'CommentAdded', N'CommentUpdated', N'CommentDeleted',
                    N'TaskAttachmentCreated', N'TaskAttachmentUpdated', N'TaskAttachmentDeleted')
               OR [AggregateType] = N''
               OR [AggregateId] = '00000000-0000-0000-0000-000000000000';

            ;WITH [OrderedEvents] AS
            (
                SELECT [Id], ROW_NUMBER() OVER (ORDER BY [CreatedAt], [Id]) AS [SequenceValue]
                FROM [VectorSyncOutbox]
            )
            UPDATE [Target]
            SET [SequenceNumber] = [Ordered].[SequenceValue]
            FROM [VectorSyncOutbox] AS [Target]
            INNER JOIN [OrderedEvents] AS [Ordered] ON [Ordered].[Id] = [Target].[Id];

            DECLARE @nextSequence bigint =
                (SELECT COALESCE(MAX([SequenceNumber]), 0) + 1 FROM [VectorSyncOutbox]);
            DECLARE @restartSequenceSql nvarchar(200) =
                N'ALTER SEQUENCE [VectorSyncOutboxSequence] RESTART WITH ' +
                CONVERT(nvarchar(30), @nextSequence);
            EXEC sys.sp_executesql @restartSequenceSql;
            """);

        migrationBuilder.AlterColumn<long>(
            name: "SequenceNumber",
            table: "VectorSyncOutbox",
            type: "bigint",
            nullable: false,
            defaultValueSql: "NEXT VALUE FOR [VectorSyncOutboxSequence]",
            oldClrType: typeof(long),
            oldType: "bigint",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_VectorSyncOutbox_AggregateType_AggregateId_SequenceNumber",
            table: "VectorSyncOutbox",
            columns: new[] { "AggregateType", "AggregateId", "SequenceNumber" });

        migrationBuilder.CreateIndex(
            name: "IX_VectorSyncOutbox_ProcessedAt_DeadLetteredAt_NextAttemptAt_LeaseExpiresAt_SequenceNumber",
            table: "VectorSyncOutbox",
            columns: new[] { "ProcessedAt", "DeadLetteredAt", "NextAttemptAt", "LeaseExpiresAt", "SequenceNumber" });

        migrationBuilder.CreateIndex(
            name: "IX_VectorSyncOutbox_SequenceNumber",
            table: "VectorSyncOutbox",
            column: "SequenceNumber",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_VectorSyncOutbox_AggregateType_AggregateId_SequenceNumber",
            table: "VectorSyncOutbox");
        migrationBuilder.DropIndex(
            name: "IX_VectorSyncOutbox_ProcessedAt_DeadLetteredAt_NextAttemptAt_LeaseExpiresAt_SequenceNumber",
            table: "VectorSyncOutbox");
        migrationBuilder.DropIndex(
            name: "IX_VectorSyncOutbox_SequenceNumber",
            table: "VectorSyncOutbox");

        migrationBuilder.DropColumn(name: "AggregateId", table: "VectorSyncOutbox");
        migrationBuilder.DropColumn(name: "AggregateType", table: "VectorSyncOutbox");
        migrationBuilder.DropColumn(name: "DeadLetteredAt", table: "VectorSyncOutbox");
        migrationBuilder.DropColumn(name: "LeaseExpiresAt", table: "VectorSyncOutbox");
        migrationBuilder.DropColumn(name: "LeaseOwner", table: "VectorSyncOutbox");
        migrationBuilder.DropColumn(name: "NextAttemptAt", table: "VectorSyncOutbox");
        migrationBuilder.DropColumn(name: "SequenceNumber", table: "VectorSyncOutbox");
        migrationBuilder.DropSequence(name: "VectorSyncOutboxSequence");

        migrationBuilder.AlterColumn<string>(
            name: "EventType",
            table: "VectorSyncOutbox",
            type: "nvarchar(max)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(64)",
            oldMaxLength: 64);

        migrationBuilder.AlterColumn<string>(
            name: "ErrorMessage",
            table: "VectorSyncOutbox",
            type: "nvarchar(max)",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(2000)",
            oldMaxLength: 2000,
            oldNullable: true);

        migrationBuilder.AlterColumn<DateTimeOffset>(
            name: "CreatedAt",
            table: "VectorSyncOutbox",
            type: "datetimeoffset",
            nullable: false,
            oldClrType: typeof(DateTimeOffset),
            oldType: "datetimeoffset",
            oldDefaultValueSql: "SYSDATETIMEOFFSET()");

        migrationBuilder.AlterColumn<Guid>(
            name: "Id",
            table: "VectorSyncOutbox",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldDefaultValueSql: "NEWID()");
    }
}
