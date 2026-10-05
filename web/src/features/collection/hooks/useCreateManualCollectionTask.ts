import { useMutation, useQueryClient } from '@tanstack/react-query';
import { collectionTasksApi } from '../api/collectionTasksApi';
import type { CreateManualCollectionTaskRequest } from '../types/collectionTasks';

export function useCreateManualCollectionTask() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateManualCollectionTaskRequest) => collectionTasksApi.createManualTask(request),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['collection-needs'] }),
        queryClient.invalidateQueries({ queryKey: ['collection-tasks'] }),
      ]);
    },
  });
}
