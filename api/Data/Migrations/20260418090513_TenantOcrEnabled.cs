using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScribAi.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TenantOcrEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OcrEnabled",
                table: "tenant_settings",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OcrEnabled",
                table: "tenant_settings");
        }
    }
}
