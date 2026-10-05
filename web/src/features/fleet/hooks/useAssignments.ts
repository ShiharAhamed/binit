import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { assignmentsApi } from '../api/assignmentsApi';
import type {
  AssignmentListParams,
  CancelCollectionAssignmentRequest,
  CreateCollectionAssignmentRequest,
  ReorderRouteStopsRequest,
} from '../types/assignments';

const key = ['fleet', 'assignments'];

export const useAssignments = (params: AssignmentListParams) =>
  useQuery({
    queryKey: [...key, params],
    queryFn: () => assignmentsApi.getAssignments(params),
    placeholderData: keepPreviousData,
  });

export const useAssignmentDetail = (id: string | null) =>
  useQuery({
    queryKey: ['fleet', 'assignment', id],
    queryFn: () => assignmentsApi.getAssignmentDetail(id!),
    enabled: Boolean(id),
  });

export const useCreateAssignment = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateCollectionAssignmentRequest) => assignmentsApi.createAssignment(request),
    onSuccess: async () => {
      await Promise.all([
        client.invalidateQueries({ queryKey: ['fleet', 'assignments'] }),
        client.invalidateQueries({ queryKey: ['fleet', 'available-assignment-tasks'] }),
        client.invalidateQueries({ queryKey: ['fleet', 'drivers'] }),
        client.invalidateQueries({ queryKey: ['fleet', 'vehicles'] }),
      ]);
    },
  });
};

export const useReorderRouteStops = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: ReorderRouteStopsRequest }) =>
      assignmentsApi.reorderRouteStops(id, request),
    onSuccess: async (_data, variables) => {
      await Promise.all([
        client.invalidateQueries({ queryKey: ['fleet', 'assignments'] }),
        client.invalidateQueries({ queryKey: ['fleet', 'assignment', variables.id] }),
      ]);
    },
  });
};

export const useCancelAssignment = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: CancelCollectionAssignmentRequest }) =>
      assignmentsApi.cancelAssignment(id, request),
    onSuccess: async (_data, variables) => {
      await Promise.all([
        client.invalidateQueries({ queryKey: ['fleet', 'assignments'] }),
        client.invalidateQueries({ queryKey: ['fleet', 'assignment', variables.id] }),
        client.invalidateQueries({ queryKey: ['fleet', 'available-assignment-tasks'] }),
        client.invalidateQueries({ queryKey: ['fleet', 'drivers'] }),
        client.invalidateQueries({ queryKey: ['fleet', 'vehicles'] }),
      ]);
    },
  });
};

