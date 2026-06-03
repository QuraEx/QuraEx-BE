using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QuraEx.Authoring.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitAuthoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.CreateTable(
                name: "authoring_outbox_message",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_authoring_outbox_message", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "authoring_processed_message",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_authoring_processed_message", x => x.message_id);
                });

            migrationBuilder.CreateTable(
                name: "membership_snapshot",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_membership_snapshot", x => new { x.project_id, x.user_id });
                });

            migrationBuilder.CreateTable(
                name: "user_story",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    as_a = table.Column<string>(type: "text", nullable: true),
                    i_want_to = table.Column<string>(type: "text", nullable: true),
                    so_that = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    external_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_story", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "acceptance_criteria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_story_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content = table.Column<string>(type: "text", nullable: false),
                    order_no = table.Column<int>(type: "integer", nullable: false),
                    completed = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acceptance_criteria", x => x.id);
                    table.ForeignKey(
                        name: "fk_acceptance_criteria_acceptance_criteria_parent_id",
                        column: x => x.parent_id,
                        principalTable: "acceptance_criteria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_acceptance_criteria_user_stories_user_story_id",
                        column: x => x.user_story_id,
                        principalTable: "user_story",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "business_rule",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_story_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_business_rule", x => x.id);
                    table.ForeignKey(
                        name: "fk_business_rule_user_stories_user_story_id",
                        column: x => x.user_story_id,
                        principalTable: "user_story",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_acceptance_criteria_parent_id",
                table: "acceptance_criteria",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_acceptance_criteria_user_story_id",
                table: "acceptance_criteria",
                column: "user_story_id");

            migrationBuilder.CreateIndex(
                name: "ix_authoring_outbox_message_processed_at",
                table: "authoring_outbox_message",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "ix_authoring_processed_message_processed_at",
                table: "authoring_processed_message",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "ix_business_rule_user_story_id",
                table: "business_rule",
                column: "user_story_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acceptance_criteria");

            migrationBuilder.DropTable(
                name: "authoring_outbox_message");

            migrationBuilder.DropTable(
                name: "authoring_processed_message");

            migrationBuilder.DropTable(
                name: "business_rule");

            migrationBuilder.DropTable(
                name: "membership_snapshot");

            migrationBuilder.DropTable(
                name: "user_story");
        }
    }
}
