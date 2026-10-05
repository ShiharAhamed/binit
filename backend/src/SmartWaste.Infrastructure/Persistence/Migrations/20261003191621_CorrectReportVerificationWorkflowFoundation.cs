using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartWaste.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CorrectReportVerificationWorkflowFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentWorkflows_WasteReports_TriggeringWasteReportId",
                table: "AgentWorkflows");

            migrationBuilder.DropIndex(
                name: "IX_AgentWorkflows_TriggeringWasteReportId",
                table: "AgentWorkflows");

            migrationBuilder.AddColumn<int>(
                name: "ProcessingAttemptCount",
                table: "AgentWorkflows",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingLeaseExpiresAt",
                table: "AgentWorkflows",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProcessingLeaseId",
                table: "AgentWorkflows",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflows_TriggeringWasteReportId",
                table: "AgentWorkflows",
                column: "TriggeringWasteReportId",
                unique: true,
                filter: "\"TriggeringWasteReportId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AgentWorkflows_ProcessingAttemptCount",
                table: "AgentWorkflows",
                sql: "\"ProcessingAttemptCount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AgentWorkflows_TriggerReport",
                table: "AgentWorkflows",
                sql: "(\"TriggerType\" = 'ManualOperationalPlanning' AND \"TriggeringWasteReportId\" IS NULL) OR (\"TriggerType\" = 'CitizenReportSubmission' AND \"TriggeringWasteReportId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_AgentWorkflows_WasteReports_TriggeringWasteReportId",
                table: "AgentWorkflows",
                column: "TriggeringWasteReportId",
                principalTable: "WasteReports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.DropCheckConstraint(
                name: "CK_AgentWorkflows_ProcessingAttemptCount",
                table: "AgentWorkflows");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AgentWorkflows_TriggerReport",
                table: "AgentWorkflows");

            migrationBuilder.DropColumn(
                name: "ProcessingAttemptCount",
                table: "AgentWorkflows");

            migrationBuilder.DropColumn(
                name: "ProcessingLeaseExpiresAt",
                table: "AgentWorkflows");

            migrationBuilder.DropColumn(
                name: "ProcessingLeaseId",
                table: "AgentWorkflows");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflows_TriggeringWasteReportId",
                table: "AgentWorkflows",
                column: "TriggeringWasteReportId");

            migrationBuilder.AddForeignKey(
                name: "FK_AgentWorkflows_WasteReports_TriggeringWasteReportId",
                table: "AgentWorkflows",
                column: "TriggeringWasteReportId",
                principalTable: "WasteReports",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
