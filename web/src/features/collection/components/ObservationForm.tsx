import React, { useState } from 'react';
import { Button } from '../../../components/ui/Button';
import type { BinCondition, RecordBinObservationRequest } from '../types/bins';

const fillLevels = [0, 25, 50, 75, 100] as const;
const conditions: BinCondition[] = ['Good', 'Damaged', 'Blocked', 'Missing'];

interface ObservationFormProps {
  isSubmitting: boolean;
  onSubmit: (request: RecordBinObservationRequest) => void;
  onCancel: () => void;
  submitLabel?: string;
}

export const ObservationForm: React.FC<ObservationFormProps> = ({ isSubmitting, onSubmit, onCancel, submitLabel = 'Record Observation' }) => {
  const [fillLevelPercent, setFillLevelPercent] = useState<RecordBinObservationRequest['fillLevelPercent'] | ''>('');
  const [condition, setCondition] = useState<BinCondition | ''>('');
  const [notes, setNotes] = useState('');
  const [errors, setErrors] = useState<{ fillLevelPercent?: string; condition?: string; notes?: string }>({});
  const submit = (event: React.FormEvent) => { event.preventDefault(); const nextErrors = { ...(fillLevelPercent === '' ? { fillLevelPercent: 'Select an observed fill level.' } : {}), ...(condition === '' ? { condition: 'Select the observed bin condition.' } : {}), ...(notes.length > 500 ? { notes: 'Notes cannot exceed 500 characters.' } : {}) }; setErrors(nextErrors); if (Object.keys(nextErrors).length) return; onSubmit({ fillLevelPercent: fillLevelPercent as RecordBinObservationRequest['fillLevelPercent'], condition: condition as BinCondition, notes: notes.trim() || null }); };
  return <form onSubmit={submit} noValidate className="space-y-5" data-testid="observation-form"><fieldset><legend className="text-sm font-semibold text-slate-800">Observed fill level <span className="text-rose-600">*</span></legend><p className="mt-1 text-xs text-slate-500">Record a human observation, not a live sensor reading.</p><div className="mt-3 grid grid-cols-5 gap-2">{fillLevels.map((level) => <label key={level} className={`cursor-pointer text-center px-2 py-2 rounded-lg border text-xs font-semibold ${fillLevelPercent === level ? 'bg-emerald-600 border-emerald-600 text-white' : 'bg-white border-slate-300 text-slate-700 hover:bg-emerald-50'}`}><input className="sr-only" type="radio" name="fillLevelPercent" value={level} checked={fillLevelPercent === level} disabled={isSubmitting} onChange={() => setFillLevelPercent(level)} />{level}%</label>)}</div>{errors.fillLevelPercent && <p className="mt-1 text-xs text-red-600">{errors.fillLevelPercent}</p>}</fieldset><fieldset><legend className="text-sm font-semibold text-slate-800">Bin condition <span className="text-rose-600">*</span></legend><div className="mt-3 grid grid-cols-2 sm:grid-cols-4 gap-2">{conditions.map((item) => <label key={item} className={`cursor-pointer text-center px-2 py-2 rounded-lg border text-xs font-semibold ${condition === item ? 'bg-emerald-600 border-emerald-600 text-white' : 'bg-white border-slate-300 text-slate-700 hover:bg-emerald-50'}`}><input className="sr-only" type="radio" name="condition" value={item} checked={condition === item} disabled={isSubmitting} onChange={() => setCondition(item)} />{item}</label>)}</div>{errors.condition && <p className="mt-1 text-xs text-red-600">{errors.condition}</p>}</fieldset><div><label htmlFor="observation-notes" className="block text-sm font-semibold text-slate-800">Notes <span className="font-normal text-slate-500">(optional)</span></label><textarea id="observation-notes" value={notes} maxLength={500} disabled={isSubmitting} onChange={(event) => setNotes(event.target.value)} className="mt-2 w-full min-h-24 rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-emerald-600/20 focus:border-emerald-600" placeholder="Add field inspection remarks..." /><p className="mt-1 text-right text-[11px] text-slate-400">{notes.length}/500</p>{errors.notes && <p className="mt-1 text-xs text-red-600">{errors.notes}</p>}</div><div className="flex justify-end gap-3 pt-2"><Button type="button" variant="secondary" disabled={isSubmitting} onClick={onCancel}>Cancel</Button><Button type="submit" variant="primary" isLoading={isSubmitting}>{submitLabel}</Button></div></form>;
};
