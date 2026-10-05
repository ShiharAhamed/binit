import React, { useState } from 'react';
import { Alert } from '../../../components/ui/Alert';
import { Button } from '../../../components/ui/Button';
import { Card } from '../../../components/ui/Card';
import { LoadingSpinner } from '../../../components/ui/LoadingSpinner';
import { AssignmentDetailPanel } from './AssignmentDetailPanel';
import { AssignmentStatusBadge } from './AssignmentStatusBadge';
import { useAssignments } from '../hooks/useAssignments';
import type { CollectionAssignmentStatus } from '../types/assignments';

interface AssignmentsSectionProps {
  role: 'WasteOfficer' | 'MunicipalManager';
  createdAssignmentId?: string | null;
  createdAssignmentReference?: string | null;
}

const assignmentStatuses: CollectionAssignmentStatus[] = [
  'Assigned',
  'InProgress',
  'Completed',
  'PartiallyCompleted',
  'Failed',
  'Cancelled',
];

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

export const AssignmentsSection: React.FC<AssignmentsSectionProps> = ({ role, createdAssignmentId, createdAssignmentReference }) => {
  const [status, setStatus] = useState<CollectionAssignmentStatus | ''>('');
  const [page, setPage] = useState(1);
  const [selectedAssignmentId, setSelectedAssignmentId] = useState<string | null>(null);

  const assignments = useAssignments({ status: status || undefined, page, pageSize: 20 });
  const assignmentList = assignments.data;

  if (selectedAssignmentId) {
    return (
      <AssignmentDetailPanel
        assignmentId={selectedAssignmentId}
        role={role}
        onClose={() => setSelectedAssignmentId(null)}
        onAssignmentCancelled={() => {
          setPage(1);
        }}
      />
    );
  }

  return (
    <div className="space-y-5" data-testid="assignments-section">
      <div>
        <h2 className="text-lg font-bold text-slate-900">
          {role === 'MunicipalManager' ? 'Fleet Assignments & Routes' : 'Collection assignments'}
        </h2>
        <p className="mt-1 text-sm text-slate-500">
          {role === 'MunicipalManager'
            ? 'Executive oversight of municipal collection assignments, vehicle allocations, and route outcomes.'
            : 'Assignments record the Driver, Vehicle, saved stop count, and current collection outcome.'}
        </p>
      </div>

      {createdAssignmentId && (
        <Alert variant="success" title="Assignment created">
          <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
            <span>
              {createdAssignmentReference ?? assignmentList?.items.find((assignment) => assignment.id === createdAssignmentId)?.assignmentReference ?? 'New assignment'} is now listed. You can
              review its details or reorder its unstarted stops.
            </span>
            <Button
              size="sm"
              variant="secondary"
              className="self-start sm:self-auto shrink-0"
              onClick={() => setSelectedAssignmentId(createdAssignmentId)}
            >
              View details
            </Button>
          </div>
        </Alert>
      )}

      {/* Filter Card */}
      <Card className="p-4 sm:p-5">
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          <label className="text-xs font-semibold text-slate-700">
            Assignment status
            <select
              aria-label="Filter assignment status"
              value={status}
              onChange={(e) => {
                setStatus(e.target.value as CollectionAssignmentStatus | '');
                setPage(1);
              }}
              className="mt-1.5 w-full rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-900 transition-colors focus:border-emerald-600 focus:bg-white focus:outline-none focus:ring-2 focus:ring-emerald-600/20"
            >
              <option value="">All assignment statuses</option>
              {assignmentStatuses.map((st) => (
                <option key={st} value={st}>
                  {st}
                </option>
              ))}
            </select>
          </label>
        </div>
      </Card>

      {/* Assignments Table Card */}
      <Card className="overflow-hidden p-0">
        {assignments.isLoading ? (
          <div className="flex flex-col items-center gap-3 py-16 text-slate-500">
            <LoadingSpinner size="lg" />
            <p className="text-sm font-medium">Loading collection assignments…</p>
          </div>
        ) : assignments.isError ? (
          <Alert variant="error" title="Unable to load assignments" className="m-4 sm:m-5">
            Please retry the request.{' '}
            <Button size="sm" variant="secondary" className="ml-3" onClick={() => assignments.refetch()}>
              Retry
            </Button>
          </Alert>
        ) : !assignmentList?.items.length ? (
          <div className="py-16 text-center">
            <p className="text-base font-semibold text-slate-800">No collection assignments found</p>
            <p className="mt-1 text-xs text-slate-500">
              {status ? 'No assignments match the selected status filter.' : 'Work will appear here once dispatched.'}
            </p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-[880px] w-full text-left text-xs" aria-label="Collection assignments">
              <thead>
                <tr className="border-b border-slate-200 bg-slate-50/80 font-semibold uppercase tracking-wider text-slate-500">
                  <th className="px-4 py-3.5">Assignment</th>
                  <th className="px-4 py-3.5">Driver</th>
                  <th className="px-4 py-3.5">Vehicle</th>
                  <th className="px-4 py-3.5">Stops</th>
                  <th className="px-4 py-3.5">Status</th>
                  <th className="px-4 py-3.5">Assigned (Colombo)</th>
                  <th className="px-4 py-3.5 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {assignmentList.items.map((assignment) => (
                  <tr key={assignment.id} className="transition-colors hover:bg-slate-50/70">
                    <td className="px-4 py-3.5 font-semibold text-slate-800">{assignment.assignmentReference ?? assignment.id}</td>
                    <td className="px-4 py-3.5 font-semibold text-slate-900">
                      {assignment.driverName || 'Driver unavailable'}
                    </td>
                    <td className="px-4 py-3.5 text-slate-700">
                      {assignment.vehicleRegistrationNumber || 'Vehicle unavailable'}
                    </td>
                    <td className="px-4 py-3.5 text-slate-700">
                      {assignment.stopCount} total · {assignment.completedStopCount} done · {assignment.failedStopCount}{' '}
                      failed
                    </td>
                    <td className="px-4 py-3.5">
                      <AssignmentStatusBadge status={assignment.status} />
                    </td>
                    <td className="whitespace-nowrap px-4 py-3.5 text-slate-700">
                      {formatDateTime(assignment.assignedAt)}
                    </td>
                    <td className="px-4 py-3.5 text-right">
                      <Button size="sm" variant="secondary" onClick={() => setSelectedAssignmentId(assignment.id)}>
                        View details
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {assignmentList && assignmentList.totalCount > 0 && (
          <div className="flex flex-col justify-between gap-3 border-t border-slate-100 p-4 text-xs text-slate-500 sm:flex-row sm:items-center">
            <span>
              Showing {Math.min((assignmentList.page - 1) * assignmentList.pageSize + 1, assignmentList.totalCount)}–
              {Math.min(assignmentList.page * assignmentList.pageSize, assignmentList.totalCount)} of{' '}
              {assignmentList.totalCount} assignments
            </span>
            <div className="flex items-center gap-2">
              <Button
                size="sm"
                variant="secondary"
                disabled={assignmentList.page <= 1 || assignments.isFetching}
                onClick={() => setPage(page - 1)}
              >
                Previous
              </Button>
              <span className="px-1 font-semibold text-slate-700">
                Page {assignmentList.page} of {Math.max(1, assignmentList.totalPages)}
              </span>
              <Button
                size="sm"
                variant="secondary"
                disabled={assignmentList.page >= assignmentList.totalPages || assignments.isFetching}
                onClick={() => setPage(page + 1)}
              >
                Next
              </Button>
            </div>
          </div>
        )}
      </Card>
    </div>
  );
};
