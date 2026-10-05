import React, { useState } from 'react';
import { Alert } from '../../../components/ui/Alert';
import { Button } from '../../../components/ui/Button';
import { colomboLocalToUtcIso } from './ManualCollectionTaskForm';
import type {
  CollectionTaskDetailDto,
  CreateReplacementCollectionTaskRequest,
} from '../types/collectionTasks';

interface ReplacementTaskFormProps {
  failedTask: CollectionTaskDetailDto;
  isSubmitting: boolean;
  apiError?: string | null;
  onSubmit: (request: CreateReplacementCollectionTaskRequest) => void;
  onCancel: () => void;
}

export const ReplacementTaskForm: React.FC<ReplacementTaskFormProps> = ({
  failedTask,
  isSubmitting,
  apiError,
  onSubmit,
  onCancel,
}) => {
  const [scheduledLocal, setScheduledLocal] = useState('');
  const [replacementReason, setReplacementReason] = useState('');
  const [handlingNotes, setHandlingNotes] = useState(failedTask.handlingNotes ?? '');
  const [schedulingReason, setSchedulingReason] = useState(failedTask.schedulingReason ?? '');
  const [errors, setErrors] = useState<Record<string, string>>({});

  const validate = (): CreateReplacementCollectionTaskRequest | null => {
    const nextErrors: Record<string, string> = {};

    if (!scheduledLocal.trim()) {
      nextErrors.scheduledAt = 'Enter a scheduled collection date and time.';
    } else {
      const utcIso = colomboLocalToUtcIso(scheduledLocal);
      if (!utcIso) {
        nextErrors.scheduledAt = 'Enter a valid date and time format.';
      } else if (new Date(utcIso).getTime() <= Date.now()) {
        nextErrors.scheduledAt = 'Scheduled time must be in the future.';
      }
    }

    const trimmedReason = replacementReason.trim();
    if (!trimmedReason || trimmedReason.length < 5) {
      nextErrors.replacementReason = 'Replacement reason must be between 5 and 500 characters.';
    } else if (replacementReason.length > 500) {
      nextErrors.replacementReason = 'Replacement reason cannot exceed 500 characters.';
    }

    if (handlingNotes.length > 1000) {
      nextErrors.handlingNotes = 'Handling notes cannot exceed 1000 characters.';
    }

    if (schedulingReason.length > 500) {
      nextErrors.schedulingReason = 'Scheduling justification cannot exceed 500 characters.';
    }

    setErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) {
      return null;
    }

    const utcIso = colomboLocalToUtcIso(scheduledLocal)!;
    return {
      scheduledAt: utcIso,
      replacementReason: trimmedReason,
      handlingNotes: handlingNotes.trim() ? handlingNotes.trim() : null,
      schedulingReason: schedulingReason.trim() ? schedulingReason.trim() : null,
    };
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const payload = validate();
    if (payload) {
      onSubmit(payload);
    }
  };

  return (
    <form
      onSubmit={handleSubmit}
      noValidate
      className="space-y-4"
      data-testid="replacement-task-form"
      aria-label="Schedule replacement collection task"
    >
      <div className="rounded-lg border border-amber-200 bg-amber-50/70 p-3 text-xs leading-relaxed text-amber-900">
        <p className="font-semibold text-amber-950">Important operational note</p>
        <p className="mt-0.5">
          Submitting this form schedules a <span className="font-semibold">NEW</span> collection task for this target.
          Original task <span className="font-mono font-semibold">{failedTask.taskCode}</span> and its failure history
          will remain intact as an auditable historical record.
        </p>
      </div>

      {apiError && (
        <Alert variant="error" title="Unable to schedule replacement">
          {apiError}
        </Alert>
      )}

      {/* New Scheduled Collection Time */}
      <div>
        <label htmlFor="replacement-scheduled-time" className="block text-xs font-semibold text-slate-700">
          New scheduled collection time <span className="text-rose-600">*</span>
        </label>
        <input
          id="replacement-scheduled-time"
          aria-label="New scheduled collection time"
          type="datetime-local"
          value={scheduledLocal}
          disabled={isSubmitting}
          onChange={(e) => {
            setScheduledLocal(e.target.value);
            if (errors.scheduledAt) setErrors((prev) => ({ ...prev, scheduledAt: '' }));
          }}
          className="mt-1.5 block w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs text-slate-900 transition-colors focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-600/20 disabled:bg-slate-100 disabled:opacity-60"
        />
        <div className="mt-1 flex items-center justify-between text-[11px]">
          <span className="text-slate-500">Asia/Colombo local time; converted to UTC instant for submission.</span>
          {errors.scheduledAt && <span className="font-medium text-rose-600">{errors.scheduledAt}</span>}
        </div>
      </div>

      {/* Replacement Reason */}
      <div>
        <label htmlFor="replacement-reason" className="block text-xs font-semibold text-slate-700">
          Replacement reason <span className="text-rose-600">*</span>
        </label>
        <textarea
          id="replacement-reason"
          aria-label="Replacement reason"
          rows={3}
          maxLength={500}
          value={replacementReason}
          disabled={isSubmitting}
          placeholder="Enter mandatory justification for replacement (5–500 characters)..."
          onChange={(e) => {
            setReplacementReason(e.target.value);
            if (errors.replacementReason) setErrors((prev) => ({ ...prev, replacementReason: '' }));
          }}
          className="mt-1.5 block w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs text-slate-900 placeholder:text-slate-400 transition-colors focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-600/20 disabled:bg-slate-100 disabled:opacity-60"
        />
        <div className="mt-1 flex items-center justify-between text-[11px]">
          {errors.replacementReason ? (
            <span className="font-medium text-rose-600">{errors.replacementReason}</span>
          ) : (
            <span className="text-slate-400">Must be between 5 and 500 characters</span>
          )}
          <span className="text-slate-400">{replacementReason.length} / 500</span>
        </div>
      </div>

      {/* Handling Notes (Optional) */}
      <div>
        <label htmlFor="replacement-handling-notes" className="block text-xs font-semibold text-slate-700">
          Handling notes <span className="text-slate-400 font-normal">(optional)</span>
        </label>
        <textarea
          id="replacement-handling-notes"
          aria-label="Handling notes"
          rows={2}
          maxLength={1000}
          value={handlingNotes}
          disabled={isSubmitting}
          placeholder="Special handling instructions for the replacement crew (defaults to original notes if blank)..."
          onChange={(e) => {
            setHandlingNotes(e.target.value);
            if (errors.handlingNotes) setErrors((prev) => ({ ...prev, handlingNotes: '' }));
          }}
          className="mt-1.5 block w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs text-slate-900 placeholder:text-slate-400 transition-colors focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-600/20 disabled:bg-slate-100 disabled:opacity-60"
        />
        <div className="mt-1 flex items-center justify-between text-[11px]">
          {errors.handlingNotes ? (
            <span className="font-medium text-rose-600">{errors.handlingNotes}</span>
          ) : (
            <span className="text-slate-400">Maximum 1000 characters</span>
          )}
          <span className="text-slate-400">{handlingNotes.length} / 1000</span>
        </div>
      </div>

      {/* Scheduling Justification (Optional) */}
      <div>
        <label htmlFor="replacement-scheduling-reason" className="block text-xs font-semibold text-slate-700">
          Scheduling justification <span className="text-slate-400 font-normal">(optional)</span>
        </label>
        <textarea
          id="replacement-scheduling-reason"
          aria-label="Scheduling justification"
          rows={2}
          maxLength={500}
          value={schedulingReason}
          disabled={isSubmitting}
          placeholder="Operational justification for scheduling (defaults to original reason if blank)..."
          onChange={(e) => {
            setSchedulingReason(e.target.value);
            if (errors.schedulingReason) setErrors((prev) => ({ ...prev, schedulingReason: '' }));
          }}
          className="mt-1.5 block w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs text-slate-900 placeholder:text-slate-400 transition-colors focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-600/20 disabled:bg-slate-100 disabled:opacity-60"
        />
        <div className="mt-1 flex items-center justify-between text-[11px]">
          {errors.schedulingReason ? (
            <span className="font-medium text-rose-600">{errors.schedulingReason}</span>
          ) : (
            <span className="text-slate-400">Maximum 500 characters</span>
          )}
          <span className="text-slate-400">{schedulingReason.length} / 500</span>
        </div>
      </div>

      {/* Action Buttons */}
      <div className="flex flex-col-reverse gap-2 pt-2 sm:flex-row sm:justify-end">
        <Button
          type="button"
          variant="secondary"
          disabled={isSubmitting}
          onClick={onCancel}
        >
          Cancel
        </Button>
        <Button
          type="submit"
          isLoading={isSubmitting}
          disabled={isSubmitting}
        >
          Schedule replacement task
        </Button>
      </div>
    </form>
  );
};
