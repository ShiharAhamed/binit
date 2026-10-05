using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartWaste.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddC3CollectionAssignmentAndRouteFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollectionAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Assigned"),
                    CompatibilityAcknowledgement = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CompatibilityAcknowledgedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompatibilityAcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionAssignments", x => x.Id);
                    table.CheckConstraint("CK_CollectionAssignments_CancellationReason", "(\"Status\" <> 'Cancelled') OR (\"CancellationReason\" IS NOT NULL AND LENGTH(TRIM(\"CancellationReason\")) BETWEEN 5 AND 500)");
                    table.CheckConstraint("CK_CollectionAssignments_Status", "\"Status\" IN ('Assigned', 'InProgress', 'Completed', 'PartiallyCompleted', 'Failed', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_CollectionAssignments_AspNetUsers_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionAssignments_AspNetUsers_CompatibilityAcknowledged~",
                        column: x => x.CompatibilityAcknowledgedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionAssignments_DriverProfiles_DriverId",
                        column: x => x.DriverId,
                        principalTable: "DriverProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionAssignments_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectionAssignmentStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionAssignmentStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionAssignmentStatusHistories_AspNetUsers_ChangedByUs~",
                        column: x => x.ChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionAssignmentStatusHistories_CollectionAssignments_C~",
                        column: x => x.CollectionAssignmentId,
                        principalTable: "CollectionAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectionAssignmentTaskClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReleaseReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionAssignmentTaskClaims", x => x.Id);
                    table.UniqueConstraint("AK_CollectionAssignmentTaskClaims_Id_CollectionTaskId", x => new { x.Id, x.CollectionTaskId });
                    table.ForeignKey(
                        name: "FK_CollectionAssignmentTaskClaims_AspNetUsers_ReleasedByUserId",
                        column: x => x.ReleasedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionAssignmentTaskClaims_CollectionAssignments_Collec~",
                        column: x => x.CollectionAssignmentId,
                        principalTable: "CollectionAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionAssignmentTaskClaims_CollectionTasks_CollectionTa~",
                        column: x => x.CollectionTaskId,
                        principalTable: "CollectionTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Routes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoutingMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "ManualOrder"),
                    RouteGeometry = table.Column<string>(type: "text", nullable: true),
                    EstimatedDistanceMeters = table.Column<double>(type: "double precision", nullable: true),
                    EstimatedDurationSeconds = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                    table.CheckConstraint("CK_Routes_RoutingMethod", "\"RoutingMethod\" IN ('ManualOrder', 'VerifiedProvider')");
                    table.ForeignKey(
                        name: "FK_Routes_CollectionAssignments_CollectionAssignmentId",
                        column: x => x.CollectionAssignmentId,
                        principalTable: "CollectionAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RouteStops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionAssignmentTaskClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteStops", x => x.Id);
                    table.CheckConstraint("CK_RouteStops_Failure", "(\"Status\" <> 'Failed' AND \"FailureReason\" IS NULL) OR (\"Status\" = 'Failed' AND \"FailureReason\" IS NOT NULL AND LENGTH(TRIM(\"FailureReason\")) BETWEEN 5 AND 500)");
                    table.CheckConstraint("CK_RouteStops_Sequence", "\"Sequence\" > 0");
                    table.CheckConstraint("CK_RouteStops_Status", "\"Status\" IN ('Pending', 'Completed', 'Failed')");
                    table.ForeignKey(
                        name: "FK_RouteStops_CollectionAssignmentTaskClaims_CollectionAssignm~",
                        columns: x => new { x.CollectionAssignmentTaskClaimId, x.CollectionTaskId },
                        principalTable: "CollectionAssignmentTaskClaims",
                        principalColumns: new[] { "Id", "CollectionTaskId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RouteStops_CollectionTasks_CollectionTaskId",
                        column: x => x.CollectionTaskId,
                        principalTable: "CollectionTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RouteStops_Routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RouteStopStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteStopId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteStopStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RouteStopStatusHistories_AspNetUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RouteStopStatusHistories_RouteStops_RouteStopId",
                        column: x => x.RouteStopId,
                        principalTable: "RouteStops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionAssignments_AssignedByUserId",
                table: "CollectionAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionAssignments_CompatibilityAcknowledgedByUserId",
                table: "CollectionAssignments",
                column: "CompatibilityAcknowledgedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionAssignments_DriverId_Unfinished",
                table: "CollectionAssignments",
                column: "DriverId",
                unique: true,
                filter: "\"Status\" IN ('Assigned', 'InProgress')");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionAssignments_VehicleId_Unfinished",
                table: "CollectionAssignments",
                column: "VehicleId",
                unique: true,
                filter: "\"Status\" IN ('Assigned', 'InProgress')");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionAssignmentStatusHistories_ChangedByUserId",
                table: "CollectionAssignmentStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionAssignmentStatusHistories_CollectionAssignmentId_~",
                table: "CollectionAssignmentStatusHistories",
                columns: new[] { "CollectionAssignmentId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionAssignmentTaskClaims_CollectionAssignmentId_Colle~",
                table: "CollectionAssignmentTaskClaims",
                columns: new[] { "CollectionAssignmentId", "CollectionTaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionAssignmentTaskClaims_ReleasedByUserId",
                table: "CollectionAssignmentTaskClaims",
                column: "ReleasedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionAssignmentTaskClaims_TaskId_Active",
                table: "CollectionAssignmentTaskClaims",
                column: "CollectionTaskId",
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_CollectionAssignmentId",
                table: "Routes",
                column: "CollectionAssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RouteStops_CollectionAssignmentTaskClaimId",
                table: "RouteStops",
                column: "CollectionAssignmentTaskClaimId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RouteStops_CollectionAssignmentTaskClaimId_CollectionTaskId",
                table: "RouteStops",
                columns: new[] { "CollectionAssignmentTaskClaimId", "CollectionTaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_RouteStops_CollectionTaskId",
                table: "RouteStops",
                column: "CollectionTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteStops_RouteId_Sequence",
                table: "RouteStops",
                columns: new[] { "RouteId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RouteStopStatusHistories_ChangedByUserId",
                table: "RouteStopStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteStopStatusHistories_RouteStopId_ChangedAt",
                table: "RouteStopStatusHistories",
                columns: new[] { "RouteStopId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectionAssignmentStatusHistories");

            migrationBuilder.DropTable(
                name: "RouteStopStatusHistories");

            migrationBuilder.DropTable(
                name: "RouteStops");

            migrationBuilder.DropTable(
                name: "CollectionAssignmentTaskClaims");

            migrationBuilder.DropTable(
                name: "Routes");

            migrationBuilder.DropTable(
                name: "CollectionAssignments");
        }
    }
}
