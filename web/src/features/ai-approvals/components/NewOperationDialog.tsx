import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { Button } from '../../../components/ui/Button';

interface NewOperationDialogProps {
  isOpen: boolean;
  isSubmitting: boolean;
  errorMessage: string | null;
  createdWorkflowId: string | null;
  workflowBasePath: string;
  onClose: () => void;
  onSubmit: (objective: string) => void;
}

const defaultObjective = 'Prepare an end-to-end waste collection operation for the current verified waste needs, including collection planning, fleet dispatch planning, validation, and human-approved execution.';

export const NewOperationDialog: React.FC<NewOperationDialogProps> = ({
  isOpen,
  isSubmitting,
  errorMessage,
  createdWorkflowId,
  workflowBasePath,
  onClose,
  onSubmit,
}) => {
  const [objective, setObjective] = useState(defaultObjective);
  const [validationError, setValidationError] = useState<string | null>(null);

  if (!isOpen) return null;

  const close = () => {
    setValidationError(null);
    onClose();
  };

  const submit = (event: React.FormEvent) => {
    event.preventDefault();
    const trimmed = objective.trim();
    if (!trimmed) {
      setValidationError('Objective is required.');
      return;
    }
    if (trimmed.length < 5) {
      setValidationError('Objective must be at least 5 characters.');
      return;
    }
    if (trimmed.length > 1000) {
      setValidationError('Objective cannot exceed 1000 characters.');
      return;
    }
    setValidationError(null);
    onSubmit(trimmed);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4" role="dialog" aria-modal="true" aria-labelledby="new-operation-title">
      <div className="w-full max-w-2xl rounded-xl border border-slate-200 bg-white p-5 shadow-xl sm:p-6">
        <div className="mb-5">
          <h2 id="new-operation-title" className="text-lg font-bold text-slate-900">New End-to-End Collection Operation</h2>
          <p className="mt-1 text-sm text-slate-500">Define the operational objective. ASP.NET will persist and start the Agentic workflow.</p>
        </div>

        <form onSubmit={submit} className="space-y-4">
          <label className="block text-sm font-semibold text-slate-800" htmlFor="workflow-objective">
            Objective
          </label>
          <textarea
            id="workflow-objective"
            value={objective}
            onChange={(event) => setObjective(event.target.value)}
            disabled={isSubmitting}
            maxLength={1000}
            rows={6}
            className="w-full resize-y rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-emerald-500 focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:bg-slate-100"
            aria-describedby="workflow-objective-help"
          />
          <div id="workflow-objective-help" className="flex justify-between text-xs text-slate-500">
            <span>Describe the collection operation to prepare.</span>
            <span>{objective.length}/1000</span>
          </div>

          {(validationError || errorMessage) && (
            <div role="alert" className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-800">
              {validationError || errorMessage}
              {createdWorkflowId && (
                <Link to={`${workflowBasePath}/${createdWorkflowId}`} className="ml-1 font-semibold underline">
                  Open the created workflow.
                </Link>
              )}
            </div>
          )}

          {isSubmitting && (
            <p role="status" className="rounded-lg border border-emerald-100 bg-emerald-50 px-3 py-2 text-sm text-emerald-800">
              Starting Agentic workflow. Analyzing verified waste needs and preparing the collection plan…
            </p>
          )}

          <div className="flex flex-col-reverse gap-2 pt-2 sm:flex-row sm:justify-end">
            <Button type="button" variant="secondary" onClick={close} disabled={isSubmitting}>Cancel</Button>
            <Button type="submit" disabled={isSubmitting} isLoading={isSubmitting}>
              {isSubmitting ? 'Starting Operation...' : 'Start Operation'}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
};
