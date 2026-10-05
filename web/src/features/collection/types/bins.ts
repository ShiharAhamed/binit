/**
 * Component 2 bin-management types aligned with GET /api/v1/bins.
 */

export type BinAdministrativeStatus = 'Active' | 'OutOfService' | 'Retired';
export type BinCondition = 'Good' | 'Damaged' | 'Blocked' | 'Missing';
export type WasteType =
  | 'General'
  | 'Organic'
  | 'Recyclable'
  | 'Hazardous'
  | 'Bulky'
  | 'Other';

export interface WasteBinSummaryDto {
  id: string;
  binCode: string;
  latitude: number;
  longitude: number;
  addressText: string | null;
  capacityLiters: number;
  administrativeStatus: BinAdministrativeStatus;
  acceptedWasteTypes: string[];
  collectionWeekdays: number[];
  latestFillLevelPercent: number | null;
  latestCondition: BinCondition | null;
  latestObservationAt: string | null;
  hasActiveTask: boolean;
  lastCollectedAt: string | null;
  createdAt: string;
}

/** Latest observation embedded in GET /api/v1/bins/{id}. */
export interface BinObservationSummaryDto {
  id: string;
  fillLevelPercent: number;
  condition: BinCondition;
  notes: string | null;
  recordedByUserId: string;
  recordedByUserName: string | null;
  recordedAt: string;
}

/** Detail DTO returned by GET /api/v1/bins/{id}. */
export interface WasteBinDetailDto {
  id: string;
  binCode: string;
  latitude: number;
  longitude: number;
  addressText: string | null;
  capacityLiters: number;
  administrativeStatus: BinAdministrativeStatus;
  acceptedWasteTypes: string[];
  collectionWeekdays: number[];
  lastCollectedAt: string | null;
  latestObservation: BinObservationSummaryDto | null;
  hasActiveTask: boolean;
  activeTaskId: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface CreateWasteBinRequest {
  binCode: string;
  latitude: number;
  longitude: number;
  addressText: string | null;
  capacityLiters: number;
  acceptedWasteTypes: WasteType[];
  collectionWeekdays: number[];
}

export interface UpdateWasteBinRequest {
  latitude: number;
  longitude: number;
  addressText: string | null;
  capacityLiters: number;
  acceptedWasteTypes: WasteType[];
  collectionWeekdays: number[];
}

/** Request accepted by POST /api/v1/bins/{id}/deactivate. */
export interface DeactivateWasteBinRequest {
  targetStatus: 'OutOfService' | 'Retired';
  reason: string | null;
}

export interface BinObservationDto {
  id: string;
  wasteBinId: string;
  fillLevelPercent: 0 | 25 | 50 | 75 | 100;
  condition: BinCondition;
  notes: string | null;
  recordedByUserId: string;
  recordedByUserName: string | null;
  recordedAt: string;
}

export interface RecordBinObservationRequest {
  fillLevelPercent: 0 | 25 | 50 | 75 | 100;
  condition: BinCondition;
  notes: string | null;
}

export interface ObservationListParams {
  page?: number;
  pageSize?: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/** Query parameters supported by GET /api/v1/bins. */
export interface WasteBinListParams {
  status?: BinAdministrativeStatus | '';
  wasteType?: WasteType | '';
  condition?: BinCondition | '';
  minFillLevel?: 0 | 25 | 50 | 75 | 100 | '';
  search?: string;
  page?: number;
  pageSize?: number;
}

export const BIN_STATUS_LABELS: Record<BinAdministrativeStatus, string> = {
  Active: 'Active',
  OutOfService: 'Out of service',
  Retired: 'Retired',
};

export const BIN_CONDITION_LABELS: Record<BinCondition, string> = {
  Good: 'Good',
  Damaged: 'Damaged',
  Blocked: 'Blocked',
  Missing: 'Missing',
};
