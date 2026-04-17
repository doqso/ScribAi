using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScribAi.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationNameToGlobalSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicationName",
                table: "global_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "ScribAi");

            migrationBuilder.Sql("UPDATE global_settings SET \"ApplicationName\" = 'ScribAi' WHERE \"ApplicationName\" = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicationName",
                table: "global_settings");
        }
    }
}
