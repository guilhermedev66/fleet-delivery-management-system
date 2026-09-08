using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetDelivery.Modules.Shipments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_shipments_status",
                schema: "shipments",
                table: "shipments",
                sql: "status IN ('Draft', 'ReadyForDispatch', 'Assigned', 'PickedUp', 'InTransit', 'OutForDelivery', 'Delivered', 'DeliveryFailed', 'Rescheduled', 'Returned', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_delivery_attempts_outcome",
                schema: "shipments",
                table: "delivery_attempts",
                sql: "outcome IN ('Failed', 'Successful')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_shipments_status",
                schema: "shipments",
                table: "shipments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_delivery_attempts_outcome",
                schema: "shipments",
                table: "delivery_attempts");
        }
    }
}
