using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartWaste.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddC4ComplaintsAndOperationalIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Complaints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CitizenId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    LocationDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Submitted"),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Complaints", x => x.Id);
                    table.CheckConstraint("CK_Complaints_Category", "\"Category\" IN ('MissedCollection', 'DelayedService', 'PoorService', 'UnresolvedIssue', 'Other')");
                    table.CheckConstraint("CK_Complaints_Coordinates", "(\"Latitude\" IS NULL AND \"Longitude\" IS NULL) OR (\"Latitude\" IS NOT NULL AND \"Longitude\" IS NOT NULL AND \"Latitude\" >= -90.0 AND \"Latitude\" <= 90.0 AND \"Longitude\" >= -180.0 AND \"Longitude\" <= 180.0)");
                    table.CheckConstraint("CK_Complaints_Status", "\"Status\" IN ('Submitted', 'InReview', 'Resolved')");
                    table.ForeignKey(
                        name: "FK_Complaints_AspNetUsers_CitizenId",
                        column: x => x.CitizenId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Complaints_AspNetUsers_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationalIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    LocationDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Reported"),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalIssues", x => x.Id);
                    table.CheckConstraint("CK_OperationalIssues_Coordinates", "(\"Latitude\" IS NULL AND \"Longitude\" IS NULL) OR (\"Latitude\" IS NOT NULL AND \"Longitude\" IS NOT NULL AND \"Latitude\" >= -90.0 AND \"Latitude\" <= 90.0 AND \"Longitude\" >= -180.0 AND \"Longitude\" <= 180.0)");
                    table.CheckConstraint("CK_OperationalIssues_Status", "\"Status\" IN ('Reported', 'InReview', 'Resolved')");
                    table.CheckConstraint("CK_OperationalIssues_Type", "\"IssueType\" IN ('VehicleProblem', 'RoadOrAccessIssue', 'EquipmentProblem', 'SafetyConcern', 'OperationalDelay', 'Other')");
                    table.ForeignKey(
                        name: "FK_OperationalIssues_AspNetUsers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalIssues_AspNetUsers_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_CitizenId",
                table: "Complaints",
                column: "CitizenId");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_CreatedAt",
                table: "Complaints",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_ResolvedByUserId",
                table: "Complaints",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_Status",
                table: "Complaints",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_Status_Category",
                table: "Complaints",
                columns: new[] { "Status", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalIssues_CreatedAt",
                table: "OperationalIssues",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalIssues_DriverId",
                table: "OperationalIssues",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalIssues_ResolvedByUserId",
                table: "OperationalIssues",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalIssues_Status",
                table: "OperationalIssues",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalIssues_Status_IssueType",
                table: "OperationalIssues",
                columns: new[] { "Status", "IssueType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Complaints");

            migrationBuilder.DropTable(
                name: "OperationalIssues");
        }
    }
}
