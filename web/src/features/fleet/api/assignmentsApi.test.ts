import { describe, expect, it, vi } from 'vitest';
import { axiosClient } from '../../../api/axiosClient';
import { assignmentsApi } from './assignmentsApi';

vi.mock('../../../api/axiosClient', () => ({
  axiosClient: { get: vi.fn(), post: vi.fn(), patch: vi.fn() },
}));

describe('assignmentsApi', () => {
  it('uses the contracted read-only dispatch endpoints and safe pagination', async () => {
    (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { items: [] } });

    await assignmentsApi.getAvailableTasks({ page: 2, pageSize: 20 });
    await assignmentsApi.getAssignments({ page: 3, pageSize: 20, status: 'InProgress' });

    expect(axiosClient.get).toHaveBeenNthCalledWith(1, '/collection-tasks/available-for-assignment', { params: { page: 2, pageSize: 20 } });
    expect(axiosClient.get).toHaveBeenNthCalledWith(2, '/assignments', { params: { page: 3, pageSize: 20, status: 'InProgress' } });
  });

  it('posts only the contracted assignment membership, sequence, and optional acknowledgement fields', async () => {
    (axiosClient.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { id: 'assignment-1' } });
    const request = {
      driverId: 'driver-1',
      vehicleId: 'vehicle-1',
      collectionTaskIds: ['task-2', 'task-1'],
      stops: [{ collectionTaskId: 'task-2', sequence: 1 }, { collectionTaskId: 'task-1', sequence: 2 }],
      compatibilityAcknowledgement: 'Officer reviewed the mixed bin waste types.',
    };

    await assignmentsApi.createAssignment(request);

    expect(axiosClient.post).toHaveBeenCalledWith('/assignments', request);
  });

  it('fetches assignment detail and history using contracted routes', async () => {
    (axiosClient.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { id: 'assignment-1' } });

    await assignmentsApi.getAssignmentDetail('assignment-1');
    await assignmentsApi.getAssignmentHistory('assignment-1');

    expect(axiosClient.get).toHaveBeenCalledWith('/assignments/assignment-1');
    expect(axiosClient.get).toHaveBeenCalledWith('/assignments/assignment-1/history');
  });

  it('patches route stops ordering with contiguous 1-based sequence numbers', async () => {
    (axiosClient.patch as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { id: 'route-1', stops: [] } });
    const request = {
      stops: [
        { routeStopId: 'stop-2', sequence: 1 },
        { routeStopId: 'stop-1', sequence: 2 },
      ],
    };

    await assignmentsApi.reorderRouteStops('assignment-1', request);

    expect(axiosClient.patch).toHaveBeenCalledWith('/assignments/assignment-1/route/stops', request);
  });

  it('posts assignment cancellation with mandatory justification reason', async () => {
    (axiosClient.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { id: 'assignment-1', status: 'Cancelled' } });
    const request = {
      reason: 'Vehicle mechanical breakdown before route start.',
    };

    await assignmentsApi.cancelAssignment('assignment-1', request);

    expect(axiosClient.post).toHaveBeenCalledWith('/assignments/assignment-1/cancel', request);
  });
});
