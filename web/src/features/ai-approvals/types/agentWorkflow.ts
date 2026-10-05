export type AgentWorkflowStatus =
  | 'Created'
  | 'Planning'
  | 'AwaitingReportVerification'
  | 'AwaitingCollectionApproval'
  | 'CollectionNeedsRevision'
  | 'CollectionApproved'
  | 'CreatingScheduledTasks'
  | 'FleetPlanning'
  | 'OperationalValidation'
  | 'AwaitingDispatchApproval'
  | 'DispatchNeedsRevision'
  | 'DispatchApproved'
  | 'ExecutingAssignments'
  | 'Completed'
  | 'Rejected'
  | 'Failed';

export type WorkflowStepType =
  | 'None'
  | 'SharedPlanning'
  | 'WasteAnalysis'
  | 'CollectionPlanning'
  | 'ScheduledTaskCreation'
  | 'FleetPlanning'
  | 'OperationalValidation'
  | 'DispatchApproval'
  | 'AssignmentExecution';

export type WorkflowStepStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Skipped';
export type WorkflowApprovalStage = 'CollectionPlanning' | 'FleetDispatch';
export type WorkflowApprovalDecision = 'Approved' | 'Rejected' | 'RevisionRequested';
export type WorkflowExecutionType = 'CollectionTaskCreation' | 'CollectionAssignment';
export type WorkflowExecutionStatus = 'Pending' | 'Succeeded' | 'Failed' | 'PartiallySucceeded';
export type AgentWorkflowTriggerType = 'ManualOperationalPlanning' | 'CitizenReportSubmission';

export type JsonPrimitive = string | number | boolean | null;
export type JsonValue = JsonPrimitive | JsonValue[] | { [key: string]: JsonValue };

export interface AgentWorkflowSummary {
  id: string;
  triggerType?: AgentWorkflowTriggerType;
  triggeringWasteReportId?: string | null;
  reportReference?: string | null;
  objective: string;
  status: AgentWorkflowStatus;
  currentStep: WorkflowStepType;
  initiatedByUserId: string;
  createdAt: string;
  updatedAt: string | null;
  completedAt: string | null;
  finalOutcome: string | null;
  version: number;
}

export interface AgentWorkflowStep {
  id: string;
  sequence: number;
  stepType: WorkflowStepType;
  agentName: string | null;
  status: WorkflowStepStatus;
  input: JsonValue | null;
  output: JsonValue | null;
  validation: JsonValue | null;
  errorMessage: string | null;
  startedAt: string | null;
  completedAt: string | null;
}

export interface AgentWorkflowTransition {
  id: string;
  fromStatus: AgentWorkflowStatus | null;
  toStatus: AgentWorkflowStatus;
  reason: string | null;
  changedByUserId: string | null;
  changedAt: string;
}

export interface AgentWorkflowApproval {
  id: string;
  workflowStepId: string | null;
  approvalStage: WorkflowApprovalStage;
  decision: WorkflowApprovalDecision;
  decisionReason: string | null;
  decisionPayload: JsonValue | null;
  decidedByUserId: string;
  decidedAt: string;
}

export interface AgentWorkflowExecutionResult {
  id: string;
  workflowStepId: string | null;
  executionType: WorkflowExecutionType;
  status: WorkflowExecutionStatus;
  result: JsonValue | null;
  errorMessage: string | null;
  executedAt: string;
}

export interface AgentWorkflowDetail extends AgentWorkflowSummary {
  binCodes?: Record<string, string>;
  steps: AgentWorkflowStep[];
  transitions: AgentWorkflowTransition[];
  approvals: AgentWorkflowApproval[];
  executionResults: AgentWorkflowExecutionResult[];
}

export interface PagedAgentWorkflows {
  items: AgentWorkflowSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface AgentWorkflowListParams {
  page?: number;
  pageSize?: number;
  status?: AgentWorkflowStatus;
}

export interface CreateAgentWorkflowRequest {
  objective: string;
}

export interface VersionedWorkflowRequest {
  expectedVersion: number;
}

export interface ApproveCollectionPlanningRequest extends VersionedWorkflowRequest {
  reason?: string | null;
}

export interface RequestCollectionRevisionRequest extends VersionedWorkflowRequest {
  reason: string;
}

export interface RejectCollectionPlanningRequest extends VersionedWorkflowRequest {
  reason: string;
}

export interface ApproveDispatchPlanRequest extends VersionedWorkflowRequest {
  reason?: string | null;
  acknowledgeWarnings: boolean;
}

export interface RequestDispatchRevisionRequest extends VersionedWorkflowRequest {
  reason: string;
}

export interface RejectDispatchPlanRequest extends VersionedWorkflowRequest {
  reason: string;
}

export type ExecuteCollectionPlanRequest = VersionedWorkflowRequest;
export type ExecuteDispatchPlanRequest = VersionedWorkflowRequest;

export type SpecialistType =
  | 'WasteAnalysis'
  | 'CollectionPlanning'
  | 'FleetRoute'
  | 'ValidationOperations';

export interface SharedPlannerStepResult {
  stepId: string;
  specialist: SpecialistType;
  objective: string;
  dependsOn: string[];
  sequence: number | null;
}

export interface SharedPlannerResult {
  objective: string;
  steps: SharedPlannerStepResult[];
  summary: string;
  warnings: string[];
  agentName: string;
  modelName: string | null;
  advisoryOnly: true;
  status: 'completed';
}

export interface WasteReportAnalysisResult {
  reportId: string;
  categoryAssessment: string;
  recommendedPriority: 'Low' | 'Medium' | 'High' | 'Urgent';
  operationalConcerns: string[];
  recommendedHandling: string;
  confidence: 'Low' | 'Medium' | 'High';
  rationale: string;
}

export interface WasteAnalysisResult {
  objective: string;
  analyses: WasteReportAnalysisResult[];
  sourcePage: number;
  sourcePageSize: number;
  sourceTotalCount: number;
  agentName: string;
  modelName: string | null;
  status: 'completed' | 'empty';
}

export interface CollectionNeedReference {
  needId: string;
  targetType: 'Report' | 'Bin';
  collectionReason: 'VerifiedReport' | 'FullOrBlockedBin' | 'RoutineCollection';
  urgency: 'Low' | 'Medium' | 'High' | 'Urgent';
}

export interface ProposedSchedule {
  scheduledAt: string;
  schedulingReason: string;
}

export interface CandidateCollectionGroup {
  groupId: string;
  attentionOrder: number;
  needReferences: CollectionNeedReference[];
  proposedSchedule: ProposedSchedule;
  rationale: string;
  wasteHandlingConsiderations: string[];
  warnings: string[];
}

export interface NeedHandlingRecommendation {
  needReference: CollectionNeedReference;
  attentionOrder: number | null;
  proposedSchedule: ProposedSchedule | null;
  rationale: string;
}

export interface CollectionPlanningResult {
  objective: string;
  candidateGroups: CandidateCollectionGroup[];
  separateHandling: NeedHandlingRecommendation[];
  deferredNeeds: NeedHandlingRecommendation[];
  warnings: string[];
  sourcePage: number;
  sourcePageSize: number;
  sourceTotalCount: number;
  sourceTotalPages: number;
  retrievedPages: number[];
  isCompleteSnapshot: boolean;
  agentName: string;
  modelName: string | null;
  advisoryOnly: true;
  status: 'completed' | 'partial' | 'empty';
}

export interface RecommendedFleetTask {
  taskId: string;
  taskCode: string;
  sequence: number;
  addressText: string | null;
  reason: string;
}

export interface DriverRecommendation {
  driverId: string;
  displayName: string;
  reason: string;
}

export interface VehicleRecommendation {
  vehicleId: string;
  registrationNumber: string;
  vehicleType: string;
  reason: string;
}

export interface RecommendationCompatibility {
  status: 'Compatible' | 'Incompatible' | 'Unknown';
  requiresAcknowledgement: boolean;
  issues: string[];
}

export interface DispatchPlanRecommendation {
  planId: string;
  recommendedDriver: DriverRecommendation;
  recommendedVehicle: VehicleRecommendation;
  recommendedTasks: RecommendedFleetTask[];
  compatibility: RecommendationCompatibility | null;
  rationale: string;
  warnings: string[];
}

export interface UnplannedTask {
  taskId: string;
  taskCode: string | null;
  reason: string;
}

export interface FleetRouteResult {
  objective: string;
  dispatchPlans: DispatchPlanRecommendation[];
  unplannedTasks: UnplannedTask[];
  warnings: string[];
  rationale: string;
  sourceTaskPage: number;
  sourceTaskPageSize: number;
  sourceTaskTotalCount: number;
  sourceTaskTotalPages: number;
  agentName: string;
  modelName: string | null;
  advisoryOnly: true;
  status: 'completed' | 'empty';
}

export interface ValidationFinding {
  code: string;
  severity: 'Info' | 'Warning' | 'Error';
  message: string;
  relatedTaskIds: string[];
  relatedDriverId: string | null;
  relatedVehicleId: string | null;
}

export interface PlanValidationReview {
  planId: string;
  outcome: 'ReadyForHumanReview' | 'NeedsRevision';
  requiresAcknowledgement: boolean;
  findings: ValidationFinding[];
  summary: string;
}

export interface ValidationOperationsResult {
  objective: string;
  validationOutcome: 'ReadyForHumanReview' | 'NeedsRevision' | null;
  planReviews: PlanValidationReview[];
  unplannedTaskFindings: ValidationFinding[];
  requiresAcknowledgement: boolean;
  warnings: string[];
  summary: string;
  agentName: string;
  modelName: string | null;
  advisoryOnly: true;
  status: 'completed' | 'empty';
}
