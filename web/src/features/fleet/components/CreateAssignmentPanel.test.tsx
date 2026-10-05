import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { CreateAssignmentPanel } from './CreateAssignmentPanel';

const hooks = vi.hoisted(() => ({
  useAvailableAssignmentTasks: vi.fn(),
  useDrivers: vi.fn(),
  useVehicles: vi.fn(),
  useCreateAssignment: vi.fn(),
}));

vi.mock('../hooks/useAvailableAssignmentTasks', () => ({ useAvailableAssignmentTasks: hooks.useAvailableAssignmentTasks }));
vi.mock('../hooks/useDrivers', () => ({ useDrivers: hooks.useDrivers }));
vi.mock('../hooks/useVehicles', () => ({ useVehicles: hooks.useVehicles }));
vi.mock('../hooks/useAssignments', () => ({ useCreateAssignment: hooks.useCreateAssignment }));
vi.mock('./CollectionStopsMap', () => ({
  CollectionStopsMap: ({ stops, selectedStopId }: { stops: Array<{ id: string; sequence: number; latitude: number | null }>; selectedStopId: string | null }) => <div data-testid="stop-map" data-selected-stop={selectedStopId}>{stops.map((stop) => <span key={stop.id}>{`${stop.id}:${stop.sequence}:${stop.latitude ?? 'none'}`}</span>)}</div>,
  isValidCollectionStopLocation: (latitude: unknown, longitude: unknown) => typeof latitude === 'number' && Number.isFinite(latitude) && latitude >= -90 && latitude <= 90 && typeof longitude === 'number' && Number.isFinite(longitude) && longitude >= -180 && longitude <= 180,
}));

const taskOne = { id: 'task-1', taskCode: 'TASK-1', targetType: 'Report' as const, wasteReportId: 'report-1', wasteBinId: null, collectionReason: 'VerifiedReport' as const, status: 'Scheduled' as const, scheduledAt: '2026-09-26T05:30:00Z', addressText: 'First Street', latitude: 6.93, longitude: 79.85 };
const taskTwo = { id: 'task-2', taskCode: 'TASK-2', targetType: 'Bin' as const, wasteReportId: null, wasteBinId: 'bin-1', collectionReason: 'FullOrBlockedBin' as const, status: 'Scheduled' as const, scheduledAt: '2026-09-26T06:30:00Z', addressText: 'Second Street', latitude: null, longitude: 79.86 };
const availableDriver = { id: 'driver-1', displayName: 'Asha Driver', availabilityStatus: 'Available' as const, isOccupied: false };
const offDutyDriver = { id: 'driver-2', displayName: 'Off duty Driver', availabilityStatus: 'OffDuty' as const, isOccupied: false };
const availableVehicle = { id: 'vehicle-1', registrationNumber: 'WP-CA-1001', vehicleType: 'Compactor' as const, operationalStatus: 'Available' as const, supportedWasteTypes: ['General' as const], isOccupied: false };
const occupiedVehicle = { id: 'vehicle-2', registrationNumber: 'WP-CA-1002', vehicleType: 'Tipper' as const, operationalStatus: 'Available' as const, supportedWasteTypes: ['General' as const], isOccupied: true };

const queryResult = (items: unknown[], page = 1, totalPages = 1) => ({ data: { items, page, pageSize: 20, totalCount: totalPages, totalPages }, isLoading: false, isError: false, isFetching: false, refetch: vi.fn() });

describe('CreateAssignmentPanel', () => {
  const mutateAsync = vi.fn();
  const onCreated = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    hooks.useAvailableAssignmentTasks.mockImplementation(({ page }: { page: number }) => queryResult(page === 1 ? [taskOne] : [taskTwo], page, 2));
    hooks.useDrivers.mockReturnValue(queryResult([availableDriver, offDutyDriver]));
    hooks.useVehicles.mockReturnValue(queryResult([availableVehicle, occupiedVehicle]));
    hooks.useCreateAssignment.mockReturnValue({ mutateAsync, isPending: false });
    mutateAsync.mockResolvedValue({ id: 'assignment-1', assignmentReference: 'Assignment 001' });
  });

  it('keeps selected tasks across pages, sends their manual order, and preserves missing-coordinate tasks in the draft', async () => {
    const user = userEvent.setup();
    render(<CreateAssignmentPanel onCancel={vi.fn()} onCreated={onCreated} />);

    await user.click(screen.getByLabelText('Select TASK-1'));
    await user.click(screen.getAllByRole('button', { name: 'Next' })[0]);
    await user.click(screen.getByLabelText('Select TASK-2'));
    expect(screen.getByText('2 tasks selected. Selections stay in the draft when pages change.')).toBeInTheDocument();
    expect(screen.getByText(/TASK-2 remain in the assignment order/i)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Move TASK-2 up' }));
    expect(screen.getByTestId('stop-map')).toHaveTextContent('task-2:1:none');
    expect(screen.getByTestId('stop-map')).toHaveTextContent('task-1:2:6.93');

    await user.click(screen.getByLabelText('Select Driver Asha Driver'));
    await user.click(screen.getByLabelText('Select Vehicle WP-CA-1001'));
    await user.type(screen.getByLabelText('Waste-handling acknowledgement'), 'Officer reviewed the handling uncertainty.');
    await user.click(screen.getByRole('button', { name: 'Create assignment' }));

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledWith({
      driverId: 'driver-1', vehicleId: 'vehicle-1', collectionTaskIds: ['task-2', 'task-1'],
      stops: [{ collectionTaskId: 'task-2', sequence: 1 }, { collectionTaskId: 'task-1', sequence: 2 }],
      compatibilityAcknowledgement: 'Officer reviewed the handling uncertainty.',
    }));
    expect(onCreated).toHaveBeenCalledWith('assignment-1', 'Assignment 001');
  });

  it('disables OffDuty and occupied resources and blocks a too-short acknowledgement locally', async () => {
    const user = userEvent.setup();
    render(<CreateAssignmentPanel onCancel={vi.fn()} onCreated={onCreated} />);

    expect(screen.getByLabelText('Select Driver Off duty Driver')).toBeDisabled();
    expect(screen.getByLabelText('Select Vehicle WP-CA-1002')).toBeDisabled();
    await user.click(screen.getByLabelText('Select TASK-1'));
    await user.click(screen.getByLabelText('Select Driver Asha Driver'));
    await user.click(screen.getByLabelText('Select Vehicle WP-CA-1001'));
    await user.type(screen.getByLabelText('Waste-handling acknowledgement'), 'No');
    await user.click(screen.getByRole('button', { name: 'Create assignment' }));

    expect(screen.getByText('Compatibility acknowledgement must contain at least 5 characters when supplied.')).toBeInTheDocument();
    expect(mutateAsync).not.toHaveBeenCalled();
  });

  it('keeps the officer draft and displays a readable backend conflict', async () => {
    const user = userEvent.setup();
    mutateAsync.mockRejectedValueOnce(Object.assign(new Error('Conflict'), { isAxiosError: true, response: { data: { detail: 'A selected task was claimed by another assignment.' } } }));
    render(<CreateAssignmentPanel onCancel={vi.fn()} onCreated={onCreated} />);

    await user.click(screen.getByLabelText('Select TASK-1'));
    await user.click(screen.getByLabelText('Select Driver Asha Driver'));
    await user.click(screen.getByLabelText('Select Vehicle WP-CA-1001'));
    await user.click(screen.getByRole('button', { name: 'Create assignment' }));

    expect(await screen.findByText('A selected task was claimed by another assignment.')).toBeInTheDocument();
    expect(screen.getByText('1 task selected. Selections stay in the draft when pages change.')).toBeInTheDocument();
    expect(screen.getByLabelText('Select Driver Asha Driver')).toBeChecked();
    expect(screen.getByLabelText('Select Vehicle WP-CA-1001')).toBeChecked();
  });
});
