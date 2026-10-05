import React from 'react';
import axios from 'axios';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Alert } from '../../../components/ui/Alert';
import { Card } from '../../../components/ui/Card';
import { LoadingSpinner } from '../../../components/ui/LoadingSpinner';
import { binsApi } from '../api/binsApi';
import { ObservationForm } from '../components/ObservationForm';
import { useBinDetail } from '../hooks/useBinDetail';
import type { RecordBinObservationRequest } from '../types/bins';

const errorMessageFor = (error: unknown) => {
  if (!axios.isAxiosError(error)) return 'Unable to reach the service. Your entered values have been kept; please try again.';
  switch (error.response?.status) {
    case 400: return 'Some observation details were not accepted. Review the form and try again.';
    case 401: return 'Your session has expired. Please sign in again.';
    case 403: return 'You do not have permission to record observations.';
    case 404: return 'This bin could not be found.';
    case 409: return 'This observation cannot be recorded for the bin in its current state. Refresh the bin details and try again.';
    default: return 'Unable to record the observation. Your entered values have been kept; please try again.';
  }
};

export const RecordBinObservationPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { bin, isLoading, isError, isNotFound, refetch } = useBinDetail(id);
  const mutation = useMutation({
    mutationFn: (request: RecordBinObservationRequest) => binsApi.recordObservation(id!, request),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['bins'] }),
        queryClient.invalidateQueries({ queryKey: ['bins', id] }),
        queryClient.invalidateQueries({ queryKey: ['bin-observations', id] }),
      ]);
      navigate(`/officer/bins/${id}`, { replace: true });
    },
  });
  const detailPath = id ? `/officer/bins/${id}` : '/officer/bins';

  if (isLoading) return <div className="flex justify-center py-16" data-testid="record-observation-loading"><LoadingSpinner label="Loading bin" /></div>;
  if (isError || !bin) return <div className="space-y-6"><Link to={detailPath} className="inline-flex items-center gap-1.5 text-xs font-semibold text-emerald-700 hover:text-emerald-800">← Back to Bin Details</Link><Alert variant="error" title={isNotFound ? 'Bin not found' : 'Failed to load bin'}><p>{isNotFound ? 'This bin could not be found.' : 'Unable to retrieve this bin. Please try again.'}</p><button type="button" onClick={() => refetch()} className="mt-3 text-xs font-semibold text-emerald-700 hover:text-emerald-800">Retry</button></Alert></div>;

  return <div className="space-y-6 pb-12" data-testid="record-observation-page">
    <Link to={detailPath} className="inline-flex items-center gap-1.5 text-xs font-semibold text-emerald-700 hover:text-emerald-800">← Back to Bin Details</Link>
    <div className="border-b border-slate-200/80 pb-5"><h1 className="text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl">Record Observation</h1><p className="mt-1 text-sm text-slate-500">Add an append-only human-recorded field observation for <span className="font-mono font-semibold text-slate-700">{bin.binCode}</span>.</p></div>
    <Card className="max-w-3xl p-5 sm:p-7"><div className="mb-5 rounded-lg border border-slate-200 bg-slate-50 p-3"><p className="text-xs font-medium text-slate-500">Observing bin</p><p className="mt-1 font-mono text-sm font-bold text-emerald-800">{bin.binCode}</p><p className="mt-1 text-xs text-slate-600">{bin.addressText?.trim() || 'Address not recorded'}</p></div>{mutation.isError && <Alert variant="error" className="mb-5" title="Observation was not recorded">{errorMessageFor(mutation.error)}</Alert>}<ObservationForm submitLabel="Save Observation" isSubmitting={mutation.isPending} onSubmit={(request) => mutation.mutate(request)} onCancel={() => navigate(detailPath)} /></Card>
  </div>;
};
