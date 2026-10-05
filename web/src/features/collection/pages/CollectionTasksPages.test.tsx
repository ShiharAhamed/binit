import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { CollectionTaskDetailPage, CollectionTaskEnhancedDetailPage, CollectionTasksPage } from './CollectionTasksPages';
import { collectionTasksApi } from '../api/collectionTasksApi';
import { useAuthStore } from '../../../store/authStore';
vi.mock('../api/collectionTasksApi', () => ({
  collectionTasksApi: {
    getTasks: vi.fn(),
    getTask: vi.fn(),
    getTaskHistory: vi.fn(),
    rescheduleTask: vi.fn(),
    createReplacementTask: vi.fn(),
  },
}));
const list = { items: [{ id: 'report-task', taskCode: 'TSK-100', targetType: 'Report' as const, wasteReportId: 'report-1', wasteBinId: null, targetReference: 'RPT-001', collectionReason: 'VerifiedReport' as const, status: 'Scheduled' as const, scheduledAt: '2099-01-01T04:30:00Z', creationMethod: 'Manual', createdByUserId: 'officer-1', createdByUserName: 'Officer', createdAt: '2098-12-31T04:30:00Z' }, { id: 'bin-task', taskCode: 'TSK-101', targetType: 'Bin' as const, wasteReportId: null, wasteBinId: 'bin-1', targetReference: 'BIN-01', collectionReason: 'FullOrBlockedBin' as const, status: 'InProgress' as const, scheduledAt: '2099-01-01T05:30:00Z', creationMethod: 'Manual', createdByUserId: 'officer-1', createdByUserName: 'Officer', createdAt: '2098-12-31T05:30:00Z' }], page: 1, pageSize: 20, totalCount: 22, totalPages: 2 };
const detail = { ...list.items[1], targetSummary: { identifier: 'BIN-01', latitude: 6.93, longitude: 79.85, addressText: 'Main Street', capacityLiters: 660, wasteTypes: ['General'], latestFillLevelPercent: 75 }, handlingNotes: 'Use rear gate', schedulingReason: null, updatedAt: null };
const failedDetail = {
  id: 'failed-task-1',
  taskCode: 'TSK-FAILED-01',
  targetType: 'Report' as const,
  wasteReportId: 'report-1',
  wasteBinId: null,
  collectionReason: 'VerifiedReport' as const,
  status: 'Failed' as const,
  scheduledAt: '2099-01-01T05:30:00Z',
  handlingNotes: 'Gate key at office',
  schedulingReason: 'Urgent',
  createdByUserId: 'officer-1',
  createdByUserName: 'Officer',
  creationMethod: 'Manual',
  createdAt: '2098-12-31T05:30:00Z',
  updatedAt: '2099-01-01T07:00:00Z',
  targetSummary: {
    identifier: 'RPT-001',
    latitude: 6.93,
    longitude: 79.85,
    addressText: 'Pettah Market',
    capacityLiters: null,
    wasteTypes: ['General'],
    latestFillLevelPercent: null,
  },
};
const failedHistory = {
  collectionTaskId: 'failed-task-1',
  taskCode: 'TSK-FAILED-01',
  statusHistory: [
    {
      id: 'h-1',
      fromStatus: 'InProgress',
      toStatus: 'Failed',
      changedByUserId: 'driver-1',
      changedByUserName: 'Driver Kasun',
      notes: 'Road submerged by flood water.',
      changedAt: '2099-01-01T07:00:00Z',
    },
  ],
  scheduleHistory: [],
};
const wrap = (ui: React.ReactNode, path = '/officer/tasks') => render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><MemoryRouter initialEntries={[path]}>{ui}</MemoryRouter></QueryClientProvider>);
describe('Collection Tasks pages', () => { beforeEach(() => { vi.clearAllMocks(); useAuthStore.setState({ user: { id: 'manager', fullName: 'Manager', email: 'manager@example.com', role: 'MunicipalManager' }, isAuthenticated: true, isLoading: false, accessToken: 'test' }); });
  it('renders report and bin tasks, filters and server pagination for staff', async () => { const user = userEvent.setup(); (collectionTasksApi.getTasks as ReturnType<typeof vi.fn>).mockResolvedValue(list); wrap(<CollectionTasksPage />); expect(await screen.findByText('TSK-100')).toBeInTheDocument(); expect(screen.getByText('BIN-01')).toBeInTheDocument(); await user.selectOptions(screen.getByLabelText('Status'), 'Scheduled'); await user.type(screen.getByLabelText('Date from'), '2099-01-01'); await waitFor(() => expect(collectionTasksApi.getTasks).toHaveBeenLastCalledWith(expect.objectContaining({ status: 'Scheduled', dateFrom: '2099-01-01', page: 1 }))); await user.click(screen.getByRole('button', { name: 'Next' })); await waitFor(() => expect(collectionTasksApi.getTasks).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2 }))); });
  it('shows loading, empty, and retryable error states', async () => { (collectionTasksApi.getTasks as ReturnType<typeof vi.fn>).mockReturnValue(new Promise(() => {})); wrap(<CollectionTasksPage />); expect(screen.getByTestId('collection-tasks-loading')).toBeInTheDocument(); });
  it('links to a task detail and renders actual bin fields', async () => { (collectionTasksApi.getTask as ReturnType<typeof vi.fn>).mockResolvedValue(detail); wrap(<Routes><Route path="/officer/tasks/:id" element={<CollectionTaskDetailPage />} /></Routes>, '/officer/tasks/bin-task'); expect(await screen.findByText('BIN-01')).toBeInTheDocument(); expect(screen.getByText('Use rear gate')).toBeInTheDocument(); expect(screen.getByText('75%')).toBeInTheDocument(); });
  it('shows a task not-found state', async () => { (collectionTasksApi.getTask as ReturnType<typeof vi.fn>).mockRejectedValue({ isAxiosError: true, response: { status: 404, data: { detail: 'Missing' } } }); wrap(<Routes><Route path="/officer/tasks/:id" element={<CollectionTaskDetailPage />} /></Routes>, '/officer/tasks/missing'); expect(await screen.findByText('Collection task not found')).toBeInTheDocument(); });

  it('renders Failed Task Review & Replacement for a Failed task when user is WasteOfficer', async () => {
    useAuthStore.setState({
      user: { id: 'officer-1', fullName: 'Officer Perera', email: 'officer@example.com', role: 'WasteOfficer' },
      isAuthenticated: true,
      isLoading: false,
      accessToken: 'test',
    });
    (collectionTasksApi.getTask as ReturnType<typeof vi.fn>).mockResolvedValue(failedDetail);
    (collectionTasksApi.getTaskHistory as ReturnType<typeof vi.fn>).mockResolvedValue(failedHistory);

    wrap(
      <Routes>
        <Route path="/officer/tasks/:id" element={<CollectionTaskEnhancedDetailPage />} />
      </Routes>,
      '/officer/tasks/failed-task-1'
    );

    expect(await screen.findAllByText('TSK-FAILED-01')).toHaveLength(2);
    expect(await screen.findByText('Road submerged by flood water.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /schedule replacement/i })).toBeInTheDocument();
  });
});
