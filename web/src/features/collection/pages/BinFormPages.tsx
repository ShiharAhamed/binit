import React from 'react';
import axios from 'axios';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Alert } from '../../../components/ui/Alert';
import { Card } from '../../../components/ui/Card';
import { LoadingSpinner } from '../../../components/ui/LoadingSpinner';
import { binsApi } from '../api/binsApi';
import { BinForm, type BinFormInitialValues } from '../components/BinForm';
import { useBinDetail } from '../hooks/useBinDetail';
import type { CreateWasteBinRequest, UpdateWasteBinRequest } from '../types/bins';

const emptyValues: BinFormInitialValues = { binCode: '', latitude: undefined, longitude: undefined, addressText: '', capacityLiters: 0, acceptedWasteTypes: [], collectionWeekdays: [] };

const messageForError = (error: unknown, mode: 'create' | 'edit') => {
  if (!axios.isAxiosError(error)) return 'Unable to reach the service. Your entered values have been kept; please try again.';
  switch (error.response?.status) {
    case 400: return 'Some details were not accepted. Review the highlighted fields and try again.';
    case 401: return 'Your session has expired. Please sign in again.';
    case 403: return 'You do not have permission to manage bins.';
    case 404: return mode === 'edit' ? 'This bin could not be found.' : 'The requested resource could not be found.';
    case 409: return mode === 'edit' ? 'This bin cannot be updated because it is retired or its state has changed.' : 'That bin code is already registered. Choose a different code.';
    default: return 'Unable to save the bin. Your entered values have been kept; please try again.';
  }
};

const FormPageShell: React.FC<{ title: string; subtitle: string; children: React.ReactNode }> = ({ title, subtitle, children }) => <div className="space-y-6 pb-12"><Link to="/officer/bins" className="inline-flex items-center gap-1.5 text-xs font-semibold text-emerald-700 hover:text-emerald-800">← Back to Bin Management</Link><div className="border-b border-slate-200/80 pb-5"><h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-slate-900">{title}</h1><p className="mt-1 text-sm text-slate-500">{subtitle}</p></div><Card className="max-w-3xl p-5 sm:p-7">{children}</Card></div>;

export const RegisterBinPage: React.FC = () => {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const mutation = useMutation({ mutationFn: (request: CreateWasteBinRequest) => binsApi.createBin(request), onSuccess: async (bin) => { await queryClient.invalidateQueries({ queryKey: ['bins'] }); navigate(`/officer/bins/${bin.id}`, { replace: true }); } });
  return <FormPageShell title="Register Bin" subtitle="Add a municipal roadside bin to the operational registry.">{mutation.isError && <Alert variant="error" className="mb-5" title="Bin registration failed">{messageForError(mutation.error, 'create')}</Alert>}<BinForm mode="create" initialValues={emptyValues} isSubmitting={mutation.isPending} onCancel={() => navigate('/officer/bins')} onSubmit={(request) => mutation.mutate(request as CreateWasteBinRequest)} /></FormPageShell>;
};

export const EditBinPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { bin, isLoading, isError, isNotFound, refetch } = useBinDetail(id);
  const mutation = useMutation({ mutationFn: (request: UpdateWasteBinRequest) => binsApi.updateBin(id!, request), onSuccess: async (updated) => { await queryClient.invalidateQueries({ queryKey: ['bins'] }); await queryClient.invalidateQueries({ queryKey: ['bins', id] }); navigate(`/officer/bins/${updated.id}`, { replace: true }); } });
  if (isLoading) return <div className="py-16 flex justify-center" data-testid="edit-bin-loading"><LoadingSpinner label="Loading bin" /></div>;
  if (isError || !bin) return <FormPageShell title="Edit Bin" subtitle="Update bin operational metadata."><Alert variant="error" title={isNotFound ? 'Bin not found' : 'Failed to load bin'}><p>{isNotFound ? 'This bin could not be found.' : 'Unable to retrieve this bin. Please try again.'}</p><button type="button" onClick={() => refetch()} className="mt-3 text-xs font-semibold text-emerald-700 hover:text-emerald-800">Retry</button></Alert></FormPageShell>;
  if (bin.administrativeStatus === 'Retired') return <FormPageShell title="Edit Bin" subtitle="Update bin operational metadata."><Alert variant="warning" title="Retired bins cannot be edited">This bin is retired and the backend does not allow metadata updates.</Alert></FormPageShell>;
  const initialValues: BinFormInitialValues = { binCode: bin.binCode, latitude: bin.latitude, longitude: bin.longitude, addressText: bin.addressText ?? '', capacityLiters: bin.capacityLiters, acceptedWasteTypes: bin.acceptedWasteTypes as BinFormInitialValues['acceptedWasteTypes'], collectionWeekdays: bin.collectionWeekdays };
  return <FormPageShell title="Edit Bin" subtitle="Update the location, capacity, accepted waste types, and routine schedule.">{mutation.isError && <Alert variant="error" className="mb-5" title="Bin update failed">{messageForError(mutation.error, 'edit')}</Alert>}<BinForm mode="edit" initialValues={initialValues} isSubmitting={mutation.isPending} onCancel={() => navigate(`/officer/bins/${bin.id}`)} onSubmit={(request) => mutation.mutate(request as UpdateWasteBinRequest)} /></FormPageShell>;
};
