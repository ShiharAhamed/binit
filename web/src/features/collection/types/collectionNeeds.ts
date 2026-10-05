import type { BinCondition, PagedResult, WasteType } from './bins';

export type CollectionNeedTargetType = 'Report' | 'Bin';
export type CollectionReason = 'VerifiedReport' | 'FullOrBlockedBin' | 'RoutineCollection' | 'OfficerDiscretion';
export type CollectionNeedReason = Exclude<CollectionReason, 'OfficerDiscretion'>;

export interface CollectionNeedBinDetailsDto {
  binCode: string;
  capacityLiters: number;
  latestFillLevelPercent: number | null;
  latestCondition: BinCondition | null;
  observationAgeHours: number | null;
}

export interface CollectionNeedItemDto {
  id: string;
  reportReference?: string | null;
  targetType: CollectionNeedTargetType;
  collectionReason: CollectionNeedReason;
  title: string;
  latitude: number;
  longitude: number;
  addressText: string | null;
  wasteTypes: string[];
  urgency: string;
  triggerDate: string;
  attachmentCount: number;
  binDetails: CollectionNeedBinDetailsDto | null;
}

export interface CollectionNeedListParams {
  targetType?: CollectionNeedTargetType | '';
  collectionReason?: CollectionNeedReason | '';
  wasteType?: WasteType | '';
  search?: string;
  page?: number;
  pageSize?: number;
}

export type PagedCollectionNeeds = PagedResult<CollectionNeedItemDto>;
