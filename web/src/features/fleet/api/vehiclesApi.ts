import { axiosClient } from '../../../api/axiosClient';
import type { PagedVehicles, VehicleDetailDto, VehicleListParams, VehicleOperationalStatus, VehicleRequest } from '../types/fleet';

export const vehiclesApi = {
  getVehicles: async (query: VehicleListParams = {}) => (await axiosClient.get<PagedVehicles>('/vehicles', { params: query })).data,
  getVehicle: async (id: string) => (await axiosClient.get<VehicleDetailDto>(`/vehicles/${id}`)).data,
  createVehicle: async (request: VehicleRequest) => (await axiosClient.post<VehicleDetailDto>('/vehicles', request)).data,
  updateVehicle: async (id: string, request: VehicleRequest) => (await axiosClient.patch<VehicleDetailDto>(`/vehicles/${id}`, request)).data,
  updateOperationalStatus: async (id: string, operationalStatus: VehicleOperationalStatus) => (await axiosClient.patch<VehicleDetailDto>(`/vehicles/${id}/operational-status`, { operationalStatus })).data,
};
