import React, { useState, useEffect, useCallback } from 'react';
import { Button } from '../../../components/ui/Button';

export interface RejectReportModalProps {
  isOpen: boolean;
  isSubmitting: boolean;
  reason: string;
  onReasonChange: (value: string) => void;
  onConfirm: () => void;
  onCancel: () => void;
}

export const RejectReportModal: React.FC<RejectReportModalProps> = ({
  isOpen,
  isSubmitting,
  reason,
  onReasonChange,
  onConfirm,
  onCancel,
}) => {
  const [validationError, setValidationError] = useState<string | null>(null);

  const handleClose = useCallback(() => {
    setValidationError(null);
    onCancel();
  }, [onCancel]);

  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !isSubmitting) {
        handleClose();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    document.body.style.overflow = 'hidden';

    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      document.body.style.overflow = 'unset';
    };
  }, [isOpen, isSubmitting, handleClose]);

  if (!isOpen) {
    return null;
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (isSubmitting) return;

    const trimmed = reason.trim();
    if (!trimmed || trimmed.length < 5) {
      setValidationError('Please enter a reason of at least 5 characters.');
      return;
    }

    if (reason.length > 500) {
      setValidationError('Reason cannot exceed 500 characters.');
      return;
    }

    setValidationError(null);
    onConfirm();
  };

  const handleTextChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    onReasonChange(e.target.value);
    if (validationError) {
      const updatedTrimmed = e.target.value.trim();
      if (updatedTrimmed.length >= 5 && e.target.value.length <= 500) {
        setValidationError(null);
      }
    }
  };

  const charCount = reason.length;
  const isOverLimit = charCount > 500;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6 bg-slate-900/50 backdrop-blur-xs animate-in fade-in duration-150"
      role="dialog"
      aria-modal="true"
      aria-labelledby="reject-report-modal-title"
      onClick={() => {
        if (!isSubmitting) handleClose();
      }}
    >
      <div
        className="relative bg-white rounded-xl shadow-xl border border-slate-200/90 max-w-lg w-full p-6 text-left"
        onClick={(e) => e.stopPropagation()}
      >
        <form onSubmit={handleSubmit}>
          <div className="flex items-start gap-3.5 mb-4">
            <div className="w-10 h-10 rounded-full bg-red-50 border border-red-200/70 text-red-600 flex items-center justify-center shrink-0">
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
                />
              </svg>
            </div>
            <div className="flex-1">
              <h3
                id="reject-report-modal-title"
                className="text-base font-bold text-slate-900 leading-snug"
              >
                Reject Waste Report
              </h3>
              <p className="text-xs sm:text-sm text-slate-600 mt-1 leading-relaxed">
                Explain why this report is being rejected. This reason will be recorded in the audit history.
              </p>
            </div>
          </div>

          <div className="mt-4 space-y-1.5">
            <div className="flex items-center justify-between">
              <label htmlFor="reject-reason-input" className="block text-xs font-semibold text-slate-700">
                Rejection Reason <span className="text-red-600">*</span>
              </label>
              <span
                className={`text-[11px] font-mono ${
                  isOverLimit ? 'text-red-600 font-bold' : 'text-slate-400'
                }`}
              >
                {charCount} / 500
              </span>
            </div>

            <textarea
              id="reject-reason-input"
              data-testid="reject-reason-input"
              rows={4}
              disabled={isSubmitting}
              value={reason}
              onChange={handleTextChange}
              placeholder="e.g. Insufficient photo evidence, duplicate report, or location outside municipal jurisdiction..."
              className={`w-full rounded-lg border px-3.5 py-2 text-sm text-slate-900 placeholder:text-slate-400 transition-colors focus:outline-none focus:ring-2 disabled:bg-slate-50 disabled:text-slate-500 disabled:cursor-not-allowed resize-none ${
                validationError || isOverLimit
                  ? 'border-red-500 focus:border-red-500 focus:ring-red-500/20'
                  : 'border-slate-300 focus:border-emerald-600 focus:ring-emerald-600/20'
              }`}
              aria-invalid={Boolean(validationError || isOverLimit)}
              aria-describedby={validationError ? 'reject-reason-error' : undefined}
            />

            {validationError && (
              <p
                id="reject-reason-error"
                data-testid="reject-reason-error"
                className="text-xs text-red-600 font-medium"
              >
                {validationError}
              </p>
            )}
          </div>

          <div className="mt-6 flex items-center justify-end gap-3 pt-3 border-t border-slate-100">
            <Button
              type="button"
              variant="secondary"
              size="sm"
              onClick={handleClose}
              disabled={isSubmitting}
              className="text-xs"
            >
              Cancel
            </Button>
            <Button
              type="submit"
              variant="danger"
              size="sm"
              isLoading={isSubmitting}
              disabled={isSubmitting}
              className="text-xs inline-flex items-center gap-1.5"
              data-testid="confirm-reject-report-button"
            >
              Reject Report
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
};
