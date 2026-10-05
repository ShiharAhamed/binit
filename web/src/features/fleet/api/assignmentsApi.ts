import { axiosClient } from '../../../api/axiosClient';
import type {
  AssignmentDetailDto,
  AssignmentHistoryDto,
  AssignmentListParams,
  CancelCollectionAssignmentRequest,
  CreateCollectionAssignmentRequest,
  CreatedAssignmentDto,
  PagedAssignments,
  PagedAvailableAssignmentTasks,
  AvailableAssignmentTaskListParams,
  ReorderRouteStopsRequest,
  RouteReadDto,
} from '../types/assignments';

export const assignmentsApi = {
  getAvailableTasks: async (query: AvailableAssignmentTaskListParams = {}) =>
    (await axiosClient.get<PagedAvailableAssignmentTasks>('/collection-tasks/available-for-assignment', { params: query })).data,
  getAssignments: async (query: AssignmentListParams = {}) =>
    (await axiosClient.get<PagedAssignments>('/assignments', { params: query })).data,
  getAssignmentDetail: async (id: string) =>
    (await axiosClient.get<AssignmentDetailDto>(`/assignments/${id}`)).data,
  getAssignmentHistory: async (id: string) =>
    (await axiosClient.get<AssignmentHistoryDto[]>(`/assignments/${id}/history`)).data,
  createAssignment: async (request: CreateCollectionAssignmentRequest) =>
    (await axiosClient.post<CreatedAssignmentDto>('/assignments', request)).data,
  reorderRouteStops: async (id: string, request: ReorderRouteStopsRequest) =>
    (await axiosClient.patch<RouteReadDto>(`/assignments/${id}/route/stops`, request)).data,
  cancelAssignment: async (id: string, request: CancelCollectionAssignmentRequest) =>
    (await axiosClient.post<AssignmentDetailDto>(`/assignments/${id}/cancel`, request)).data,
};

