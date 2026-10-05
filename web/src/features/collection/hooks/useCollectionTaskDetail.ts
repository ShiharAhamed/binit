import axios from 'axios';
import { useQuery } from '@tanstack/react-query';
import { collectionTasksApi } from '../api/collectionTasksApi';
import type { CollectionTaskDetailDto } from '../types/collectionTasks';
export function useCollectionTaskDetail(id: string | undefined) { const query = useQuery<CollectionTaskDetailDto, Error>({ queryKey: ['collection-tasks', id], queryFn: () => collectionTasksApi.getTask(id!), enabled: Boolean(id), staleTime: 30_000 }); const status = axios.isAxiosError(query.error) ? query.error.response?.status : undefined; const errorMessage = query.error ? axios.isAxiosError(query.error) ? query.error.response?.data?.detail || query.error.response?.data?.title || 'Unable to load this collection task.' : query.error.message : null; return { ...query, task: query.data, errorMessage, isNotFound: status === 404 || !id }; }
