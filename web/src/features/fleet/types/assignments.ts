import type { PagedResult } from '../../collection/types/bins';

export type AssignmentTaskTargetType = 'Report' | 'Bin';
export type CollectionReason = 'VerifiedReport' | 'FullOrBlockedBin' | 'RoutineCollection' | 'OfficerDiscretion';
export type CollectionTaskStatus = 'Scheduled' | 'Assigned' | 'InProgress' | 'Completed' | 'Failed' | 'Cancelled';
export type CollectionAssignmentStatus = 'Assigned' | 'InProgress' | 'Completed' | 'PartiallyCompleted' | 'Failed' | 'Cancelled';
export type RouteStopStatus = 'Pending' | 'Completed' | 'Failed';

/**
 * Presentation-only data required to render an ordered collection route on a map.
 * Callers map available tasks or persisted route stops into this shape.
 */
export interface CollectionMapStop {
  id: string;
  sequence: number;
  label: string;
  latitude: number | null | undefined;
  longitude: number | null | undefined;
  targetType?: AssignmentTaskTargetType;
  addressText?: string | null;
  status?: RouteStopStatus;
}

export interface AvailableAssignmentTaskDto {
  id: string;
  taskCode: string;
  targetType: AssignmentTaskTargetType;
  wasteReportId: string | null;
  wasteBinId: string | null;
  collectionReason: CollectionReason;
  status: CollectionTaskStatus;
  scheduledAt: string;
  addressText: string | null;
  latitude: number | null;
  longitude: number | null;
}

export interface AssignmentSummaryDto {
  id: string;
  assignmentNumber?: number;
  assignmentReference?: string;
  status: CollectionAssignmentStatus;
  driverId: string;
  driverName: string;
  vehicleId: string;
  vehicleRegistrationNumber: string;
  stopCount: number;
  completedStopCount: number;
  failedStopCount: number;
  assignedAt: string;
}

export interface AssignmentListParams {
  status?: CollectionAssignmentStatus | '';
  page?: number;
  pageSize?: number;
}

export interface AvailableAssignmentTaskListParams {
  page?: number;
  pageSize?: number;
}

export interface CreateRouteStopRequest {
  collectionTaskId: string;
  sequence: number;
}

export interface CreateCollectionAssignmentRequest {
  driverId: string;
  vehicleId: string;
  collectionTaskIds: string[];
  stops: CreateRouteStopRequest[];
  compatibilityAcknowledgement?: string;
}

export interface CreatedAssignmentDto {
  id: string;
  assignmentNumber?: number;
  assignmentReference?: string;
}

export interface RouteStopHistoryReadDto {
  id: string;
  fromStatus: RouteStopStatus | null;
  toStatus: RouteStopStatus;
  changedByUserId: string | null;
  changedAt: string;
  notes: string | null;
}

export interface AssignmentTaskDto {
  id: string;
  taskCode: string;
  targetType: AssignmentTaskTargetType;
  wasteReportId: string | null;
  wasteBinId: string | null;
  collectionReason: CollectionReason;
  status: CollectionTaskStatus;
  scheduledAt: string;
  addressText: string | null;
  latitude: number | null;
  longitude: number | null;
}

export interface RouteStopReadDto {
  id: string;
  sequence: number;
  status: RouteStopStatus;
  completedAt: string | null;
  failedAt: string | null;
  failureReason: string | null;
  task: AssignmentTaskDto;
  history: RouteStopHistoryReadDto[];
}

export interface RouteReadDto {
  id: string;
  collectionAssignmentId: string;
  routingMethod: string;
  routeGeometry: string | null;
  estimatedDistanceMeters: number | null;
  estimatedDurationSeconds: number | null;
  stops: RouteStopReadDto[];
}

export interface AssignmentHistoryDto {
  id: string;
  fromStatus: CollectionAssignmentStatus | null;
  toStatus: CollectionAssignmentStatus;
  changedByUserId: string | null;
  changedAt: string;
  notes: string | null;
}

export interface AssignmentDetailDto extends AssignmentSummaryDto {
  route: RouteReadDto | null;
  history: AssignmentHistoryDto[];
}

export interface RouteStopSequenceRequest {
  routeStopId: string;
  sequence: number;
}

export interface ReorderRouteStopsRequest {
  stops: RouteStopSequenceRequest[];
}

export interface CancelCollectionAssignmentRequest {
  reason: string;
}

export type PagedAvailableAssignmentTasks = PagedResult<AvailableAssignmentTaskDto>;
export type PagedAssignments = PagedResult<AssignmentSummaryDto>;
