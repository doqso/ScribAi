using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScribAi.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TenantThinkSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Think",
                table: "tenant_settings",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Think",
                table: "tenant_settings");
        }
    }
}
