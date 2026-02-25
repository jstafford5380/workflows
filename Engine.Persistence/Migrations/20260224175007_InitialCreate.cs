using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Engine.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventInboxes",
                columns: table => new
                {
                    EventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CorrelationKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventInboxes", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "EventSubscriptions",
                columns: table => new
                {
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CorrelationKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FulfilledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSubscriptions", x => x.SubscriptionId);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    OutboxId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.OutboxId);
                });

            migrationBuilder.CreateTable(
                name: "StepDependencies",
                columns: table => new
                {
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DependsOnStepId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StepDependencies", x => new { x.InstanceId, x.StepId, x.DependsOnStepId });
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    DefinitionJson = table.Column<string>(type: "text", nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowInstances",
                columns: table => new
                {
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WorkflowVersion = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    InputsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowInstances", x => x.InstanceId);
                });

            migrationBuilder.CreateTable(
                name: "WorkQueueItems",
                columns: table => new
                {
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DequeuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseOwner = table.Column<string>(type: "text", nullable: true),
                    DequeueCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkQueueItems", x => x.WorkItemId);
                });

            migrationBuilder.CreateTable(
                name: "StepRuns",
                columns: table => new
                {
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ActivityRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StepDefinitionJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    LeaseOwner = table.Column<string>(type: "text", nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    OutputsJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StepRuns", x => new { x.InstanceId, x.StepId });
                    table.ForeignKey(
                        name: "FK_StepRuns_WorkflowInstances_InstanceId",
                        column: x => x.InstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "InstanceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventInboxes_EventType_CorrelationKey_PayloadHash",
                table: "EventInboxes",
                columns: new[] { "EventType", "CorrelationKey", "PayloadHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventSubscriptions_Status_EventType_CorrelationKey",
                table: "EventSubscriptions",
                columns: new[] { "Status", "EventType", "CorrelationKey" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAt_CreatedAt",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StepDependencies_InstanceId_DependsOnStepId",
                table: "StepDependencies",
                columns: new[] { "InstanceId", "DependsOnStepId" });

            migrationBuilder.CreateIndex(
                name: "IX_StepRuns_InstanceId_Status_NextAttemptAt",
                table: "StepRuns",
                columns: new[] { "InstanceId", "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_Name_Version",
                table: "WorkflowDefinitions",
                columns: new[] { "Name", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkQueueItems_CompletedAt_AvailableAt_LeaseExpiresAt",
                table: "WorkQueueItems",
                columns: new[] { "CompletedAt", "AvailableAt", "LeaseExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventInboxes");

            migrationBuilder.DropTable(
                name: "EventSubscriptions");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "StepDependencies");

            migrationBuilder.DropTable(
                name: "StepRuns");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitions");

            migrationBuilder.DropTable(
                name: "WorkQueueItems");

            migrationBuilder.DropTable(
                name: "WorkflowInstances");
        }
    }
}
