using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartWaste.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportVerificationWorkflowTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AgentWorkflows_Status",
                table: "AgentWorkflows");

            migrationBuilder.AddColumn<string>(
                name: "TriggerType",
                table: "AgentWorkflows",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "ManualOperationalPlanning");

            migrationBuilder.AddColumn<Guid>(
                name: "TriggeringWasteReportId",
                table: "AgentWorkflows",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflows_TriggeringWasteReportId",
                table: "AgentWorkflows",
                column: "TriggeringWasteReportId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflows_TriggerType",
                table: "AgentWorkflows",
                column: "TriggerType");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AgentWorkflows_Status",
                table: "AgentWorkflows",
                sql: "\"Status\" IN ('Created', 'Planning', 'AwaitingCollectionApproval', 'CollectionNeedsRevision', 'CollectionApproved', 'CreatingScheduledTasks', 'FleetPlanning', 'OperationalValidation', 'AwaitingDispatchApproval', 'DispatchNeedsRevision', 'DispatchApproved', 'ExecutingAssignments', 'Completed', 'Rejected', 'Failed', 'AwaitingReportVerification')");

            migrationBuilder.AddForeignKey(
                name: "FK_AgentWorkflows_WasteReports_TriggeringWasteReportId",
                table: "AgentWorkflows",
                column: "TriggeringWasteReportId",
                principalTable: "WasteReports",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentWorkflows_WasteReports_TriggeringWasteReportId",
                table: "AgentWorkflows");

            migrationBuilder.DropIndex(
                name: "IX_AgentWorkflows_TriggeringWasteReportId",
                table: "AgentWorkflows");

            migrationBuilder.DropIndex(
                name: "IX_AgentWorkflows_TriggerType",
                table: "AgentWorkflows");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AgentWorkflows_Status",
                table: "AgentWorkflows");

            migrationBuilder.DropColumn(
                name: "TriggerType",
                table: "AgentWorkflows");

            migrationBuilder.DropColumn(
                name: "TriggeringWasteReportId",
                table: "AgentWorkflows");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AgentWorkflows_Status",
                table: "AgentWorkflows",
                sql: "\"Status\" IN ('Created', 'Planning', 'AwaitingCollectionApproval', 'CollectionNeedsRevision', 'CollectionApproved', 'CreatingScheduledTasks', 'FleetPlanning', 'OperationalValidation', 'AwaitingDispatchApproval', 'DispatchNeedsRevision', 'DispatchApproved', 'ExecutingAssignments', 'Completed', 'Rejected', 'Failed')");
        }
    }
}
