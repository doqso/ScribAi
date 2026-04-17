using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScribAi.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SchemaScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_schemas_TenantId_Name_Version",
                table: "schemas");

            migrationBuilder.AddColumn<Guid>(
                name: "ApiKeyId",
                table: "schemas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "schemas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_schemas_TenantId_ApiKeyId_Name_Version",
                table: "schemas",
                columns: new[] { "TenantId", "ApiKeyId", "Name", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_schemas_TenantId_ApiKeyId_Name_Version",
                table: "schemas");

            migrationBuilder.DropColumn(
                name: "ApiKeyId",
                table: "schemas");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "schemas");

            migrationBuilder.CreateIndex(
                name: "IX_schemas_TenantId_Name_Version",
                table: "schemas",
                columns: new[] { "TenantId", "Name", "Version" },
                unique: true);
        }
    }
}
