import axios from 'axios';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { vehiclesApi } from '../api/vehiclesApi';
import type { VehicleListParams, VehicleOperationalStatus, VehicleRequest } from '../types/fleet';
const key = ['fleet', 'vehicles'];
export const useVehicles = (params: VehicleListParams) => useQuery({ queryKey: [...key, params], queryFn: () => vehiclesApi.getVehicles(params), placeholderData: keepPreviousData });
export const useVehicle = (id: string | null) => useQuery({ queryKey: [...key, id], queryFn: () => vehiclesApi.getVehicle(id!), enabled: Boolean(id) });
export const useVehicleMutations = () => { const client = useQueryClient(); const refresh = () => client.invalidateQueries({ queryKey: key }); const error = (e: unknown) => axios.isAxiosError(e) ? e.response?.data?.detail || e.response?.data?.title || 'Vehicle request failed.' : 'Vehicle request failed.'; return { create: useMutation({ mutationFn: (r: VehicleRequest) => vehiclesApi.createVehicle(r), onSuccess: refresh }), update: useMutation({ mutationFn: ({ id, request }: { id: string; request: VehicleRequest }) => vehiclesApi.updateVehicle(id, request), onSuccess: refresh }), status: useMutation({ mutationFn: ({ id, operationalStatus }: { id: string; operationalStatus: VehicleOperationalStatus }) => vehiclesApi.updateOperationalStatus(id, operationalStatus), onSuccess: refresh }), error }; };
