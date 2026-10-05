import React, { useEffect, useState } from 'react';
import { Button } from '../../../components/ui/Button';
import type { DeactivateWasteBinRequest } from '../types/bins';

interface DeactivateBinModalProps {
  binCode: string;
  isOpen: boolean;
  isSubmitting: boolean;
  errorMessage: string | null;
  onConfirm: (request: DeactivateWasteBinRequest) => void;
  onCancel: () => void;
}

export const DeactivateBinModal: React.FC<DeactivateBinModalProps> = ({ binCode, isOpen, isSubmitting, errorMessage, onConfirm, onCancel }) => {
  const [targetStatus, setTargetStatus] = useState<DeactivateWasteBinRequest['targetStatus'] | ''>('');
  const [reason, setReason] = useState('');
  const [statusError, setStatusError] = useState<string | null>(null);

  useEffect(() => {
    if (!isOpen) return;
    setTargetStatus('');
    setReason('');
    setStatusError(null);
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen) return;
    const onKeyDown = (event: KeyboardEvent) => { if (event.key === 'Escape' && !isSubmitting) onCancel(); };
    window.addEventListener('keydown', onKeyDown);
    document.body.style.overflow = 'hidden';
    return () => { window.removeEventListener('keydown', onKeyDown); document.body.style.overflow = 'unset'; };
  }, [isOpen, isSubmitting, onCancel]);

  if (!isOpen) return null;
  const submit = () => {
    if (!targetStatus) { setStatusError('Select a deactivation status.'); return; }
    onConfirm({ targetStatus, reason: reason.trim() || null });
  };

  return <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 backdrop-blur-xs" role="dialog" aria-modal="true" aria-labelledby="deactivate-bin-modal-title" onClick={() => { if (!isSubmitting) onCancel(); }}>
    <div className="w-full max-w-md rounded-xl border border-slate-200/90 bg-white p-6 text-left shadow-xl" onClick={(event) => event.stopPropagation()}>
      <div className="mb-4 flex items-start gap-3.5"><div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full border border-rose-200 bg-rose-50 text-rose-700">!</div><div><h3 id="deactivate-bin-modal-title" className="text-base font-bold text-slate-900">Deactivate bin {binCode}?</h3><p className="mt-1 text-xs leading-relaxed text-slate-600">This bin will no longer be available for normal collection use. The backend will confirm whether deactivation is currently permitted.</p></div></div>
      {errorMessage && <div role="alert" className="mb-4 rounded-lg border border-red-200 bg-red-50 p-3 text-xs text-red-800">{errorMessage}</div>}
      <label className="block text-sm font-semibold text-slate-800">Deactivation status <span className="text-rose-600">*</span><select aria-label="Deactivation status" value={targetStatus} disabled={isSubmitting} onChange={(event) => { setTargetStatus(event.target.value as DeactivateWasteBinRequest['targetStatus']); setStatusError(null); }} className="mt-2 block w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"><option value="">Select status</option><option value="OutOfService">Out of service</option><option value="Retired">Retired</option></select></label>
      {statusError && <p className="mt-1 text-xs text-red-600">{statusError}</p>}
      <div className="mt-4"><label htmlFor="deactivation-reason" className="block text-sm font-semibold text-slate-800">Reason <span className="font-normal text-slate-500">(optional)</span></label><textarea id="deactivation-reason" value={reason} maxLength={500} disabled={isSubmitting} onChange={(event) => setReason(event.target.value)} className="mt-2 min-h-24 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-600/20" placeholder="Add an operational reason..." /><p className="mt-1 text-right text-[11px] text-slate-400">{reason.length}/500</p></div>
      <div className="mt-6 flex items-center justify-end gap-3 border-t border-slate-100 pt-3"><Button type="button" variant="secondary" size="sm" onClick={onCancel} disabled={isSubmitting}>Cancel</Button><Button type="button" variant="danger" size="sm" onClick={submit} disabled={isSubmitting || !targetStatus} isLoading={isSubmitting} data-testid="confirm-deactivate-bin-button">Confirm Deactivation</Button></div>
    </div>
  </div>;
};
