using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CommerceReservationsAndIssuance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EventResources_TenantId_VenueId_EventId",
                schema: "app",
                table: "EventResources");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EventResources_TenantId_VenueId_EventId_Id",
                schema: "app",
                table: "EventResources",
                columns: new[] { "TenantId", "VenueId", "EventId", "Id" });

            migrationBuilder.CreateTable(
                name: "AuditEntry",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntry", x => x.Id);
                    table.UniqueConstraint("AK_AuditEntry_TenantId_VenueId_EventId_Id", x => new { x.TenantId, x.VenueId, x.EventId, x.Id });
                    table.ForeignKey(
                        name: "FK_AuditEntry_Events_TenantId_VenueId_EventId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId },
                        principalSchema: "app",
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventInventory",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Limit = table.Column<int>(type: "integer", nullable: false),
                    Held = table.Column<int>(type: "integer", nullable: false),
                    Sold = table.Column<int>(type: "integer", nullable: false),
                    Blocked = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventInventory", x => x.Id);
                    table.UniqueConstraint("AK_EventInventory_TenantId_VenueId_EventId_Id", x => new { x.TenantId, x.VenueId, x.EventId, x.Id });
                    table.CheckConstraint("CK_Inventory_Counts", "\"Limit\" >= 0 AND \"Held\" >= 0 AND \"Sold\" >= 0 AND \"Held\" + \"Sold\" <= \"Limit\"");
                    table.ForeignKey(
                        name: "FK_EventInventory_EventResources_TenantId_VenueId_EventId_Reso~",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId, x.ResourceId },
                        principalSchema: "app",
                        principalTable: "EventResources",
                        principalColumns: new[] { "TenantId", "VenueId", "EventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventInventory_Events_TenantId_VenueId_EventId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId },
                        principalSchema: "app",
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Hold",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TotalCents = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hold", x => x.Id);
                    table.UniqueConstraint("AK_Hold_TenantId_VenueId_EventId_Id", x => new { x.TenantId, x.VenueId, x.EventId, x.Id });
                    table.CheckConstraint("CK_Hold_State", "\"Status\" IN ('Active','Converted','Expired','Cancelled') AND \"TotalCents\" >= 0");
                    table.ForeignKey(
                        name: "FK_Hold_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "app",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Hold_Events_TenantId_VenueId_EventId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId },
                        principalSchema: "app",
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecord",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    Operation = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    BodyHash = table.Column<string>(type: "text", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecord", x => x.Id);
                    table.UniqueConstraint("AK_IdempotencyRecord_TenantId_VenueId_EventId_Id", x => new { x.TenantId, x.VenueId, x.EventId, x.Id });
                    table.ForeignKey(
                        name: "FK_IdempotencyRecord_Events_TenantId_VenueId_EventId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId },
                        principalSchema: "app",
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Offer",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Archived = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Offer", x => x.Id);
                    table.UniqueConstraint("AK_Offer_TenantId_VenueId_EventId_Id", x => new { x.TenantId, x.VenueId, x.EventId, x.Id });
                    table.ForeignKey(
                        name: "FK_Offer_Events_TenantId_VenueId_EventId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId },
                        principalSchema: "app",
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Product",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Product", x => x.Id);
                    table.UniqueConstraint("AK_Product_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Product_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "app",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryAllocation",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryAllocation", x => x.Id);
                    table.UniqueConstraint("AK_InventoryAllocation_TenantId_VenueId_EventId_Id", x => new { x.TenantId, x.VenueId, x.EventId, x.Id });
                    table.CheckConstraint("CK_Allocation", "\"Quantity\" > 0 AND \"Status\" IN ('Held','Sold','Released')");
                    table.ForeignKey(
                        name: "FK_InventoryAllocation_EventInventory_TenantId_VenueId_EventId~",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId, x.InventoryId },
                        principalSchema: "app",
                        principalTable: "EventInventory",
                        principalColumns: new[] { "TenantId", "VenueId", "EventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryAllocation_Events_TenantId_VenueId_EventId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId },
                        principalSchema: "app",
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryAllocation_Hold_TenantId_VenueId_EventId_HoldId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId, x.HoldId },
                        principalSchema: "app",
                        principalTable: "Hold",
                        principalColumns: new[] { "TenantId", "VenueId", "EventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Order",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    TotalCents = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Order", x => x.Id);
                    table.UniqueConstraint("AK_Order_TenantId_VenueId_EventId_Id", x => new { x.TenantId, x.VenueId, x.EventId, x.Id });
                    table.CheckConstraint("CK_Order_State", "\"Status\" IN ('PendingPayment','Confirmed','Expired','Cancelled','PaymentReview') AND \"TotalCents\" >= 0");
                    table.ForeignKey(
                        name: "FK_Order_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "app",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Order_Events_TenantId_VenueId_EventId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId },
                        principalSchema: "app",
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Order_Hold_TenantId_VenueId_EventId_HoldId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId, x.HoldId },
                        principalSchema: "app",
                        principalTable: "Hold",
                        principalColumns: new[] { "TenantId", "VenueId", "EventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OfferVersion",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PriceCents = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceUnits = table.Column<int>(type: "integer", nullable: false),
                    Admissions = table.Column<int>(type: "integer", nullable: false),
                    DrinksJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferVersion", x => x.Id);
                    table.UniqueConstraint("AK_OfferVersion_TenantId_VenueId_EventId_Id", x => new { x.TenantId, x.VenueId, x.EventId, x.Id });
                    table.CheckConstraint("CK_Offer_Amounts", "\"PriceCents\" BETWEEN 0 AND 100000000 AND \"Admissions\" BETWEEN 0 AND 1000 AND \"ResourceUnits\" BETWEEN 0 AND 1000 AND \"Currency\" = 'PEN'");
                    table.ForeignKey(
                        name: "FK_OfferVersion_EventResources_TenantId_VenueId_EventId_Resour~",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId, x.ResourceId },
                        principalSchema: "app",
                        principalTable: "EventResources",
                        principalColumns: new[] { "TenantId", "VenueId", "EventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfferVersion_Events_TenantId_VenueId_EventId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId },
                        principalSchema: "app",
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfferVersion_Offer_TenantId_VenueId_EventId_OfferId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId, x.OfferId },
                        principalSchema: "app",
                        principalTable: "Offer",
                        principalColumns: new[] { "TenantId", "VenueId", "EventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsumptionVoucher",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    EncryptedToken = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumptionVoucher", x => x.Id);
                    table.UniqueConstraint("AK_ConsumptionVoucher_TenantId_VenueId_EventId_Id", x => new { x.TenantId, x.VenueId, x.EventId, x.Id });
                    table.ForeignKey(
                        name: "FK_ConsumptionVoucher_Events_TenantId_VenueId_EventId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId },
                        principalSchema: "app",
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsumptionVoucher_Order_TenantId_VenueId_EventId_OrderId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId, x.OrderId },
                        principalSchema: "app",
                        principalTable: "Order",
                        principalColumns: new[] { "TenantId", "VenueId", "EventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAttempt",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AmountCents = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAttempt", x => x.Id);
                    table.UniqueConstraint("AK_PaymentAttempt_TenantId_VenueId_EventId_Id", x => new { x.TenantId, x.VenueId, x.EventId, x.Id });
                    table.ForeignKey(
                        name: "FK_PaymentAttempt_Events_TenantId_VenueId_EventId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId },
                        principalSchema: "app",
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentAttempt_Order_TenantId_VenueId_EventId_OrderId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId, x.OrderId },
                        principalSchema: "app",
                        principalTable: "Order",
                        principalColumns: new[] { "TenantId", "VenueId", "EventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HoldLine",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoldLine", x => x.Id);
                    table.UniqueConstraint("AK_HoldLine_TenantId_VenueId_EventId_Id", x => new { x.TenantId, x.VenueId, x.EventId, x.Id });
                    table.CheckConstraint("CK_HoldLine_Quantity", "\"Quantity\" BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "FK_HoldLine_Events_TenantId_VenueId_EventId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId },
                        principalSchema: "app",
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HoldLine_Hold_TenantId_VenueId_EventId_HoldId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId, x.HoldId },
                        principalSchema: "app",
                        principalTable: "Hold",
                        principalColumns: new[] { "TenantId", "VenueId", "EventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HoldLine_OfferVersion_TenantId_VenueId_EventId_OfferVersion~",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId, x.OfferVersionId },
                        principalSchema: "app",
                        principalTable: "OfferVersion",
                        principalColumns: new[] { "TenantId", "VenueId", "EventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderLine",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    UnitPriceCents = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Admissions = table.Column<int>(type: "integer", nullable: false),
                    Zone = table.Column<string>(type: "text", nullable: false),
                    DrinksJson = table.Column<string>(type: "jsonb", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLine", x => x.Id);
                    table.UniqueConstraint("AK_OrderLine_TenantId_VenueId_EventId_Id", x => new { x.TenantId, x.VenueId, x.EventId, x.Id });
                    table.ForeignKey(
                        name: "FK_OrderLine_Events_TenantId_VenueId_EventId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId },
                        principalSchema: "app",
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderLine_OfferVersion_TenantId_VenueId_EventId_OfferVersio~",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId, x.OfferVersionId },
                        principalSchema: "app",
                        principalTable: "OfferVersion",
                        principalColumns: new[] { "TenantId", "VenueId", "EventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderLine_Order_TenantId_VenueId_EventId_OrderId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId, x.OrderId },
                        principalSchema: "app",
                        principalTable: "Order",
                        principalColumns: new[] { "TenantId", "VenueId", "EventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsumptionEntitlement",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Granted = table.Column<int>(type: "integer", nullable: false),
                    Delivered = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumptionEntitlement", x => x.Id);
                    table.UniqueConstraint("AK_ConsumptionEntitlement_TenantId_VenueId_EventId_Id", x => new { x.TenantId, x.VenueId, x.EventId, x.Id });
                    table.CheckConstraint("CK_Entitlement", "\"Granted\" > 0 AND \"Delivered\" >= 0 AND \"Delivered\" <= \"Granted\"");
                    table.ForeignKey(
                        name: "FK_ConsumptionEntitlement_ConsumptionVoucher_TenantId_VenueId_~",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId, x.VoucherId },
                        principalSchema: "app",
                        principalTable: "ConsumptionVoucher",
                        principalColumns: new[] { "TenantId", "VenueId", "EventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsumptionEntitlement_Events_TenantId_VenueId_EventId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId },
                        principalSchema: "app",
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsumptionEntitlement_Product_TenantId_ProductId",
                        columns: x => new { x.TenantId, x.ProductId },
                        principalSchema: "app",
                        principalTable: "Product",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ticket",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Zone = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    EncryptedToken = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ticket", x => x.Id);
                    table.UniqueConstraint("AK_Ticket_TenantId_VenueId_EventId_Id", x => new { x.TenantId, x.VenueId, x.EventId, x.Id });
                    table.ForeignKey(
                        name: "FK_Ticket_Events_TenantId_VenueId_EventId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId },
                        principalSchema: "app",
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ticket_OrderLine_TenantId_VenueId_EventId_OrderLineId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId, x.OrderLineId },
                        principalSchema: "app",
                        principalTable: "OrderLine",
                        principalColumns: new[] { "TenantId", "VenueId", "EventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ticket_Order_TenantId_VenueId_EventId_OrderId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId, x.OrderId },
                        principalSchema: "app",
                        principalTable: "Order",
                        principalColumns: new[] { "TenantId", "VenueId", "EventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntry_TenantId_EventId",
                schema: "app",
                table: "AuditEntry",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumptionEntitlement_TenantId_EventId",
                schema: "app",
                table: "ConsumptionEntitlement",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumptionEntitlement_TenantId_ProductId",
                schema: "app",
                table: "ConsumptionEntitlement",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumptionEntitlement_TenantId_VenueId_EventId_VoucherId",
                schema: "app",
                table: "ConsumptionEntitlement",
                columns: new[] { "TenantId", "VenueId", "EventId", "VoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumptionEntitlement_VoucherId_ProductId",
                schema: "app",
                table: "ConsumptionEntitlement",
                columns: new[] { "VoucherId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsumptionVoucher_OrderId",
                schema: "app",
                table: "ConsumptionVoucher",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsumptionVoucher_TenantId_EventId",
                schema: "app",
                table: "ConsumptionVoucher",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumptionVoucher_TenantId_VenueId_EventId_OrderId",
                schema: "app",
                table: "ConsumptionVoucher",
                columns: new[] { "TenantId", "VenueId", "EventId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumptionVoucher_TokenHash",
                schema: "app",
                table: "ConsumptionVoucher",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventInventory_EventId_ResourceId",
                schema: "app",
                table: "EventInventory",
                columns: new[] { "EventId", "ResourceId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_EventInventory_TenantId_EventId",
                schema: "app",
                table: "EventInventory",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_EventInventory_TenantId_VenueId_EventId_ResourceId",
                schema: "app",
                table: "EventInventory",
                columns: new[] { "TenantId", "VenueId", "EventId", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Hold_Status_ExpiresAt",
                schema: "app",
                table: "Hold",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Hold_TenantId_EventId",
                schema: "app",
                table: "Hold",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_Hold_UserId",
                schema: "app",
                table: "Hold",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HoldLine_TenantId_EventId",
                schema: "app",
                table: "HoldLine",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_HoldLine_TenantId_VenueId_EventId_HoldId",
                schema: "app",
                table: "HoldLine",
                columns: new[] { "TenantId", "VenueId", "EventId", "HoldId" });

            migrationBuilder.CreateIndex(
                name: "IX_HoldLine_TenantId_VenueId_EventId_OfferVersionId",
                schema: "app",
                table: "HoldLine",
                columns: new[] { "TenantId", "VenueId", "EventId", "OfferVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecord_ActorId_TenantId_Operation_Key",
                schema: "app",
                table: "IdempotencyRecord",
                columns: new[] { "ActorId", "TenantId", "Operation", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecord_TenantId_EventId",
                schema: "app",
                table: "IdempotencyRecord",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAllocation_HoldId_InventoryId",
                schema: "app",
                table: "InventoryAllocation",
                columns: new[] { "HoldId", "InventoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAllocation_TenantId_EventId",
                schema: "app",
                table: "InventoryAllocation",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAllocation_TenantId_VenueId_EventId_HoldId",
                schema: "app",
                table: "InventoryAllocation",
                columns: new[] { "TenantId", "VenueId", "EventId", "HoldId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAllocation_TenantId_VenueId_EventId_InventoryId",
                schema: "app",
                table: "InventoryAllocation",
                columns: new[] { "TenantId", "VenueId", "EventId", "InventoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Offer_TenantId_EventId",
                schema: "app",
                table: "Offer",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_OfferVersion_OfferId_Number",
                schema: "app",
                table: "OfferVersion",
                columns: new[] { "OfferId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfferVersion_TenantId_EventId",
                schema: "app",
                table: "OfferVersion",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_OfferVersion_TenantId_VenueId_EventId_OfferId",
                schema: "app",
                table: "OfferVersion",
                columns: new[] { "TenantId", "VenueId", "EventId", "OfferId" });

            migrationBuilder.CreateIndex(
                name: "IX_OfferVersion_TenantId_VenueId_EventId_ResourceId",
                schema: "app",
                table: "OfferVersion",
                columns: new[] { "TenantId", "VenueId", "EventId", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Order_HoldId",
                schema: "app",
                table: "Order",
                column: "HoldId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Order_TenantId_EventId",
                schema: "app",
                table: "Order",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_Order_TenantId_VenueId_EventId_HoldId",
                schema: "app",
                table: "Order",
                columns: new[] { "TenantId", "VenueId", "EventId", "HoldId" });

            migrationBuilder.CreateIndex(
                name: "IX_Order_UserId",
                schema: "app",
                table: "Order",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLine_TenantId_EventId",
                schema: "app",
                table: "OrderLine",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderLine_TenantId_VenueId_EventId_OfferVersionId",
                schema: "app",
                table: "OrderLine",
                columns: new[] { "TenantId", "VenueId", "EventId", "OfferVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderLine_TenantId_VenueId_EventId_OrderId",
                schema: "app",
                table: "OrderLine",
                columns: new[] { "TenantId", "VenueId", "EventId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempt_Reference",
                schema: "app",
                table: "PaymentAttempt",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempt_TenantId_EventId",
                schema: "app",
                table: "PaymentAttempt",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempt_TenantId_VenueId_EventId_OrderId",
                schema: "app",
                table: "PaymentAttempt",
                columns: new[] { "TenantId", "VenueId", "EventId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_OrderId_OrderLineId_Ordinal",
                schema: "app",
                table: "Ticket",
                columns: new[] { "OrderId", "OrderLineId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_TenantId_EventId",
                schema: "app",
                table: "Ticket",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_TenantId_VenueId_EventId_OrderId",
                schema: "app",
                table: "Ticket",
                columns: new[] { "TenantId", "VenueId", "EventId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_TenantId_VenueId_EventId_OrderLineId",
                schema: "app",
                table: "Ticket",
                columns: new[] { "TenantId", "VenueId", "EventId", "OrderLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_TokenHash",
                schema: "app",
                table: "Ticket",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntry",
                schema: "app");

            migrationBuilder.DropTable(
                name: "ConsumptionEntitlement",
                schema: "app");

            migrationBuilder.DropTable(
                name: "HoldLine",
                schema: "app");

            migrationBuilder.DropTable(
                name: "IdempotencyRecord",
                schema: "app");

            migrationBuilder.DropTable(
                name: "InventoryAllocation",
                schema: "app");

            migrationBuilder.DropTable(
                name: "PaymentAttempt",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Ticket",
                schema: "app");

            migrationBuilder.DropTable(
                name: "ConsumptionVoucher",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Product",
                schema: "app");

            migrationBuilder.DropTable(
                name: "EventInventory",
                schema: "app");

            migrationBuilder.DropTable(
                name: "OrderLine",
                schema: "app");

            migrationBuilder.DropTable(
                name: "OfferVersion",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Order",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Offer",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Hold",
                schema: "app");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EventResources_TenantId_VenueId_EventId_Id",
                schema: "app",
                table: "EventResources");

            migrationBuilder.CreateIndex(
                name: "IX_EventResources_TenantId_VenueId_EventId",
                schema: "app",
                table: "EventResources",
                columns: new[] { "TenantId", "VenueId", "EventId" });
        }
    }
}
