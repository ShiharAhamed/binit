using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartWaste.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentWorkflowPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentWorkflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Objective = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Created"),
                    CurrentStep = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "None"),
                    InitiatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalOutcome = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentWorkflows", x => x.Id);
                    table.CheckConstraint("CK_AgentWorkflows_Objective", "LENGTH(TRIM(\"Objective\")) >= 5");
                    table.CheckConstraint("CK_AgentWorkflows_Status", "\"Status\" IN ('Created', 'Planning', 'AwaitingCollectionApproval', 'CollectionNeedsRevision', 'CollectionApproved', 'CreatingScheduledTasks', 'FleetPlanning', 'OperationalValidation', 'AwaitingDispatchApproval', 'DispatchNeedsRevision', 'DispatchApproved', 'ExecutingAssignments', 'Completed', 'Rejected', 'Failed')");
                    table.ForeignKey(
                        name: "FK_AgentWorkflows_AspNetUsers_InitiatedByUserId",
                        column: x => x.InitiatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgentWorkflowSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    StepType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AgentName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Pending"),
                    InputJson = table.Column<string>(type: "jsonb", nullable: true),
                    OutputJson = table.Column<string>(type: "jsonb", nullable: true),
                    ValidationJson = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentWorkflowSteps", x => x.Id);
                    table.CheckConstraint("CK_AgentWorkflowSteps_Sequence", "\"Sequence\" > 0");
                    table.CheckConstraint("CK_AgentWorkflowSteps_Status", "\"Status\" IN ('Pending', 'Running', 'Completed', 'Failed', 'Skipped')");
                    table.ForeignKey(
                        name: "FK_AgentWorkflowSteps_AgentWorkflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "AgentWorkflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentWorkflowTransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentWorkflowTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentWorkflowTransitions_AgentWorkflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "AgentWorkflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentWorkflowTransitions_AspNetUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgentWorkflowApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowStepId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovalStage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Decision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecisionPayloadJson = table.Column<string>(type: "jsonb", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentWorkflowApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentWorkflowApprovals_AgentWorkflowSteps_WorkflowStepId",
                        column: x => x.WorkflowStepId,
                        principalTable: "AgentWorkflowSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AgentWorkflowApprovals_AgentWorkflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "AgentWorkflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentWorkflowApprovals_AspNetUsers_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgentWorkflowExecutionResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowStepId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExecutionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentWorkflowExecutionResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentWorkflowExecutionResults_AgentWorkflowSteps_WorkflowSt~",
                        column: x => x.WorkflowStepId,
                        principalTable: "AgentWorkflowSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AgentWorkflowExecutionResults_AgentWorkflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "AgentWorkflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowApprovals_DecidedByUserId",
                table: "AgentWorkflowApprovals",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowApprovals_WorkflowId",
                table: "AgentWorkflowApprovals",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowApprovals_WorkflowStepId",
                table: "AgentWorkflowApprovals",
                column: "WorkflowStepId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowExecutionResults_WorkflowId",
                table: "AgentWorkflowExecutionResults",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowExecutionResults_WorkflowStepId",
                table: "AgentWorkflowExecutionResults",
                column: "WorkflowStepId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflows_CreatedAt",
                table: "AgentWorkflows",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflows_InitiatedByUserId",
                table: "AgentWorkflows",
                column: "InitiatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflows_Status",
                table: "AgentWorkflows",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowSteps_WorkflowId_Sequence",
                table: "AgentWorkflowSteps",
                columns: new[] { "WorkflowId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowTransitions_ChangedByUserId",
                table: "AgentWorkflowTransitions",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowTransitions_WorkflowId_ChangedAt",
                table: "AgentWorkflowTransitions",
                columns: new[] { "WorkflowId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentWorkflowApprovals");

            migrationBuilder.DropTable(
                name: "AgentWorkflowExecutionResults");

            migrationBuilder.DropTable(
                name: "AgentWorkflowTransitions");

            migrationBuilder.DropTable(
                name: "AgentWorkflowSteps");

            migrationBuilder.DropTable(
                name: "AgentWorkflows");
        }
    }
}
