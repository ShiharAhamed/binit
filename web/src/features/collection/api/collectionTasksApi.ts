import { axiosClient } from '../../../api/axiosClient';
import type {
  CollectionTaskAuditTrailDto,
  CollectionTaskDetailDto,
  CollectionTaskListParams,
  CreateManualCollectionTaskRequest,
  CreateReplacementCollectionTaskRequest,
  PagedCollectionTasks,
  RescheduleCollectionTaskRequest,
} from '../types/collectionTasks';

export const collectionTasksApi = {
  getTasks: async (query: CollectionTaskListParams = {}): Promise<PagedCollectionTasks> => {
    const params = { ...(query.status ? { status: query.status } : {}), ...(query.targetType ? { targetType: query.targetType } : {}), ...(query.collectionReason ? { collectionReason: query.collectionReason } : {}), ...(query.dateFrom ? { dateFrom: query.dateFrom } : {}), ...(query.dateTo ? { dateTo: query.dateTo } : {}), ...(query.page ? { page: query.page } : {}), ...(query.pageSize ? { pageSize: query.pageSize } : {}) };
    const response = await axiosClient.get<PagedCollectionTasks>('/collection-tasks', { params }); return response.data;
  },
  getTask: async (id: string): Promise<CollectionTaskDetailDto> => { const response = await axiosClient.get<CollectionTaskDetailDto>(`/collection-tasks/${id}`); return response.data; },
  getTaskHistory: async (id: string): Promise<CollectionTaskAuditTrailDto> => { const response = await axiosClient.get<CollectionTaskAuditTrailDto>(`/collection-tasks/${id}/history`); return response.data; },
  rescheduleTask: async (id: string, request: RescheduleCollectionTaskRequest): Promise<CollectionTaskDetailDto> => { const response = await axiosClient.post<CollectionTaskDetailDto>(`/collection-tasks/${id}/reschedule`, request); return response.data; },
  createManualTask: async (request: CreateManualCollectionTaskRequest): Promise<CollectionTaskDetailDto> => {
    const response = await axiosClient.post<CollectionTaskDetailDto>('/collection-tasks/manual', request);
    return response.data;
  },
  createReplacementTask: async (
    failedTaskId: string,
    request: CreateReplacementCollectionTaskRequest
  ): Promise<CollectionTaskDetailDto> => {
    const response = await axiosClient.post<CollectionTaskDetailDto>(
      `/collection-tasks/${failedTaskId}/replacement`,
      request
    );
    return response.data;
  },
};
