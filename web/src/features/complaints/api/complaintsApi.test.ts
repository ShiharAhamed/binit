import { describe, it, expect, vi, beforeEach } from 'vitest';
import { complaintsApi } from './complaintsApi';
import { axiosClient } from '../../../api/axiosClient';

vi.mock('../../../api/axiosClient', () => ({
  axiosClient: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

describe('complaintsApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getComplaints', () => {
    it('calls GET /complaints with cleaned parameters', async () => {
      const mockResult = {
        items: [],
        page: 1,
        pageSize: 20,
        totalCount: 0,
        totalPages: 0,
      };
      (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockResult });

      const result = await complaintsApi.getComplaints({
        page: 2,
        pageSize: 10,
        status: 'Submitted',
        category: 'MissedCollection',
        search: '  Pettah  ',
        sortBy: 'createdAt',
        sortDirection: 'desc',
      });

      expect(axiosClient.get).toHaveBeenCalledWith('/complaints', {
        params: {
          page: 2,
          pageSize: 10,
          status: 'Submitted',
          category: 'MissedCollection',
          search: 'Pettah',
          sortBy: 'createdAt',
          sortDirection: 'desc',
        },
      });
      expect(result).toEqual(mockResult);
    });

    it('omits empty filter parameters', async () => {
      (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { items: [] } });

      await complaintsApi.getComplaints({
        page: 1,
        pageSize: 20,
        status: '',
        category: '',
        search: '',
      });

      expect(axiosClient.get).toHaveBeenCalledWith('/complaints', {
        params: {
          page: 1,
          pageSize: 20,
        },
      });
    });
  });

  describe('getComplaint', () => {
    it('calls GET /complaints/{id}', async () => {
      const mockDetail = {
        id: 'complaint-1',
        subject: 'Missed bin',
        status: 'Submitted',
      };
      (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockDetail });

      const result = await complaintsApi.getComplaint('complaint-1');

      expect(axiosClient.get).toHaveBeenCalledWith('/complaints/complaint-1');
      expect(result).toEqual(mockDetail);
    });
  });

  describe('startReview', () => {
    it('calls POST /complaints/{id}/review', async () => {
      const mockUpdated = {
        id: 'complaint-1',
        status: 'InReview',
      };
      (axiosClient.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockUpdated });

      const result = await complaintsApi.startReview('complaint-1');

      expect(axiosClient.post).toHaveBeenCalledWith('/complaints/complaint-1/review');
      expect(result).toEqual(mockUpdated);
    });
  });

  describe('resolveComplaint', () => {
    it('calls POST /complaints/{id}/resolve with payload', async () => {
      const mockResolved = {
        id: 'complaint-1',
        status: 'Resolved',
        resolutionNote: 'Fixed and verified by field team.',
      };
      (axiosClient.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockResolved });

      const result = await complaintsApi.resolveComplaint(
        'complaint-1',
        'Fixed and verified by field team.'
      );

      expect(axiosClient.post).toHaveBeenCalledWith('/complaints/complaint-1/resolve', {
        resolutionNote: 'Fixed and verified by field team.',
      });
      expect(result).toEqual(mockResolved);
    });
  });
});
