import { describe, expect, it, vi } from 'vitest';
import { axiosClient } from '../../../api/axiosClient';
import { driversApi } from './driversApi';

vi.mock('../../../api/axiosClient', () => ({ axiosClient: { get: vi.fn(), put: vi.fn() } }));

describe('driversApi', () => {
  it('uses AppUser IDs for simplified Driver list and detail routes', async () => {
    (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { items: [] } });
    await driversApi.getDrivers({ search: 'Kasun', availabilityStatus: 'Available', page: 2, pageSize: 20 });
    await driversApi.getDriver('driver-user-1');
    expect(axiosClient.get).toHaveBeenNthCalledWith(1, '/drivers', { params: { search: 'Kasun', availabilityStatus: 'Available', page: 2, pageSize: 20 } });
    expect(axiosClient.get).toHaveBeenNthCalledWith(2, '/drivers/driver-user-1');
    expect(axiosClient.put).not.toHaveBeenCalled();
  });
});
