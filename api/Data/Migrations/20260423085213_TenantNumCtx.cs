using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScribAi.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TenantNumCtx : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NumCtx",
                table: "tenant_settings",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NumCtx",
                table: "tenant_settings");
        }
    }
}
