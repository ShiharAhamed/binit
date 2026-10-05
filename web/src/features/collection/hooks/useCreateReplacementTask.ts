import { useMutation, useQueryClient } from '@tanstack/react-query';
import { collectionTasksApi } from '../api/collectionTasksApi';
import type { CollectionTaskDetailDto, CreateReplacementCollectionTaskRequest } from '../types/collectionTasks';

export function useCreateReplacementTask(failedTaskId: string) {
  const queryClient = useQueryClient();

  return useMutation<CollectionTaskDetailDto, Error, CreateReplacementCollectionTaskRequest>({
    mutationFn: (request: CreateReplacementCollectionTaskRequest) =>
      collectionTasksApi.createReplacementTask(failedTaskId, request),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['collection-tasks'] }),
        queryClient.invalidateQueries({ queryKey: ['collection-task-history', failedTaskId] }),
        queryClient.invalidateQueries({ queryKey: ['fleet', 'available-assignment-tasks'] }),
        queryClient.invalidateQueries({ queryKey: ['fleet', 'assignments'] }),
      ]);
    },
  });
}
