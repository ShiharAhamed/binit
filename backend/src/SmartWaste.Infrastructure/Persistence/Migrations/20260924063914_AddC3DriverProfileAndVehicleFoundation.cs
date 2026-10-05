using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartWaste.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddC3DriverProfileAndVehicleFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DriverProfiles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AvailabilityStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Available"),
                    IsEligible = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    EligibilityNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverProfiles", x => x.UserId);
                    table.CheckConstraint("CK_DriverProfiles_AvailabilityStatus", "\"AvailabilityStatus\" IN ('Available', 'OffDuty')");
                    table.ForeignKey(
                        name: "FK_DriverProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VehicleType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CapacityLiters = table.Column<int>(type: "integer", nullable: false),
                    OperationalStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Available"),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.CheckConstraint("CK_Vehicles_CapacityLiters", "\"CapacityLiters\" > 0");
                    table.CheckConstraint("CK_Vehicles_OperationalStatus", "\"OperationalStatus\" IN ('Available', 'Maintenance', 'Inactive')");
                    table.CheckConstraint("CK_Vehicles_VehicleType", "\"VehicleType\" IN ('Compactor', 'Flatbed', 'Tipper', 'SmallVan')");
                });

            migrationBuilder.CreateTable(
                name: "VehicleSupportedWasteTypes",
                columns: table => new
                {
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    WasteType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleSupportedWasteTypes", x => new { x.VehicleId, x.WasteType });
                    table.ForeignKey(
                        name: "FK_VehicleSupportedWasteTypes_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DriverProfiles_LicenseNumber",
                table: "DriverProfiles",
                column: "LicenseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_RegistrationNumber",
                table: "Vehicles",
                column: "RegistrationNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverProfiles");

            migrationBuilder.DropTable(
                name: "VehicleSupportedWasteTypes");

            migrationBuilder.DropTable(
                name: "Vehicles");
        }
    }
}
