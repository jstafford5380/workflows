using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Engine.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowDefinitionRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "WorkflowDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Revision",
                table: "WorkflowDefinitions");
        }
    }
}
