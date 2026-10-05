import { z } from 'zod';
import type { WasteType } from '../types/bins';

const wasteTypes: [WasteType, ...WasteType[]] = [
  'General', 'Organic', 'Recyclable', 'Hazardous', 'Bulky', 'Other',
];

const sharedFields = {
  latitude: z.number({ message: 'Latitude is required.' }).finite().min(-90, 'Latitude must be between -90 and 90.').max(90, 'Latitude must be between -90 and 90.'),
  longitude: z.number({ message: 'Longitude is required.' }).finite().min(-180, 'Longitude must be between -180 and 180.').max(180, 'Longitude must be between -180 and 180.'),
  addressText: z.string().max(500, 'Address cannot exceed 500 characters.'),
  capacityLiters: z.number({ message: 'Capacity is required.' }).int('Capacity must be a whole number.').positive('Capacity must be greater than 0 liters.'),
  acceptedWasteTypes: z.array(z.enum(wasteTypes)).min(1, 'Select at least one accepted waste type.'),
  collectionWeekdays: z.array(z.number().int().min(1).max(7)),
};

export const createBinSchema = z.object({
  binCode: z.string().trim().min(1, 'Bin code is required.').min(3, 'Bin code must be between 3 and 50 characters.').max(50, 'Bin code must be between 3 and 50 characters.').regex(/^[A-Za-z0-9-]+$/, 'Use only letters, numbers, and hyphens.'),
  ...sharedFields,
});

export const updateBinSchema = z.object(sharedFields);

export type CreateBinFormValues = z.infer<typeof createBinSchema>;
export type UpdateBinFormValues = z.infer<typeof updateBinSchema>;
