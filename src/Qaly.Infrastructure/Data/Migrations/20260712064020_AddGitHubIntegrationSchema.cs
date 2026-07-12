using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubIntegrationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GitHubInstallations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallationId = table.Column<long>(type: "bigint", nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    AccountLogin = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InstalledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubInstallations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GitHubInstallations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GitHubWebhookInbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    DeliveryId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InstallationId = table.Column<long>(type: "bigint", nullable: true),
                    RepositoryExternalId = table.Column<long>(type: "bigint", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubWebhookInbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskDevelopmentLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ExternalEntityId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LinkSource = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, defaultValue: "TaskKey"),
                    Confidence = table.Column<double>(type: "float", nullable: false),
                    LinkedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskDevelopmentLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskDevelopmentLinks_TaskItems_TaskId",
                        column: x => x.TaskId,
                        principalTable: "TaskItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GitHubRepositoryConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GitHubInstallationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryExternalId = table.Column<long>(type: "bigint", nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DefaultBranch = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "main"),
                    IsPrivate = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubRepositoryConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GitHubRepositoryConnections_GitHubInstallations_GitHubInstallationId",
                        column: x => x.GitHubInstallationId,
                        principalTable: "GitHubInstallations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GitHubRepositoryConnections_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GitHubCommits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sha = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AuthorLogin = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AuthorEmailHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CommittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    BranchName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubCommits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GitHubCommits_GitHubRepositoryConnections_RepositoryConnectionId",
                        column: x => x.RepositoryConnectionId,
                        principalTable: "GitHubRepositoryConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GitHubPullRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Open"),
                    AuthorLogin = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    HeadBranch = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    BaseBranch = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IsDraft = table.Column<bool>(type: "bit", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GitHubUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MergedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MergedByLogin = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubPullRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GitHubPullRequests_GitHubRepositoryConnections_RepositoryConnectionId",
                        column: x => x.RepositoryConnectionId,
                        principalTable: "GitHubRepositoryConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GitHubReleases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReleaseExternalId = table.Column<long>(type: "bigint", nullable: false),
                    TagName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDraft = table.Column<bool>(type: "bit", nullable: false),
                    IsPrerelease = table.Column<bool>(type: "bit", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubReleases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GitHubReleases_GitHubRepositoryConnections_RepositoryConnectionId",
                        column: x => x.RepositoryConnectionId,
                        principalTable: "GitHubRepositoryConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GitHubPullRequestReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PullRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewExternalId = table.Column<long>(type: "bigint", nullable: false),
                    ReviewerLogin = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Commented"),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubPullRequestReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GitHubPullRequestReviews_GitHubPullRequests_PullRequestId",
                        column: x => x.PullRequestId,
                        principalTable: "GitHubPullRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GitHubCommits_OrganizationId",
                table: "GitHubCommits",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubCommits_RepositoryConnectionId_Sha",
                table: "GitHubCommits",
                columns: new[] { "RepositoryConnectionId", "Sha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitHubInstallations_InstallationId",
                table: "GitHubInstallations",
                column: "InstallationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitHubInstallations_OrganizationId",
                table: "GitHubInstallations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubPullRequestReviews_OrganizationId",
                table: "GitHubPullRequestReviews",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubPullRequestReviews_PullRequestId_ReviewExternalId",
                table: "GitHubPullRequestReviews",
                columns: new[] { "PullRequestId", "ReviewExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitHubPullRequests_OrganizationId",
                table: "GitHubPullRequests",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubPullRequests_RepositoryConnectionId_Number",
                table: "GitHubPullRequests",
                columns: new[] { "RepositoryConnectionId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitHubPullRequests_State",
                table: "GitHubPullRequests",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubReleases_OrganizationId",
                table: "GitHubReleases",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubReleases_RepositoryConnectionId_ReleaseExternalId",
                table: "GitHubReleases",
                columns: new[] { "RepositoryConnectionId", "ReleaseExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitHubRepositoryConnections_GitHubInstallationId",
                table: "GitHubRepositoryConnections",
                column: "GitHubInstallationId");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubRepositoryConnections_OrganizationId",
                table: "GitHubRepositoryConnections",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubRepositoryConnections_ProjectId_RepositoryExternalId",
                table: "GitHubRepositoryConnections",
                columns: new[] { "ProjectId", "RepositoryExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitHubWebhookInbox_DeliveryId",
                table: "GitHubWebhookInbox",
                column: "DeliveryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitHubWebhookInbox_Status",
                table: "GitHubWebhookInbox",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDevelopmentLinks_OrganizationId",
                table: "TaskDevelopmentLinks",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDevelopmentLinks_TaskId_EntityType_ExternalEntityId",
                table: "TaskDevelopmentLinks",
                columns: new[] { "TaskId", "EntityType", "ExternalEntityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GitHubCommits");

            migrationBuilder.DropTable(
                name: "GitHubPullRequestReviews");

            migrationBuilder.DropTable(
                name: "GitHubReleases");

            migrationBuilder.DropTable(
                name: "GitHubWebhookInbox");

            migrationBuilder.DropTable(
                name: "TaskDevelopmentLinks");

            migrationBuilder.DropTable(
                name: "GitHubPullRequests");

            migrationBuilder.DropTable(
                name: "GitHubRepositoryConnections");

            migrationBuilder.DropTable(
                name: "GitHubInstallations");
        }
    }
}
