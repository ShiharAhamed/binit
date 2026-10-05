import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { assignmentsApi } from '../api/assignmentsApi';
import type { AvailableAssignmentTaskListParams } from '../types/assignments';

const key = ['fleet', 'available-assignment-tasks'];

export const useAvailableAssignmentTasks = (params: AvailableAssignmentTaskListParams) =>
  useQuery({
    queryKey: [...key, params],
    queryFn: () => assignmentsApi.getAvailableTasks(params),
    placeholderData: keepPreviousData,
  });
