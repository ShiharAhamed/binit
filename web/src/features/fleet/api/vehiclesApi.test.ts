import { describe, expect, it, vi } from 'vitest';
import { axiosClient } from '../../../api/axiosClient';
import { vehiclesApi } from './vehiclesApi';
import type { VehicleRequest } from '../types/fleet';

vi.mock('../../../api/axiosClient', () => ({
  axiosClient: { get: vi.fn(), post: vi.fn(), patch: vi.fn() },
}));

describe('vehiclesApi', () => {
  const vehicleRequest: VehicleRequest = {
    registrationNumber: 'WP-C34-1001',
    vehicleType: 'Compactor' as const,
    capacityLiters: 8500,
    supportedWasteTypes: ['General', 'Organic'],
    notes: 'Depot vehicle',
  };

  it('uses the contracted vehicle list and detail routes', async () => {
    (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { items: [] } });

    await vehiclesApi.getVehicles({ search: 'WP-C34', operationalStatus: 'Available', vehicleType: 'Compactor', page: 2, pageSize: 20 });
    await vehiclesApi.getVehicle('vehicle-1');

    expect(axiosClient.get).toHaveBeenNthCalledWith(1, '/vehicles', { params: { search: 'WP-C34', operationalStatus: 'Available', vehicleType: 'Compactor', page: 2, pageSize: 20 } });
    expect(axiosClient.get).toHaveBeenNthCalledWith(2, '/vehicles/vehicle-1');
  });

  it('keeps create, edit, and operational-status payloads distinct', async () => {
    (axiosClient.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { id: 'vehicle-1' } });
    (axiosClient.patch as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { id: 'vehicle-1' } });

    await vehiclesApi.createVehicle(vehicleRequest);
    await vehiclesApi.updateVehicle('vehicle-1', vehicleRequest);
    await vehiclesApi.updateOperationalStatus('vehicle-1', 'Maintenance');

    expect(axiosClient.post).toHaveBeenCalledWith('/vehicles', vehicleRequest);
    expect(axiosClient.patch).toHaveBeenNthCalledWith(1, '/vehicles/vehicle-1', vehicleRequest);
    expect(axiosClient.patch).toHaveBeenNthCalledWith(2, '/vehicles/vehicle-1/operational-status', { operationalStatus: 'Maintenance' });
  });
});
