/**
 * Component 1 — Waste Reporting & Citizen Management TypeScript Types.
 * Strictly aligned with ASP.NET Core backend DTOs and Enums.
 */

export type WasteType =
  | 'General'
  | 'Organic'
  | 'Recyclable'
  | 'Hazardous'
  | 'Bulky'
  | 'Other';

export type WasteReportStatus =
  | 'Submitted'
  | 'UnderReview'
  | 'Verified'
  | 'Rejected'
  | 'Scheduled'
  | 'InProgress'
  | 'Resolved'
  | 'Cancelled';

export type WasteReportPriority = 'Low' | 'Medium' | 'High' | 'Urgent';
export interface VerifyWasteReportRequest { priority: WasteReportPriority; }

/**
 * Summary DTO returned by GET /api/v1/waste-reports (WasteReportSummaryDto)
 */
export interface WasteReportSummaryDto {
  reportReference?: string;
  id: string;
  description: string;
  wasteType: WasteType;
  status: WasteReportStatus;
  priority: WasteReportPriority | null;
  addressText: string | null;
  latitude: number;
  longitude: number;
  citizenId: string | null;
  citizenName: string | null;
  createdAt: string;
  updatedAt: string | null;
}

/**
 * Photographic attachment response DTO (ReportAttachmentDto)
 */
export interface ReportAttachmentDto {
  id: string;
  wasteReportId: string;
  fileUrl: string;
  fileType: string;
  createdAt: string;
}

/**
 * Full detail response DTO for a specific waste report (WasteReportDetailDto)
 */
export interface WasteReportDetailDto {
  reportReference?: string;
  id: string;
  citizenId: string;
  citizenName: string;
  description: string;
  wasteType: WasteType;
  latitude: number;
  longitude: number;
  addressText: string | null;
  status: WasteReportStatus;
  priority: WasteReportPriority | null;
  verifiedByUserId: string | null;
  verifiedByUserName: string | null;
  verifiedAt: string | null;
  attachments: ReportAttachmentDto[];
  createdAt: string;
  updatedAt: string | null;
}

/**
 * Status transition audit record response DTO (WasteReportStatusHistoryDto)
 */
export interface WasteReportStatusHistoryDto {
  id: string;
  wasteReportId: string;
  fromStatus: WasteReportStatus | null;
  toStatus: WasteReportStatus;
  changedByUserId: string | null;
  changedByUserName: string | null;
  notes: string | null;
  changedAt: string;
}

/**
 * Standard pagination response envelope conforming to api-contract.md (PagedResult<T>)
 */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/**
 * Query filter, search, sort, and pagination parameters for GET /api/v1/waste-reports
 */
export interface WasteReportListParams {
  page?: number;
  pageSize?: number;
  status?: WasteReportStatus | '';
  wasteType?: WasteType | '';
  search?: string;
  sortBy?: 'createdAt' | 'updatedAt';
  sortDirection?: 'asc' | 'desc';
  fromDate?: string;
  toDate?: string;
}

/**
 * Request payload for POST /api/v1/waste-reports/{id}/reject
 */
export interface RejectWasteReportRequest {
  reason: string;
}

/**
 * Readable display labels for WasteReportStatus
 */
export const STATUS_LABELS: Record<WasteReportStatus, string> = {
  Submitted: 'Submitted',
  UnderReview: 'Under Review',
  Verified: 'Verified',
  Rejected: 'Rejected',
  Scheduled: 'Scheduled',
  InProgress: 'In Progress',
  Resolved: 'Resolved',
  Cancelled: 'Cancelled',
};

/**
 * Readable display labels for WasteType
 */
export const WASTE_TYPE_LABELS: Record<WasteType, string> = {
  General: 'General',
  Organic: 'Organic',
  Recyclable: 'Recyclable',
  Hazardous: 'Hazardous',
  Bulky: 'Bulky',
  Other: 'Other',
};
