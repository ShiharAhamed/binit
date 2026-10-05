import { describe, it, expect, vi, beforeEach } from 'vitest';
import { operationsApi } from './operationsApi';
import { axiosClient } from '../../../api/axiosClient';

vi.mock('../../../api/axiosClient', () => ({
  axiosClient: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

describe('operationsApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getOperationalIssues', () => {
    it('calls GET /operational-issues with cleaned parameters', async () => {
      const mockResult = {
        items: [],
        page: 1,
        pageSize: 20,
        totalCount: 0,
        totalPages: 0,
      };
      (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockResult });

      const result = await operationsApi.getOperationalIssues({
        page: 2,
        pageSize: 10,
        status: 'Reported',
        issueType: 'VehicleProblem',
        search: '  Flat tire  ',
        sortBy: 'createdAt',
        sortDirection: 'desc',
      });

      expect(axiosClient.get).toHaveBeenCalledWith('/operational-issues', {
        params: {
          page: 2,
          pageSize: 10,
          status: 'Reported',
          issueType: 'VehicleProblem',
          search: 'Flat tire',
          sortBy: 'createdAt',
          sortDirection: 'desc',
        },
      });
      expect(result).toEqual(mockResult);
    });

    it('omits empty filter parameters', async () => {
      (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { items: [] } });

      await operationsApi.getOperationalIssues({
        page: 1,
        pageSize: 20,
        status: '',
        issueType: '',
        search: '',
      });

      expect(axiosClient.get).toHaveBeenCalledWith('/operational-issues', {
        params: {
          page: 1,
          pageSize: 20,
        },
      });
    });
  });

  describe('getOperationalIssue', () => {
    it('calls GET /operational-issues/{id}', async () => {
      const mockDetail = {
        id: 'issue-1',
        title: 'Hydraulic leak',
        status: 'Reported',
      };
      (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockDetail });

      const result = await operationsApi.getOperationalIssue('issue-1');

      expect(axiosClient.get).toHaveBeenCalledWith('/operational-issues/issue-1');
      expect(result).toEqual(mockDetail);
    });
  });

  describe('startReview', () => {
    it('calls POST /operational-issues/{id}/review', async () => {
      const mockUpdated = {
        id: 'issue-1',
        status: 'InReview',
      };
      (axiosClient.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockUpdated });

      const result = await operationsApi.startReview('issue-1');

      expect(axiosClient.post).toHaveBeenCalledWith('/operational-issues/issue-1/review');
      expect(result).toEqual(mockUpdated);
    });
  });

  describe('resolveOperationalIssue', () => {
    it('calls POST /operational-issues/{id}/resolve with payload', async () => {
      const mockResolved = {
        id: 'issue-1',
        status: 'Resolved',
        resolutionNote: 'Maintenance team dispatched and truck serviced.',
      };
      (axiosClient.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockResolved });

      const result = await operationsApi.resolveOperationalIssue(
        'issue-1',
        'Maintenance team dispatched and truck serviced.'
      );

      expect(axiosClient.post).toHaveBeenCalledWith('/operational-issues/issue-1/resolve', {
        resolutionNote: 'Maintenance team dispatched and truck serviced.',
      });
      expect(result).toEqual(mockResolved);
    });
  });
});
