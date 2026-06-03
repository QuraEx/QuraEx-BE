using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QuraEx.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "identity_outbox_message",
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
                    table.PrimaryKey("pk_identity_outbox_message", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "identity_processed_message",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identity_processed_message", x => x.message_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_identity_outbox_message_processed_at",
                table: "identity_outbox_message",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "ix_identity_processed_message_processed_at",
                table: "identity_processed_message",
                column: "processed_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "identity_outbox_message");

            migrationBuilder.DropTable(
                name: "identity_processed_message");
        }
    }
}
