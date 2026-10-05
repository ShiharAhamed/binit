import React, { useMemo, useState } from 'react';
import { Button } from '../../../components/ui/Button';
import type { CollectionNeedItemDto, CollectionReason } from '../types/collectionNeeds';
import type { CreateManualCollectionTaskRequest } from '../types/collectionTasks';

const binReasons: CollectionReason[] = ['FullOrBlockedBin', 'RoutineCollection', 'OfficerDiscretion'];
const reasonLabels: Record<CollectionReason, string> = { VerifiedReport: 'Verified report', FullOrBlockedBin: 'Full or blocked bin', RoutineCollection: 'Routine collection', OfficerDiscretion: 'Officer discretion' };

const colomboParts = (date: Date) => Object.fromEntries(new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Colombo', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).formatToParts(date).filter((part) => part.type !== 'literal').map((part) => [part.type, part.value]));
export const colomboNowForInput = () => { const parts = colomboParts(new Date()); return `${parts.year}-${parts.month}-${parts.day}T${parts.hour}:${parts.minute}`; };
export const colomboLocalToUtcIso = (value: string) => {
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(value);
  if (!match) return null;
  const [, year, month, day, hour, minute] = match;
  const wallClockUtc = Date.UTC(Number(year), Number(month) - 1, Number(day), Number(hour), Number(minute));
  const local = colomboParts(new Date(wallClockUtc));
  const offset = Date.UTC(Number(local.year), Number(local.month) - 1, Number(local.day), Number(local.hour), Number(local.minute)) - wallClockUtc;
  return new Date(wallClockUtc - offset).toISOString();
};

interface ManualCollectionTaskFormProps {
  need: CollectionNeedItemDto;
  isSubmitting: boolean;
  onSubmit: (request: CreateManualCollectionTaskRequest) => void;
  onCancel: () => void;
}

export const ManualCollectionTaskForm: React.FC<ManualCollectionTaskFormProps> = ({ need, isSubmitting, onSubmit, onCancel }) => {
  const permittedReasons = useMemo<CollectionReason[]>(() => need.targetType === 'Report' ? ['VerifiedReport'] : binReasons, [need.targetType]);
  const [reason, setReason] = useState<CollectionReason>(need.collectionReason);
  const [scheduledLocal, setScheduledLocal] = useState('');
  const [handlingNotes, setHandlingNotes] = useState('');
  const [schedulingReason, setSchedulingReason] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const targetName = need.binDetails?.binCode || need.title;
  const location = need.addressText?.trim() || `${need.latitude.toFixed(5)}, ${need.longitude.toFixed(5)}`;

  const submit = (event: React.FormEvent) => {
    event.preventDefault();
    const scheduledAt = colomboLocalToUtcIso(scheduledLocal);
    const nextErrors: Record<string, string> = {};
    if (!permittedReasons.includes(reason)) nextErrors.reason = 'Select a collection reason permitted for this target.';
    if (!scheduledAt) nextErrors.scheduledAt = 'Enter a scheduled collection date and time.';
    else if (new Date(scheduledAt).getTime() < Date.now()) nextErrors.scheduledAt = 'Scheduled time must be in the future.';
    if (reason === 'OfficerDiscretion' && schedulingReason.trim().length < 5) nextErrors.schedulingReason = 'Provide a scheduling justification of at least 5 characters.';
    if (schedulingReason.length > 500) nextErrors.schedulingReason = 'Scheduling justification cannot exceed 500 characters.';
    if (handlingNotes.length > 1000) nextErrors.handlingNotes = 'Handling notes cannot exceed 1000 characters.';
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length || !scheduledAt) return;
    onSubmit({ wasteReportId: need.targetType === 'Report' ? need.id : null, wasteBinId: need.targetType === 'Bin' ? need.id : null, collectionReason: reason, scheduledAt, handlingNotes: handlingNotes.trim() || null, schedulingReason: schedulingReason.trim() || null });
  };

  return <form noValidate onSubmit={submit} className="space-y-5" data-testid="manual-task-form"><div className="rounded-lg border border-emerald-200 bg-emerald-50/60 p-4"><p className="text-xs font-semibold uppercase tracking-wide text-emerald-800">Selected {need.targetType === 'Bin' ? 'waste bin' : 'waste report'}</p><p className="mt-1 font-semibold text-slate-900">{targetName}</p><dl className="mt-3 grid grid-cols-1 sm:grid-cols-3 gap-3 text-xs"><div><dt className="text-slate-500">Location</dt><dd className="mt-0.5 text-slate-800">{location}</dd></div><div><dt className="text-slate-500">Derived reason</dt><dd className="mt-0.5 text-slate-800">{reasonLabels[need.collectionReason]}</dd></div><div><dt className="text-slate-500">Urgency</dt><dd className="mt-0.5 text-slate-800">{need.urgency}</dd></div></dl></div>
    <div><label htmlFor="collection-reason" className="block text-sm font-semibold text-slate-800">Collection reason <span className="text-rose-600">*</span></label><select id="collection-reason" value={reason} disabled={isSubmitting || need.targetType === 'Report'} onChange={(event) => setReason(event.target.value as CollectionReason)} className="mt-2 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-emerald-600/20 focus:border-emerald-600">{permittedReasons.map((item) => <option key={item} value={item}>{reasonLabels[item]}</option>)}</select>{need.targetType === 'Report' && <p className="mt-1 text-xs text-slate-500">Report-targeted tasks must use Verified report.</p>}{errors.reason && <p className="mt-1 text-xs text-red-600">{errors.reason}</p>}</div>
    <div><label htmlFor="scheduled-at" className="block text-sm font-semibold text-slate-800">Scheduled collection time <span className="text-rose-600">*</span></label><input id="scheduled-at" type="datetime-local" value={scheduledLocal} min={colomboNowForInput()} disabled={isSubmitting} onChange={(event) => setScheduledLocal(event.target.value)} className="mt-2 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-emerald-600/20 focus:border-emerald-600" /><p className="mt-1 text-xs text-slate-500">Enter local Asia/Colombo time. It is sent to the API as UTC.</p>{errors.scheduledAt && <p className="mt-1 text-xs text-red-600">{errors.scheduledAt}</p>}</div>
    {reason === 'OfficerDiscretion' && <div><label htmlFor="scheduling-reason" className="block text-sm font-semibold text-slate-800">Scheduling justification <span className="text-rose-600">*</span></label><textarea id="scheduling-reason" value={schedulingReason} maxLength={500} disabled={isSubmitting} onChange={(event) => setSchedulingReason(event.target.value)} className="mt-2 min-h-24 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-emerald-600/20 focus:border-emerald-600" placeholder="Explain why officer discretion is required..." /><p className="mt-1 text-right text-[11px] text-slate-400">{schedulingReason.length}/500</p>{errors.schedulingReason && <p className="mt-1 text-xs text-red-600">{errors.schedulingReason}</p>}</div>}
    <div><label htmlFor="handling-notes" className="block text-sm font-semibold text-slate-800">Handling notes <span className="font-normal text-slate-500">(optional)</span></label><textarea id="handling-notes" value={handlingNotes} maxLength={1000} disabled={isSubmitting} onChange={(event) => setHandlingNotes(event.target.value)} className="mt-2 min-h-24 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-emerald-600/20 focus:border-emerald-600" placeholder="Add operational instructions for the collection crew..." /><p className="mt-1 text-right text-[11px] text-slate-400">{handlingNotes.length}/1000</p>{errors.handlingNotes && <p className="mt-1 text-xs text-red-600">{errors.handlingNotes}</p>}</div>
    <div className="flex flex-col-reverse sm:flex-row sm:justify-end gap-3 pt-1"><Button type="button" variant="secondary" disabled={isSubmitting} onClick={onCancel}>Cancel</Button><Button type="submit" variant="primary" isLoading={isSubmitting}>Schedule Task</Button></div></form>;
};
