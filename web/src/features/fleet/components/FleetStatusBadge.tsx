import React from 'react';

type FleetBadgeTone = 'available' | 'maintenance' | 'inactive' | 'offDuty' | 'occupied';

const toneClasses: Record<FleetBadgeTone, string> = {
  available: 'bg-emerald-50 text-emerald-800 border-emerald-200/80',
  maintenance: 'bg-amber-50 text-amber-800 border-amber-200/80',
  inactive: 'bg-slate-100 text-slate-700 border-slate-200',
  offDuty: 'bg-slate-100 text-slate-700 border-slate-200',
  occupied: 'bg-amber-50 text-amber-800 border-amber-200/80',
};

interface FleetStatusBadgeProps {
  label: string;
  tone: FleetBadgeTone;
  className?: string;
}

export const FleetStatusBadge: React.FC<FleetStatusBadgeProps> = ({ label, tone, className = '' }) => (
  <span className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-[11px] font-semibold ${toneClasses[tone]} ${className}`}>
    <span className="h-1.5 w-1.5 rounded-full bg-current opacity-70" aria-hidden="true" />
    {label}
  </span>
);

export const vehicleStatusTone = (status: 'Available' | 'Maintenance' | 'Inactive'): FleetBadgeTone => {
  if (status === 'Available') return 'available';
  if (status === 'Maintenance') return 'maintenance';
  return 'inactive';
};

export const driverAvailabilityTone = (status: 'Available' | 'OffDuty'): FleetBadgeTone =>
  status === 'Available' ? 'available' : 'offDuty';
