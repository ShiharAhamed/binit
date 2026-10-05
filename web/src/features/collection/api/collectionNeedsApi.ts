import { axiosClient } from '../../../api/axiosClient';
import type { CollectionNeedListParams, PagedCollectionNeeds } from '../types/collectionNeeds';

export const collectionNeedsApi = {
  getCollectionNeeds: async (query: CollectionNeedListParams = {}): Promise<PagedCollectionNeeds> => {
    const params = {
      ...(query.targetType ? { targetType: query.targetType } : {}),
      ...(query.collectionReason ? { collectionReason: query.collectionReason } : {}),
      ...(query.wasteType ? { wasteType: query.wasteType } : {}),
      ...(query.search?.trim() ? { search: query.search.trim() } : {}),
      ...(query.page ? { page: query.page } : {}),
      ...(query.pageSize ? { pageSize: query.pageSize } : {}),
    };
    const response = await axiosClient.get<PagedCollectionNeeds>('/collection-needs', { params });
    return response.data;
  },
};
