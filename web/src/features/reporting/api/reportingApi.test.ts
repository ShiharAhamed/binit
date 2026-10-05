import { describe, it, expect, beforeEach, vi } from 'vitest';
import { axiosClient } from '../../../api/axiosClient';
import { reportingApi } from './reportingApi';
import type { PagedResult, WasteReportSummaryDto } from '../types/reporting';

vi.mock('../../../api/axiosClient', () => ({
  axiosClient: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

describe('reportingApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls GET /waste-reports with default parameters when none provided', async () => {
    const mockResponse: PagedResult<WasteReportSummaryDto> = {
      items: [],
      page: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0,
    };
    (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ data: mockResponse });

    const result = await reportingApi.getWasteReports();

    expect(axiosClient.get).toHaveBeenCalledWith('/waste-reports', { params: {} });
    expect(result).toEqual(mockResponse);
  });

  it('passes all specified query parameters to the backend', async () => {
    const mockReports: WasteReportSummaryDto[] = [
      {
        id: '9f2e3441-2ab3-4796-9ee8-7497ecb8782a',
        description: 'Overflowing garbage bin near station',
        wasteType: 'General',
        status: 'Submitted',
        priority: null,
        addressText: '123 Station Road',
        latitude: 6.9271,
        longitude: 79.8612,
        citizenId: 'c1',
        citizenName: 'Perera',
        createdAt: '2026-09-17T10:00:00Z',
        updatedAt: null,
      },
    ];
    const mockResponse: PagedResult<WasteReportSummaryDto> = {
      items: mockReports,
      page: 2,
      pageSize: 10,
      totalCount: 25,
      totalPages: 3,
    };
    (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ data: mockResponse });

    const result = await reportingApi.getWasteReports({
      page: 2,
      pageSize: 10,
      status: 'Submitted',
      wasteType: 'General',
      search: '  station  ',
      sortBy: 'createdAt',
      sortDirection: 'desc',
      fromDate: '2026-09-01T00:00:00Z',
      toDate: '2026-09-17T23:59:59Z',
    });

    expect(axiosClient.get).toHaveBeenCalledWith('/waste-reports', {
      params: {
        page: 2,
        pageSize: 10,
        status: 'Submitted',
        wasteType: 'General',
        search: 'station',
        sortBy: 'createdAt',
        sortDirection: 'desc',
        fromDate: '2026-09-01T00:00:00Z',
        toDate: '2026-09-17T23:59:59Z',
      },
    });
    expect(result).toEqual(mockResponse);
  });

  it('omits empty, null, or undefined parameters from query object', async () => {
    (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      data: { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 },
    });

    await reportingApi.getWasteReports({
      page: 1,
      pageSize: 20,
      status: '',
      wasteType: '',
      search: '   ',
    });

    expect(axiosClient.get).toHaveBeenCalledWith('/waste-reports', {
      params: {
        page: 1,
        pageSize: 20,
      },
    });
  });

  it('calls GET /waste-reports/{id} to fetch full report details', async () => {
    const mockDetail = {
      id: 'report-123',
      citizenId: 'c1',
      citizenName: 'Perera',
      description: 'Test report',
      wasteType: 'General',
      latitude: 6.9,
      longitude: 79.8,
      addressText: 'Colombo 01',
      status: 'Submitted',
      priority: null,
      verifiedByUserId: null,
      verifiedByUserName: null,
      verifiedAt: null,
      attachments: [],
      createdAt: '2026-09-17T10:00:00Z',
      updatedAt: null,
    };
    (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ data: mockDetail });

    const result = await reportingApi.getWasteReport('report-123');

    expect(axiosClient.get).toHaveBeenCalledWith('/waste-reports/report-123');
    expect(result).toEqual(mockDetail);
  });

  it('calls GET /waste-reports/{id}/history to fetch status audit trail', async () => {
    const mockHistory = [
      {
        id: 'hist-1',
        wasteReportId: 'report-123',
        fromStatus: null,
        toStatus: 'Submitted',
        changedByUserId: 'c1',
        changedByUserName: 'Perera',
        notes: 'Initial submission',
        changedAt: '2026-09-17T10:00:00Z',
      },
    ];
    (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ data: mockHistory });

    const result = await reportingApi.getWasteReportHistory('report-123');

    expect(axiosClient.get).toHaveBeenCalledWith('/waste-reports/report-123/history');
    expect(result).toEqual(mockHistory);
  });

  it('calls POST /waste-reports/{id}/start-review to initiate report review without request body', async () => {
    const mockUpdatedDetail = {
      id: 'report-123',
      citizenId: 'c1',
      citizenName: 'Perera',
      description: 'Test report',
      wasteType: 'General',
      latitude: 6.9,
      longitude: 79.8,
      addressText: 'Colombo 01',
      status: 'UnderReview',
      priority: null,
      verifiedByUserId: null,
      verifiedByUserName: null,
      verifiedAt: null,
      attachments: [],
      createdAt: '2026-09-17T10:00:00Z',
      updatedAt: '2026-09-17T11:00:00Z',
    };
    (axiosClient.post as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ data: mockUpdatedDetail });

    const result = await reportingApi.startReview('report-123');

    expect(axiosClient.post).toHaveBeenCalledWith('/waste-reports/report-123/start-review');
    expect(result).toEqual(mockUpdatedDetail);
    expect(result.status).toBe('UnderReview');
  });

  it('calls POST /waste-reports/{id}/verify to verify an under-review report without request body', async () => {
    const mockVerifiedDetail = {
      id: 'report-123',
      citizenId: 'c1',
      citizenName: 'Perera',
      description: 'Test report',
      wasteType: 'General',
      latitude: 6.9,
      longitude: 79.8,
      addressText: 'Colombo 01',
      status: 'Verified',
      priority: null,
      verifiedByUserId: 'u-officer-1',
      verifiedByUserName: 'Officer Perera',
      verifiedAt: '2026-09-17T11:30:00Z',
      attachments: [],
      createdAt: '2026-09-17T10:00:00Z',
      updatedAt: '2026-09-17T11:30:00Z',
    };
    (axiosClient.post as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ data: mockVerifiedDetail });

    const result = await reportingApi.verifyReport('report-123', 'High');

    expect(axiosClient.post).toHaveBeenCalledWith('/waste-reports/report-123/verify', { priority: 'High' });
    expect(result).toEqual(mockVerifiedDetail);
    expect(result.status).toBe('Verified');
    expect(result.verifiedByUserName).toBe('Officer Perera');
  });

  it('calls POST /waste-reports/{id}/reject with reason payload to reject an under-review report', async () => {
    const mockRejectedDetail = {
      id: 'report-123',
      citizenId: 'c1',
      citizenName: 'Perera',
      description: 'Test report',
      wasteType: 'General',
      latitude: 6.9,
      longitude: 79.8,
      addressText: 'Colombo 01',
      status: 'Rejected',
      priority: null,
      verifiedByUserId: null,
      verifiedByUserName: null,
      verifiedAt: null,
      attachments: [],
      createdAt: '2026-09-17T10:00:00Z',
      updatedAt: '2026-09-17T11:30:00Z',
    };
    (axiosClient.post as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ data: mockRejectedDetail });

    const result = await reportingApi.rejectReport('report-123', 'Duplicate report already being serviced.');

    expect(axiosClient.post).toHaveBeenCalledWith('/waste-reports/report-123/reject', {
      reason: 'Duplicate report already being serviced.',
    });
    expect(result).toEqual(mockRejectedDetail);
    expect(result.status).toBe('Rejected');
  });
});
