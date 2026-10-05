import React, { useState, useEffect, useCallback } from 'react';
import { Button } from '../../../components/ui/Button';

export interface ResolveComplaintModalProps {
  isOpen: boolean;
  isSubmitting: boolean;
  onConfirm: (resolutionNote: string) => void;
  onCancel: () => void;
}

export const ResolveComplaintModal: React.FC<ResolveComplaintModalProps> = ({
  isOpen,
  isSubmitting,
  onConfirm,
  onCancel,
}) => {
  const [resolutionNote, setResolutionNote] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);

  const handleClose = useCallback(() => {
    setResolutionNote('');
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

    const trimmed = resolutionNote.trim();
    if (!trimmed) {
      setValidationError('Resolution note is required.');
      return;
    }

    if (trimmed.length < 5) {
      setValidationError('Resolution note must be at least 5 characters long.');
      return;
    }

    if (resolutionNote.length > 1000) {
      setValidationError('Resolution note cannot exceed 1000 characters.');
      return;
    }

    setValidationError(null);
    onConfirm(trimmed);
  };

  const handleTextChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    const val = e.target.value;
    setResolutionNote(val);
    if (validationError) {
      const updatedTrimmed = val.trim();
      if (updatedTrimmed.length >= 5 && val.length <= 1000) {
        setValidationError(null);
      }
    }
  };

  const charCount = resolutionNote.length;
  const isOverLimit = charCount > 1000;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6 bg-slate-900/50 backdrop-blur-xs animate-in fade-in duration-150"
      role="dialog"
      aria-modal="true"
      aria-labelledby="resolve-complaint-modal-title"
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
            <div className="w-10 h-10 rounded-full bg-emerald-50 border border-emerald-200/70 text-emerald-600 flex items-center justify-center shrink-0">
              <svg
                className="w-5 h-5"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
                aria-hidden="true"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"
                />
              </svg>
            </div>
            <div className="flex-1">
              <h3
                id="resolve-complaint-modal-title"
                className="text-base font-bold text-slate-900 leading-snug"
              >
                Resolve Complaint
              </h3>
              <p className="text-xs sm:text-sm text-slate-600 mt-1 leading-relaxed">
                Add a resolution note that will be visible to the citizen.
              </p>
            </div>
          </div>

          <div className="mt-4 space-y-1.5">
            <div className="flex items-center justify-between">
              <label
                htmlFor="resolution-note-input"
                className="block text-xs font-semibold text-slate-700"
              >
                Resolution Note <span className="text-red-600">*</span>
              </label>
              <span
                className={`text-[11px] font-mono ${
                  isOverLimit ? 'text-red-600 font-bold' : 'text-slate-400'
                }`}
                data-testid="char-counter"
              >
                {charCount} / 1000
              </span>
            </div>

            <textarea
              id="resolution-note-input"
              data-testid="resolution-note-input"
              rows={4}
              disabled={isSubmitting}
              value={resolutionNote}
              onChange={handleTextChange}
              placeholder="e.g. Area inspected and missed collection completed by recovery crew on 14th Oct..."
              className={`w-full rounded-lg border px-3.5 py-2 text-sm text-slate-900 placeholder:text-slate-400 transition-colors focus:outline-none focus:ring-2 disabled:bg-slate-50 disabled:text-slate-500 disabled:cursor-not-allowed resize-none ${
                validationError || isOverLimit
                  ? 'border-red-500 focus:border-red-500 focus:ring-red-500/20'
                  : 'border-slate-300 focus:border-emerald-600 focus:ring-emerald-600/20'
              }`}
              aria-invalid={Boolean(validationError || isOverLimit)}
              aria-describedby={validationError ? 'resolution-note-error' : undefined}
            />

            {validationError && (
              <p
                id="resolution-note-error"
                data-testid="resolution-note-error"
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
              variant="primary"
              size="sm"
              isLoading={isSubmitting}
              disabled={isSubmitting}
              className="text-xs inline-flex items-center gap-1.5"
              data-testid="confirm-resolve-complaint-button"
            >
              Resolve Complaint
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
};
