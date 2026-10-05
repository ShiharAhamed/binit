import React, { useState } from 'react';
import { Alert } from '../../../components/ui/Alert';
import { Button } from '../../../components/ui/Button';
import { Card } from '../../../components/ui/Card';
import { LoadingSpinner } from '../../../components/ui/LoadingSpinner';
import { useDriver, useDrivers } from '../hooks/useDrivers';
import type { DriverAvailabilityStatus } from '../types/fleet';
import { driverAvailabilityTone, FleetStatusBadge } from './FleetStatusBadge';

const availability: DriverAvailabilityStatus[] = ['Available', 'OffDuty'];

export const DriversSection: React.FC = () => {
  const [search, setSearch] = useState('');
  const [availabilityStatus, setAvailabilityStatus] = useState<DriverAvailabilityStatus | ''>('');
  const [page, setPage] = useState(1);
  const [detailId, setDetailId] = useState<string | null>(null);
  const drivers = useDrivers({ search, availabilityStatus, page, pageSize: 20 });
  const detail = useDriver(detailId);
  const list = drivers.data;

  return <div className="space-y-5">
    <div>
      <h2 className="text-lg font-bold text-slate-900">Collection drivers</h2>
      <p className="mt-1 text-sm text-slate-500">Duty availability is Driver-controlled. Assignment occupancy is derived from unfinished assignments.</p>
    </div>
    <Card className="p-4 sm:p-5">
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="text-xs font-semibold text-slate-700">Search drivers
          <input aria-label="Search drivers" placeholder="Search by driver name" value={search} onChange={(event) => { setSearch(event.target.value); setPage(1); }} className="mt-1.5 w-full rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-900 placeholder:text-slate-400 transition-colors focus:border-emerald-600 focus:bg-white focus:outline-none focus:ring-2 focus:ring-emerald-600/20" />
        </label>
        <label className="text-xs font-semibold text-slate-700">Duty availability
          <select aria-label="Filter duty availability" value={availabilityStatus} onChange={(event) => { setAvailabilityStatus(event.target.value as DriverAvailabilityStatus | ''); setPage(1); }} className="mt-1.5 w-full rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-900 transition-colors focus:border-emerald-600 focus:bg-white focus:outline-none focus:ring-2 focus:ring-emerald-600/20">
            <option value="">All duty availability</option>{availability.map((value) => <option key={value}>{value}</option>)}
          </select>
        </label>
      </div>
    </Card>
    {detailId && <Card className="border-emerald-100 p-5 sm:p-6">
      {detail.isLoading ? <div className="flex items-center gap-3 py-3 text-sm text-slate-500"><LoadingSpinner /> Loading driver details…</div> : detail.isError ? <Alert variant="error" title="Unable to load driver details">Please retry the request. <Button size="sm" variant="secondary" className="ml-3" onClick={() => detail.refetch()}>Retry</Button></Alert> : detail.data && <>
        <div className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><p className="text-xs font-semibold uppercase tracking-wider text-emerald-700">Driver details</p><h3 className="mt-1 text-lg font-bold text-slate-900">{detail.data.displayName}</h3><p className="mt-1 text-sm text-slate-500">Operational assignment information.</p></div><Button size="sm" variant="secondary" onClick={() => setDetailId(null)}>Close details</Button></div>
        <dl className="grid gap-3 sm:grid-cols-2"><div className="rounded-lg border border-slate-100 bg-slate-50 p-3"><dt className="text-xs font-medium text-slate-500">Duty availability</dt><dd className="mt-2"><FleetStatusBadge label={detail.data.availabilityStatus} tone={driverAvailabilityTone(detail.data.availabilityStatus)} /></dd></div><div className="rounded-lg border border-slate-100 bg-slate-50 p-3"><dt className="text-xs font-medium text-slate-500">Assignment occupancy</dt><dd className="mt-2"><FleetStatusBadge label={detail.data.isOccupied ? 'Occupied' : 'Not occupied'} tone={detail.data.isOccupied ? 'occupied' : 'available'} /></dd></div></dl>
      </>}
    </Card>}
    <Card className="overflow-hidden p-0">
      {drivers.isLoading ? <div className="flex flex-col items-center gap-3 py-16 text-slate-500"><LoadingSpinner size="lg" /><p className="text-sm font-medium">Loading collection drivers…</p></div> : drivers.isError ? <Alert variant="error" title="Unable to load drivers" className="m-4 sm:m-5">Please retry the request. <Button size="sm" variant="secondary" className="ml-3" onClick={() => drivers.refetch()}>Retry</Button></Alert> : !list?.items.length ? <div className="py-16 text-center"><p className="text-base font-semibold text-slate-800">No drivers match the current filters</p><p className="mt-1 text-xs text-slate-500">Adjust the search or availability filter to review other Driver accounts.</p></div> : <div className="overflow-x-auto"><table className="min-w-[620px] w-full text-left text-xs"><thead><tr className="border-b border-slate-200 bg-slate-50/80 font-semibold uppercase tracking-wider text-slate-500"><th className="px-4 py-3.5">Driver</th><th className="px-4 py-3.5">Duty availability</th><th className="px-4 py-3.5">Occupancy</th><th className="px-4 py-3.5 text-right">Actions</th></tr></thead><tbody className="divide-y divide-slate-100">{list.items.map((driver) => <tr key={driver.id} className="transition-colors hover:bg-slate-50/70"><td className="px-4 py-3.5 font-semibold text-slate-900">{driver.displayName}</td><td className="px-4 py-3.5"><FleetStatusBadge label={driver.availabilityStatus} tone={driverAvailabilityTone(driver.availabilityStatus)} /></td><td className="px-4 py-3.5"><FleetStatusBadge label={driver.isOccupied ? 'Occupied' : 'Not occupied'} tone={driver.isOccupied ? 'occupied' : 'available'} /></td><td className="px-4 py-3.5 text-right"><Button size="sm" variant="secondary" onClick={() => setDetailId(driver.id)}>View details</Button></td></tr>)}</tbody></table></div>}
      {list && list.totalCount > 0 && <div className="flex flex-col justify-between gap-3 border-t border-slate-100 p-4 text-xs text-slate-500 sm:flex-row sm:items-center"><span>Showing {Math.min((list.page - 1) * list.pageSize + 1, list.totalCount)}–{Math.min(list.page * list.pageSize, list.totalCount)} of {list.totalCount} drivers</span><div className="flex items-center gap-2"><Button size="sm" variant="secondary" disabled={list.page <= 1 || drivers.isFetching} onClick={() => setPage(page - 1)}>Previous</Button><span className="px-1 font-semibold text-slate-700">Page {list.page} of {Math.max(1, list.totalPages)}</span><Button size="sm" variant="secondary" disabled={list.page >= list.totalPages || drivers.isFetching} onClick={() => setPage(page + 1)}>Next</Button></div></div>}
    </Card>
  </div>;
};
