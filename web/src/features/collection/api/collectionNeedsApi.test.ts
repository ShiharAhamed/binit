import { beforeEach, describe, expect, it, vi } from 'vitest';
import { axiosClient } from '../../../api/axiosClient';
import { collectionNeedsApi } from './collectionNeedsApi';

vi.mock('../../../api/axiosClient', () => ({ axiosClient: { get: vi.fn() } }));

describe('collectionNeedsApi', () => {
  beforeEach(() => vi.clearAllMocks());
  it('calls the read-only queue with only actual supported query fields', async () => {
    (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { items: [], page: 2, pageSize: 20, totalCount: 0, totalPages: 0 } });
    await collectionNeedsApi.getCollectionNeeds({ targetType: 'Bin', collectionReason: 'RoutineCollection', wasteType: 'General', search: '  Pettah ', page: 2, pageSize: 20 });
    expect(axiosClient.get).toHaveBeenCalledWith('/collection-needs', { params: { targetType: 'Bin', collectionReason: 'RoutineCollection', wasteType: 'General', search: 'Pettah', page: 2, pageSize: 20 } });
  });
});
