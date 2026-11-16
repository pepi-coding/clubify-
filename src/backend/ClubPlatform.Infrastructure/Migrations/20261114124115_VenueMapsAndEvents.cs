using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VenueMapsAndEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Archived",
                schema: "app",
                table: "Venues",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                schema: "app",
                table: "Venues",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.CreateTable(
                name: "LayoutTemplates",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LayoutTemplates", x => x.Id);
                    table.UniqueConstraint("AK_LayoutTemplates_TenantId_VenueId_Id", x => new { x.TenantId, x.VenueId, x.Id });
                    table.ForeignKey(
                        name: "FK_LayoutTemplates_Venues_TenantId_VenueId",
                        columns: x => new { x.TenantId, x.VenueId },
                        principalSchema: "app",
                        principalTable: "Venues",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LayoutElements",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    GeometryJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LayoutElements", x => new { x.TemplateId, x.Id });
                    table.CheckConstraint("CK_LayoutElement_Mode", "(\"Mode\" = 'Decoration' AND \"Capacity\" = 0) OR (\"Mode\" IN ('Exclusive','Capacity') AND \"Capacity\" > 0)");
                    table.ForeignKey(
                        name: "FK_LayoutElements_LayoutTemplates_TenantId_VenueId_TemplateId",
                        columns: x => new { x.TenantId, x.VenueId, x.TemplateId },
                        principalSchema: "app",
                        principalTable: "LayoutTemplates",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LayoutVersions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    SourceRevision = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ElementsJson = table.Column<string>(type: "jsonb", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LayoutVersions", x => x.Id);
                    table.UniqueConstraint("AK_LayoutVersions_TenantId_VenueId_Id", x => new { x.TenantId, x.VenueId, x.Id });
                    table.ForeignKey(
                        name: "FK_LayoutVersions_LayoutTemplates_TenantId_VenueId_TemplateId",
                        columns: x => new { x.TenantId, x.VenueId, x.TemplateId },
                        principalSchema: "app",
                        principalTable: "LayoutTemplates",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EntryOpensAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EntryClosesAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RedemptionOpensAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RedemptionClosesAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LayoutVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    MapJson = table.Column<string>(type: "jsonb", nullable: false),
                    VenueName = table.Column<string>(type: "text", nullable: false),
                    TimeZone = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.UniqueConstraint("AK_Events_TenantId_VenueId_Id", x => new { x.TenantId, x.VenueId, x.Id });
                    table.CheckConstraint("CK_Event_Capacity", "\"Capacity\" > 0");
                    table.CheckConstraint("CK_Event_PublishedVersion", "\"Status\" <> 'Published' OR \"LayoutVersionId\" IS NOT NULL");
                    table.CheckConstraint("CK_Event_Status", "\"Status\" IN ('Draft','Published')");
                    table.CheckConstraint("CK_Event_Times", "\"StartsAt\" < \"EndsAt\" AND \"EntryOpensAt\" < \"EntryClosesAt\" AND \"EntryClosesAt\" >= \"StartsAt\" AND \"EntryClosesAt\" <= \"EndsAt\" AND \"RedemptionOpensAt\" >= \"StartsAt\" AND \"RedemptionOpensAt\" < \"RedemptionClosesAt\" AND \"RedemptionClosesAt\" <= \"EndsAt\"");
                    table.ForeignKey(
                        name: "FK_Events_LayoutVersions_TenantId_VenueId_LayoutVersionId",
                        columns: x => new { x.TenantId, x.VenueId, x.LayoutVersionId },
                        principalSchema: "app",
                        principalTable: "LayoutVersions",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Venues_TenantId_VenueId",
                        columns: x => new { x.TenantId, x.VenueId },
                        principalSchema: "app",
                        principalTable: "Venues",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventResources",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceElementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    HumanCapacity = table.Column<int>(type: "integer", nullable: false),
                    AllocationLimit = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventResources", x => x.Id);
                    table.CheckConstraint("CK_Resource_Limits", "\"HumanCapacity\" > 0 AND ((\"Mode\" = 'Exclusive' AND \"AllocationLimit\" = 1) OR (\"Mode\" = 'Capacity' AND \"AllocationLimit\" = \"HumanCapacity\"))");
                    table.ForeignKey(
                        name: "FK_EventResources_Events_TenantId_VenueId_EventId",
                        columns: x => new { x.TenantId, x.VenueId, x.EventId },
                        principalSchema: "app",
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "VenueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Venue_Capacity",
                schema: "app",
                table: "Venues",
                sql: "\"Capacity\" > 0 AND \"Capacity\" <= 100000");

            migrationBuilder.CreateIndex(
                name: "IX_EventResources_EventId_SourceElementId",
                schema: "app",
                table: "EventResources",
                columns: new[] { "EventId", "SourceElementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventResources_TenantId_VenueId_EventId",
                schema: "app",
                table: "EventResources",
                columns: new[] { "TenantId", "VenueId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_TenantId_VenueId_LayoutVersionId",
                schema: "app",
                table: "Events",
                columns: new[] { "TenantId", "VenueId", "LayoutVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_LayoutElements_TenantId_VenueId_TemplateId",
                schema: "app",
                table: "LayoutElements",
                columns: new[] { "TenantId", "VenueId", "TemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_LayoutVersions_TemplateId_Number",
                schema: "app",
                table: "LayoutVersions",
                columns: new[] { "TemplateId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LayoutVersions_TemplateId_SourceRevision",
                schema: "app",
                table: "LayoutVersions",
                columns: new[] { "TemplateId", "SourceRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LayoutVersions_TenantId_VenueId_TemplateId",
                schema: "app",
                table: "LayoutVersions",
                columns: new[] { "TenantId", "VenueId", "TemplateId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventResources",
                schema: "app");

            migrationBuilder.DropTable(
                name: "LayoutElements",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Events",
                schema: "app");

            migrationBuilder.DropTable(
                name: "LayoutVersions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "LayoutTemplates",
                schema: "app");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Venue_Capacity",
                schema: "app",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "Archived",
                schema: "app",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "Capacity",
                schema: "app",
                table: "Venues");
        }
    }
}
