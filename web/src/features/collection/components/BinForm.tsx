import React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Button } from '../../../components/ui/Button';
import { Input } from '../../../components/ui/Input';
import { createBinSchema, updateBinSchema, type CreateBinFormValues, type UpdateBinFormValues } from '../schemas/binSchema';
import type { CreateWasteBinRequest, UpdateWasteBinRequest, WasteType } from '../types/bins';
import { BinLocationPicker, isValidBinLocation } from './BinLocationMap';

const wasteTypes: WasteType[] = ['General', 'Organic', 'Recyclable', 'Hazardous', 'Bulky', 'Other'];
const weekdays = [
  { value: 1, label: 'Mon' }, { value: 2, label: 'Tue' }, { value: 3, label: 'Wed' },
  { value: 4, label: 'Thu' }, { value: 5, label: 'Fri' }, { value: 6, label: 'Sat' }, { value: 7, label: 'Sun' },
];

export interface BinFormInitialValues {
  binCode?: string;
  latitude: number | undefined;
  longitude: number | undefined;
  addressText: string;
  capacityLiters: number;
  acceptedWasteTypes: WasteType[];
  collectionWeekdays: number[];
}

interface BinFormProps {
  mode: 'create' | 'edit';
  initialValues: BinFormInitialValues;
  isSubmitting: boolean;
  onCancel: () => void;
  onSubmit: (request: CreateWasteBinRequest | UpdateWasteBinRequest) => void;
}

export const BinForm: React.FC<BinFormProps> = ({ mode, initialValues, isSubmitting, onCancel, onSubmit }) => {
  const isCreate = mode === 'create';
  const form = useForm<CreateBinFormValues | UpdateBinFormValues>({
    resolver: zodResolver(isCreate ? createBinSchema : updateBinSchema),
    defaultValues: initialValues,
  });
  const { register, handleSubmit, setValue, watch, formState: { errors } } = form;
  const selectedWasteTypes = watch('acceptedWasteTypes') as WasteType[];
  const selectedWeekdays = watch('collectionWeekdays') as number[];
  const latitude = watch('latitude');
  const longitude = watch('longitude');
  const hasSelectedLocation = isValidBinLocation(latitude, longitude);

  const toggleWasteType = (type: WasteType) => {
    setValue('acceptedWasteTypes', selectedWasteTypes.includes(type) ? selectedWasteTypes.filter((item) => item !== type) : [...selectedWasteTypes, type], { shouldValidate: true, shouldDirty: true });
  };
  const toggleWeekday = (day: number) => {
    setValue('collectionWeekdays', selectedWeekdays.includes(day) ? selectedWeekdays.filter((item) => item !== day) : [...selectedWeekdays, day].sort((a, b) => a - b), { shouldValidate: true, shouldDirty: true });
  };

  const submit = (values: CreateBinFormValues | UpdateBinFormValues) => {
    const shared: UpdateWasteBinRequest = {
      latitude: values.latitude,
      longitude: values.longitude,
      addressText: values.addressText.trim() || null,
      capacityLiters: values.capacityLiters,
      acceptedWasteTypes: values.acceptedWasteTypes,
      collectionWeekdays: values.collectionWeekdays,
    };
    onSubmit(isCreate ? { ...shared, binCode: (values as CreateBinFormValues).binCode.trim() } : shared);
  };

  return <form onSubmit={handleSubmit(submit)} noValidate className="space-y-6">
    <section className="space-y-4"><div><h2 className="text-base font-bold text-slate-900">Location and identity</h2><p className="mt-1 text-xs text-slate-500"><span className="text-rose-600">*</span> Required fields</p></div>
      {isCreate ? <Input id="binCode" label="Bin code *" placeholder="e.g. BIN-COL-0043" error={(errors as { binCode?: { message?: string } }).binCode?.message} disabled={isSubmitting} {...register('binCode' as 'binCode')} /> : <Input id="binCode" label="Bin code" value={initialValues.binCode ?? ''} readOnly helperText="Bin code cannot be changed after registration." />}
      <div><p className="mb-2 text-sm font-medium text-slate-700">Bin location <span className="text-rose-600">*</span></p><BinLocationPicker latitude={latitude} longitude={longitude} addressText={watch('addressText')} disabled={isSubmitting} onSelect={(location) => { setValue('latitude', location.latitude, { shouldValidate: true, shouldDirty: true }); setValue('longitude', location.longitude, { shouldValidate: true, shouldDirty: true }); }} /><div className="mt-3 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-xs"><span className="font-medium text-slate-600">Selected coordinates: </span>{hasSelectedLocation ? <span className="font-mono text-slate-800">{latitude.toFixed(6)}, {(longitude as number).toFixed(6)}</span> : <span className="text-slate-500">No location selected</span>}</div>{(errors.latitude || errors.longitude) && <p className="mt-1 text-xs text-red-600">Select a valid location on the map.</p>}</div>
      <Input id="addressText" label="Address (optional)" placeholder="e.g. Galle Face Green Promenade" maxLength={500} error={errors.addressText?.message} disabled={isSubmitting} {...register('addressText')} />
    </section>
    <section className="pt-5 border-t border-slate-100 space-y-4"><h2 className="text-base font-bold text-slate-900">Capacity and waste streams</h2><Input id="capacityLiters" label="Capacity in liters *" type="number" min="1" step="1" error={errors.capacityLiters?.message} disabled={isSubmitting} {...register('capacityLiters', { valueAsNumber: true })} />
      <fieldset><legend className="text-sm font-medium text-slate-700 mb-2">Accepted waste types <span className="text-rose-600">*</span></legend><div className="grid grid-cols-2 sm:grid-cols-3 gap-2">{wasteTypes.map((type) => <label key={type} className="flex items-center gap-2 p-2.5 rounded-lg border border-slate-200 bg-slate-50 hover:bg-emerald-50 cursor-pointer text-xs text-slate-700"><input type="checkbox" checked={selectedWasteTypes.includes(type)} onChange={() => toggleWasteType(type)} disabled={isSubmitting} className="accent-emerald-600" />{type}</label>)}</div>{errors.acceptedWasteTypes?.message && <p className="mt-1 text-xs text-red-600">{errors.acceptedWasteTypes.message}</p>}</fieldset>
    </section>
    <section className="pt-5 border-t border-slate-100"><fieldset><legend className="text-base font-bold text-slate-900">Routine collection weekdays</legend><p className="mt-1 text-xs text-slate-500">Optional. Select all scheduled ISO weekdays.</p><div className="mt-3 flex flex-wrap gap-2">{weekdays.map((day) => <label key={day.value} className={`cursor-pointer px-3 py-2 rounded-lg border text-xs font-semibold transition-colors ${selectedWeekdays.includes(day.value) ? 'bg-emerald-600 text-white border-emerald-600' : 'bg-white text-slate-700 border-slate-300 hover:bg-emerald-50'}`}><input type="checkbox" className="sr-only" checked={selectedWeekdays.includes(day.value)} onChange={() => toggleWeekday(day.value)} disabled={isSubmitting} />{day.label}</label>)}</div></fieldset></section>
    <div className="pt-5 border-t border-slate-100 flex flex-col-reverse sm:flex-row sm:justify-end gap-3"><Button type="button" variant="secondary" onClick={onCancel} disabled={isSubmitting}>Cancel</Button><Button type="submit" variant="primary" isLoading={isSubmitting}>{isCreate ? 'Register Bin' : 'Save Changes'}</Button></div>
  </form>;
};
