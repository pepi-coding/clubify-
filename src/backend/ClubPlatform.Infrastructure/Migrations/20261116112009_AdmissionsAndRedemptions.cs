using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdmissionsAndRedemptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ticket_TenantId_VenueId_EventId_OrderId",
                schema: "app",
                table: "Ticket");

            migrationBuilder.DropIndex(
                name: "IX_ConsumptionVoucher_TenantId_VenueId_EventId_OrderId",
                schema: "app",
                table: "ConsumptionVoucher");

            migrationBuilder.DropIndex(
                name: "IX_ConsumptionEntitlement_TenantId_VenueId_EventId_VoucherId",
                schema: "app",
                table: "ConsumptionEntitlement");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "app",
                table: "ConsumptionVoucher",
                type: "text",
                nullable: false,
                defaultValue: "Granted");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Ticket_TenantId_VenueId_EventId_OrderId_Id",
                schema: "app",
                table: "Ticket",
                columns: new[] { "TenantId", "VenueId", "EventId", "OrderId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ConsumptionVoucher_TenantId_VenueId_EventId_OrderId_Id",
                schema: "app",
                table: "ConsumptionVoucher",
                columns: new[] { "TenantId", "VenueId", "EventId", "OrderId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ConsumptionEntitlement_TenantId_VenueId_EventId_VoucherId_Id",
                schema: "app",
                table: "ConsumptionEntitlement",
                columns: new[] { "TenantId", "VenueId", "EventId", "VoucherId", "Id" });

            migrationBuilder.CreateTable(
                name: "AdmissionLog",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    Station = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AdmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionLog", x => x.Id);
                    table.UniqueConstraint("AK_AdmissionLog_TenantId_VenueId_EventId_Id", x => new { x.TenantId, x.VenueId, x.EventId, x.Id });
                    table.ForeignKey(
                        name: "FK_AdmissionLog_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalSchema: "app",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdmissionLog_Events_TenantId_VenueId_EventId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId },
                        principalSchema: "app",
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdmissionLog_Ticket_TenantId_VenueId_EventId_OrderId_Ticket~",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId, x.OrderId, x.TicketId },
                        principalSchema: "app",
                        principalTable: "Ticket",
                        principalColumns: new[] { "TenantId", "VenueId", "EventId", "OrderId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RedemptionOperation",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    Station = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedemptionOperation", x => x.Id);
                    table.UniqueConstraint("AK_RedemptionOperation_TenantId_VenueId_EventId_Id", x => new { x.TenantId, x.VenueId, x.EventId, x.Id });
                    table.UniqueConstraint("AK_RedemptionOperation_TenantId_VenueId_EventId_VoucherId_Id", x => new { x.TenantId, x.VenueId, x.EventId, x.VoucherId, x.Id });
                    table.ForeignKey(
                        name: "FK_RedemptionOperation_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalSchema: "app",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RedemptionOperation_ConsumptionVoucher_TenantId_VenueId_Eve~",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId, x.OrderId, x.VoucherId },
                        principalSchema: "app",
                        principalTable: "ConsumptionVoucher",
                        principalColumns: new[] { "TenantId", "VenueId", "EventId", "OrderId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RedemptionOperation_Events_TenantId_VenueId_EventId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId },
                        principalSchema: "app",
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScanAttempt",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    Phase = table.Column<string>(type: "text", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanAttempt", x => x.Id);
                    table.UniqueConstraint("AK_ScanAttempt_TenantId_VenueId_EventId_Id", x => new { x.TenantId, x.VenueId, x.EventId, x.Id });
                    table.ForeignKey(
                        name: "FK_ScanAttempt_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalSchema: "app",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanAttempt_Events_TenantId_VenueId_EventId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId },
                        principalSchema: "app",
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RedemptionLine",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntitlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedemptionLine", x => x.Id);
                    table.UniqueConstraint("AK_RedemptionLine_TenantId_VenueId_EventId_Id", x => new { x.TenantId, x.VenueId, x.EventId, x.Id });
                    table.CheckConstraint("CK_RedemptionLine_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_RedemptionLine_ConsumptionEntitlement_TenantId_VenueId_Even~",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId, x.VoucherId, x.EntitlementId },
                        principalSchema: "app",
                        principalTable: "ConsumptionEntitlement",
                        principalColumns: new[] { "TenantId", "VenueId", "EventId", "VoucherId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RedemptionLine_Events_TenantId_VenueId_EventId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId },
                        principalSchema: "app",
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RedemptionLine_RedemptionOperation_TenantId_VenueId_EventId~",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId, x.VoucherId, x.OperationId },
                        principalSchema: "app",
                        principalTable: "RedemptionOperation",
                        principalColumns: new[] { "TenantId", "VenueId", "EventId", "VoucherId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ticket_State",
                schema: "app",
                table: "Ticket",
                sql: "\"Status\" IN ('Issued','Used','Revoked')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Voucher_State",
                schema: "app",
                table: "ConsumptionVoucher",
                sql: "\"Status\" IN ('Granted','PartiallyRedeemed','Redeemed','Revoked')");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionLog_ActorId",
                schema: "app",
                table: "AdmissionLog",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionLog_TenantId_EventId",
                schema: "app",
                table: "AdmissionLog",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionLog_TenantId_VenueId_EventId_OrderId_TicketId",
                schema: "app",
                table: "AdmissionLog",
                columns: new[] { "TenantId", "VenueId", "EventId", "OrderId", "TicketId" });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionLog_TicketId",
                schema: "app",
                table: "AdmissionLog",
                column: "TicketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionLine_OperationId_EntitlementId",
                schema: "app",
                table: "RedemptionLine",
                columns: new[] { "OperationId", "EntitlementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionLine_TenantId_EventId",
                schema: "app",
                table: "RedemptionLine",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionLine_TenantId_VenueId_EventId_VoucherId_Entitleme~",
                schema: "app",
                table: "RedemptionLine",
                columns: new[] { "TenantId", "VenueId", "EventId", "VoucherId", "EntitlementId" });

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionLine_TenantId_VenueId_EventId_VoucherId_Operation~",
                schema: "app",
                table: "RedemptionLine",
                columns: new[] { "TenantId", "VenueId", "EventId", "VoucherId", "OperationId" });

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionOperation_ActorId",
                schema: "app",
                table: "RedemptionOperation",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionOperation_TenantId_EventId",
                schema: "app",
                table: "RedemptionOperation",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionOperation_TenantId_VenueId_EventId_OrderId_Vouche~",
                schema: "app",
                table: "RedemptionOperation",
                columns: new[] { "TenantId", "VenueId", "EventId", "OrderId", "VoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanAttempt_ActorId",
                schema: "app",
                table: "ScanAttempt",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanAttempt_CreatedAt",
                schema: "app",
                table: "ScanAttempt",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScanAttempt_TenantId_EventId",
                schema: "app",
                table: "ScanAttempt",
                columns: new[] { "TenantId", "EventId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdmissionLog",
                schema: "app");

            migrationBuilder.DropTable(
                name: "RedemptionLine",
                schema: "app");

            migrationBuilder.DropTable(
                name: "ScanAttempt",
                schema: "app");

            migrationBuilder.DropTable(
                name: "RedemptionOperation",
                schema: "app");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Ticket_TenantId_VenueId_EventId_OrderId_Id",
                schema: "app",
                table: "Ticket");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Ticket_State",
                schema: "app",
                table: "Ticket");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ConsumptionVoucher_TenantId_VenueId_EventId_OrderId_Id",
                schema: "app",
                table: "ConsumptionVoucher");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Voucher_State",
                schema: "app",
                table: "ConsumptionVoucher");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ConsumptionEntitlement_TenantId_VenueId_EventId_VoucherId_Id",
                schema: "app",
                table: "ConsumptionEntitlement");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "app",
                table: "ConsumptionVoucher");

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_TenantId_VenueId_EventId_OrderId",
                schema: "app",
                table: "Ticket",
                columns: new[] { "TenantId", "VenueId", "EventId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumptionVoucher_TenantId_VenueId_EventId_OrderId",
                schema: "app",
                table: "ConsumptionVoucher",
                columns: new[] { "TenantId", "VenueId", "EventId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumptionEntitlement_TenantId_VenueId_EventId_VoucherId",
                schema: "app",
                table: "ConsumptionEntitlement",
                columns: new[] { "TenantId", "VenueId", "EventId", "VoucherId" });
        }
    }
}
