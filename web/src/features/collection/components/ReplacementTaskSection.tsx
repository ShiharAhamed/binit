import axios from 'axios';
import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert } from '../../../components/ui/Alert';
import { Button } from '../../../components/ui/Button';
import { Card } from '../../../components/ui/Card';
import { ReplacementTaskForm } from './ReplacementTaskForm';
import { useCreateReplacementTask } from '../hooks/useCreateReplacementTask';
import type {
  CollectionTaskDetailDto,
  CollectionTaskStatusHistoryDto,
  CreateReplacementCollectionTaskRequest,
} from '../types/collectionTasks';

interface ReplacementTaskSectionProps {
  task: CollectionTaskDetailDto;
  statusHistory?: CollectionTaskStatusHistoryDto[];
  userRole?: string;
}

const formatDateTime = (value: string | null | undefined) => {
  if (!value) return 'Not recorded';
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? value
    : new Intl.DateTimeFormat('en-GB', {
        timeZone: 'Asia/Colombo',
        day: 'numeric',
        month: 'short',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        hour12: false,
      }).format(date);
};

export const ReplacementTaskSection: React.FC<ReplacementTaskSectionProps> = ({
  task,
  statusHistory,
  userRole,
}) => {
  const navigate = useNavigate();
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [createdTask, setCreatedTask] = useState<CollectionTaskDetailDto | null>(null);
  const [apiError, setApiError] = useState<string | null>(null);

  const mutation = useCreateReplacementTask(task.id);
  const canReplace = userRole === 'WasteOfficer' && task.status === 'Failed';

  // Find recorded failure event from status transition audit trail
  const failedEvent = statusHistory?.find((h) => h.toStatus === 'Failed');
  const failureReason = failedEvent?.notes?.trim();

  const handleFormSubmit = async (payload: CreateReplacementCollectionTaskRequest) => {
    setApiError(null);
    try {
      const result = await mutation.mutateAsync(payload);
      setCreatedTask(result);
      setIsFormOpen(false);
    } catch (err) {
      if (axios.isAxiosError(err)) {
        setApiError(
          err.response?.data?.detail ||
          err.response?.data?.title ||
          'Unable to schedule replacement task.'
        );
      } else {
        setApiError('Unable to schedule replacement task.');
      }
    }
  };

  return (
    <Card className="p-5 sm:p-6" data-testid="replacement-task-section">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between border-b border-slate-100 pb-4">
        <div>
          <div className="flex items-center gap-2">
            <span className="rounded-full bg-red-100 px-2.5 py-0.5 text-[11px] font-semibold text-red-800 border border-red-200">
              Terminal failure
            </span>
            <span className="text-xs text-slate-500 font-mono">{task.taskCode}</span>
          </div>
          <h2 className="mt-1.5 text-lg font-bold text-slate-900">Failed Task Review & Replacement</h2>
          <p className="mt-0.5 text-xs text-slate-500">
            Review previous failure reason and dispatch a replacement collection task for this target.
          </p>
        </div>

        {canReplace && !isFormOpen && !createdTask && (
          <Button
            size="sm"
            onClick={() => {
              setApiError(null);
              setIsFormOpen(true);
            }}
          >
            Schedule replacement
          </Button>
        )}
      </div>

      {/* Failure Context Summary */}
      <div className="mt-4 rounded-xl border border-red-100 bg-red-50/50 p-4">
        <p className="text-xs font-semibold uppercase tracking-wider text-red-800">Failure details</p>
        <div className="mt-2 grid gap-3 text-xs sm:grid-cols-2">
          <div>
            <span className="text-slate-500 font-medium">Recorded failure reason:</span>
            <p className="mt-0.5 text-slate-800 font-medium whitespace-pre-wrap">
              {failureReason || 'No specific driver notes recorded for failure.'}
            </p>
          </div>
          <div>
            <span className="text-slate-500 font-medium">Recorded at (Colombo):</span>
            <p className="mt-0.5 text-slate-800 font-medium">
              {formatDateTime(failedEvent?.changedAt || task.updatedAt)}
              {failedEvent?.changedByUserName ? ` by ${failedEvent.changedByUserName}` : ''}
            </p>
          </div>
        </div>

        <p className="mt-3 border-t border-red-100/80 pt-2.5 text-[11px] text-slate-500">
          The original Failed task and its history will remain permanently recorded. A distinct new Scheduled task is
          created for subsequent dispatch.
        </p>
      </div>

      {/* Success Notification */}
      {createdTask && (
        <div className="mt-5 space-y-3">
          <Alert variant="success" title="Replacement task scheduled successfully">
            New collection task <span className="font-mono font-bold">{createdTask.taskCode}</span> has been created in{' '}
            <span className="font-semibold text-emerald-800">Scheduled</span> status. Original task{' '}
            <span className="font-mono font-semibold">{task.taskCode}</span> remains preserved in Failed status.
          </Alert>
          <div className="flex flex-wrap gap-2 pt-1">
            <Button size="sm" onClick={() => navigate(`/officer/tasks/${createdTask.id}`)}>
              View replacement task
            </Button>
            <Button size="sm" variant="secondary" onClick={() => navigate('/officer/tasks')}>
              Back to Collection Tasks
            </Button>
          </div>
        </div>
      )}

      {/* Replacement Form */}
      {isFormOpen && (
        <div className="mt-5 rounded-xl border border-emerald-200 bg-emerald-50/20 p-4 sm:p-5">
          <h3 className="mb-3 text-sm font-bold text-slate-900 border-b border-emerald-100 pb-2">
            Schedule new replacement task
          </h3>
          <ReplacementTaskForm
            failedTask={task}
            isSubmitting={mutation.isPending}
            apiError={apiError}
            onSubmit={handleFormSubmit}
            onCancel={() => {
              setApiError(null);
              setIsFormOpen(false);
            }}
          />
        </div>
      )}

      {/* Unauthorized Notice for Non-Officers */}
      {!canReplace && userRole !== 'WasteOfficer' && (
        <p className="mt-4 text-xs text-slate-400 italic">
          Only Waste Officers are authorized to schedule replacements for failed collection tasks.
        </p>
      )}
    </Card>
  );
};
