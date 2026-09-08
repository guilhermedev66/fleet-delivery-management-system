using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetDelivery.Modules.Shipments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignedVehicleToShipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "assigned_vehicle_id",
                schema: "shipments",
                table: "shipments",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "assigned_vehicle_id",
                schema: "shipments",
                table: "shipments");
        }
    }
}
