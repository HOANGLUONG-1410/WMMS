using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthAndPasswordResetEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_login_at",
                table: "users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "roles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by_ip",
                table: "refresh_tokens",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "replaced_by_token_hash",
                table: "refresh_tokens",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "revoked_at",
                table: "refresh_tokens",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ip_address",
                table: "audit_logs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    reset_token_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    token_hash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    otp_hash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_used = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    used_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    requested_by_ip = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.reset_token_id);
                    table.ForeignKey(
                        name: "FK_password_reset_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_created_at",
                table: "audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_entity_name_action_type",
                table: "audit_logs",
                columns: new[] { "entity_name", "action_type" });

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_token_hash",
                table: "password_reset_tokens",
                column: "token_hash");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_user_id_created_at",
                table: "password_reset_tokens",
                columns: new[] { "user_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_token_hash",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_created_at",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_entity_name_action_type",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "last_login_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "description",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "created_by_ip",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "replaced_by_token_hash",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "revoked_at",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "ip_address",
                table: "audit_logs");
        }
    }
}
