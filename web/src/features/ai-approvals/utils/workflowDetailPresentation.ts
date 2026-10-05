import type {
  AgentWorkflowDetail,
  AgentWorkflowStep,
  CandidateCollectionGroup,
  CollectionPlanningResult,
  DispatchPlanRecommendation,
  FleetRouteResult,
  NeedHandlingRecommendation,
  PlanValidationReview,
  RecommendationCompatibility,
  RecommendedFleetTask,
  SharedPlannerResult,
  SpecialistType,
  UnplannedTask,
  ValidationFinding,
  ValidationOperationsResult,
  WasteAnalysisResult,
  WorkflowStepType,
} from '../types/agentWorkflow';
import { parseJsonValue } from '../types/parseJsonValue';
import { isReportTriggeredWorkflow } from './workflowPresentation';

export type WorkflowTimelineState = 'Completed' | 'Current' | 'Pending' | 'Needs Revision' | 'Failed' | 'Rejected';

export interface WorkflowTimelineStage {
  id: string;
  label: string;
  responsibility: 'AI stage' | 'Human review' | 'Backend execution';
  stepType?: WorkflowStepType;
}

const timelineStages: WorkflowTimelineStage[] = [
  { id: 'shared-planning', label: 'Shared Planning', responsibility: 'AI stage', stepType: 'SharedPlanning' },
  { id: 'waste-analysis', label: 'Waste Analysis', responsibility: 'AI stage', stepType: 'WasteAnalysis' },
  { id: 'collection-planning', label: 'Collection Planning', responsibility: 'AI stage', stepType: 'CollectionPlanning' },
  { id: 'collection-approval', label: 'Collection Approval', responsibility: 'Human review' },
  { id: 'scheduled-task-creation', label: 'Scheduled Task Creation', responsibility: 'Backend execution', stepType: 'ScheduledTaskCreation' },
  { id: 'fleet-planning', label: 'Fleet Planning', responsibility: 'AI stage', stepType: 'FleetPlanning' },
  { id: 'operational-validation', label: 'Operational Validation', responsibility: 'AI stage', stepType: 'OperationalValidation' },
  { id: 'dispatch-approval', label: 'Dispatch Approval', responsibility: 'Human review', stepType: 'DispatchApproval' },
  { id: 'assignment-creation', label: 'Assignment Creation', responsibility: 'Backend execution', stepType: 'AssignmentExecution' },
];

const stagesForWorkflow = (workflow: AgentWorkflowDetail): WorkflowTimelineStage[] =>
  isReportTriggeredWorkflow(workflow)
    ? [...timelineStages.slice(0, 2), { id: 'report-verification', label: 'Report Verification', responsibility: 'Human review' }, ...timelineStages.slice(2)]
    : timelineStages;

const specialistLabels: Record<SpecialistType, string> = {
  WasteAnalysis: 'Waste Analysis',
  CollectionPlanning: 'Collection Planning',
  FleetRoute: 'Fleet & Route',
  ValidationOperations: 'Validation & Operations',
};

const hasCompletedStep = (workflow: AgentWorkflowDetail, stepType: WorkflowStepType): boolean =>
  workflow.steps.some((step) => step.stepType === stepType && step.status === 'Completed');

const currentStageIndex = (workflow: AgentWorkflowDetail): number => {
  const stages = stagesForWorkflow(workflow);
  if (workflow.status === 'AwaitingReportVerification') return stages.findIndex((stage) => stage.id === 'report-verification');
  if (workflow.status === 'AwaitingCollectionApproval' || workflow.status === 'CollectionNeedsRevision') return stages.findIndex((stage) => stage.id === 'collection-approval');
  if (workflow.status === 'AwaitingDispatchApproval' || workflow.status === 'DispatchNeedsRevision') return stages.findIndex((stage) => stage.id === 'dispatch-approval');
  if (workflow.status === 'DispatchApproved' || workflow.status === 'ExecutingAssignments' || workflow.status === 'Completed') return stages.findIndex((stage) => stage.id === 'assignment-creation');
  const byStep = stages.findIndex((stage) => stage.stepType === workflow.currentStep);
  if (byStep >= 0) return byStep;
  return -1;
};

export const getWorkflowTimeline = (workflow: AgentWorkflowDetail): Array<WorkflowTimelineStage & { state: WorkflowTimelineState }> => {
  const currentIndex = currentStageIndex(workflow);
  return stagesForWorkflow(workflow).map((stage, index) => {
    const completed = stage.stepType ? hasCompletedStep(workflow, stage.stepType) :
      (stage.id === 'collection-approval' && ['CollectionApproved', 'CreatingScheduledTasks', 'FleetPlanning', 'OperationalValidation', 'AwaitingDispatchApproval', 'DispatchNeedsRevision', 'DispatchApproved', 'ExecutingAssignments', 'Completed'].includes(workflow.status)) ||
      (stage.id === 'dispatch-approval' && ['DispatchApproved', 'ExecutingAssignments', 'Completed'].includes(workflow.status));

    if (workflow.status === 'Failed' && (stage.stepType === workflow.currentStep || (!completed && index === currentIndex))) return { ...stage, state: 'Failed' };
    if (workflow.status === 'Rejected' && ((stage.id === 'dispatch-approval' && workflow.currentStep === 'OperationalValidation') || (stage.id === 'collection-approval' && workflow.currentStep !== 'OperationalValidation' && !(isReportTriggeredWorkflow(workflow) && workflow.currentStep === 'WasteAnalysis')))) return { ...stage, state: 'Rejected' };
    if (workflow.status === 'Rejected' && isReportTriggeredWorkflow(workflow) && stage.id === 'report-verification' && workflow.currentStep === 'WasteAnalysis') return { ...stage, state: 'Rejected' };
    if (workflow.status === 'CollectionNeedsRevision' && stage.id === 'collection-approval') return { ...stage, state: 'Needs Revision' };
    if (workflow.status === 'DispatchNeedsRevision' && stage.id === 'dispatch-approval') return { ...stage, state: 'Needs Revision' };
    if (completed || (workflow.status === 'Completed' && index <= currentIndex) || (currentIndex > index && workflow.status !== 'Failed')) return { ...stage, state: 'Completed' };
    if (index === currentIndex) return { ...stage, state: 'Current' };
    return { ...stage, state: 'Pending' };
  });
};

/** The backend's own approval path selects the highest persisted sequence for duplicate historical steps. */
export const findLatestWorkflowStep = (workflow: AgentWorkflowDetail, stepType: WorkflowStepType): AgentWorkflowStep | null =>
  [...workflow.steps]
    .filter((step) => step.stepType === stepType)
    .sort((left, right) => right.sequence - left.sequence || (right.completedAt ?? '').localeCompare(left.completedAt ?? ''))[0] ?? null;

const isRecord = (value: unknown): value is Record<string, unknown> => typeof value === 'object' && value !== null && !Array.isArray(value);
const isStringArray = (value: unknown): value is string[] => Array.isArray(value) && value.every((item) => typeof item === 'string');
const isNumberArray = (value: unknown): value is number[] => Array.isArray(value) && value.every((item) => typeof item === 'number');
const isSpecialistType = (value: unknown): value is SpecialistType => value === 'WasteAnalysis' || value === 'CollectionPlanning' || value === 'FleetRoute' || value === 'ValidationOperations';

const isSharedPlannerResult = (value: unknown): value is SharedPlannerResult => isRecord(value) &&
  typeof value.objective === 'string' && typeof value.summary === 'string' && isStringArray(value.warnings) && Array.isArray(value.steps) &&
  value.steps.every((step) => isRecord(step) && typeof step.stepId === 'string' && isSpecialistType(step.specialist) && typeof step.objective === 'string' && isStringArray(step.dependsOn) && (typeof step.sequence === 'number' || step.sequence === null));

const isWasteAnalysisResult = (value: unknown): value is WasteAnalysisResult => isRecord(value) &&
  typeof value.objective === 'string' && (value.status === 'completed' || value.status === 'empty') && typeof value.sourcePage === 'number' &&
  typeof value.sourcePageSize === 'number' && typeof value.sourceTotalCount === 'number' && Array.isArray(value.analyses) && value.analyses.every((analysis) =>
    isRecord(analysis) && typeof analysis.reportId === 'string' && typeof analysis.categoryAssessment === 'string' && typeof analysis.recommendedPriority === 'string' &&
    isStringArray(analysis.operationalConcerns) && typeof analysis.recommendedHandling === 'string' && typeof analysis.confidence === 'string' && typeof analysis.rationale === 'string');

const isNeedHandlingRecommendation = (value: unknown): value is NeedHandlingRecommendation => isRecord(value) && isRecord(value.needReference) &&
  typeof value.needReference.needId === 'string' && typeof value.needReference.targetType === 'string' && typeof value.needReference.collectionReason === 'string' &&
  typeof value.needReference.urgency === 'string' && (typeof value.attentionOrder === 'number' || value.attentionOrder === null) &&
  (value.proposedSchedule === null || (isRecord(value.proposedSchedule) && typeof value.proposedSchedule.scheduledAt === 'string' && typeof value.proposedSchedule.schedulingReason === 'string')) && typeof value.rationale === 'string';

const isCandidateCollectionGroup = (value: unknown): value is CandidateCollectionGroup => isRecord(value) && typeof value.groupId === 'string' &&
  typeof value.attentionOrder === 'number' && Array.isArray(value.needReferences) && value.needReferences.every((reference) => isRecord(reference) && typeof reference.needId === 'string' && typeof reference.targetType === 'string' && typeof reference.urgency === 'string') &&
  isRecord(value.proposedSchedule) && typeof value.proposedSchedule.scheduledAt === 'string' && typeof value.proposedSchedule.schedulingReason === 'string' && typeof value.rationale === 'string' && isStringArray(value.wasteHandlingConsiderations) && isStringArray(value.warnings);

const isCollectionPlanningResult = (value: unknown): value is CollectionPlanningResult => isRecord(value) && typeof value.objective === 'string' &&
  (value.status === 'completed' || value.status === 'partial' || value.status === 'empty') && Array.isArray(value.candidateGroups) && value.candidateGroups.every(isCandidateCollectionGroup) &&
  Array.isArray(value.separateHandling) && value.separateHandling.every(isNeedHandlingRecommendation) && Array.isArray(value.deferredNeeds) && value.deferredNeeds.every(isNeedHandlingRecommendation) &&
  isStringArray(value.warnings) && typeof value.sourcePage === 'number' && typeof value.sourcePageSize === 'number' && typeof value.sourceTotalCount === 'number' &&
  typeof value.sourceTotalPages === 'number' && isNumberArray(value.retrievedPages) && typeof value.isCompleteSnapshot === 'boolean';

const isRecommendedFleetTask = (value: unknown): value is RecommendedFleetTask => isRecord(value) && typeof value.taskId === 'string' &&
  typeof value.taskCode === 'string' && typeof value.sequence === 'number' && (typeof value.addressText === 'string' || value.addressText === null) && typeof value.reason === 'string';

const isRecommendationCompatibility = (value: unknown): value is RecommendationCompatibility => isRecord(value) &&
  (value.status === 'Compatible' || value.status === 'Unknown' || value.status === 'Incompatible') && typeof value.requiresAcknowledgement === 'boolean' && isStringArray(value.issues);

const isDispatchPlanRecommendation = (value: unknown): value is DispatchPlanRecommendation => isRecord(value) && typeof value.planId === 'string' &&
  isRecord(value.recommendedDriver) && typeof value.recommendedDriver.driverId === 'string' && typeof value.recommendedDriver.displayName === 'string' && typeof value.recommendedDriver.reason === 'string' &&
  isRecord(value.recommendedVehicle) && typeof value.recommendedVehicle.vehicleId === 'string' && typeof value.recommendedVehicle.registrationNumber === 'string' && typeof value.recommendedVehicle.vehicleType === 'string' && typeof value.recommendedVehicle.reason === 'string' &&
  Array.isArray(value.recommendedTasks) && value.recommendedTasks.every(isRecommendedFleetTask) && (value.compatibility === null || isRecommendationCompatibility(value.compatibility)) &&
  typeof value.rationale === 'string' && isStringArray(value.warnings);

const isUnplannedTask = (value: unknown): value is UnplannedTask => isRecord(value) && typeof value.taskId === 'string' &&
  (typeof value.taskCode === 'string' || value.taskCode === null) && typeof value.reason === 'string';

const isFleetRouteResult = (value: unknown): value is FleetRouteResult => isRecord(value) && typeof value.objective === 'string' &&
  Array.isArray(value.dispatchPlans) && value.dispatchPlans.every(isDispatchPlanRecommendation) && Array.isArray(value.unplannedTasks) && value.unplannedTasks.every(isUnplannedTask) &&
  isStringArray(value.warnings) && typeof value.rationale === 'string' && typeof value.sourceTaskPage === 'number' && typeof value.sourceTaskPageSize === 'number' &&
  typeof value.sourceTaskTotalCount === 'number' && typeof value.sourceTaskTotalPages === 'number' && typeof value.agentName === 'string' &&
  (typeof value.modelName === 'string' || value.modelName === null) && value.advisoryOnly === true && (value.status === 'completed' || value.status === 'empty');

const isValidationFinding = (value: unknown): value is ValidationFinding => isRecord(value) && typeof value.code === 'string' &&
  (value.severity === 'Info' || value.severity === 'Warning' || value.severity === 'Error') && typeof value.message === 'string' && isStringArray(value.relatedTaskIds) &&
  (typeof value.relatedDriverId === 'string' || value.relatedDriverId === null) && (typeof value.relatedVehicleId === 'string' || value.relatedVehicleId === null);

const isPlanValidationReview = (value: unknown): value is PlanValidationReview => isRecord(value) && typeof value.planId === 'string' &&
  (value.outcome === 'ReadyForHumanReview' || value.outcome === 'NeedsRevision') && typeof value.requiresAcknowledgement === 'boolean' &&
  Array.isArray(value.findings) && value.findings.every(isValidationFinding) && typeof value.summary === 'string';

const isOperationalValidationResult = (value: unknown): value is ValidationOperationsResult => isRecord(value) && typeof value.objective === 'string' &&
  (value.validationOutcome === 'ReadyForHumanReview' || value.validationOutcome === 'NeedsRevision' || value.validationOutcome === null) &&
  Array.isArray(value.planReviews) && value.planReviews.every(isPlanValidationReview) && Array.isArray(value.unplannedTaskFindings) && value.unplannedTaskFindings.every(isValidationFinding) &&
  typeof value.requiresAcknowledgement === 'boolean' && isStringArray(value.warnings) && typeof value.summary === 'string' && typeof value.agentName === 'string' &&
  (typeof value.modelName === 'string' || value.modelName === null) && value.advisoryOnly === true && (value.status === 'completed' || value.status === 'empty');

export type ParsedWorkflowOutput<T> = { kind: 'value'; value: T } | { kind: 'missing' | 'invalid' };

const parseStepOutput = <T>(step: AgentWorkflowStep | null, guard: (value: unknown) => value is T): ParsedWorkflowOutput<T> => {
  if (!step?.output) return { kind: 'missing' };
  const parsed = parseJsonValue<unknown>(step.output);
  return parsed !== null && guard(parsed) ? { kind: 'value', value: parsed } : { kind: 'invalid' };
};

export const getSharedPlannerOutput = (step: AgentWorkflowStep | null) => parseStepOutput(step, isSharedPlannerResult);
export const getWasteAnalysisOutput = (step: AgentWorkflowStep | null) => parseStepOutput(step, isWasteAnalysisResult);
export const getCollectionPlanningOutput = (step: AgentWorkflowStep | null) => parseStepOutput(step, isCollectionPlanningResult);
export const getFleetRouteOutput = (step: AgentWorkflowStep | null) => parseStepOutput(step, isFleetRouteResult);
export const getOperationalValidationOutput = (step: AgentWorkflowStep | null) => parseStepOutput(step, isOperationalValidationResult);
export const getSpecialistLabel = (specialist: SpecialistType): string => specialistLabels[specialist];

export const getCompatibilityLabel = (status: RecommendationCompatibility['status']): string => ({ Compatible: 'Compatible', Unknown: 'Compatibility unknown', Incompatible: 'Incompatible' })[status];
export const getValidationOutcomeLabel = (outcome: PlanValidationReview['outcome']): string => outcome === 'ReadyForHumanReview' ? 'Ready for Human Review' : 'Needs Revision';

export const getWorkflowFailureSummary = (workflow: AgentWorkflowDetail): string | null => {
  if (workflow.status !== 'Failed') return null;
  const latestFailure = [...workflow.steps].filter((step) => step.status === 'Failed' && step.errorMessage).sort((left, right) => right.sequence - left.sequence)[0];
  return latestFailure?.errorMessage ? 'This workflow could not complete. Please review the recorded workflow status.' : 'This workflow could not complete.';
};
