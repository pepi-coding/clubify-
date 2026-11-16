using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SessionContextIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TenantId_VenueId",
                schema: "app",
                table: "Sessions",
                columns: new[] { "TenantId", "VenueId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Session_Context",
                schema: "app",
                table: "Sessions",
                sql: "(\"TenantId\" IS NULL AND \"VenueId\" IS NULL) OR (\"TenantId\" IS NOT NULL AND \"VenueId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Venues_TenantId_VenueId",
                schema: "app",
                table: "Sessions",
                columns: new[] { "TenantId", "VenueId" },
                principalSchema: "app",
                principalTable: "Venues",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Venues_TenantId_VenueId",
                schema: "app",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_TenantId_VenueId",
                schema: "app",
                table: "Sessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Session_Context",
                schema: "app",
                table: "Sessions");
        }
    }
}
