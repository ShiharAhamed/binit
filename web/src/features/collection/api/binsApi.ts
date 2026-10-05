import { axiosClient } from '../../../api/axiosClient';
import type {
  PagedResult,
  CreateWasteBinRequest,
  DeactivateWasteBinRequest,
  UpdateWasteBinRequest,
  BinObservationDto,
  ObservationListParams,
  RecordBinObservationRequest,
  WasteBinDetailDto,
  WasteBinListParams,
  WasteBinSummaryDto,
} from '../types/bins';

export const binsApi = {
  /** Retrieves the paginated internal staff bin registry. */
  getBins: async (
    query: WasteBinListParams = {},
  ): Promise<PagedResult<WasteBinSummaryDto>> => {
    const params = {
      ...(query.status ? { status: query.status } : {}),
      ...(query.wasteType ? { wasteType: query.wasteType } : {}),
      ...(query.condition ? { condition: query.condition } : {}),
      ...(query.minFillLevel !== '' && query.minFillLevel !== undefined
        ? { minFillLevel: query.minFillLevel }
        : {}),
      ...(query.search?.trim() ? { search: query.search.trim() } : {}),
      ...(query.page ? { page: query.page } : {}),
      ...(query.pageSize ? { pageSize: query.pageSize } : {}),
    };

    const response = await axiosClient.get<PagedResult<WasteBinSummaryDto>>('/bins', { params });
    return response.data;
  },

  /** Retrieves a single internal staff bin detail record. */
  getBin: async (id: string): Promise<WasteBinDetailDto> => {
    const response = await axiosClient.get<WasteBinDetailDto>(`/bins/${id}`);
    return response.data;
  },

  createBin: async (request: CreateWasteBinRequest): Promise<WasteBinDetailDto> => {
    const response = await axiosClient.post<WasteBinDetailDto>('/bins', request);
    return response.data;
  },

  updateBin: async (id: string, request: UpdateWasteBinRequest): Promise<WasteBinDetailDto> => {
    const response = await axiosClient.put<WasteBinDetailDto>(`/bins/${id}`, request);
    return response.data;
  },

  deactivateBin: async (id: string, request: DeactivateWasteBinRequest): Promise<WasteBinDetailDto> => {
    const response = await axiosClient.post<WasteBinDetailDto>(`/bins/${id}/deactivate`, request);
    return response.data;
  },

  getObservations: async (id: string, query: ObservationListParams = {}): Promise<PagedResult<BinObservationDto>> => {
    const response = await axiosClient.get<PagedResult<BinObservationDto>>(`/bins/${id}/observations`, { params: query });
    return response.data;
  },

  recordObservation: async (id: string, request: RecordBinObservationRequest): Promise<BinObservationDto> => {
    const response = await axiosClient.post<BinObservationDto>(`/bins/${id}/observations`, request);
    return response.data;
  },
};
