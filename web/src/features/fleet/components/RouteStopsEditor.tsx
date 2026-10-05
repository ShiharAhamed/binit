import axios from 'axios';
import React, { useState } from 'react';
import { Alert } from '../../../components/ui/Alert';
import { Button } from '../../../components/ui/Button';
import { useReorderRouteStops } from '../hooks/useAssignments';
import type { RouteReadDto, RouteStopReadDto } from '../types/assignments';

interface RouteStopsEditorProps {
  assignmentId: string;
  initialStops: RouteStopReadDto[];
  onSaved: (updatedRoute: RouteReadDto) => void;
  onDiscard: () => void;
  onOrderPreviewChange?: (stops: RouteStopReadDto[]) => void;
}

const extractErrorMessage = (error: unknown) => {
  if (axios.isAxiosError(error)) {
    return error.response?.data?.detail || error.response?.data?.title || 'Unable to update route stops order.';
  }
  return 'Unable to update route stops order.';
};

const formatScheduledTime = (value: string) => {
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? value
    : new Intl.DateTimeFormat('en-GB', {
        timeZone: 'Asia/Colombo',
        hour: '2-digit',
        minute: '2-digit',
        hour12: false,
      }).format(date);
};

export const RouteStopsEditor: React.FC<RouteStopsEditorProps> = ({
  assignmentId,
  initialStops,
  onSaved,
  onDiscard,
  onOrderPreviewChange,
}) => {
  const [stops, setStops] = useState<RouteStopReadDto[]>(() =>
    [...initialStops].sort((a, b) => a.sequence - b.sequence)
  );
  const [apiError, setApiError] = useState<string | null>(null);
  const reorderMutation = useReorderRouteStops();

  const moveStop = (index: number, direction: -1 | 1) => {
    const nextIndex = index + direction;
    if (nextIndex < 0 || nextIndex >= stops.length) return;

    const nextStops = [...stops];
    const [moved] = nextStops.splice(index, 1);
    nextStops.splice(nextIndex, 0, moved);

    // Update sequence numbers
    const updatedWithSequences = nextStops.map((stop, idx) => ({
      ...stop,
      sequence: idx + 1,
    }));

    setStops(updatedWithSequences);
    setApiError(null);
    onOrderPreviewChange?.(updatedWithSequences);
  };

  const handleSave = async () => {
    setApiError(null);
    const payload = {
      stops: stops.map((stop, idx) => ({
        routeStopId: stop.id,
        sequence: idx + 1,
      })),
    };

    try {
      const updatedRoute = await reorderMutation.mutateAsync({
        id: assignmentId,
        request: payload,
      });
      onSaved(updatedRoute);
    } catch (err) {
      setApiError(extractErrorMessage(err));
    }
  };

  return (
    <div className="space-y-4 rounded-xl border border-emerald-200 bg-emerald-50/30 p-4 sm:p-5">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between border-b border-emerald-100 pb-3">
        <div>
          <h3 className="text-sm font-bold text-slate-900">Reorder route stops</h3>
          <p className="text-xs text-slate-500">
            Adjust the planned stop sequence for this unstarted assignment. Changes update the map preview immediately.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button size="sm" variant="secondary" onClick={onDiscard} disabled={reorderMutation.isPending}>
            Discard changes
          </Button>
          <Button
            size="sm"
            onClick={handleSave}
            isLoading={reorderMutation.isPending}
            disabled={reorderMutation.isPending}
          >
            Save route order
          </Button>
        </div>
      </div>

      {apiError && (
        <Alert variant="error" title="Reorder failed">
          {apiError}
        </Alert>
      )}

      <ul className="space-y-2" aria-label="Reorderable route stops">
        {stops.map((stop, index) => {
          const isFirst = index === 0;
          const isLast = index === stops.length - 1;

          return (
            <li
              key={stop.id}
              className="flex items-center justify-between gap-3 rounded-lg border border-slate-200 bg-white p-3 shadow-2xs transition-colors hover:border-emerald-300"
            >
              <div className="flex items-center gap-3 min-w-0">
                <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-emerald-700 text-xs font-bold text-white shadow-2xs">
                  {index + 1}
                </span>
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-1.5">
                    <span className="font-semibold text-xs text-slate-900">{stop.task.taskCode}</span>
                    <span className="rounded bg-slate-100 px-1.5 py-0.5 text-[10px] font-medium text-slate-600">
                      {stop.task.targetType === 'Report' ? 'Waste Report' : 'Waste Bin'}
                    </span>
                    {stop.task.scheduledAt && (
                      <span className="text-[11px] text-slate-400">
                        ({formatScheduledTime(stop.task.scheduledAt)} Colombo)
                      </span>
                    )}
                  </div>
                  <p className="truncate text-xs text-slate-500 mt-0.5">
                    {stop.task.addressText || 'Location not recorded'}
                  </p>
                </div>
              </div>

              <div className="flex shrink-0 items-center gap-1">
                <Button
                  size="sm"
                  variant="secondary"
                  disabled={isFirst || reorderMutation.isPending}
                  onClick={() => moveStop(index, -1)}
                  aria-label={`Move stop ${stop.task.taskCode} up`}
                  className="px-2 py-1 text-xs"
                >
                  ↑ Up
                </Button>
                <Button
                  size="sm"
                  variant="secondary"
                  disabled={isLast || reorderMutation.isPending}
                  onClick={() => moveStop(index, 1)}
                  aria-label={`Move stop ${stop.task.taskCode} down`}
                  className="px-2 py-1 text-xs"
                >
                  ↓ Down
                </Button>
              </div>
            </li>
          );
        })}
      </ul>
    </div>
  );
};
