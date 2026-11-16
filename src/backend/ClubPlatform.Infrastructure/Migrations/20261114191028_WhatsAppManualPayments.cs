using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppManualPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Membership_Role",
                schema: "app",
                table: "Memberships");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaidAt",
                schema: "app",
                table: "PaymentAttempt",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                schema: "app",
                table: "PaymentAttempt",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptReference",
                schema: "app",
                table: "PaymentAttempt",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifiedBy",
                schema: "app",
                table: "PaymentAttempt",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VenuePaymentSettings",
                schema: "app",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    WhatsAppNumber = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VenuePaymentSettings", x => new { x.TenantId, x.VenueId });
                    table.ForeignKey(
                        name: "FK_VenuePaymentSettings_Venues_TenantId_VenueId",
                        columns: x => new { x.TenantId, x.VenueId },
                        principalSchema: "app",
                        principalTable: "Venues",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempt_TenantId_PaymentMethod_ReceiptReference",
                schema: "app",
                table: "PaymentAttempt",
                columns: new[] { "TenantId", "PaymentMethod", "ReceiptReference" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Membership_Role",
                schema: "app",
                table: "Memberships",
                sql: "\"Role\" IN ('TenantAdmin','VenueManager','Cashier','Promoter','Security','BarStaff')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VenuePaymentSettings",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "IX_PaymentAttempt_TenantId_PaymentMethod_ReceiptReference",
                schema: "app",
                table: "PaymentAttempt");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Membership_Role",
                schema: "app",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                schema: "app",
                table: "PaymentAttempt");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                schema: "app",
                table: "PaymentAttempt");

            migrationBuilder.DropColumn(
                name: "ReceiptReference",
                schema: "app",
                table: "PaymentAttempt");

            migrationBuilder.DropColumn(
                name: "VerifiedBy",
                schema: "app",
                table: "PaymentAttempt");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Membership_Role",
                schema: "app",
                table: "Memberships",
                sql: "\"Role\" IN ('TenantAdmin','VenueManager','Cashier','Security','BarStaff')");
        }
    }
}
