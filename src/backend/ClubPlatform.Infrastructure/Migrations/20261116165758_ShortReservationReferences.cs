using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ShortReservationReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "OrderReferenceNumbers",
                schema: "app",
                maxValue: 1099511627775L);

            migrationBuilder.Sql("""
                CREATE FUNCTION app.next_order_reference() RETURNS text
                LANGUAGE plpgsql VOLATILE AS $code$
                DECLARE
                    alphabet constant text := '23456789ABCDEFGHJKLMNPQRSTUVWXYZ';
                    value bigint;
                    result text := '';
                    i integer;
                BEGIN
                    -- Odd multiplier is a bijection modulo 2^40; no random collisions.
                    -- Sequence is non-cycling. Arithmetic stays within signed bigint.
                    value := (nextval('app."OrderReferenceNumbers"'::regclass) * 7431887 + 604462909807) % 1099511627776;
                    FOR i IN 1..8 LOOP
                        result := substr(alphabet, (value % 32)::integer + 1, 1) || result;
                        value := value / 32;
                    END LOOP;
                    RETURN substr(result, 1, 4) || '-' || substr(result, 5, 4);
                END
                $code$;
                """);
            migrationBuilder.AddColumn<string>(
                name: "Reference",
                schema: "app",
                table: "Order",
                type: "character varying(9)",
                maxLength: 9,
                nullable: false,
                defaultValueSql: "app.next_order_reference()");

            migrationBuilder.CreateIndex(
                name: "IX_Order_Reference",
                schema: "app",
                table: "Order",
                column: "Reference",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Order_Reference",
                schema: "app",
                table: "Order",
                sql: "\"Reference\" ~ '^[2-9A-HJ-NP-Z]{4}-[2-9A-HJ-NP-Z]{4}$'");
            // Preserve replay consistency for reservations created before this migration.
            migrationBuilder.Sql("""
                UPDATE app."IdempotencyRecord" AS i
                SET "ResultJson" = jsonb_set(
                    jsonb_set(i."ResultJson", '{payment,reference}', to_jsonb(o."Reference")),
                    '{payment,whatsAppUrl}',
                    COALESCE(to_jsonb(replace(i."ResultJson" #>> '{payment,whatsAppUrl}', o."Id"::text, o."Reference")), 'null'::jsonb))
                FROM app."Order" AS o
                WHERE i."Operation" = 'whatsapp-reservation'
                  AND i."TenantId" = o."TenantId" AND i."EventId" = o."EventId"
                  AND i."ResultJson" ->> 'orderId' = o."Id"::text
                  AND jsonb_typeof(i."ResultJson" -> 'payment') = 'object';
                UPDATE app."IdempotencyRecord" AS i
                SET "ResultJson" = jsonb_set(i."ResultJson", '{reference}', to_jsonb(o."Reference"))
                FROM app."Order" AS o
                WHERE i."Operation" IN ('order','payment','cash','complimentary','manual-verification')
                  AND i."TenantId" = o."TenantId" AND i."EventId" = o."EventId"
                  AND i."ResultJson" ->> 'id' = o."Id"::text;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Order_Reference",
                schema: "app",
                table: "Order");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Order_Reference",
                schema: "app",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "Reference",
                schema: "app",
                table: "Order");

            migrationBuilder.Sql("DROP FUNCTION app.next_order_reference()");

            migrationBuilder.DropSequence(
                name: "OrderReferenceNumbers",
                schema: "app");
        }
    }
}
