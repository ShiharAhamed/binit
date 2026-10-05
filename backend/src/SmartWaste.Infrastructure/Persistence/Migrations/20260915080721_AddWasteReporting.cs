using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartWaste.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWasteReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WasteReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CitizenId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    WasteType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    AddressText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    VerifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WasteReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WasteReports_AspNetUsers_CitizenId",
                        column: x => x.CitizenId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WasteReports_AspNetUsers_VerifiedByUserId",
                        column: x => x.VerifiedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WasteReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportAttachments_WasteReports_WasteReportId",
                        column: x => x.WasteReportId,
                        principalTable: "WasteReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WasteReportStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WasteReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WasteReportStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WasteReportStatusHistories_AspNetUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WasteReportStatusHistories_WasteReports_WasteReportId",
                        column: x => x.WasteReportId,
                        principalTable: "WasteReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportAttachments_WasteReportId",
                table: "ReportAttachments",
                column: "WasteReportId");

            migrationBuilder.CreateIndex(
                name: "IX_WasteReports_CitizenId",
                table: "WasteReports",
                column: "CitizenId");

            migrationBuilder.CreateIndex(
                name: "IX_WasteReports_CreatedAt",
                table: "WasteReports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WasteReports_Status",
                table: "WasteReports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WasteReports_Status_WasteType",
                table: "WasteReports",
                columns: new[] { "Status", "WasteType" });

            migrationBuilder.CreateIndex(
                name: "IX_WasteReports_VerifiedByUserId",
                table: "WasteReports",
                column: "VerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WasteReportStatusHistories_ChangedAt",
                table: "WasteReportStatusHistories",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WasteReportStatusHistories_ChangedByUserId",
                table: "WasteReportStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WasteReportStatusHistories_WasteReportId",
                table: "WasteReportStatusHistories",
                column: "WasteReportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportAttachments");

            migrationBuilder.DropTable(
                name: "WasteReportStatusHistories");

            migrationBuilder.DropTable(
                name: "WasteReports");
        }
    }
}
