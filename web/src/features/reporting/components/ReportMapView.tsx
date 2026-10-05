import React from 'react';
import { MapContainer, TileLayer, Marker, Popup } from 'react-leaflet';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';

export interface ReportMapViewProps {
  latitude: number;
  longitude: number;
  addressText?: string | null;
}

const customMarkerIcon = L.divIcon({
  className: 'smartwaste-map-marker',
  html: `
    <div style="
      width: 32px;
      height: 32px;
      background: #059669;
      border: 2.5px solid #ffffff;
      border-radius: 50% 50% 50% 0;
      transform: rotate(-45deg);
      box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.3);
      display: flex;
      align-items: center;
      justify-content: center;
    ">
      <svg style="transform: rotate(45deg); width: 16px; height: 16px; color: white;" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
      </svg>
    </div>
  `,
  iconSize: [32, 32],
  iconAnchor: [16, 32],
  popupAnchor: [0, -32],
});

export const ReportMapView: React.FC<ReportMapViewProps> = ({
  latitude,
  longitude,
  addressText,
}) => {
  const isValidCoord =
    typeof latitude === 'number' &&
    typeof longitude === 'number' &&
    !isNaN(latitude) &&
    !isNaN(longitude) &&
    latitude >= -90 &&
    latitude <= 90 &&
    longitude >= -180 &&
    longitude <= 180;

  if (!isValidCoord) {
    return (
      <div
        className="h-80 w-full rounded-lg bg-slate-100 border border-slate-200 flex flex-col items-center justify-center text-slate-500 p-4"
        data-testid="report-map-invalid"
        role="alert"
      >
        <svg
          className="w-8 h-8 text-amber-500 mb-2"
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
          aria-hidden="true"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
          />
        </svg>
        <p className="text-sm font-semibold text-slate-700">Invalid Location Coordinates</p>
        <p className="text-xs text-slate-500 mt-1">
          Geographic coordinates are unavailable or outside valid bounds.
        </p>
      </div>
    );
  }

  return (
    <div
      className="relative rounded-lg overflow-hidden border border-slate-200/80 shadow-2xs z-0"
      data-testid="report-map-container"
      role="region"
      aria-label="Waste report location map"
    >
      <MapContainer
        center={[latitude, longitude]}
        zoom={15}
        scrollWheelZoom={false}
        className="h-80 w-full"
        attributionControl={true}
      >
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer">OpenStreetMap</a> contributors'
          url="https://tile.openstreetmap.org/{z}/{x}/{y}.png"
          maxZoom={19}
        />
        <Marker position={[latitude, longitude]} icon={customMarkerIcon}>
          <Popup>
            <div className="text-xs max-w-[200px]">
              <p className="font-semibold text-slate-900">Citizen Report Location</p>
              {addressText && (
                <p className="text-slate-600 mt-1 line-clamp-2">{addressText}</p>
              )}
              <p className="font-mono text-[11px] text-emerald-700 mt-1 font-medium">
                {latitude.toFixed(5)}, {longitude.toFixed(5)}
              </p>
            </div>
          </Popup>
        </Marker>
      </MapContainer>
    </div>
  );
};
