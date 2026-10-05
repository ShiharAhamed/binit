import axios from 'axios';
import React, { useMemo, useState } from 'react';
import { Alert } from '../../../components/ui/Alert';
import { Button } from '../../../components/ui/Button';
import { Card } from '../../../components/ui/Card';
import { LoadingSpinner } from '../../../components/ui/LoadingSpinner';
import { CollectionStopsMap, isValidCollectionStopLocation } from './CollectionStopsMap';
import { useCreateAssignment } from '../hooks/useAssignments';
import { useAvailableAssignmentTasks } from '../hooks/useAvailableAssignmentTasks';
import { useDrivers } from '../hooks/useDrivers';
import { useVehicles } from '../hooks/useVehicles';
import type { AvailableAssignmentTaskDto, CollectionMapStop, CreateCollectionAssignmentRequest } from '../types/assignments';
import type { DriverSummaryDto, VehicleSummaryDto } from '../types/fleet';

interface CreateAssignmentPanelProps {
  onCancel: () => void;
  onCreated: (assignmentId: string, assignmentReference?: string) => void;
}

const pageSize = 20;
const taskSourceLabel = (task: AvailableAssignmentTaskDto) => task.targetType === 'Report' ? 'Waste report' : 'Waste bin';
const taskLocation = (task: AvailableAssignmentTaskDto) => task.addressText || 'Location not recorded';
const formatScheduledAt = (value: string) => {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Colombo', day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false }).format(date);
};
const errorMessage = (error: unknown) => axios.isAxiosError(error)
  ? error.response?.data?.detail || error.response?.data?.title || 'The assignment could not be created.'
  : 'The assignment could not be created.';

const PagedControls: React.FC<{ page: number; totalPages: number; isFetching: boolean; onPrevious: () => void; onNext: () => void; label: string }> = ({ page, totalPages, isFetching, onPrevious, onNext, label }) => <div className="flex items-center gap-2 text-xs text-slate-500">
  <Button size="sm" variant="secondary" disabled={page <= 1 || isFetching} onClick={onPrevious}>Previous</Button>
  <span className="font-medium text-slate-600">{label} page {page} of {Math.max(1, totalPages)}</span>
  <Button size="sm" variant="secondary" disabled={page >= totalPages || isFetching} onClick={onNext}>Next</Button>
</div>;

export const CreateAssignmentPanel: React.FC<CreateAssignmentPanelProps> = ({ onCancel, onCreated }) => {
  const [taskPage, setTaskPage] = useState(1);
  const [driverPage, setDriverPage] = useState(1);
  const [vehiclePage, setVehiclePage] = useState(1);
  const [driverSearch, setDriverSearch] = useState('');
  const [vehicleSearch, setVehicleSearch] = useState('');
  const [selectedTasks, setSelectedTasks] = useState<Record<string, AvailableAssignmentTaskDto>>({});
  const [orderedTaskIds, setOrderedTaskIds] = useState<string[]>([]);
  const [selectedDriver, setSelectedDriver] = useState<DriverSummaryDto | null>(null);
  const [selectedVehicle, setSelectedVehicle] = useState<VehicleSummaryDto | null>(null);
  const [selectedStopId, setSelectedStopId] = useState<string | null>(null);
  const [compatibilityAcknowledgement, setCompatibilityAcknowledgement] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const tasks = useAvailableAssignmentTasks({ page: taskPage, pageSize });
  const drivers = useDrivers({ search: driverSearch, page: driverPage, pageSize });
  const vehicles = useVehicles({ search: vehicleSearch, page: vehiclePage, pageSize });
  const create = useCreateAssignment();
  const orderedTasks = useMemo(() => orderedTaskIds.map((id) => selectedTasks[id]).filter((task): task is AvailableAssignmentTaskDto => Boolean(task)), [orderedTaskIds, selectedTasks]);
  const mapStops = useMemo<CollectionMapStop[]>(() => orderedTasks.map((task, index) => ({ id: task.id, sequence: index + 1, label: task.taskCode, targetType: task.targetType, addressText: task.addressText, latitude: task.latitude, longitude: task.longitude })), [orderedTasks]);
  const missingCoordinateTasks = orderedTasks.filter((task) => !isValidCollectionStopLocation(task.latitude, task.longitude));

  const toggleTask = (task: AvailableAssignmentTaskDto) => {
    setFormError(null);
    if (selectedTasks[task.id]) {
      setSelectedTasks((current) => { const next = { ...current }; delete next[task.id]; return next; });
      setOrderedTaskIds((current) => current.filter((id) => id !== task.id));
      if (selectedStopId === task.id) setSelectedStopId(null);
      return;
    }
    setSelectedTasks((current) => ({ ...current, [task.id]: task }));
    setOrderedTaskIds((current) => [...current, task.id]);
  };

  const moveTask = (id: string, direction: -1 | 1) => setOrderedTaskIds((current) => {
    const currentIndex = current.indexOf(id);
    const nextIndex = currentIndex + direction;
    if (currentIndex < 0 || nextIndex < 0 || nextIndex >= current.length) return current;
    const next = [...current];
    [next[currentIndex], next[nextIndex]] = [next[nextIndex], next[currentIndex]];
    return next;
  });

  const submit = async () => {
    if (orderedTasks.length === 0) return setFormError('Select at least one Scheduled collection task.');
    if (!selectedDriver) return setFormError('Select an Available, not occupied Driver.');
    if (!selectedVehicle) return setFormError('Select an operationally Available, not occupied Vehicle.');
    if (compatibilityAcknowledgement.trim() && compatibilityAcknowledgement.trim().length < 5) return setFormError('Compatibility acknowledgement must contain at least 5 characters when supplied.');
    const request: CreateCollectionAssignmentRequest = {
      driverId: selectedDriver.id,
      vehicleId: selectedVehicle.id,
      collectionTaskIds: orderedTasks.map((task) => task.id),
      stops: orderedTasks.map((task, index) => ({ collectionTaskId: task.id, sequence: index + 1 })),
      ...(compatibilityAcknowledgement.trim() ? { compatibilityAcknowledgement: compatibilityAcknowledgement.trim() } : {}),
    };
    setFormError(null);
    try {
      const created = await create.mutateAsync(request);
      onCreated(created.id, created.assignmentReference);
    } catch (error) {
      setFormError(errorMessage(error));
    }
  };

  const driverSelectable = (driver: DriverSummaryDto) => driver.availabilityStatus === 'Available' && !driver.isOccupied;
  const vehicleSelectable = (vehicle: VehicleSummaryDto) => vehicle.operationalStatus === 'Available' && !vehicle.isOccupied;

  return <div className="space-y-5" data-testid="create-assignment-panel">
    <Card className="border-emerald-100 p-5 sm:p-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><p className="text-xs font-semibold uppercase tracking-wider text-emerald-700">Manual dispatch</p><h2 className="mt-1 text-xl font-bold text-slate-900">Create collection assignment</h2><p className="mt-1 max-w-3xl text-sm leading-relaxed text-slate-500">Select existing Scheduled tasks, a ready Driver, an available Vehicle, and the manual stop order. The backend remains the final authority.</p></div><Button variant="secondary" onClick={onCancel}>Back to dispatch lists</Button></div>
    </Card>
    {formError && <Alert variant="error" title="Unable to create assignment">{formError}</Alert>}

    <Card className="p-5 sm:p-6"><div className="mb-4 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between"><div><h3 className="font-bold text-slate-900">1. Select collection tasks</h3><p className="mt-1 text-sm text-slate-500">{orderedTasks.length} task{orderedTasks.length === 1 ? '' : 's'} selected. Selections stay in the draft when pages change.</p></div><span className="rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-1 text-xs font-semibold text-emerald-800">{orderedTasks.length} selected</span></div>
      {tasks.isLoading ? <div className="flex items-center gap-3 py-8 text-sm text-slate-500"><LoadingSpinner /> Loading Scheduled tasks…</div> : tasks.isError ? <Alert variant="error" title="Unable to load available tasks">Please retry. <Button size="sm" variant="secondary" className="ml-2" onClick={() => tasks.refetch()}>Retry</Button></Alert> : <><div className="overflow-x-auto rounded-lg border border-slate-200"><table className="min-w-[780px] w-full text-left text-xs" aria-label="Select collection tasks"><thead><tr className="border-b border-slate-200 bg-slate-50 font-semibold uppercase tracking-wider text-slate-500"><th className="w-12 px-4 py-3">Select</th><th className="px-4 py-3">Task</th><th className="px-4 py-3">Target</th><th className="px-4 py-3">Location</th><th className="px-4 py-3">Scheduled (Colombo)</th></tr></thead><tbody className="divide-y divide-slate-100">{tasks.data?.items.map((task) => <tr key={task.id} className="hover:bg-slate-50/70"><td className="px-4 py-3"><input aria-label={`Select ${task.taskCode}`} type="checkbox" checked={Boolean(selectedTasks[task.id])} onChange={() => toggleTask(task)} className="h-4 w-4 rounded border-slate-300 text-emerald-600 focus:ring-emerald-500" /></td><td className="px-4 py-3 font-semibold text-slate-900">{task.taskCode}</td><td className="px-4 py-3 text-slate-700">{taskSourceLabel(task)}</td><td className="max-w-72 px-4 py-3 text-slate-600">{taskLocation(task)}</td><td className="whitespace-nowrap px-4 py-3 text-slate-600">{formatScheduledAt(task.scheduledAt)}</td></tr>)}</tbody></table></div>{tasks.data && <div className="mt-3"><PagedControls label="Tasks" page={tasks.data.page} totalPages={tasks.data.totalPages} isFetching={tasks.isFetching} onPrevious={() => setTaskPage((page) => page - 1)} onNext={() => setTaskPage((page) => page + 1)} /></div>}</>}
    </Card>

    <div className="grid gap-5 xl:grid-cols-2"><Card className="p-5 sm:p-6"><h3 className="font-bold text-slate-900">2. Select Driver</h3><p className="mt-1 text-sm text-slate-500">Only Available, not occupied Drivers can receive new work.</p><label className="mt-4 block text-xs font-semibold text-slate-700">Search Drivers<input aria-label="Search Drivers" value={driverSearch} onChange={(event) => { setDriverSearch(event.target.value); setDriverPage(1); }} placeholder="Search Driver name" className="mt-1.5 w-full rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-900 placeholder:text-slate-400 focus:border-emerald-600 focus:bg-white focus:outline-none focus:ring-2 focus:ring-emerald-600/20" /></label>{drivers.isLoading ? <div className="py-8 text-sm text-slate-500"><LoadingSpinner /> Loading Drivers…</div> : drivers.isError ? <Alert variant="error" title="Unable to load Drivers" className="mt-4">Please retry. <Button size="sm" variant="secondary" className="ml-2" onClick={() => drivers.refetch()}>Retry</Button></Alert> : <><div className="mt-4 space-y-2">{drivers.data?.items.map((driver) => { const selectable = driverSelectable(driver); return <label key={driver.id} className={`flex cursor-pointer items-center justify-between gap-3 rounded-lg border p-3 ${selectedDriver?.id === driver.id ? 'border-emerald-500 bg-emerald-50' : selectable ? 'border-slate-200 bg-white hover:border-emerald-300' : 'cursor-not-allowed border-slate-200 bg-slate-50 opacity-70'}`}><span className="flex items-center gap-3"><input aria-label={`Select Driver ${driver.displayName}`} type="radio" name="dispatch-driver" disabled={!selectable} checked={selectedDriver?.id === driver.id} onChange={() => { setSelectedDriver(driver); setFormError(null); }} className="text-emerald-600 focus:ring-emerald-500" /><span><span className="block text-sm font-semibold text-slate-900">{driver.displayName}</span><span className="mt-0.5 block text-xs text-slate-500">{driver.availabilityStatus} · {driver.isOccupied ? 'Occupied' : 'Not occupied'}{!selectable && ` — ${driver.isOccupied ? 'already assigned' : 'OffDuty'}`}</span></span></span></label>; })}</div>{drivers.data && <div className="mt-4"><PagedControls label="Drivers" page={drivers.data.page} totalPages={drivers.data.totalPages} isFetching={drivers.isFetching} onPrevious={() => setDriverPage((page) => page - 1)} onNext={() => setDriverPage((page) => page + 1)} /></div>}</>}</Card>
      <Card className="p-5 sm:p-6"><h3 className="font-bold text-slate-900">3. Select Vehicle</h3><p className="mt-1 text-sm text-slate-500">Operational status and assignment occupancy are separate safeguards.</p><label className="mt-4 block text-xs font-semibold text-slate-700">Search Vehicles<input aria-label="Search Vehicles" value={vehicleSearch} onChange={(event) => { setVehicleSearch(event.target.value); setVehiclePage(1); }} placeholder="Search registration number" className="mt-1.5 w-full rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-900 placeholder:text-slate-400 focus:border-emerald-600 focus:bg-white focus:outline-none focus:ring-2 focus:ring-emerald-600/20" /></label>{vehicles.isLoading ? <div className="py-8 text-sm text-slate-500"><LoadingSpinner /> Loading Vehicles…</div> : vehicles.isError ? <Alert variant="error" title="Unable to load Vehicles" className="mt-4">Please retry. <Button size="sm" variant="secondary" className="ml-2" onClick={() => vehicles.refetch()}>Retry</Button></Alert> : <><div className="mt-4 space-y-2">{vehicles.data?.items.map((vehicle) => { const selectable = vehicleSelectable(vehicle); return <label key={vehicle.id} className={`flex cursor-pointer items-center justify-between gap-3 rounded-lg border p-3 ${selectedVehicle?.id === vehicle.id ? 'border-emerald-500 bg-emerald-50' : selectable ? 'border-slate-200 bg-white hover:border-emerald-300' : 'cursor-not-allowed border-slate-200 bg-slate-50 opacity-70'}`}><span className="flex items-center gap-3"><input aria-label={`Select Vehicle ${vehicle.registrationNumber}`} type="radio" name="dispatch-vehicle" disabled={!selectable} checked={selectedVehicle?.id === vehicle.id} onChange={() => { setSelectedVehicle(vehicle); setFormError(null); }} className="text-emerald-600 focus:ring-emerald-500" /><span><span className="block text-sm font-semibold text-slate-900">{vehicle.registrationNumber} <span className="font-normal text-slate-500">· {vehicle.vehicleType}</span></span><span className="mt-0.5 block text-xs text-slate-500">{vehicle.operationalStatus} · {vehicle.isOccupied ? 'Occupied' : 'Not occupied'}{!selectable && ` — ${vehicle.isOccupied ? 'already assigned' : 'not operationally Available'}`}</span><span className="mt-0.5 block text-xs text-slate-500">Supports: {vehicle.supportedWasteTypes.length ? vehicle.supportedWasteTypes.join(', ') : 'No waste types recorded'}</span></span></span></label>; })}</div>{vehicles.data && <div className="mt-4"><PagedControls label="Vehicles" page={vehicles.data.page} totalPages={vehicles.data.totalPages} isFetching={vehicles.isFetching} onPrevious={() => setVehiclePage((page) => page - 1)} onNext={() => setVehiclePage((page) => page + 1)} /></div>}</>}</Card></div>

    <div className="grid gap-5 xl:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]"><Card className="p-5 sm:p-6"><h3 className="font-bold text-slate-900">4. Arrange stops</h3><p className="mt-1 text-sm text-slate-500">Set the manual sequence. This is not route optimisation.</p>{orderedTasks.length === 0 ? <p className="mt-5 rounded-lg border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500">Select a task above to add it to the proposed route.</p> : <ol className="mt-5 space-y-2">{orderedTasks.map((task, index) => <li key={task.id} className={`flex items-center gap-3 rounded-lg border p-3 ${selectedStopId === task.id ? 'border-emerald-400 bg-emerald-50/60' : 'border-slate-200 bg-white'}`}><button type="button" className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-emerald-600 text-xs font-bold text-white focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:ring-offset-2" onClick={() => setSelectedStopId(task.id)} aria-label={`Highlight stop ${index + 1}`}>{index + 1}</button><button type="button" className="min-w-0 flex-1 text-left focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:ring-offset-2" onClick={() => setSelectedStopId(task.id)}><span className="block truncate text-sm font-semibold text-slate-900">{task.taskCode}</span><span className="block truncate text-xs text-slate-500">{taskLocation(task)}</span></button><div className="flex shrink-0 gap-1"><Button size="sm" variant="secondary" aria-label={`Move ${task.taskCode} up`} disabled={index === 0} onClick={() => moveTask(task.id, -1)}>Up</Button><Button size="sm" variant="secondary" aria-label={`Move ${task.taskCode} down`} disabled={index === orderedTasks.length - 1} onClick={() => moveTask(task.id, 1)}>Down</Button><Button size="sm" variant="ghost" aria-label={`Remove ${task.taskCode}`} onClick={() => toggleTask(task)}>Remove</Button></div></li>)}</ol>}</Card>
      <div className="space-y-5"><Card className="p-5 sm:p-6"><h3 className="font-bold text-slate-900">Stop preview</h3><p className="mt-1 text-sm text-slate-500">Numbered markers reflect the manual order selected above.</p><div className="mt-4"><CollectionStopsMap stops={mapStops} selectedStopId={selectedStopId} onStopSelect={setSelectedStopId} /></div>{missingCoordinateTasks.length > 0 && <Alert variant="warning" title="Some selected stops have no mappable coordinates" className="mt-4">{missingCoordinateTasks.map((task) => task.taskCode).join(', ')} remain in the assignment order and will be sent to the backend.</Alert>}</Card>
        <Card className="p-5 sm:p-6"><h3 className="font-bold text-slate-900">5. Review and confirm</h3><dl className="mt-4 grid gap-3 text-sm sm:grid-cols-3"><div className="rounded-lg bg-slate-50 p-3"><dt className="text-xs font-medium text-slate-500">Tasks</dt><dd className="mt-1 font-semibold text-slate-900">{orderedTasks.length} selected</dd></div><div className="rounded-lg bg-slate-50 p-3"><dt className="text-xs font-medium text-slate-500">Driver</dt><dd className="mt-1 truncate font-semibold text-slate-900">{selectedDriver?.displayName || 'Not selected'}</dd></div><div className="rounded-lg bg-slate-50 p-3"><dt className="text-xs font-medium text-slate-500">Vehicle</dt><dd className="mt-1 truncate font-semibold text-slate-900">{selectedVehicle?.registrationNumber || 'Not selected'}</dd></div></dl><label className="mt-5 block text-sm font-semibold text-slate-700">Waste-handling acknowledgement <span className="font-normal text-slate-400">(only if backend requires it)</span><textarea aria-label="Waste-handling acknowledgement" value={compatibilityAcknowledgement} onChange={(event) => setCompatibilityAcknowledgement(event.target.value)} maxLength={500} rows={3} placeholder="Add a 5–500 character officer acknowledgement only when the backend identifies handling uncertainty." className="mt-1.5 w-full rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-900 placeholder:text-slate-400 focus:border-emerald-600 focus:bg-white focus:outline-none focus:ring-2 focus:ring-emerald-600/20" /></label><p className="mt-1 text-xs leading-5 text-slate-500">Task read data does not expose waste-type requirements. The backend will reject known incompatibility and require this acknowledgement for an uncertain bin compatibility case.</p><div className="mt-5 flex flex-col-reverse gap-2 border-t border-slate-100 pt-4 sm:flex-row sm:justify-end"><Button variant="secondary" disabled={create.isPending} onClick={onCancel}>Cancel</Button><Button isLoading={create.isPending} onClick={submit}>Create assignment</Button></div></Card></div></div>
  </div>;
};
