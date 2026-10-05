export type ComplaintStatus = 'Submitted' | 'InReview' | 'Resolved';

export type ComplaintCategory =
  | 'MissedCollection'
  | 'DelayedService'
  | 'PoorService'
  | 'UnresolvedIssue'
  | 'Other';

export const COMPLAINT_CATEGORY_LABELS: Record<ComplaintCategory, string> = {
  MissedCollection: 'Missed Collection',
  DelayedService: 'Delayed Service',
  PoorService: 'Poor Service',
  UnresolvedIssue: 'Unresolved Issue',
  Other: 'Other',
};

export const COMPLAINT_STATUS_LABELS: Record<ComplaintStatus, string> = {
  Submitted: 'Submitted',
  InReview: 'In Review',
  Resolved: 'Resolved',
};

export interface ComplaintSummaryDto {
  id: string;
  citizenId: string;
  citizenName?: string | null;
  category: ComplaintCategory;
  subject: string;
  status: ComplaintStatus;
  latitude?: number | null;
  longitude?: number | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface ComplaintDetailDto {
  id: string;
  citizenId: string;
  citizenName?: string | null;
  category: ComplaintCategory;
  subject: string;
  description: string;
  latitude?: number | null;
  longitude?: number | null;
  locationDescription?: string | null;
  status: ComplaintStatus;
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

export interface ComplaintListParams {
  page?: number;
  pageSize?: number;
  status?: ComplaintStatus | '';
  category?: ComplaintCategory | '';
  search?: string;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface ResolveComplaintRequest {
  resolutionNote: string;
}
