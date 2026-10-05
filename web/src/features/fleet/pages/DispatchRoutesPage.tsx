import React, { useState } from 'react';
import { Alert } from '../../../components/ui/Alert';
import { Button } from '../../../components/ui/Button';
import { Card } from '../../../components/ui/Card';
import { LoadingSpinner } from '../../../components/ui/LoadingSpinner';
import { AssignmentsSection } from '../components/AssignmentsSection';
import { CreateAssignmentPanel } from '../components/CreateAssignmentPanel';
import { useAvailableAssignmentTasks } from '../hooks/useAvailableAssignmentTasks';

const pageSize = 20;

const formatDateTime = (value: string) => {
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

const reasonLabels = {
  VerifiedReport: 'Verified report',
  FullOrBlockedBin: 'Full or blocked bin',
  RoutineCollection: 'Routine collection',
  OfficerDiscretion: 'Officer discretion',
} as const;

const sourceBadge = (targetType: 'Report' | 'Bin') =>
  targetType === 'Report'
    ? 'bg-blue-50 text-blue-800 border-blue-200/80'
    : 'bg-emerald-50 text-emerald-800 border-emerald-200/80';

export const DispatchRoutesPage: React.FC = () => {
  const [section, setSection] = useState<'tasks' | 'assignments'>('tasks');
  const [tasksPage, setTasksPage] = useState(1);
  const [creating, setCreating] = useState(false);
  const [createdAssignmentId, setCreatedAssignmentId] = useState<string | null>(null);
  const [createdAssignmentReference, setCreatedAssignmentReference] = useState<string | null>(null);
  const tasks = useAvailableAssignmentTasks({ page: tasksPage, pageSize });
  const taskList = tasks.data;

  return (
    <div className="space-y-6">
      <header className="border-b border-slate-200/80 pb-5">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <div className="mb-1 flex items-center gap-1.5 text-xs font-medium text-slate-400">
              <span>Operations</span>
              <span>/</span>
              <span className="font-semibold text-slate-600">Dispatch & Routes</span>
            </div>
            <h1 className="text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl">Dispatch & Routes</h1>
            <p className="mt-1 max-w-3xl text-sm leading-relaxed text-slate-500">
              Review Scheduled collection work awaiting dispatch and monitor Driver and Vehicle allocations.
            </p>
          </div>
          {!creating && (
            <Button
              className="self-start sm:self-auto"
              onClick={() => {
                setCreatedAssignmentId(null);
                setCreatedAssignmentReference(null);
                setCreating(true);
              }}
            >
              Create assignment
            </Button>
          )}
        </div>
      </header>

      {creating && (
        <CreateAssignmentPanel
          onCancel={() => setCreating(false)}
          onCreated={(assignmentId, assignmentReference) => {
            setCreatedAssignmentId(assignmentId);
            setCreatedAssignmentReference(assignmentReference ?? null);
            setSection('assignments');
            setCreating(false);
          }}
        />
      )}

      {!creating && (
        <nav className="flex flex-wrap gap-1 rounded-xl border border-slate-200 bg-white p-1.5 shadow-sm" aria-label="Dispatch sections">
          <Button
            size="sm"
            variant={section === 'tasks' ? 'primary' : 'ghost'}
            className="rounded-lg"
            aria-current={section === 'tasks' ? 'page' : undefined}
            onClick={() => setSection('tasks')}
          >
            Available Tasks
          </Button>
          <Button
            size="sm"
            variant={section === 'assignments' ? 'primary' : 'ghost'}
            className="rounded-lg"
            aria-current={section === 'assignments' ? 'page' : undefined}
            onClick={() => setSection('assignments')}
          >
            Assignments
          </Button>
        </nav>
      )}

      {!creating && section === 'tasks' && (
        <>
          <Card className="border-emerald-100 bg-emerald-50/40 p-4 sm:p-5">
            <h2 className="text-sm font-bold text-slate-900">Available scheduled tasks</h2>
            <p className="mt-1 text-sm text-slate-600">
              These are existing C2 tasks in Scheduled status with no active assignment claim. Select only the work you
              intend to dispatch.
            </p>
          </Card>
          <Card className="overflow-hidden p-0">
            {tasks.isLoading ? (
              <div className="flex flex-col items-center gap-3 py-16 text-slate-500">
                <LoadingSpinner size="lg" />
                <p className="text-sm font-medium">Loading available tasks…</p>
              </div>
            ) : tasks.isError ? (
              <Alert variant="error" title="Unable to load available tasks" className="m-4 sm:m-5">
                Please retry the request.{' '}
                <Button size="sm" variant="secondary" className="ml-3" onClick={() => tasks.refetch()}>
                  Retry
                </Button>
              </Alert>
            ) : !taskList?.items.length ? (
              <div className="py-16 text-center">
                <p className="text-base font-semibold text-slate-800">No tasks are awaiting dispatch</p>
                <p className="mt-1 text-xs text-slate-500">
                  Scheduled collection tasks without an active assignment will appear here.
                </p>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="min-w-[900px] w-full text-left text-xs" aria-label="Available collection tasks">
                  <thead>
                    <tr className="border-b border-slate-200 bg-slate-50/80 font-semibold uppercase tracking-wider text-slate-500">
                      <th className="px-4 py-3.5">Task</th>
                      <th className="px-4 py-3.5">Target</th>
                      <th className="px-4 py-3.5">Location</th>
                      <th className="px-4 py-3.5">Reason</th>
                      <th className="px-4 py-3.5">Scheduled (Colombo)</th>
                      <th className="px-4 py-3.5">Status</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {taskList.items.map((task) => (
                      <tr key={task.id} className="transition-colors hover:bg-slate-50/70">
                        <td className="px-4 py-3.5 font-semibold text-slate-900">{task.taskCode}</td>
                        <td className="px-4 py-3.5">
                          <span
                            className={`inline-flex rounded-full border px-2.5 py-0.5 text-[11px] font-semibold ${sourceBadge(
                              task.targetType
                            )}`}
                          >
                            {task.targetType === 'Report' ? 'Waste report' : 'Waste bin'}
                          </span>
                        </td>
                        <td className="max-w-64 px-4 py-3.5 text-slate-700">
                          {task.addressText || 'Location not recorded'}
                        </td>
                        <td className="px-4 py-3.5 text-slate-700">{reasonLabels[task.collectionReason]}</td>
                        <td className="whitespace-nowrap px-4 py-3.5 text-slate-700">
                          {formatDateTime(task.scheduledAt)}
                        </td>
                        <td className="px-4 py-3.5">
                          <span className="inline-flex rounded-full border border-blue-200/80 bg-blue-50 px-2.5 py-0.5 text-[11px] font-semibold text-blue-800">
                            {task.status}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            {taskList && taskList.totalCount > 0 && (
              <Pagination
                page={taskList.page}
                totalPages={taskList.totalPages}
                totalCount={taskList.totalCount}
                pageSize={taskList.pageSize}
                isFetching={tasks.isFetching}
                noun="tasks"
                onPrevious={() => setTasksPage((current) => current - 1)}
                onNext={() => setTasksPage((current) => current + 1)}
              />
            )}
          </Card>
        </>
      )}

      {!creating && section === 'assignments' && (
        <AssignmentsSection role="WasteOfficer" createdAssignmentId={createdAssignmentId} createdAssignmentReference={createdAssignmentReference} />
      )}
    </div>
  );
};

const Pagination: React.FC<{
  page: number;
  totalPages: number;
  totalCount: number;
  pageSize: number;
  isFetching: boolean;
  noun: string;
  onPrevious: () => void;
  onNext: () => void;
}> = ({ page, totalPages, totalCount, pageSize, isFetching, noun, onPrevious, onNext }) => (
  <div className="flex flex-col justify-between gap-3 border-t border-slate-100 p-4 text-xs text-slate-500 sm:flex-row sm:items-center">
    <span>
      Showing {Math.min((page - 1) * pageSize + 1, totalCount)}–{Math.min(page * pageSize, totalCount)} of {totalCount}{' '}
      {noun}
    </span>
    <div className="flex items-center gap-2">
      <Button size="sm" variant="secondary" disabled={page <= 1 || isFetching} onClick={onPrevious}>
        Previous
      </Button>
      <span className="px-1 font-semibold text-slate-700">
        Page {page} of {Math.max(1, totalPages)}
      </span>
      <Button size="sm" variant="secondary" disabled={page >= totalPages || isFetching} onClick={onNext}>
        Next
      </Button>
    </div>
  </div>
);
