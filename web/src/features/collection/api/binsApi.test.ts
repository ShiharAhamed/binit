import { beforeEach, describe, expect, it, vi } from 'vitest';
import { axiosClient } from '../../../api/axiosClient';
import { binsApi } from './binsApi';

vi.mock('../../../api/axiosClient', () => ({ axiosClient: { get: vi.fn(), post: vi.fn(), put: vi.fn() } }));

describe('binsApi', () => {
  beforeEach(() => vi.clearAllMocks());

  it('calls GET /bins with only supported non-empty filters and pagination', async () => {
    const response = { items: [], page: 2, pageSize: 20, totalCount: 0, totalPages: 0 };
    (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: response });

    await expect(binsApi.getBins({ status: 'Active', wasteType: 'General', condition: 'Good', minFillLevel: 75, search: '  Pettah ', page: 2, pageSize: 20 })).resolves.toEqual(response);
    expect(axiosClient.get).toHaveBeenCalledWith('/bins', { params: { status: 'Active', wasteType: 'General', condition: 'Good', minFillLevel: 75, search: 'Pettah', page: 2, pageSize: 20 } });
  });

  it('calls GET /bins/{id} for an internal bin detail record', async () => {
    const response = { id: 'bin-1', binCode: 'BIN-COL-0042' };
    (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: response });

    await expect(binsApi.getBin('bin-1')).resolves.toEqual(response);
    expect(axiosClient.get).toHaveBeenCalledWith('/bins/bin-1');
  });

  it('uses the exact create and update routes with their request payloads', async () => {
    const createRequest = { binCode: 'BIN-COL-0043', latitude: 6.93, longitude: 79.85, addressText: null, capacityLiters: 660, acceptedWasteTypes: ['General'] as Array<'General'>, collectionWeekdays: [1, 4] };
    const updateRequest = { latitude: 6.93, longitude: 79.85, addressText: 'Main Street', capacityLiters: 660, acceptedWasteTypes: ['General'] as Array<'General'>, collectionWeekdays: [1, 4] };
    (axiosClient.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { id: 'bin-2' } });
    (axiosClient.put as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { id: 'bin-1' } });

    await binsApi.createBin(createRequest);
    await binsApi.updateBin('bin-1', updateRequest);

    expect(axiosClient.post).toHaveBeenCalledWith('/bins', createRequest);
    expect(axiosClient.put).toHaveBeenCalledWith('/bins/bin-1', updateRequest);
  });

  it('uses the observation history and recording routes with supported payload fields', async () => {
    (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { items: [], page: 2, pageSize: 10, totalCount: 11, totalPages: 2 } });
    (axiosClient.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { id: 'obs-1' } });

    await binsApi.getObservations('bin-1', { page: 2, pageSize: 10 });
    await binsApi.recordObservation('bin-1', { fillLevelPercent: 75, condition: 'Blocked', notes: 'Access obstructed' });

    expect(axiosClient.get).toHaveBeenCalledWith('/bins/bin-1/observations', { params: { page: 2, pageSize: 10 } });
    expect(axiosClient.post).toHaveBeenCalledWith('/bins/bin-1/observations', { fillLevelPercent: 75, condition: 'Blocked', notes: 'Access obstructed' });
  });
});
