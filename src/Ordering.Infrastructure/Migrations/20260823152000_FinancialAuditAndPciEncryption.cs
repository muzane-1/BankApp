using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ordering.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinancialAuditAndPciEncryption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PCI-DSS: widen PAN column for AES-256 ciphertext and persist the
            // (redacted) security code column used by the payment aggregate.
            migrationBuilder.AlterColumn<string>(
                name: "CardNumber",
                schema: "ordering",
                table: "paymentmethods",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(25)",
                oldMaxLength: 25);

            migrationBuilder.AddColumn<string>(
                name: "SecurityNumber",
                schema: "ordering",
                table: "paymentmethods",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            // AML/PSD2: immutable, append-only, hash-chained financial audit trail.
            migrationBuilder.CreateTable(
                name: "financial_audit_events",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Iso20022MessageId = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: false),
                    PreviousHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_audit_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_financial_audit_events_CorrelationId",
                schema: "ordering",
                table: "financial_audit_events",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_audit_events_OccurredAtUtc",
                schema: "ordering",
                table: "financial_audit_events",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_financial_audit_events_Subject",
                schema: "ordering",
                table: "financial_audit_events",
                column: "Subject");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financial_audit_events",
                schema: "ordering");

            migrationBuilder.DropColumn(
                name: "SecurityNumber",
                schema: "ordering",
                table: "paymentmethods");

            migrationBuilder.AlterColumn<string>(
                name: "CardNumber",
                schema: "ordering",
                table: "paymentmethods",
                type: "character varying(25)",
                maxLength: 25,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);
        }
    }
}
