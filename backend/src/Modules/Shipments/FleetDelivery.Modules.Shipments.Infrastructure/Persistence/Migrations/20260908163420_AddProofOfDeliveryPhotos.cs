using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetDelivery.Modules.Shipments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProofOfDeliveryPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "HasProofOfDelivery",
                schema: "shipments",
                table: "shipments",
                newName: "has_proof_of_delivery");

            migrationBuilder.CreateTable(
                name: "proof_of_delivery_photos",
                schema: "shipments",
                columns: table => new
                {
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    content_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proof_of_delivery_photos", x => x.shipment_id);
                    table.ForeignKey(
                        name: "FK_proof_of_delivery_photos_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalSchema: "shipments",
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "proof_of_delivery_photos",
                schema: "shipments");

            migrationBuilder.RenameColumn(
                name: "has_proof_of_delivery",
                schema: "shipments",
                table: "shipments",
                newName: "HasProofOfDelivery");
        }
    }
}
