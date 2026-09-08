using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetDelivery.Modules.Vehicles.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTypeAndStatusCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicles_status",
                schema: "vehicles",
                table: "vehicles",
                sql: "status IN ('Active', 'Maintenance', 'Retired')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicles_type",
                schema: "vehicles",
                table: "vehicles",
                sql: "type IN ('Van', 'Truck', 'Motorcycle', 'Car')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicles_status",
                schema: "vehicles",
                table: "vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicles_type",
                schema: "vehicles",
                table: "vehicles");
        }
    }
}
