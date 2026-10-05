import { describe, expect, it, vi } from 'vitest';
import { axiosClient } from '../../../api/axiosClient';
import { collectionTasksApi } from './collectionTasksApi';

vi.mock('../../../api/axiosClient', () => ({ axiosClient: { post: vi.fn(), get: vi.fn() } }));

describe('collectionTasksApi', () => {
  it('posts the exact manual task request to the contracted endpoint', async () => {
    (axiosClient.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { id: 'task-1' } });
    const request = { wasteReportId: 'report-1', wasteBinId: null, collectionReason: 'VerifiedReport' as const, scheduledAt: '2099-01-01T04:30:00.000Z', handlingNotes: null, schedulingReason: null };
    await collectionTasksApi.createManualTask(request);
    expect(axiosClient.post).toHaveBeenCalledWith('/collection-tasks/manual', request);
  });
  it('uses only contracted list filters and detail route', async () => {
    (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { items: [] } });
    await collectionTasksApi.getTasks({ status: 'Scheduled', targetType: 'Bin', collectionReason: 'FullOrBlockedBin', dateFrom: '2099-01-01', dateTo: '2099-01-02', page: 2, pageSize: 20 });
    await collectionTasksApi.getTask('task-1');
    expect(axiosClient.get).toHaveBeenNthCalledWith(1, '/collection-tasks', { params: { status: 'Scheduled', targetType: 'Bin', collectionReason: 'FullOrBlockedBin', dateFrom: '2099-01-01', dateTo: '2099-01-02', page: 2, pageSize: 20 } });
    expect(axiosClient.get).toHaveBeenNthCalledWith(2, '/collection-tasks/task-1');
  });
  it('uses contracted reschedule and audit routes', async () => { (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} }); (axiosClient.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} }); await collectionTasksApi.getTaskHistory('task-1'); await collectionTasksApi.rescheduleTask('task-1', { newScheduledAt: '2099-01-01T04:30:00.000Z', reason: 'Vehicle delay' }); expect(axiosClient.get).toHaveBeenCalledWith('/collection-tasks/task-1/history'); expect(axiosClient.post).toHaveBeenCalledWith('/collection-tasks/task-1/reschedule', { newScheduledAt: '2099-01-01T04:30:00.000Z', reason: 'Vehicle delay' }); });

  it('posts the exact replacement request to the contracted replacement endpoint', async () => {
    (axiosClient.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { id: 'task-new-1', status: 'Scheduled' } });
    const request = {
      scheduledAt: '2099-01-02T04:30:00.000Z',
      replacementReason: 'Original driver was blocked by severe road construction.',
      handlingNotes: 'Approach via alternate side road',
      schedulingReason: 'Urgent replacement requested by municipal officer',
    };

    const result = await collectionTasksApi.createReplacementTask('task-failed-1', request);

    expect(axiosClient.post).toHaveBeenCalledWith('/collection-tasks/task-failed-1/replacement', request);
    expect(result.id).toBe('task-new-1');
  });
});
