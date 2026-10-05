import type { CollectionReason } from './collectionNeeds';
import type { PagedResult } from './bins';

export type CollectionTaskStatus = 'Scheduled' | 'Assigned' | 'InProgress' | 'Completed' | 'Failed' | 'Cancelled';
export type CollectionTaskTargetType = 'Report' | 'Bin';

export interface CreateManualCollectionTaskRequest {
  wasteReportId: string | null;
  wasteBinId: string | null;
  collectionReason: CollectionReason;
  scheduledAt: string;
  handlingNotes: string | null;
  schedulingReason: string | null;
}

export interface CollectionTaskDetailDto {
  id: string;
  taskCode: string;
  targetType: CollectionTaskTargetType;
  wasteReportId: string | null;
  wasteBinId: string | null;
  collectionReason: CollectionReason;
  status: CollectionTaskStatus;
  scheduledAt: string;
  handlingNotes: string | null;
  schedulingReason: string | null;
  createdByUserId: string;
  createdByUserName: string | null;
  creationMethod: string;
  createdAt: string;
  updatedAt: string | null;
  targetSummary: CollectionTaskTargetSummaryDto;
  statusHistory?: CollectionTaskStatusHistoryDto[];
  scheduleHistory?: CollectionTaskScheduleHistoryDto[];
}
export interface RescheduleCollectionTaskRequest { newScheduledAt: string; reason: string; }
export interface CreateReplacementCollectionTaskRequest {
  scheduledAt: string;
  replacementReason: string;
  handlingNotes?: string | null;
  schedulingReason?: string | null;
}
export interface CollectionTaskStatusHistoryDto { id: string; fromStatus: string | null; toStatus: string; changedByUserId: string | null; changedByUserName: string | null; notes: string | null; changedAt: string; }
export interface CollectionTaskScheduleHistoryDto { id: string; previousScheduledAt: string; newScheduledAt: string; reason: string; rescheduledByUserId: string; rescheduledByUserName: string | null; rescheduledAt: string; }
export interface CollectionTaskAuditTrailDto { collectionTaskId: string; taskCode: string; statusHistory: CollectionTaskStatusHistoryDto[]; scheduleHistory: CollectionTaskScheduleHistoryDto[]; }

export interface CollectionTaskTargetSummaryDto { identifier: string; latitude: number; longitude: number; addressText: string | null; capacityLiters: number | null; wasteTypes: string[]; latestFillLevelPercent: number | null; }
export interface CollectionTaskSummaryDto { id: string; taskCode: string; targetType: CollectionTaskTargetType; wasteReportId: string | null; wasteBinId: string | null; targetReference: string; collectionReason: CollectionReason; status: CollectionTaskStatus; scheduledAt: string; creationMethod: string; createdByUserId: string; createdByUserName: string | null; createdAt: string; }
export interface CollectionTaskListParams { status?: CollectionTaskStatus | ''; targetType?: CollectionTaskTargetType | ''; collectionReason?: CollectionReason | ''; dateFrom?: string; dateTo?: string; page?: number; pageSize?: number; }
export type PagedCollectionTasks = PagedResult<CollectionTaskSummaryDto>;
