import React, { useState } from 'react';
import { Button } from '../../../components/ui/Button';
import type { VehicleRequest, VehicleType, WasteType } from '../types/fleet';

const types: VehicleType[] = ['Compactor', 'Flatbed', 'Tipper', 'SmallVan'];
const wastes: WasteType[] = ['General', 'Organic', 'Recyclable', 'Hazardous', 'Bulky', 'Other'];
const controlClassName = 'mt-1.5 w-full rounded-lg border border-slate-300 bg-white px-3.5 py-2 text-sm text-slate-900 transition-colors placeholder:text-slate-400 focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-600/20';
const labelClassName = 'block text-xs font-semibold text-slate-700';

export const VehicleForm: React.FC<{
  initial?: Partial<VehicleRequest>;
  submitting: boolean;
  onCancel: () => void;
  onSubmit: (request: VehicleRequest) => void;
}> = ({ initial, submitting, onCancel, onSubmit }) => {
  const [form, setForm] = useState<VehicleRequest>({
    registrationNumber: initial?.registrationNumber ?? '',
    vehicleType: initial?.vehicleType ?? '',
    capacityLiters: initial?.capacityLiters ?? '',
    supportedWasteTypes: initial?.supportedWasteTypes ?? [],
    notes: initial?.notes ?? '',
  });
  const valid = form.registrationNumber.trim() && form.vehicleType && Number(form.capacityLiters) > 0 && form.supportedWasteTypes.length;
  const set = (partial: Partial<VehicleRequest>) => setForm((current) => ({ ...current, ...partial }));

  return (
    <form
      className="grid grid-cols-1 gap-5 md:grid-cols-2"
      onSubmit={(event) => {
        event.preventDefault();
        if (valid) onSubmit({ ...form, registrationNumber: form.registrationNumber.trim(), capacityLiters: Number(form.capacityLiters), notes: form.notes.trim() });
      }}
    >
      <label className={labelClassName}>
        Registration number <span className="text-rose-600">*</span>
        <input required value={form.registrationNumber} onChange={(event) => set({ registrationNumber: event.target.value })} className={controlClassName} placeholder="e.g. WP-CAB-1234" />
      </label>
      <label className={labelClassName}>
        Vehicle type <span className="text-rose-600">*</span>
        <select required value={form.vehicleType} onChange={(event) => set({ vehicleType: event.target.value as VehicleType })} className={controlClassName}>
          <option value="">Select vehicle type</option>
          {types.map((type) => <option key={type}>{type}</option>)}
        </select>
      </label>
      <label className={labelClassName}>
        Capacity (litres) <span className="text-rose-600">*</span>
        <input required min="1" type="number" value={form.capacityLiters} onChange={(event) => set({ capacityLiters: event.target.value === '' ? '' : Number(event.target.value) })} className={controlClassName} placeholder="e.g. 8500" />
        <span className="mt-1 block text-[11px] font-normal text-slate-500">Enter the vehicle&apos;s usable collection capacity.</span>
      </label>
      <fieldset>
        <legend className={labelClassName}>Supported waste types <span className="text-rose-600">*</span></legend>
        <div className="mt-2 flex flex-wrap gap-2">
          {wastes.map((waste) => (
            <label key={waste} className="inline-flex cursor-pointer items-center gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-xs font-medium text-slate-700 transition-colors hover:border-emerald-300 hover:bg-emerald-50 has-[:checked]:border-emerald-400 has-[:checked]:bg-emerald-50 has-[:checked]:text-emerald-800">
              <input type="checkbox" checked={form.supportedWasteTypes.includes(waste)} onChange={(event) => set({ supportedWasteTypes: event.target.checked ? [...form.supportedWasteTypes, waste] : form.supportedWasteTypes.filter((item) => item !== waste) })} className="h-3.5 w-3.5 rounded border-slate-300 text-emerald-600 focus:ring-emerald-500" />
              {waste}
            </label>
          ))}
        </div>
      </fieldset>
      <label className={`md:col-span-2 ${labelClassName}`}>
        Notes <span className="font-normal text-slate-400">(optional)</span>
        <textarea value={form.notes} onChange={(event) => set({ notes: event.target.value })} maxLength={1000} rows={3} className={controlClassName} placeholder="Add operational notes that help staff identify this vehicle." />
      </label>
      <div className="flex flex-col-reverse gap-2 border-t border-slate-100 pt-5 sm:col-span-2 sm:flex-row sm:justify-end">
        <Button type="button" variant="secondary" onClick={onCancel}>Cancel</Button>
        <Button type="submit" disabled={!valid} isLoading={submitting}>{initial ? 'Save changes' : 'Register vehicle'}</Button>
      </div>
    </form>
  );
};
