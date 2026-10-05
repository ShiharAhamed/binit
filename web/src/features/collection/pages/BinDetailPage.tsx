import React, { useState } from 'react';
import axios from 'axios';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { Alert } from '../../../components/ui/Alert';
import { Button } from '../../../components/ui/Button';
import { Card, CardContent, CardHeader, CardTitle } from '../../../components/ui/Card';
import { BinsIcon } from '../../../components/ui/Icons';
import { useBinDetail } from '../hooks/useBinDetail';
import { useBinObservations } from '../hooks/useBinObservations';
import { ObservationHistory } from '../components/ObservationHistory';
import { BinLocationMap, isValidBinLocation } from '../components/BinLocationMap';
import { DeactivateBinModal } from '../components/DeactivateBinModal';
import { binsApi } from '../api/binsApi';
import { useAuthStore } from '../../../store/authStore';
import { BIN_CONDITION_LABELS, BIN_STATUS_LABELS, type BinAdministrativeStatus, type BinCondition, type DeactivateWasteBinRequest } from '../types/bins';

const weekdayNames = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

const statusClasses: Record<BinAdministrativeStatus, string> = {
  Active: 'bg-emerald-50 text-emerald-800 border-emerald-200',
  OutOfService: 'bg-amber-50 text-amber-800 border-amber-200',
  Retired: 'bg-slate-100 text-slate-700 border-slate-200',
};

const conditionClasses: Record<BinCondition, string> = {
  Good: 'text-emerald-700',
  Damaged: 'text-amber-700',
  Blocked: 'text-red-700',
  Missing: 'text-red-700',
};

const formatDateTime = (value: string | null) => {
  if (!value) return 'Not recorded';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false }).format(date);
};

export const BinDetailPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const { bin, isLoading, isError, errorMessage, isNotFound, refetch } = useBinDetail(id);
  const { user } = useAuthStore();
  const queryClient = useQueryClient();
  const [observationPage, setObservationPage] = useState(1);
  const [isDeactivateModalOpen, setIsDeactivateModalOpen] = useState(false);
  const history = useBinObservations(id, { page: observationPage, pageSize: 10 });
  const isOfficer = user?.role === 'WasteOfficer';
  const deactivationMutation = useMutation({
    mutationFn: (request: DeactivateWasteBinRequest) => binsApi.deactivateBin(id!, request),
    onSuccess: async (updatedBin) => {
      queryClient.setQueryData(['bins', id], updatedBin);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['bins'] }),
        queryClient.invalidateQueries({ queryKey: ['bins', id] }),
      ]);
      setIsDeactivateModalOpen(false);
    },
  });

  if (isLoading) {
    return <div className="space-y-6 animate-pulse" data-testid="bin-detail-loading"><div className="h-8 w-48 bg-slate-200 rounded" /><div className="h-44 bg-slate-200 rounded-xl" /><div className="grid grid-cols-1 lg:grid-cols-2 gap-6"><div className="h-72 bg-slate-200 rounded-xl" /><div className="h-72 bg-slate-200 rounded-xl" /></div></div>;
  }

  if (isError || !bin) {
    return <div className="space-y-6" data-testid="bin-detail-error">
      <Link to="/officer/bins" className="inline-flex items-center gap-1.5 text-xs font-semibold text-emerald-700 hover:text-emerald-800" data-testid="back-to-bins-link">← Back to Bin Management</Link>
      <Alert variant="error" title={isNotFound ? 'Bin not found' : 'Failed to load bin'}>
        <p>{isNotFound ? 'This bin could not be found. It may no longer be available in the registry.' : errorMessage}</p>
        <div className="mt-4 flex gap-3"><Button variant="secondary" size="sm" onClick={() => refetch()}>Retry</Button><Link to="/officer/bins" className="inline-flex items-center justify-center px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-100 rounded-md">Return to list</Link></div>
      </Alert>
    </div>;
  }

  const hasValidLocation = isValidBinLocation(bin.latitude, bin.longitude);
  const location = bin.addressText?.trim() || (hasValidLocation ? `${bin.latitude.toFixed(5)}, ${bin.longitude.toFixed(5)}` : 'Location unavailable');
  const observation = bin.latestObservation;
  const deactivationError = (() => {
    if (!deactivationMutation.error) return null;
    if (!axios.isAxiosError(deactivationMutation.error)) return 'Unable to reach the service. The bin remains active; please try again.';
    const detail = deactivationMutation.error.response?.data?.detail;
    switch (deactivationMutation.error.response?.status) {
      case 400: return 'Some deactivation details were not accepted. Review the form and try again.';
      case 401: return 'Your session has expired. Please sign in again.';
      case 403: return 'You do not have permission to deactivate bins.';
      case 404: return 'This bin could not be found.';
      case 409: return detail || 'This bin cannot be deactivated while it has an active collection task or its state has changed.';
      default: return 'Unable to deactivate this bin. The bin remains active; please try again.';
    }
  })();

  return <div className="space-y-6 pb-12" data-testid="bin-detail-page">
    <Link to="/officer/bins" className="inline-flex items-center gap-1.5 text-xs font-semibold text-emerald-700 hover:text-emerald-800 transition-colors" data-testid="back-to-bins-link">← Back to Bin Management</Link>
    <section className="bg-white rounded-xl border border-slate-200 p-5 sm:p-6 shadow-2xs">
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4"><div><div className="flex flex-wrap items-center gap-2.5"><span className="font-mono text-xs font-bold text-emerald-800 bg-emerald-50 px-2 py-0.5 rounded border border-emerald-200/80">{bin.binCode}</span><span className={`inline-flex px-2 py-0.5 rounded border text-[11px] font-semibold ${statusClasses[bin.administrativeStatus]}`}>{BIN_STATUS_LABELS[bin.administrativeStatus]}</span></div><h1 className="text-xl sm:text-2xl font-bold text-slate-900 mt-2 flex items-center gap-2">Bin Details <BinsIcon className="w-5 h-5 text-emerald-600" /></h1></div><div className="flex flex-wrap items-center gap-3">{bin.administrativeStatus !== 'Retired' && <Link to={`/officer/bins/${bin.id}/edit`} className="inline-flex items-center justify-center px-3 py-1.5 rounded-md text-xs font-semibold bg-emerald-600 text-white hover:bg-emerald-700">Edit Bin</Link>}{isOfficer && <Link to={`/officer/bins/${bin.id}/observations/new`} className="inline-flex items-center justify-center rounded-md bg-emerald-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-emerald-700">Record Observation</Link>}{isOfficer && bin.administrativeStatus === 'Active' && <Button variant="danger" size="sm" onClick={() => { deactivationMutation.reset(); setIsDeactivateModalOpen(true); }}>Deactivate Bin</Button>}<span className={bin.hasActiveTask ? 'text-xs font-semibold text-amber-700 bg-amber-50 border border-amber-200 rounded-full px-3 py-1' : 'text-xs font-medium text-slate-600 bg-slate-100 border border-slate-200 rounded-full px-3 py-1'}>{bin.hasActiveTask ? 'An active collection task exists' : 'No active collection task'}</span></div></div>
    </section>
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
      <Card><CardHeader className="mb-4"><CardTitle className="text-base">Bin overview</CardTitle></CardHeader><CardContent><dl className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-xs"><DetailItem label="Capacity" value={`${bin.capacityLiters.toLocaleString()} L`} /><DetailItem label="Last collected" value={formatDateTime(bin.lastCollectedAt)} /><DetailItem label="Registered" value={formatDateTime(bin.createdAt)} /><DetailItem label="Last updated" value={formatDateTime(bin.updatedAt)} /></dl></CardContent></Card>
      <Card><CardHeader className="mb-4"><CardTitle className="text-base">Location and collection setup</CardTitle></CardHeader><CardContent className="space-y-4"><div className="bg-slate-50/70 p-3 rounded-lg border border-slate-100"><p className="text-xs font-medium text-slate-500">Address</p><p className="mt-1 text-sm font-semibold text-slate-900">{location}</p><p className="mt-1 text-[11px] font-mono text-slate-500">{hasValidLocation ? `Latitude: ${bin.latitude.toFixed(6)} · Longitude: ${bin.longitude.toFixed(6)}` : 'Coordinates unavailable'}</p></div><BinLocationMap latitude={bin.latitude} longitude={bin.longitude} addressText={bin.addressText} /><div><p className="text-xs font-medium text-slate-500">Accepted waste types</p><div className="mt-2 flex flex-wrap gap-1.5">{bin.acceptedWasteTypes.map((type) => <span key={type} className="px-2 py-0.5 text-xs rounded bg-slate-100 border border-slate-200 text-slate-700">{type}</span>)}</div></div><div><p className="text-xs font-medium text-slate-500">Collection weekdays</p><p className="mt-1 text-sm text-slate-800">{bin.collectionWeekdays.length ? bin.collectionWeekdays.map((day) => weekdayNames[day - 1] ?? `Day ${day}`).join(', ') : 'No routine weekdays configured'}</p></div></CardContent></Card>
      <Card className="lg:col-span-2"><CardHeader className="mb-4"><CardTitle className="text-base">Latest recorded observation</CardTitle><p className="text-xs text-slate-500 mt-1">This is the latest recorded field observation, not a live bin reading.</p></CardHeader><CardContent>{observation ? <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 text-xs"><DetailItem label="Recorded fill level" value={`${observation.fillLevelPercent}%`} /><DetailItem label="Condition" value={<span className={conditionClasses[observation.condition]}>{BIN_CONDITION_LABELS[observation.condition]}</span>} /><DetailItem label="Recorded at" value={formatDateTime(observation.recordedAt)} /><DetailItem label="Recorded by" value={observation.recordedByUserName || 'Officer name unavailable'} /><div className="sm:col-span-2 bg-slate-50/70 p-3 rounded-lg border border-slate-100"><dt className="text-slate-500 font-medium">Observation notes</dt><dd className="mt-1 text-sm text-slate-800 whitespace-pre-wrap">{observation.notes?.trim() || 'No notes recorded'}</dd></div></div> : <div className="rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600"><p className="font-semibold text-slate-800">Unknown / Not recorded</p><p className="mt-1 text-xs">Fill level and condition are unavailable until a field observation is recorded.</p></div>}</CardContent></Card>
      <Card className="lg:col-span-2"><CardHeader className="mb-4"><CardTitle className="text-base">Observation history</CardTitle><p className="text-xs text-slate-500 mt-1">Append-only human-recorded history. Values are not real-time sensor readings.</p></CardHeader><CardContent><ObservationHistory observations={history.observations} isLoading={history.isLoading} isError={history.isError} errorMessage={history.errorMessage} totalCount={history.totalCount} currentPage={history.currentPage} totalPages={history.totalPages} onPageChange={setObservationPage} onRetry={() => history.refetch()} /></CardContent></Card>
    </div>
    <DeactivateBinModal binCode={bin.binCode} isOpen={isDeactivateModalOpen} isSubmitting={deactivationMutation.isPending} errorMessage={deactivationError} onConfirm={(request) => deactivationMutation.mutate(request)} onCancel={() => setIsDeactivateModalOpen(false)} />
  </div>;
};

const DetailItem: React.FC<{ label: string; value: React.ReactNode }> = ({ label, value }) => <div className="bg-slate-50/70 p-3 rounded-lg border border-slate-100"><dt className="text-slate-500 font-medium">{label}</dt><dd className="mt-1 text-sm font-semibold text-slate-900">{value}</dd></div>;

export default BinDetailPage;
