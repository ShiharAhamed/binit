import React, { useEffect } from 'react';
import { Button } from '../../../components/ui/Button';

export interface StartReviewModalProps {
  isOpen: boolean;
  isSubmitting: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

export const StartReviewModal: React.FC<StartReviewModalProps> = ({
  isOpen,
  isSubmitting,
  onConfirm,
  onCancel,
}) => {
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
      aria-labelledby="start-review-modal-title"
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
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4" />
            </svg>
          </div>
          <div>
            <h3
              id="start-review-modal-title"
              className="text-base font-bold text-slate-900 leading-snug"
            >
              Start reviewing this report?
            </h3>
            <p className="text-xs sm:text-sm text-slate-600 mt-1 leading-relaxed">
              This will move the report to Under Review.
            </p>
          </div>
        </div>

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
            onClick={onConfirm}
            isLoading={isSubmitting}
            disabled={isSubmitting}
            className="text-xs inline-flex items-center gap-1.5"
            data-testid="confirm-start-review-button"
          >
            Start Review
          </Button>
        </div>
      </div>
    </div>
  );
};
