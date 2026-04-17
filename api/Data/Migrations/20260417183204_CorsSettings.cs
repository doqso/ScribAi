using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScribAi.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CorsSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowAnyOrigin",
                table: "global_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string[]>(
                name: "AllowedOrigins",
                table: "global_settings",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowAnyOrigin",
                table: "global_settings");

            migrationBuilder.DropColumn(
                name: "AllowedOrigins",
                table: "global_settings");
        }
    }
}
