import React, { useMemo, useState } from 'react';
import { Alert } from '../../../components/ui/Alert';
import { Button } from '../../../components/ui/Button';
import { Card } from '../../../components/ui/Card';
import { LoadingSpinner } from '../../../components/ui/LoadingSpinner';
import { AssignmentCancelDialog } from './AssignmentCancelDialog';
import { AssignmentStatusBadge } from './AssignmentStatusBadge';
import { CollectionStopsMap, isValidCollectionStopLocation } from './CollectionStopsMap';
import { RouteStopsEditor } from './RouteStopsEditor';
import { useAssignmentDetail } from '../hooks/useAssignments';
import type {
  AssignmentDetailDto,
  CollectionMapStop,
  RouteStopReadDto,
  RouteStopStatus,
} from '../types/assignments';

interface AssignmentDetailPanelProps {
  assignmentId: string;
  role: 'WasteOfficer' | 'MunicipalManager';
  onClose: () => void;
  onAssignmentCancelled?: () => void;
}

const formatDateTime = (value: string | null | undefined) => {
  if (!value) return '—';
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

const stopStatusTone = (status: RouteStopStatus) => {
  switch (status) {
    case 'Completed':
      return 'bg-emerald-50 text-emerald-800 border-emerald-200';
    case 'Failed':
      return 'bg-rose-50 text-rose-800 border-rose-200';
    default:
      return 'bg-amber-50 text-amber-800 border-amber-200';
  }
};

const taskSourceBadge = (targetType: 'Report' | 'Bin') =>
  targetType === 'Report'
    ? 'bg-blue-50 text-blue-800 border-blue-200/80'
    : 'bg-emerald-50 text-emerald-800 border-emerald-200/80';

export const AssignmentDetailPanel: React.FC<AssignmentDetailPanelProps> = ({
  assignmentId,
  role,
  onClose,
  onAssignmentCancelled,
}) => {
  const { data: assignment, isLoading, isError, error, refetch, isFetching } = useAssignmentDetail(assignmentId);
  const [selectedStopId, setSelectedStopId] = useState<string | null>(null);
  const [isReordering, setIsReordering] = useState(false);
  const [isCancelDialogOpen, setIsCancelDialogOpen] = useState(false);
  const [previewStops, setPreviewStops] = useState<RouteStopReadDto[] | null>(null);
  const [actionSuccessMessage, setActionSuccessMessage] = useState<string | null>(null);

  // Determine current active stops (either preview order during editing, or authoritative saved order)
  const currentStops = useMemo<RouteStopReadDto[]>(() => {
    if (previewStops) return previewStops;
    if (!assignment?.route?.stops) return [];
    return [...assignment.route.stops].sort((a, b) => a.sequence - b.sequence);
  }, [previewStops, assignment?.route?.stops]);

  // Convert to map stops presentation model
  const mapStops = useMemo<CollectionMapStop[]>(() => {
    return currentStops.map((stop) => ({
      id: stop.id,
      sequence: stop.sequence,
      label: stop.task.taskCode,
      targetType: stop.task.targetType,
      addressText: stop.task.addressText,
      latitude: stop.task.latitude,
      longitude: stop.task.longitude,
      status: stop.status,
    }));
  }, [currentStops]);

  const missingCoordinatesCount = useMemo(
    () => currentStops.filter((stop) => !isValidCollectionStopLocation(stop.task.latitude, stop.task.longitude)).length,
    [currentStops]
  );

  if (isLoading) {
    return (
      <Card className="p-8 text-center">
        <div className="flex flex-col items-center justify-center gap-3 py-12 text-slate-500">
          <LoadingSpinner size="lg" />
          <p className="text-sm font-medium">Loading collection assignment details…</p>
        </div>
      </Card>
    );
  }

  if (isError || !assignment) {
    return (
      <Card className="p-6">
        <Alert variant="error" title="Unable to load assignment">
          {error instanceof Error ? error.message : 'The requested assignment could not be loaded.'}
        </Alert>
        <div className="mt-4 flex gap-2">
          <Button variant="secondary" onClick={onClose}>
            Back to list
          </Button>
          <Button onClick={() => refetch()}>Retry</Button>
        </div>
      </Card>
    );
  }

  const isUnstarted = assignment.status === 'Assigned';
  const canReorder = role === 'WasteOfficer' && isUnstarted;
  const canCancel = (role === 'WasteOfficer' || role === 'MunicipalManager') && isUnstarted;

  const handleSavedReorder = () => {
    setIsReordering(false);
    setPreviewStops(null);
    setActionSuccessMessage('Route stops reordered successfully.');
    refetch();
  };

  const handleDiscardReorder = () => {
    setIsReordering(false);
    setPreviewStops(null);
  };

  const handleCancelled = (_cancelledAssignment: AssignmentDetailDto) => {
    setActionSuccessMessage('Assignment cancelled successfully. Linked tasks returned to Scheduled.');
    refetch();
    onAssignmentCancelled?.();
  };

  return (
    <div className="space-y-6" data-testid="assignment-detail-panel">
      {/* Top action navigation */}
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200/80 pb-4">
        <div className="flex items-center gap-2">
          <Button size="sm" variant="secondary" onClick={onClose} aria-label="Back to assignments list">
            ← Back to assignments
          </Button>
          <Button
            size="sm"
            variant="ghost"
            onClick={() => refetch()}
            disabled={isFetching}
            aria-label="Refresh assignment details"
          >
            {isFetching ? 'Refreshing…' : '↻ Refresh'}
          </Button>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {canReorder && !isReordering && (
            <Button size="sm" variant="secondary" onClick={() => setIsReordering(true)}>
              Reorder route
            </Button>
          )}

          {canCancel && !isReordering && (
            <Button size="sm" variant="danger" onClick={() => setIsCancelDialogOpen(true)}>
              Cancel assignment
            </Button>
          )}
        </div>
      </div>

      {actionSuccessMessage && (
        <Alert variant="success" title="Success">
          {actionSuccessMessage}
        </Alert>
      )}

      {/* Assignment Overview Card */}
      <Card className="p-5 sm:p-6">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between border-b border-slate-100 pb-5">
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-mono text-xs font-bold text-slate-800 bg-slate-100 px-2 py-0.5 rounded border border-slate-200">
                {assignment.assignmentReference ?? assignment.id}
              </span>
              <AssignmentStatusBadge status={assignment.status} />
              {assignment.route && (
                <span className="rounded border border-slate-200 bg-slate-50 px-2 py-0.5 text-[11px] font-medium text-slate-600">
                  Method: {assignment.route.routingMethod}
                </span>
              )}
            </div>
            <h2 className="mt-2 text-xl font-bold tracking-tight text-slate-900 sm:text-2xl">
              Assignment Details & Route
            </h2>
            <p className="mt-1 text-xs text-slate-500">
              Assigned on <span className="font-medium text-slate-700">{formatDateTime(assignment.assignedAt)}</span>{' '}
              (Asia/Colombo)
            </p>
          </div>
        </div>

        {/* Operational Metrics */}
        <dl className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-4">
          <div className="rounded-lg border border-slate-100 bg-slate-50/70 p-3">
            <dt className="text-xs font-medium text-slate-500">Driver</dt>
            <dd className="mt-1 text-sm font-semibold text-slate-900">{assignment.driverName || 'Not assigned'}</dd>
          </div>

          <div className="rounded-lg border border-slate-100 bg-slate-50/70 p-3">
            <dt className="text-xs font-medium text-slate-500">Vehicle</dt>
            <dd className="mt-1 text-sm font-semibold text-slate-900">
              {assignment.vehicleRegistrationNumber || 'Not assigned'}
            </dd>
          </div>

          <div className="rounded-lg border border-slate-100 bg-slate-50/70 p-3">
            <dt className="text-xs font-medium text-slate-500">Total stops</dt>
            <dd className="mt-1 text-sm font-semibold text-slate-900">{assignment.stopCount}</dd>
          </div>

          <div className="rounded-lg border border-slate-100 bg-slate-50/70 p-3">
            <dt className="text-xs font-medium text-slate-500">Outcome summary</dt>
            <dd className="mt-1 flex flex-wrap gap-2 text-xs font-medium">
              <span className="text-emerald-700">{assignment.completedStopCount} completed</span>
              <span className="text-slate-300">·</span>
              <span className="text-rose-700">{assignment.failedStopCount} failed</span>
            </dd>
          </div>
        </dl>
      </Card>

      {/* Reorder Mode Editor (Officer only) */}
      {isReordering && (
        <RouteStopsEditor
          assignmentId={assignment.id}
          initialStops={currentStops}
          onSaved={handleSavedReorder}
          onDiscard={handleDiscardReorder}
          onOrderPreviewChange={(newOrder) => setPreviewStops(newOrder)}
        />
      )}

      {/* Route Map & Ordered Stops Grid */}
      <div className="grid gap-6 lg:grid-cols-12">
        {/* Map Preview */}
        <div className="lg:col-span-6 space-y-2">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-bold text-slate-900">Collection route map</h3>
            {missingCoordinatesCount > 0 && (
              <span className="text-xs text-amber-700 font-medium">
                {missingCoordinatesCount} {missingCoordinatesCount === 1 ? 'stop lacks' : 'stops lack'} coordinates
              </span>
            )}
          </div>
          <CollectionStopsMap
            stops={mapStops}
            selectedStopId={selectedStopId}
            onStopSelect={(stopId) => setSelectedStopId(stopId)}
            showSequenceLine
          />
        </div>

        {/* Ordered Stops List */}
        <div className="lg:col-span-6 space-y-3">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-bold text-slate-900">
              Planned stops ({currentStops.length})
            </h3>
            <span className="text-xs text-slate-500">Authoritative sequence</span>
          </div>

          {currentStops.length === 0 ? (
            <Card className="p-8 text-center text-xs text-slate-500">
              No stops are recorded for this assignment route.
            </Card>
          ) : (
            <div className="space-y-2 max-h-[460px] overflow-y-auto pr-1">
              {currentStops.map((stop) => {
                const isSelected = stop.id === selectedStopId;
                const hasValidCoords = isValidCollectionStopLocation(stop.task.latitude, stop.task.longitude);

                return (
                  <div
                    key={stop.id}
                    onClick={() => setSelectedStopId(stop.id)}
                    className={`cursor-pointer rounded-xl border p-3.5 transition-all text-xs ${
                      isSelected
                        ? 'border-emerald-600 bg-emerald-50/50 shadow-sm ring-2 ring-emerald-600/20'
                        : 'border-slate-200 bg-white hover:border-slate-300'
                    }`}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="flex items-center gap-2">
                        <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-emerald-600 text-xs font-bold text-white">
                          {stop.sequence}
                        </span>
                        <div>
                          <div className="flex flex-wrap items-center gap-1.5">
                            <span className="font-semibold text-slate-900">{stop.task.taskCode}</span>
                            <span
                              className={`rounded-full border px-2 py-0.2 text-[10px] font-semibold ${taskSourceBadge(
                                stop.task.targetType
                              )}`}
                            >
                              {stop.task.targetType === 'Report' ? 'Waste Report' : 'Waste Bin'}
                            </span>
                            <span
                              className={`rounded-full border px-2 py-0.2 text-[10px] font-medium ${stopStatusTone(
                                stop.status
                              )}`}
                            >
                              Stop: {stop.status}
                            </span>
                            <span className="text-[10px] text-slate-400">
                              (Task: {stop.task.status})
                            </span>
                          </div>
                        </div>
                      </div>

                      <div className="text-right text-[11px] text-slate-400 whitespace-nowrap">
                        {stop.completedAt && (
                          <span className="text-emerald-700 font-medium">
                            Done {formatDateTime(stop.completedAt)}
                          </span>
                        )}
                        {stop.failedAt && (
                          <span className="text-rose-700 font-medium">
                            Failed {formatDateTime(stop.failedAt)}
                          </span>
                        )}
                      </div>
                    </div>

                    <p className="mt-2 text-slate-600">
                      {stop.task.addressText || 'Location text not recorded'}
                    </p>

                    {!hasValidCoords && (
                      <p className="mt-1 text-[11px] font-medium text-amber-700">
                        ⚠ Coordinates unavailable for map pin
                      </p>
                    )}

                    {stop.failureReason && (
                      <div className="mt-2 rounded bg-rose-50 p-2 border border-rose-200 text-rose-800 text-[11px]">
                        <span className="font-semibold">Failure reason:</span> {stop.failureReason}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </div>
      </div>

      {/* Assignment Status Transition History */}
      {assignment.history && assignment.history.length > 0 && (
        <Card className="p-5 sm:p-6">
          <h3 className="text-sm font-bold text-slate-900 border-b border-slate-100 pb-3">
            Assignment Audit History ({assignment.history.length})
          </h3>
          <div className="mt-4 flow-root">
            <ul className="-mb-8">
              {assignment.history.map((record, recordIdx) => {
                const isLast = recordIdx === assignment.history.length - 1;

                return (
                  <li key={record.id}>
                    <div className="relative pb-8">
                      {!isLast && (
                        <span
                          className="absolute top-4 left-4 -ml-px h-full w-0.5 bg-slate-200"
                          aria-hidden="true"
                        />
                      )}
                      <div className="relative flex space-x-3">
                        <div>
                          <span className="flex h-8 w-8 items-center justify-center rounded-full bg-slate-100 text-slate-600 ring-8 ring-white text-xs font-semibold">
                            ●
                          </span>
                        </div>
                        <div className="flex min-w-0 flex-1 justify-between space-x-4 pt-1.5 text-xs">
                          <div>
                            <p className="text-slate-800 font-medium">
                              Transitioned to{' '}
                              <span className="font-bold text-slate-900">{record.toStatus}</span>
                              {record.fromStatus && (
                                <span className="text-slate-400"> (from {record.fromStatus})</span>
                              )}
                            </p>
                            {record.notes && (
                              <p className="mt-1 text-slate-600 bg-slate-50 p-2 rounded border border-slate-100">
                                {record.notes}
                              </p>
                            )}
                          </div>
                          <div className="whitespace-nowrap text-right text-[11px] text-slate-400">
                            {formatDateTime(record.changedAt)}
                          </div>
                        </div>
                      </div>
                    </div>
                  </li>
                );
              })}
            </ul>
          </div>
        </Card>
      )}

      {/* Cancellation Dialog */}
      <AssignmentCancelDialog
        assignment={assignment}
        isOpen={isCancelDialogOpen}
        onClose={() => setIsCancelDialogOpen(false)}
        onSuccess={handleCancelled}
      />
    </div>
  );
};
