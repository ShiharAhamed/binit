import axios from 'axios'; import React, { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import { Alert } from '../../../components/ui/Alert';
import { Button } from '../../../components/ui/Button';
import { Card } from '../../../components/ui/Card';
import { useCollectionTaskDetail } from '../hooks/useCollectionTaskDetail';
import { useCollectionTaskHistory } from '../hooks/useCollectionTaskHistory';
import { collectionTasksApi } from '../api/collectionTasksApi';
import { RescheduleTaskForm } from '../components/RescheduleTaskForm';
import { useAuthStore } from '../../../store/authStore';
import { useCollectionTasks } from '../hooks/useCollectionTasks';
import type { CollectionReason } from '../types/collectionNeeds';
import { ReplacementTaskSection } from '../components/ReplacementTaskSection';
import type { CollectionTaskDetailDto, CollectionTaskListParams, CollectionTaskStatus, CollectionTaskTargetType } from '../types/collectionTasks';

const statuses: CollectionTaskStatus[] = ['Scheduled', 'Assigned', 'InProgress', 'Completed', 'Failed', 'Cancelled'];
const reasons: CollectionReason[] = ['VerifiedReport', 'FullOrBlockedBin', 'RoutineCollection', 'OfficerDiscretion'];
const labels: Record<CollectionReason, string> = { VerifiedReport: 'Verified report', FullOrBlockedBin: 'Full or blocked bin', RoutineCollection: 'Routine collection', OfficerDiscretion: 'Officer discretion' };
const statusClasses: Record<CollectionTaskStatus, string> = { Scheduled: 'bg-blue-50 text-blue-800 border-blue-200', Assigned: 'bg-violet-50 text-violet-800 border-violet-200', InProgress: 'bg-amber-50 text-amber-800 border-amber-200', Completed: 'bg-emerald-50 text-emerald-800 border-emerald-200', Failed: 'bg-red-50 text-red-800 border-red-200', Cancelled: 'bg-slate-100 text-slate-700 border-slate-200' };
const time = (value: string | null) => { if (!value) return 'Not recorded'; const date = new Date(value); return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Colombo', day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false }).format(date); };

export const CollectionTasksPage: React.FC = () => { const [filters, setFilters] = useState<CollectionTaskListParams>({ page: 1, pageSize: 20 }); const { tasks, totalCount, totalPages, currentPage, pageSize, isLoading, isFetching, isError, errorMessage, refetch } = useCollectionTasks(filters); const location = useLocation(); const detailBase = location.pathname.startsWith('/manager') ? '/manager/tasks' : '/officer/tasks'; const set = (partial: Partial<CollectionTaskListParams>) => setFilters((old) => ({ ...old, ...partial, page: 1 })); const filtered = Boolean(filters.status || filters.targetType || filters.collectionReason || filters.dateFrom || filters.dateTo); return <div className="space-y-6"><header className="border-b border-slate-200 pb-5"><p className="text-xs font-medium text-slate-400">Operations / Collection Tasks</p><h1 className="mt-1 text-2xl font-bold text-slate-900">Collection Tasks</h1><p className="mt-1 text-sm text-slate-500">Read-only operational task tracking for municipal staff.</p></header><Card className="p-4"><div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-3"><Select label="Status" value={filters.status ?? ''} onChange={(v) => set({ status: v as CollectionTaskStatus | '' })}><option value="">All statuses</option>{statuses.map((v) => <option key={v}>{v}</option>)}</Select><Select label="Target type" value={filters.targetType ?? ''} onChange={(v) => set({ targetType: v as CollectionTaskTargetType | '' })}><option value="">All targets</option><option value="Report">Waste reports</option><option value="Bin">Waste bins</option></Select><Select label="Collection reason" value={filters.collectionReason ?? ''} onChange={(v) => set({ collectionReason: v as CollectionReason | '' })}><option value="">All reasons</option>{reasons.map((v) => <option key={v} value={v}>{labels[v]}</option>)}</Select><label className="text-xs font-semibold text-slate-700">From<input aria-label="Date from" type="date" value={filters.dateFrom ?? ''} onChange={(e) => set({ dateFrom: e.target.value })} className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2" /></label><label className="text-xs font-semibold text-slate-700">To<input aria-label="Date to" type="date" value={filters.dateTo ?? ''} onChange={(e) => set({ dateTo: e.target.value })} className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2" /></label></div>{filtered && <Button variant="ghost" size="sm" className="mt-3" onClick={() => setFilters({ page: 1, pageSize: 20 })}>Clear filters</Button>}</Card>{isError && <Alert variant="error" title="Unable to load collection tasks">{errorMessage}<Button variant="secondary" size="sm" className="mt-3" onClick={() => refetch()}>Retry</Button></Alert>}<Card className="p-0 overflow-hidden">{isLoading ? <div data-testid="collection-tasks-loading" className="p-6 space-y-3 animate-pulse">{Array.from({ length: 6 }).map((_, i) => <div key={i} className="h-14 rounded bg-slate-100" />)}</div> : !tasks.length ? <div className="py-16 text-center"><p className="font-bold text-slate-800">{filtered ? 'No collection tasks match the current filters.' : 'No collection tasks have been scheduled.'}</p></div> : <div className="overflow-x-auto"><table className="min-w-[900px] w-full text-left text-xs"><thead className="bg-slate-50 text-slate-500 uppercase"><tr><th className="p-4">Task</th><th className="p-4">Target</th><th className="p-4">Reason</th><th className="p-4">Scheduled (Colombo)</th><th className="p-4">Status</th><th className="p-4">Created</th></tr></thead><tbody className="divide-y divide-slate-100">{tasks.map((task) => <tr key={task.id}><td className="p-4"><Link data-testid={`task-detail-${task.id}`} to={`${detailBase}/${task.id}`} className="font-semibold text-emerald-700">{task.taskCode}</Link></td><td className="p-4"><span className="text-slate-500">{task.targetType === 'Bin' ? 'Waste bin' : 'Waste report'}</span><p className="mt-1 text-slate-800">{task.targetReference}</p></td><td className="p-4">{labels[task.collectionReason]}</td><td className="p-4 text-slate-700">{time(task.scheduledAt)}</td><td className="p-4"><span className={`inline-flex rounded border px-2 py-0.5 font-semibold ${statusClasses[task.status]}`}>{task.status}</span></td><td className="p-4 text-slate-500">{time(task.createdAt)}</td></tr>)}</tbody></table></div>}{!isLoading && !isError && totalCount > 0 && <div className="flex justify-between border-t p-4 text-xs"><span>Showing {Math.min((currentPage - 1) * pageSize + 1, totalCount)}–{Math.min(currentPage * pageSize, totalCount)} of {totalCount}</span><div className="flex gap-2"><Button size="sm" variant="secondary" disabled={currentPage <= 1 || isFetching} onClick={() => setFilters((old) => ({ ...old, page: currentPage - 1 }))}>Previous</Button><Button size="sm" variant="secondary" disabled={currentPage >= totalPages || isFetching} onClick={() => setFilters((old) => ({ ...old, page: currentPage + 1 }))}>Next</Button></div></div>}</Card></div>; };
const Select: React.FC<{ label: string; value: string; onChange: (value: string) => void; children: React.ReactNode }> = ({ label, value, onChange, children }) => <label className="text-xs font-semibold text-slate-700">{label}<select value={value} onChange={(e) => onChange(e.target.value)} className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2">{children}</select></label>;
export const CollectionTaskDetailPage: React.FC = () => { const { id } = useParams(); const navigate = useNavigate(); const location = useLocation(); const back = location.pathname.startsWith('/manager') ? '/manager/tasks' : '/officer/tasks'; const { task, isLoading, isError, isNotFound, errorMessage, refetch } = useCollectionTaskDetail(id); if (isLoading) return <div data-testid="collection-task-detail-loading" className="p-8 text-sm text-slate-500">Loading collection task…</div>; if (isError || !task) return <Alert variant="error" title={isNotFound ? 'Collection task not found' : 'Unable to load collection task'}>{isNotFound ? 'This task may no longer be available.' : errorMessage}<div className="mt-3 flex gap-2"><Button variant="secondary" size="sm" onClick={() => navigate(back)}>Back to Collection Tasks</Button>{!isNotFound && <Button variant="secondary" size="sm" onClick={() => refetch()}>Retry</Button>}</div></Alert>; const target = task.targetSummary; return <div className="space-y-6"><Button variant="ghost" size="sm" onClick={() => navigate(back)}>← Back to Collection Tasks</Button><header className="flex flex-wrap justify-between gap-3 border-b pb-5"><div><p className="text-xs text-slate-400">Collection Task</p><h1 className="mt-1 text-2xl font-bold text-slate-900">{task.taskCode}</h1><p className="mt-1 text-sm text-slate-500">{task.targetType === 'Bin' ? 'Waste bin' : 'Waste report'} task</p></div><span className={`inline-flex h-fit rounded border px-3 py-1 text-sm font-semibold ${statusClasses[task.status]}`}>{task.status}</span></header><div className="grid grid-cols-1 lg:grid-cols-2 gap-5"><Card className="p-5"><h2 className="font-semibold text-slate-900">Target information</h2><dl className="mt-4 grid grid-cols-1 sm:grid-cols-2 gap-4 text-sm"><Item label="Target" value={task.targetType === 'Bin' ? 'Waste Bin' : 'Waste Report'} /><Item label="Reference" value={target.identifier} /><Item label="Location" value={target.addressText || `${target.latitude.toFixed(5)}, ${target.longitude.toFixed(5)}`} />{task.targetType === 'Bin' && <><Item label="Capacity" value={target.capacityLiters === null ? 'Not recorded' : `${target.capacityLiters.toLocaleString()} L`} /><Item label="Latest recorded fill" value={target.latestFillLevelPercent === null ? 'Not recorded' : `${target.latestFillLevelPercent}%`} /></>}<Item label="Waste types" value={target.wasteTypes.length ? target.wasteTypes.join(', ') : 'Not recorded'} /></dl></Card><Card className="p-5"><h2 className="font-semibold text-slate-900">Scheduling</h2><dl className="mt-4 grid grid-cols-1 sm:grid-cols-2 gap-4 text-sm"><Item label="Collection reason" value={labels[task.collectionReason]} /><Item label="Scheduled (Colombo)" value={time(task.scheduledAt)} /><Item label="Creation method" value={task.creationMethod} /><Item label="Created by" value={task.createdByUserName || 'Officer name unavailable'} /><Item label="Created" value={time(task.createdAt)} /><Item label="Last updated" value={time(task.updatedAt)} /></dl></Card><Card className="p-5 lg:col-span-2"><h2 className="font-semibold text-slate-900">Operational notes</h2><dl className="mt-4 grid grid-cols-1 sm:grid-cols-2 gap-4 text-sm"><Item label="Scheduling justification" value={task.schedulingReason?.trim() || 'No scheduling justification recorded'} /><Item label="Handling notes" value={task.handlingNotes?.trim() || 'No handling notes recorded'} /></dl></Card></div></div>; };
const Item: React.FC<{ label: string; value: string }> = ({ label, value }) => <div><dt className="text-xs font-medium text-slate-500">{label}</dt><dd className="mt-1 text-slate-800 whitespace-pre-wrap">{value}</dd></div>;

export const CollectionTaskEnhancedDetailPage: React.FC = () => {
  const { id } = useParams();
  const { task } = useCollectionTaskDetail(id);
  return (
    <div className="space-y-5">
      <CollectionTaskDetailPage />
      {task && <TaskExtensions task={task} />}
    </div>
  );
};

const TaskExtensions: React.FC<{ task: CollectionTaskDetailDto }> = ({ task }) => {
  const { user } = useAuthStore();
  const [open, setOpen] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const client = useQueryClient();
  const history = useCollectionTaskHistory(task.id);
  const mutation = useMutation({
    mutationFn: (request: import('../types/collectionTasks').RescheduleCollectionTaskRequest) =>
      collectionTasksApi.rescheduleTask(task.id, request),
    onSuccess: async () => {
      await Promise.all([
        client.invalidateQueries({ queryKey: ['collection-tasks'] }),
        client.invalidateQueries({ queryKey: ['collection-task-history', task.id] }),
      ]);
      setOpen(false);
      setMessage('Collection task rescheduled successfully.');
    },
    onError: (e) => {
      const r = axios.isAxiosError(e) ? e.response : undefined;
      setMessage(r?.data?.detail || r?.data?.title || 'Unable to reschedule this task.');
    },
  });

  const canReschedule = user?.role === 'WasteOfficer' && task.status === 'Scheduled';

  return (
    <>
      {task.status === 'Failed' && (
        <ReplacementTaskSection
          task={task}
          statusHistory={history.history?.statusHistory}
          userRole={user?.role}
        />
      )}

      {task.status === 'Scheduled' && (
        <Card className="p-5">
          <div className="flex justify-between gap-3">
            <div>
              <h2 className="font-semibold">Schedule changes</h2>
              <p className="text-xs text-slate-500">{task.taskCode} · {task.targetSummary.identifier}</p>
            </div>
            {canReschedule && (
              <Button onClick={() => { setMessage(null); setOpen(true); }}>Reschedule</Button>
            )}
          </div>
          {message && (
            <Alert variant={message.includes('successfully') ? 'success' : 'error'} className="mt-3">
              {message}
            </Alert>
          )}
          {open && (
            <div className="mt-4 rounded border p-4">
              <RescheduleTaskForm
                isSubmitting={mutation.isPending}
                onCancel={() => setOpen(false)}
                onSubmit={(request) => mutation.mutate(request)}
              />
            </div>
          )}
        </Card>
      )}

      <Card className="p-5">
        <h2 className="font-semibold">Task audit history</h2>
        {history.isLoading ? (
          <p data-testid="task-history-loading" className="mt-3 text-sm">Loading history…</p>
        ) : history.isError ? (
          <Alert variant="error" className="mt-3">
            {history.errorMessage}
            <Button size="sm" variant="secondary" className="mt-2" onClick={() => history.refetch()}>
              Retry
            </Button>
          </Alert>
        ) : (
          <div className="mt-4 grid grid-cols-1 lg:grid-cols-2 gap-4">
            <Audit
              title="Status transitions"
              empty="No status transitions recorded."
              items={history.history?.statusHistory ?? []}
              render={(x: any) =>
                `${x.fromStatus || 'Created'} → ${x.toStatus} · ${time(x.changedAt)} · ${x.changedByUserName || 'System'}${x.notes ? ` · ${x.notes}` : ''}`
              }
            />
            <Audit
              title="Schedule changes"
              empty="No schedule changes recorded."
              items={history.history?.scheduleHistory ?? []}
              render={(x: any) =>
                `${time(x.previousScheduledAt)} → ${time(x.newScheduledAt)} · ${time(x.rescheduledAt)} · ${x.rescheduledByUserName || 'Officer'} · ${x.reason}`
              }
            />
          </div>
        )}
      </Card>
    </>
  );
};

const Audit: React.FC<{ title: string; empty: string; items: any[]; render: (x: any) => string }> = ({ title, empty, items, render }) => <section><h3 className="text-sm font-semibold">{title}</h3>{items.length ? <ul className="mt-2 space-y-2">{items.map((x) => <li key={x.id} className="rounded border bg-slate-50 p-3 text-xs text-slate-600">{render(x)}</li>)}</ul> : <p className="mt-2 text-xs text-slate-500">{empty}</p>}</section>;
