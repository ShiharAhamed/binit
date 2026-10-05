import axios from 'axios';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { collectionTasksApi } from '../api/collectionTasksApi';
import type { CollectionTaskListParams, PagedCollectionTasks } from '../types/collectionTasks';
export function useCollectionTasks(params: CollectionTaskListParams) { const query = useQuery<PagedCollectionTasks, Error>({ queryKey: ['collection-tasks', params], queryFn: () => collectionTasksApi.getTasks(params), placeholderData: keepPreviousData, staleTime: 30_000 }); const errorMessage = query.error ? axios.isAxiosError(query.error) ? query.error.response?.data?.detail || query.error.response?.data?.title || 'Unable to load collection tasks.' : query.error.message : null; return { ...query, tasks: query.data?.items ?? [], totalCount: query.data?.totalCount ?? 0, totalPages: query.data?.totalPages ?? 1, currentPage: query.data?.page ?? params.page ?? 1, pageSize: query.data?.pageSize ?? params.pageSize ?? 20, errorMessage }; }
