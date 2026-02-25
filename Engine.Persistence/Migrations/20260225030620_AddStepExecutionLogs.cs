using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Engine.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStepExecutionLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StepExecutionLogs",
                columns: table => new
                {
                    LogId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    ConsoleOutput = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StepExecutionLogs", x => x.LogId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StepExecutionLogs_CreatedAt",
                table: "StepExecutionLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StepExecutionLogs_InstanceId_StepId_Attempt",
                table: "StepExecutionLogs",
                columns: new[] { "InstanceId", "StepId", "Attempt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StepExecutionLogs");
        }
    }
}
