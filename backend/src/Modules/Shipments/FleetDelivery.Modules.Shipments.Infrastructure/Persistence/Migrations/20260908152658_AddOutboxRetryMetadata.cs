using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetDelivery.Modules.Shipments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxRetryMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "attempt_count",
                schema: "shipments",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_on",
                schema: "shipments",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_unprocessed",
                schema: "shipments",
                table: "outbox_messages",
                columns: new[] { "occurred_on", "next_attempt_on" },
                filter: "processed_on IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_unprocessed",
                schema: "shipments",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "attempt_count",
                schema: "shipments",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "next_attempt_on",
                schema: "shipments",
                table: "outbox_messages");
        }
    }
}
