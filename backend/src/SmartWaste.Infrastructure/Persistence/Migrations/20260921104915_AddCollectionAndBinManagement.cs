using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartWaste.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionAndBinManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WasteBins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BinCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CapacityLiters = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    AddressText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AdministrativeStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Active"),
                    CollectionWeekdays = table.Column<int[]>(type: "integer[]", nullable: false),
                    LastCollectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WasteBins", x => x.Id);
                    table.CheckConstraint("CK_WasteBins_AdministrativeStatus", "\"AdministrativeStatus\" IN ('Active', 'OutOfService', 'Retired')");
                    table.CheckConstraint("CK_WasteBins_CapacityLiters", "\"CapacityLiters\" > 0");
                    table.CheckConstraint("CK_WasteBins_Coordinates", "\"Latitude\" >= -90.0 AND \"Latitude\" <= 90.0 AND \"Longitude\" >= -180.0 AND \"Longitude\" <= 180.0");
                });

            migrationBuilder.CreateTable(
                name: "BinObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WasteBinId = table.Column<Guid>(type: "uuid", nullable: false),
                    FillLevelPercent = table.Column<int>(type: "integer", nullable: false),
                    Condition = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BinObservations", x => x.Id);
                    table.CheckConstraint("CK_BinObservations_Condition", "\"Condition\" IN ('Good', 'Damaged', 'Blocked', 'Missing')");
                    table.CheckConstraint("CK_BinObservations_FillLevelPercent", "\"FillLevelPercent\" IN (0, 25, 50, 75, 100)");
                    table.ForeignKey(
                        name: "FK_BinObservations_AspNetUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BinObservations_WasteBins_WasteBinId",
                        column: x => x.WasteBinId,
                        principalTable: "WasteBins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WasteBinAcceptedWasteTypes",
                columns: table => new
                {
                    WasteBinId = table.Column<Guid>(type: "uuid", nullable: false),
                    WasteType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WasteBinAcceptedWasteTypes", x => new { x.WasteBinId, x.WasteType });
                    table.ForeignKey(
                        name: "FK_WasteBinAcceptedWasteTypes_WasteBins_WasteBinId",
                        column: x => x.WasteBinId,
                        principalTable: "WasteBins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectionTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    WasteReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    WasteBinId = table.Column<Guid>(type: "uuid", nullable: true),
                    CollectionReason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Scheduled"),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HandlingNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SchedulingReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreationMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Manual"),
                    TriggerObservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoutineDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionTasks", x => x.Id);
                    table.CheckConstraint("CK_CollectionTasks_OfficerDiscretion_Reason", "(\"CollectionReason\" != 'OfficerDiscretion') OR (\"SchedulingReason\" IS NOT NULL AND LENGTH(TRIM(\"SchedulingReason\")) > 0)");
                    table.CheckConstraint("CK_CollectionTasks_Reason_Consistency", "(\"WasteReportId\" IS NOT NULL AND \"CollectionReason\" = 'VerifiedReport') OR (\"WasteBinId\" IS NOT NULL AND \"CollectionReason\" IN ('FullOrBlockedBin', 'RoutineCollection', 'OfficerDiscretion'))");
                    table.CheckConstraint("CK_CollectionTasks_Status", "\"Status\" IN ('Scheduled', 'Assigned', 'InProgress', 'Completed', 'Failed', 'Cancelled')");
                    table.CheckConstraint("CK_CollectionTasks_Target_XOR", "(\"WasteReportId\" IS NOT NULL AND \"WasteBinId\" IS NULL) OR (\"WasteReportId\" IS NULL AND \"WasteBinId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CollectionTasks_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionTasks_BinObservations_TriggerObservationId",
                        column: x => x.TriggerObservationId,
                        principalTable: "BinObservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CollectionTasks_WasteBins_WasteBinId",
                        column: x => x.WasteBinId,
                        principalTable: "WasteBins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionTasks_WasteReports_WasteReportId",
                        column: x => x.WasteReportId,
                        principalTable: "WasteReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectionTaskScheduleHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NewScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RescheduledByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RescheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionTaskScheduleHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionTaskScheduleHistories_AspNetUsers_RescheduledByUs~",
                        column: x => x.RescheduledByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionTaskScheduleHistories_CollectionTasks_CollectionT~",
                        column: x => x.CollectionTaskId,
                        principalTable: "CollectionTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectionTaskStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionTaskStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionTaskStatusHistories_AspNetUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionTaskStatusHistories_CollectionTasks_CollectionTas~",
                        column: x => x.CollectionTaskId,
                        principalTable: "CollectionTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BinObservations_RecordedByUserId",
                table: "BinObservations",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BinObservations_WasteBinId_RecordedAt",
                table: "BinObservations",
                columns: new[] { "WasteBinId", "RecordedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTasks_CreatedByUserId",
                table: "CollectionTasks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTasks_Status_ScheduledAt",
                table: "CollectionTasks",
                columns: new[] { "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTasks_TaskCode",
                table: "CollectionTasks",
                column: "TaskCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTasks_TriggerObservationId",
                table: "CollectionTasks",
                column: "TriggerObservationId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTasks_WasteBinId_Active",
                table: "CollectionTasks",
                column: "WasteBinId",
                unique: true,
                filter: "\"WasteBinId\" IS NOT NULL AND \"Status\" IN ('Scheduled', 'Assigned', 'InProgress')");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTasks_WasteReportId_Active",
                table: "CollectionTasks",
                column: "WasteReportId",
                unique: true,
                filter: "\"WasteReportId\" IS NOT NULL AND \"Status\" IN ('Scheduled', 'Assigned', 'InProgress')");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTaskScheduleHistories_CollectionTaskId_Reschedule~",
                table: "CollectionTaskScheduleHistories",
                columns: new[] { "CollectionTaskId", "RescheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTaskScheduleHistories_RescheduledByUserId",
                table: "CollectionTaskScheduleHistories",
                column: "RescheduledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTaskStatusHistories_ChangedByUserId",
                table: "CollectionTaskStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTaskStatusHistories_CollectionTaskId_ChangedAt",
                table: "CollectionTaskStatusHistories",
                columns: new[] { "CollectionTaskId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WasteBins_AdministrativeStatus",
                table: "WasteBins",
                column: "AdministrativeStatus");

            migrationBuilder.CreateIndex(
                name: "IX_WasteBins_BinCode",
                table: "WasteBins",
                column: "BinCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WasteBins_Latitude_Longitude",
                table: "WasteBins",
                columns: new[] { "Latitude", "Longitude" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectionTaskScheduleHistories");

            migrationBuilder.DropTable(
                name: "CollectionTaskStatusHistories");

            migrationBuilder.DropTable(
                name: "WasteBinAcceptedWasteTypes");

            migrationBuilder.DropTable(
                name: "CollectionTasks");

            migrationBuilder.DropTable(
                name: "BinObservations");

            migrationBuilder.DropTable(
                name: "WasteBins");
        }
    }
}
