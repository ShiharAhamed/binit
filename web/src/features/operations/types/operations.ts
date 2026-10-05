export type OperationalIssueStatus = 'Reported' | 'InReview' | 'Resolved';

export type OperationalIssueType =
  | 'VehicleProblem'
  | 'RoadOrAccessIssue'
  | 'EquipmentProblem'
  | 'SafetyConcern'
  | 'OperationalDelay'
  | 'Other';

export const OPERATIONAL_ISSUE_TYPE_LABELS: Record<OperationalIssueType, string> = {
  VehicleProblem: 'Vehicle Problem',
  RoadOrAccessIssue: 'Road / Access Issue',
  EquipmentProblem: 'Equipment Problem',
  SafetyConcern: 'Safety Concern',
  OperationalDelay: 'Operational Delay',
  Other: 'Other',
};

export const OPERATIONAL_ISSUE_STATUS_LABELS: Record<OperationalIssueStatus, string> = {
  Reported: 'Reported',
  InReview: 'In Review',
  Resolved: 'Resolved',
};

export interface OperationalIssueSummaryDto {
  id: string;
  driverId: string;
  driverName?: string | null;
  issueType: OperationalIssueType;
  title: string;
  status: OperationalIssueStatus;
  latitude?: number | null;
  longitude?: number | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface OperationalIssueDetailDto {
  id: string;
  driverId: string;
  driverName?: string | null;
  issueType: OperationalIssueType;
  title: string;
  description: string;
  latitude?: number | null;
  longitude?: number | null;
  locationDescription?: string | null;
  status: OperationalIssueStatus;
  resolutionNote?: string | null;
  resolvedAt?: string | null;
  resolvedByUserId?: string | null;
  resolvedByUserName?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface OperationalIssueListParams {
  page?: number;
  pageSize?: number;
  status?: OperationalIssueStatus | '';
  issueType?: OperationalIssueType | '';
  search?: string;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface ResolveOperationalIssueRequest {
  resolutionNote: string;
}
