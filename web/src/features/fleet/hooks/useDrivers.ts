import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { driversApi } from '../api/driversApi';
import type { DriverListParams } from '../types/fleet';

const key = ['fleet', 'drivers'];
export const useDrivers = (params: DriverListParams) => useQuery({ queryKey: [...key, params], queryFn: () => driversApi.getDrivers(params), placeholderData: keepPreviousData });
export const useDriver = (id: string | null) => useQuery({ queryKey: [...key, id], queryFn: () => driversApi.getDriver(id!), enabled: Boolean(id) });
