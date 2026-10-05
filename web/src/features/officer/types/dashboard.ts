export interface WasteOfficerDashboardOverview {
  reportsAwaitingReview: number;
  activeBins: number;
  scheduledCollections: number;
  openCollectionTasks: number;
  openComplaints: number;
}

export interface WasteOfficerNeedsAttentionItem {
  id: string;
  itemType: 'WasteReport' | 'Complaint';
  reference: string;
  createdAt: string;
  secondaryLabel: string;
  submittedByName: string;
  addressText: string | null;
}
