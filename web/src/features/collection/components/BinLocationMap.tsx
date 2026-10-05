import React from 'react';
import { MapContainer, Marker, Popup, TileLayer, useMapEvents } from 'react-leaflet';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';

export interface BinLocation {
  latitude: number;
  longitude: number;
}

interface BinLocationMapProps extends Partial<BinLocation> {
  addressText?: string | null;
}

interface BinLocationPickerProps extends BinLocationMapProps {
  disabled?: boolean;
  onSelect: (location: BinLocation) => void;
}

const mapViewingCenter: [number, number] = [6.9271, 79.8612];

const binMarkerIcon = L.divIcon({
  className: 'smartwaste-map-marker',
  html: '<div class="h-8 w-8 rounded-full border-2 border-white bg-emerald-600 shadow-md"></div>',
  iconSize: [32, 32],
  iconAnchor: [16, 16],
  popupAnchor: [0, -16],
});

export const isValidBinLocation = (latitude: unknown, longitude: unknown): latitude is number =>
  typeof latitude === 'number' && Number.isFinite(latitude) && latitude >= -90 && latitude <= 90
  && typeof longitude === 'number' && Number.isFinite(longitude) && longitude >= -180 && longitude <= 180;

const LocationSelectionEvents: React.FC<{ disabled: boolean; onSelect: (location: BinLocation) => void }> = ({ disabled, onSelect }) => {
  useMapEvents({
    click: (event) => {
      if (!disabled) onSelect({ latitude: event.latlng.lat, longitude: event.latlng.lng });
    },
  });
  return null;
};

const MapFrame: React.FC<BinLocationMapProps & { children?: React.ReactNode; interactive?: boolean }> = ({ latitude, longitude, addressText, children, interactive = false }) => {
  const hasLocation = isValidBinLocation(latitude, longitude);
  const selectedLocation: BinLocation | null = hasLocation ? { latitude: latitude as number, longitude: longitude as number } : null;
  const center: [number, number] = selectedLocation ? [selectedLocation.latitude, selectedLocation.longitude] : mapViewingCenter;

  return <div className="relative z-0 h-72 w-full overflow-hidden rounded-lg border border-slate-200/80 shadow-2xs" role="region" aria-label={interactive ? 'Bin location selector map' : 'Bin location map'}>
    <MapContainer center={center} zoom={hasLocation ? 15 : 12} scrollWheelZoom={false} className="h-full w-full" attributionControl>
      <TileLayer attribution='&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer">OpenStreetMap</a> contributors' url="https://tile.openstreetmap.org/{z}/{x}/{y}.png" maxZoom={19} />
      {selectedLocation && <Marker position={[selectedLocation.latitude, selectedLocation.longitude]} icon={binMarkerIcon}><Popup><div className="text-xs"><p className="font-semibold text-slate-900">Bin location</p>{addressText?.trim() && <p className="mt-1 text-slate-600">{addressText}</p>}<p className="mt-1 font-mono text-[11px] text-emerald-700">{selectedLocation.latitude.toFixed(6)}, {selectedLocation.longitude.toFixed(6)}</p></div></Popup></Marker>}
      {children}
    </MapContainer>
  </div>;
};

export const BinLocationPicker: React.FC<BinLocationPickerProps> = ({ latitude, longitude, addressText, disabled = false, onSelect }) => <div className="space-y-2" data-testid="bin-location-picker">
  <MapFrame latitude={latitude} longitude={longitude} addressText={addressText} interactive>
    <LocationSelectionEvents disabled={disabled} onSelect={onSelect} />
  </MapFrame>
  <p className="text-xs text-slate-500">Select the bin location on the map. Selecting another point moves the marker.</p>
</div>;

export const BinLocationMap: React.FC<BinLocationMapProps> = ({ latitude, longitude, addressText }) => {
  if (!isValidBinLocation(latitude, longitude)) return <div className="flex h-48 w-full flex-col items-center justify-center rounded-lg border border-slate-200 bg-slate-50 p-4 text-center" data-testid="bin-location-unavailable" role="alert"><p className="text-sm font-semibold text-slate-700">Location unavailable</p><p className="mt-1 text-xs text-slate-500">Saved geographic coordinates are unavailable or invalid.</p></div>;
  return <div data-testid="bin-location-map"><MapFrame latitude={latitude} longitude={longitude} addressText={addressText} /></div>;
};
