import React, { useState } from 'react';
import { Alert } from '../../../components/ui/Alert';
import { Button } from '../../../components/ui/Button';
import { Card } from '../../../components/ui/Card';
import { LoadingSpinner } from '../../../components/ui/LoadingSpinner';
import { AssignmentsSection } from '../components/AssignmentsSection';
import { DriversSection } from '../components/DriversSection';
import { FleetStatusBadge, vehicleStatusTone } from '../components/FleetStatusBadge';
import { VehicleForm } from '../components/VehicleForm';
import { useVehicle, useVehicleMutations, useVehicles } from '../hooks/useVehicles';
import type { VehicleDetailDto, VehicleOperationalStatus, VehicleRequest, VehicleType } from '../types/fleet';

const statuses: VehicleOperationalStatus[] = ['Available', 'Maintenance', 'Inactive'];
const vehicleTypes: VehicleType[] = ['Compactor', 'Flatbed', 'Tipper', 'SmallVan'];
const pageSize = 20;

export const ManagerFleetRoutesPage: React.FC = () => {
  const [search, setSearch] = useState('');
  const [operationalStatus, setOperationalStatus] = useState<VehicleOperationalStatus | ''>('');
  const [vehicleType, setVehicleType] = useState<VehicleType | ''>('');
  const [page, setPage] = useState(1);
  const [section, setSection] = useState<'vehicles' | 'drivers' | 'assignments'>('vehicles');
  const [editing, setEditing] = useState<VehicleDetailDto | null | undefined>(undefined);
  const [detailId, setDetailId] = useState<string | null>(null);
  const [pendingStatusChange, setPendingStatusChange] = useState<{ vehicle: VehicleDetailDto; nextStatus: VehicleOperationalStatus } | null>(null);
  const [message, setMessage] = useState<{ text: string; variant: 'success' | 'error' } | null>(null);
  const vehicles = useVehicles({ search, operationalStatus, vehicleType, page, pageSize });
  const detail = useVehicle(detailId);
  const mutations = useVehicleMutations();

  const resetPage = () => setPage(1);
  const showError = (error: unknown) => setMessage({ text: mutations.error(error), variant: 'error' });
  const submit = async (request: VehicleRequest) => {
    try {
      if (editing) await mutations.update.mutateAsync({ id: editing.id, request });
      else await mutations.create.mutateAsync(request);
      setMessage({ text: `Vehicle ${editing ? 'updated' : 'registered'} successfully.`, variant: 'success' });
      setEditing(undefined);
      setDetailId(null);
    } catch (error) {
      showError(error);
    }
  };
  const requestStatusChange = (vehicle: VehicleDetailDto, nextStatus: VehicleOperationalStatus) => {
    if (nextStatus !== vehicle.operationalStatus) setPendingStatusChange({ vehicle, nextStatus });
  };
  const confirmStatusChange = () => {
    if (!pendingStatusChange) return;
    const { vehicle, nextStatus } = pendingStatusChange;
    mutations.status.mutate(
      { id: vehicle.id, operationalStatus: nextStatus },
      {
        onSuccess: () => {
          setMessage({ text: 'Operational status updated.', variant: 'success' });
          setPendingStatusChange(null);
        },
        onError: (error) => {
          setPendingStatusChange(null);
          showError(error);
        },
      },
    );
  };
  const openDetail = (id: string) => { setEditing(undefined); setDetailId(id); };
  const openEdit = () => { if (detail.data) { setEditing(detail.data); setDetailId(null); } };
  const pagination = vehicles.data;

  return (
    <div className="space-y-6">
      <header className="border-b border-slate-200/80 pb-5">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <div className="mb-1 flex items-center gap-1.5 text-xs font-medium text-slate-400"><span>Operations</span><span>/</span><span className="font-semibold text-slate-600">Fleet & Routes</span></div>
            <h1 className="text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl">Fleet & Routes</h1>
            <p className="mt-1 text-sm leading-relaxed text-slate-500">Manage municipal vehicles and review collection driver readiness.</p>
          </div>
          {section === 'vehicles' && <Button className="self-start sm:self-auto" onClick={() => { setMessage(null); setDetailId(null); setEditing(null); }}>Register vehicle</Button>}
        </div>
      </header>

      <nav className="flex flex-wrap gap-1 rounded-xl border border-slate-200 bg-white p-1.5 shadow-sm" aria-label="Fleet sections">
        <Button size="sm" variant={section === 'vehicles' ? 'primary' : 'ghost'} className="rounded-lg" aria-current={section === 'vehicles' ? 'page' : undefined} onClick={() => setSection('vehicles')}>Vehicles</Button>
        <Button size="sm" variant={section === 'drivers' ? 'primary' : 'ghost'} className="rounded-lg" aria-current={section === 'drivers' ? 'page' : undefined} onClick={() => setSection('drivers')}>Drivers</Button>
        <Button size="sm" variant={section === 'assignments' ? 'primary' : 'ghost'} className="rounded-lg" aria-current={section === 'assignments' ? 'page' : undefined} onClick={() => setSection('assignments')}>Assignments & Routes</Button>
      </nav>

      {message && <Alert variant={message.variant} title={message.variant === 'success' ? 'Fleet updated' : 'Unable to update fleet'}>{message.text}</Alert>}
      {section === 'drivers' && <DriversSection />}
      {section === 'assignments' && <AssignmentsSection role="MunicipalManager" />}
      {section === 'vehicles' && <>
        <Card className="p-4 sm:p-5">
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            <label className="text-xs font-semibold text-slate-700">Search vehicles
              <input aria-label="Search vehicles" placeholder="Search registration number" value={search} onChange={(event) => { setSearch(event.target.value); resetPage(); }} className="mt-1.5 w-full rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-900 placeholder:text-slate-400 transition-colors focus:border-emerald-600 focus:bg-white focus:outline-none focus:ring-2 focus:ring-emerald-600/20" />
            </label>
            <label className="text-xs font-semibold text-slate-700">Operational status
              <select aria-label="Filter operational status" value={operationalStatus} onChange={(event) => { setOperationalStatus(event.target.value as VehicleOperationalStatus | ''); resetPage(); }} className="mt-1.5 w-full rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-900 transition-colors focus:border-emerald-600 focus:bg-white focus:outline-none focus:ring-2 focus:ring-emerald-600/20"><option value="">All operational statuses</option>{statuses.map((value) => <option key={value}>{value}</option>)}</select>
            </label>
            <label className="text-xs font-semibold text-slate-700">Vehicle type
              <select aria-label="Filter vehicle type" value={vehicleType} onChange={(event) => { setVehicleType(event.target.value as VehicleType | ''); resetPage(); }} className="mt-1.5 w-full rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-900 transition-colors focus:border-emerald-600 focus:bg-white focus:outline-none focus:ring-2 focus:ring-emerald-600/20"><option value="">All vehicle types</option>{vehicleTypes.map((value) => <option key={value}>{value}</option>)}</select>
            </label>
          </div>
        </Card>

        {detailId && <Card className="border-emerald-100 p-5 sm:p-6">
          {detail.isLoading ? <div className="flex items-center gap-3 py-3 text-sm text-slate-500"><LoadingSpinner /> Loading vehicle details…</div> : detail.isError ? <Alert variant="error" title="Unable to load vehicle details">Please retry the request. <Button size="sm" variant="secondary" className="ml-3" onClick={() => detail.refetch()}>Retry</Button></Alert> : detail.data && <>
            <div className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><p className="text-xs font-semibold uppercase tracking-wider text-emerald-700">Vehicle details</p><h2 className="mt-1 text-lg font-bold text-slate-900">{detail.data.registrationNumber}</h2><p className="mt-1 text-sm text-slate-500">{detail.data.vehicleType} · {detail.data.capacityLiters.toLocaleString()} L capacity</p></div><div className="flex gap-2"><Button size="sm" variant="secondary" onClick={() => setDetailId(null)}>Close</Button><Button size="sm" onClick={openEdit}>Edit vehicle</Button></div></div>
            <dl className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4"><div className="rounded-lg border border-slate-100 bg-slate-50 p-3"><dt className="text-xs font-medium text-slate-500">Supported waste types</dt><dd className="mt-2 flex flex-wrap gap-1.5">{detail.data.supportedWasteTypes.map((type) => <span key={type} className="rounded border border-slate-200 bg-white px-1.5 py-0.5 text-[11px] font-medium text-slate-600">{type}</span>)}</dd></div><div className="rounded-lg border border-slate-100 bg-slate-50 p-3"><dt className="text-xs font-medium text-slate-500">Operational status</dt><dd className="mt-2"><FleetStatusBadge label={detail.data.operationalStatus} tone={vehicleStatusTone(detail.data.operationalStatus)} /></dd></div><div className="rounded-lg border border-slate-100 bg-slate-50 p-3"><dt className="text-xs font-medium text-slate-500">Assignment occupancy</dt><dd className="mt-2"><FleetStatusBadge label={detail.data.isOccupied ? 'Occupied' : 'Not occupied'} tone={detail.data.isOccupied ? 'occupied' : 'available'} /></dd></div><div className="rounded-lg border border-slate-100 bg-slate-50 p-3"><dt className="text-xs font-medium text-slate-500">Notes</dt><dd className="mt-2 text-sm text-slate-700">{detail.data.notes || 'No operational notes'}</dd></div></dl>
            <div className="mt-5 flex flex-col gap-2 border-t border-slate-100 pt-4 sm:flex-row sm:items-center"><label htmlFor="vehicle-operational-status" className="text-xs font-semibold text-slate-700">Change operational status</label><select id="vehicle-operational-status" aria-label="Change selected vehicle operational status" value={detail.data.operationalStatus} onChange={(event) => requestStatusChange(detail.data!, event.target.value as VehicleOperationalStatus)} className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs text-slate-900 transition-colors focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-600/20 sm:w-auto">{statuses.map((value) => <option key={value}>{value}</option>)}</select></div>
          </>}
        </Card>}

        {editing !== undefined && <Card className="p-5 sm:p-6"><div className="mb-5"><p className="text-xs font-semibold uppercase tracking-wider text-emerald-700">Vehicle administration</p><h2 className="mt-1 text-lg font-bold text-slate-900">{editing ? 'Edit vehicle' : 'Register vehicle'}</h2><p className="mt-1 text-sm text-slate-500">Submit accurate vehicle details for backend validation.</p></div><VehicleForm initial={editing ? { ...editing, notes: editing.notes ?? '' } : undefined} submitting={mutations.create.isPending || mutations.update.isPending} onCancel={() => setEditing(undefined)} onSubmit={submit} /></Card>}

        <Card className="overflow-hidden p-0">
          {vehicles.isLoading ? <div className="flex flex-col items-center gap-3 py-16 text-slate-500"><LoadingSpinner size="lg" /><p className="text-sm font-medium">Loading vehicles…</p></div> : vehicles.isError ? <Alert variant="error" title="Unable to load vehicles" className="m-4 sm:m-5">Please retry the request. <Button size="sm" variant="secondary" className="ml-3" onClick={() => vehicles.refetch()}>Retry</Button></Alert> : !pagination?.items.length ? <div className="py-16 text-center"><p className="text-base font-semibold text-slate-800">No vehicles match the current filters</p><p className="mt-1 text-xs text-slate-500">Adjust the filters to review other registered vehicles.</p></div> : <div className="overflow-x-auto"><table className="min-w-[880px] w-full text-left text-xs"><thead><tr className="border-b border-slate-200 bg-slate-50/80 font-semibold uppercase tracking-wider text-slate-500"><th className="px-4 py-3.5">Registration</th><th className="px-4 py-3.5">Vehicle</th><th className="px-4 py-3.5">Supported waste</th><th className="px-4 py-3.5">Operational status</th><th className="px-4 py-3.5">Occupancy</th><th className="px-4 py-3.5 text-right">Actions</th></tr></thead><tbody className="divide-y divide-slate-100">{pagination.items.map((vehicle) => <tr key={vehicle.id} className="transition-colors hover:bg-slate-50/70"><td className="px-4 py-3.5 font-semibold text-slate-900">{vehicle.registrationNumber}</td><td className="px-4 py-3.5 text-slate-700">{vehicle.vehicleType}</td><td className="px-4 py-3.5"><div className="flex max-w-56 flex-wrap gap-1">{vehicle.supportedWasteTypes.map((type) => <span key={type} className="rounded border border-slate-200 bg-slate-50 px-1.5 py-0.5 text-[11px] font-medium text-slate-600">{type}</span>)}</div></td><td className="px-4 py-3.5"><FleetStatusBadge label={vehicle.operationalStatus} tone={vehicleStatusTone(vehicle.operationalStatus)} /></td><td className="px-4 py-3.5"><FleetStatusBadge label={vehicle.isOccupied ? 'Occupied' : 'Not occupied'} tone={vehicle.isOccupied ? 'occupied' : 'available'} /></td><td className="px-4 py-3.5 text-right"><Button size="sm" variant="secondary" onClick={() => openDetail(vehicle.id)}>View details</Button></td></tr>)}</tbody></table></div>}
          {pagination && pagination.totalCount > 0 && <div className="flex flex-col justify-between gap-3 border-t border-slate-100 p-4 text-xs text-slate-500 sm:flex-row sm:items-center"><span>Showing {Math.min((pagination.page - 1) * pagination.pageSize + 1, pagination.totalCount)}–{Math.min(pagination.page * pagination.pageSize, pagination.totalCount)} of {pagination.totalCount} vehicles</span><div className="flex items-center gap-2"><Button size="sm" variant="secondary" disabled={pagination.page <= 1 || vehicles.isFetching} onClick={() => setPage(page - 1)}>Previous</Button><span className="px-1 font-semibold text-slate-700">Page {pagination.page} of {Math.max(1, pagination.totalPages)}</span><Button size="sm" variant="secondary" disabled={pagination.page >= pagination.totalPages || vehicles.isFetching} onClick={() => setPage(page + 1)}>Next</Button></div></div>}
        </Card>
      </>}

      {pendingStatusChange && <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4 backdrop-blur-2xs"><div role="dialog" aria-modal="true" aria-labelledby="status-change-title" className="w-full max-w-md rounded-2xl border border-slate-100 bg-white p-6 shadow-xl"><p className="text-xs font-semibold uppercase tracking-wider text-amber-700">Confirm operational change</p><h2 id="status-change-title" className="mt-1 text-lg font-bold text-slate-900">Update vehicle status?</h2><p className="mt-2 text-sm leading-relaxed text-slate-600">Set <span className="font-semibold text-slate-800">{pendingStatusChange.vehicle.registrationNumber}</span> to <span className="font-semibold text-slate-800">{pendingStatusChange.nextStatus}</span>. The backend will validate whether the change is allowed.</p><div className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end"><Button variant="secondary" onClick={() => setPendingStatusChange(null)}>Cancel</Button><Button onClick={confirmStatusChange} isLoading={mutations.status.isPending}>Confirm change</Button></div></div></div>}
    </div>
  );
};
