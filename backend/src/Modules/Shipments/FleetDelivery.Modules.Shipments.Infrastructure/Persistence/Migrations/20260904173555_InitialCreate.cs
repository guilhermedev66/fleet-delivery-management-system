using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetDelivery.Modules.Shipments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "shipments");

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "shipments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    occurred_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shipments",
                schema: "shipments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tracking_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    recipient_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    recipient_phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    origin_street = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    origin_city = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    origin_state = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    origin_postal_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    origin_country = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    destination_street = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    destination_city = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    destination_state = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    destination_postal_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    destination_country = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    assigned_driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "delivery_attempts",
                schema: "shipments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_delivery_attempts_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalSchema: "shipments",
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tracking_events",
                schema: "shipments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracking_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_tracking_events_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalSchema: "shipments",
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_attempts_shipment_id",
                schema: "shipments",
                table: "delivery_attempts",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_shipments_tracking_number",
                schema: "shipments",
                table: "shipments",
                column: "tracking_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tracking_events_shipment_id",
                schema: "shipments",
                table: "tracking_events",
                column: "shipment_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "delivery_attempts",
                schema: "shipments");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "shipments");

            migrationBuilder.DropTable(
                name: "tracking_events",
                schema: "shipments");

            migrationBuilder.DropTable(
                name: "shipments",
                schema: "shipments");
        }
    }
}
