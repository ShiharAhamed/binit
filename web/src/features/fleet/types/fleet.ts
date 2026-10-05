import type { PagedResult } from '../../collection/types/bins';

export type VehicleType = 'Compactor' | 'Flatbed' | 'Tipper' | 'SmallVan';
export type VehicleOperationalStatus = 'Available' | 'Maintenance' | 'Inactive';
export type WasteType = 'General' | 'Organic' | 'Recyclable' | 'Hazardous' | 'Bulky' | 'Other';

export interface VehicleSummaryDto { id: string; registrationNumber: string; vehicleType: VehicleType; operationalStatus: VehicleOperationalStatus; supportedWasteTypes: WasteType[]; isOccupied: boolean; }
export interface VehicleDetailDto extends VehicleSummaryDto { capacityLiters: number; notes: string | null; currentAssignmentId: string | null; createdAt: string; updatedAt: string | null; }
export interface VehicleRequest { registrationNumber: string; vehicleType: VehicleType | ''; capacityLiters: number | ''; supportedWasteTypes: WasteType[]; notes: string; }
export interface VehicleListParams { search?: string; operationalStatus?: VehicleOperationalStatus | ''; vehicleType?: VehicleType | ''; page?: number; pageSize?: number; }
export type PagedVehicles = PagedResult<VehicleSummaryDto>;

export type DriverAvailabilityStatus = 'Available' | 'OffDuty';
export interface DriverSummaryDto { id: string; displayName: string; availabilityStatus: DriverAvailabilityStatus; isOccupied: boolean; }
export interface DriverDetailDto extends DriverSummaryDto {}
export interface DriverListParams { search?: string; availabilityStatus?: DriverAvailabilityStatus | ''; page?: number; pageSize?: number; }
export type PagedDrivers = PagedResult<DriverSummaryDto>;
