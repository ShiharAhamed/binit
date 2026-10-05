import { axiosClient } from '../../../api/axiosClient';
import type { DriverDetailDto, DriverListParams, PagedDrivers } from '../types/fleet';

export const driversApi = {
  getDrivers: async (query: DriverListParams = {}) => (await axiosClient.get<PagedDrivers>('/drivers', { params: query })).data,
  getDriver: async (id: string) => (await axiosClient.get<DriverDetailDto>(`/drivers/${id}`)).data,
};
