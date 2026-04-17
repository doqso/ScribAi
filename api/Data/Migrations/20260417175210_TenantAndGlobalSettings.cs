using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScribAi.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TenantAndGlobalSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "global_settings",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    SeqEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SeqUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SeqApiKeyEncrypted = table.Column<byte[]>(type: "bytea", nullable: true),
                    SeqMinimumLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_global_settings", x => x.Id);
                    table.CheckConstraint("ck_global_settings_singleton", "\"Id\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "tenant_settings",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultTextModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VisionModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OllamaTimeoutSeconds = table.Column<int>(type: "integer", nullable: true),
                    WebhookMaxAttempts = table.Column<int>(type: "integer", nullable: true),
                    WebhookTimeoutSeconds = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_settings", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_tenant_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(@"
                INSERT INTO global_settings (""Id"", ""SeqEnabled"", ""SeqMinimumLevel"", ""UpdatedAt"")
                VALUES (1, false, 'Information', now())
                ON CONFLICT (""Id"") DO NOTHING;");

            migrationBuilder.Sql(@"
                INSERT INTO tenant_settings (""TenantId"", ""UpdatedAt"")
                SELECT ""Id"", now() FROM tenants
                ON CONFLICT (""TenantId"") DO NOTHING;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "global_settings");

            migrationBuilder.DropTable(
                name: "tenant_settings");
        }
    }
}
