using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Engine.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowDrafts",
                columns: table => new
                {
                    DraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    DefinitionJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDrafts", x => x.DraftId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDrafts_UpdatedAt",
                table: "WorkflowDrafts",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowDrafts");
        }
    }
}
