import React, { useEffect, useState } from 'react';
import type { WasteReportPriority } from '../types/reporting';
import { Button } from '../../../components/ui/Button';

export interface VerifyReportModalProps {
  isOpen: boolean;
  isSubmitting: boolean;
  onConfirm: (priority: WasteReportPriority) => void;
  onCancel: () => void;
  aiRecommendedPriority?: WasteReportPriority | null;
}

export const VerifyReportModal: React.FC<VerifyReportModalProps> = ({
  isOpen,
  isSubmitting,
  onConfirm,
  onCancel,
  aiRecommendedPriority,
}) => {
  const [priority, setPriority] = useState<WasteReportPriority | ''>('');
  useEffect(() => {
    if (isOpen) setPriority('');
  }, [isOpen]);
  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !isSubmitting) {
        onCancel();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    document.body.style.overflow = 'hidden';

    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      document.body.style.overflow = 'unset';
    };
  }, [isOpen, isSubmitting, onCancel]);

  if (!isOpen) {
    return null;
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6 bg-slate-900/50 backdrop-blur-xs animate-in fade-in duration-150"
      role="dialog"
      aria-modal="true"
      aria-labelledby="verify-report-modal-title"
      onClick={() => {
        if (!isSubmitting) onCancel();
      }}
    >
      <div
        className="relative bg-white rounded-xl shadow-xl border border-slate-200/90 max-w-md w-full p-6 text-left"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-start gap-3.5 mb-4">
          <div className="w-10 h-10 rounded-full bg-emerald-50 border border-emerald-200/70 text-emerald-600 flex items-center justify-center shrink-0">
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
            </svg>
          </div>
          <div>
            <h3
              id="verify-report-modal-title"
              className="text-base font-bold text-slate-900 leading-snug"
            >
              Verify this report?
            </h3>
            <p className="text-xs sm:text-sm text-slate-600 mt-1 leading-relaxed">
              Confirm that the submitted waste report has been reviewed and is valid.
            </p>
          </div>
        </div>
        {aiRecommendedPriority && <p className="mb-4 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">AI advisory recommendation: <strong>{aiRecommendedPriority}</strong>. Select the final priority yourself.</p>}
        <label className="block text-sm font-semibold text-slate-800">Priority <span className="text-rose-600">*</span><select aria-label="Verification priority" value={priority} disabled={isSubmitting} onChange={(e) => setPriority(e.target.value as WasteReportPriority)} className="mt-2 block w-full rounded-lg border border-slate-300 px-3 py-2"><option value="">Select priority</option>{(['Low', 'Medium', 'High', 'Urgent'] as WasteReportPriority[]).map((value) => <option key={value}>{value}</option>)}</select></label>

        <div className="mt-6 flex items-center justify-end gap-3 pt-3 border-t border-slate-100">
          <Button
            type="button"
            variant="secondary"
            size="sm"
            onClick={onCancel}
            disabled={isSubmitting}
            className="text-xs"
          >
            Cancel
          </Button>
          <Button
            type="button"
            variant="primary"
            size="sm"
            onClick={() => priority && onConfirm(priority)}
            isLoading={isSubmitting}
            disabled={isSubmitting || !priority}
            className="text-xs inline-flex items-center gap-1.5"
            data-testid="confirm-verify-report-button"
          >
            Verify Report
          </Button>
        </div>
      </div>
    </div>
  );
};
