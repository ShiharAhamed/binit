import axios from 'axios';
import React, { useState } from 'react';
import { Alert } from '../../../components/ui/Alert';
import { Button } from '../../../components/ui/Button';
import { useCancelAssignment } from '../hooks/useAssignments';
import type { AssignmentDetailDto } from '../types/assignments';

interface AssignmentCancelDialogProps {
  assignment: AssignmentDetailDto;
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (updated: AssignmentDetailDto) => void;
}

const extractErrorMessage = (error: unknown) => {
  if (axios.isAxiosError(error)) {
    return error.response?.data?.detail || error.response?.data?.title || 'Unable to cancel this assignment.';
  }
  return 'Unable to cancel this assignment.';
};

export const AssignmentCancelDialog: React.FC<AssignmentCancelDialogProps> = ({
  assignment,
  isOpen,
  onClose,
  onSuccess,
}) => {
  const [reason, setReason] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);
  const [apiError, setApiError] = useState<string | null>(null);
  const cancelMutation = useCancelAssignment();

  if (!isOpen) return null;

  const handleReasonChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    setReason(e.target.value);
    setValidationError(null);
    setApiError(null);
  };

  const handleConfirm = async () => {
    const trimmed = reason.trim();
    if (!trimmed) {
      setValidationError('Cancellation reason is required.');
      return;
    }
    if (trimmed.length < 5) {
      setValidationError('Cancellation reason must be at least 5 characters.');
      return;
    }
    if (trimmed.length > 500) {
      setValidationError('Cancellation reason cannot exceed 500 characters.');
      return;
    }

    try {
      const result = await cancelMutation.mutateAsync({
        id: assignment.id,
        request: { reason: trimmed },
      });
      onSuccess(result);
      onClose();
    } catch (err) {
      setApiError(extractErrorMessage(err));
    }
  };

  const isEligible = assignment.status === 'Assigned';

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4 backdrop-blur-2xs"
      role="dialog"
      aria-modal="true"
      aria-labelledby="cancel-assignment-title"
    >
      <div className="w-full max-w-lg rounded-2xl border border-slate-100 bg-white p-6 shadow-xl">
        <div className="flex items-center justify-between border-b border-slate-100 pb-3">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-rose-700">Operational action</p>
            <h2 id="cancel-assignment-title" className="mt-1 text-lg font-bold text-slate-900">
              Cancel assignment
            </h2>
          </div>
          <Button size="sm" variant="ghost" onClick={onClose} aria-label="Close dialog">
            ✕
          </Button>
        </div>

        {!isEligible ? (
          <div className="mt-4 space-y-4">
            <Alert variant="error" title="Assignment cannot be cancelled">
              Only unstarted assignments in <span className="font-semibold">Assigned</span> status can be cancelled.
              Current status is <span className="font-semibold">{assignment.status}</span>.
            </Alert>
            <div className="flex justify-end">
              <Button variant="secondary" onClick={onClose}>
                Close
              </Button>
            </div>
          </div>
        ) : (
          <div className="mt-4 space-y-4">
            <p className="text-xs leading-relaxed text-slate-600">
              Cancelling assignment <span className="font-mono font-semibold text-slate-800">{assignment.id}</span>{' '}
              will atomically:
            </p>
            <ul className="list-inside list-disc space-y-1 text-xs text-slate-600">
              <li>
                Mark this collection assignment as <span className="font-semibold text-rose-700">Cancelled</span>.
              </li>
              <li>
                Return all <span className="font-semibold">{assignment.stopCount}</span> unstarted collection tasks back
                to <span className="font-semibold text-blue-700">Scheduled</span> status.
              </li>
              <li>
                Release the driver (<span className="font-medium text-slate-800">{assignment.driverName}</span>) and
                vehicle (
                <span className="font-medium text-slate-800">{assignment.vehicleRegistrationNumber}</span>) so they
                become available for new dispatches.
              </li>
              <li>Record your cancellation reason in the audit history.</li>
            </ul>

            {apiError && (
              <Alert variant="error" title="Cancellation failed">
                {apiError}
              </Alert>
            )}

            <div>
              <label htmlFor="cancel-reason" className="block text-xs font-semibold text-slate-700">
                Cancellation reason <span className="text-rose-600">*</span>
              </label>
              <textarea
                id="cancel-reason"
                aria-label="Cancellation reason"
                placeholder="Enter mandatory justification for cancellation (5–500 characters)..."
                rows={3}
                value={reason}
                onChange={handleReasonChange}
                disabled={cancelMutation.isPending}
                className="mt-1.5 w-full rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-900 placeholder:text-slate-400 transition-colors focus:border-rose-600 focus:bg-white focus:outline-none focus:ring-2 focus:ring-rose-600/20 disabled:opacity-50"
              />
              <div className="mt-1 flex items-center justify-between text-[11px] text-slate-400">
                <span>{validationError ? <span className="text-rose-600 font-medium">{validationError}</span> : '5 to 500 characters'}</span>
                <span>{reason.trim().length} / 500</span>
              </div>
            </div>

            <div className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
              <Button variant="secondary" onClick={onClose} disabled={cancelMutation.isPending}>
                Keep assignment
              </Button>
              <Button
                variant="danger"
                onClick={handleConfirm}
                isLoading={cancelMutation.isPending}
                disabled={cancelMutation.isPending}
              >
                Confirm cancellation
              </Button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
