import axios from 'axios';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { collectionNeedsApi } from '../api/collectionNeedsApi';
import type { CollectionNeedListParams, PagedCollectionNeeds } from '../types/collectionNeeds';

export function useCollectionNeeds(params: CollectionNeedListParams) {
  const query = useQuery<PagedCollectionNeeds, Error>({ queryKey: ['collection-needs', params], queryFn: () => collectionNeedsApi.getCollectionNeeds(params), placeholderData: keepPreviousData, staleTime: 30_000 });
  const errorMessage = query.error ? axios.isAxiosError(query.error) ? query.error.response?.data?.detail || query.error.response?.data?.title || 'Unable to load collection needs.' : query.error.message || 'Unable to load collection needs.' : null;
  return { ...query, needs: query.data?.items ?? [], totalCount: query.data?.totalCount ?? 0, totalPages: query.data?.totalPages ?? 1, currentPage: query.data?.page ?? params.page ?? 1, pageSize: query.data?.pageSize ?? params.pageSize ?? 20, errorMessage };
}
