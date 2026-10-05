import React from 'react';
import { MapContainer, TileLayer, Marker, Popup } from 'react-leaflet';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';

export interface ComplaintLocationMapProps {
  latitude?: number | null;
  longitude?: number | null;
  locationDescription?: string | null;
  subject?: string;
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

export const ComplaintLocationMap: React.FC<ComplaintLocationMapProps> = ({
  latitude,
  longitude,
  locationDescription,
  subject,
}) => {
  const hasCoords =
    typeof latitude === 'number' &&
    typeof longitude === 'number' &&
    !isNaN(latitude) &&
    !isNaN(longitude);

  const isValidCoord =
    hasCoords &&
    latitude! >= -90 &&
    latitude! <= 90 &&
    longitude! >= -180 &&
    longitude! <= 180;

  if (!hasCoords) {
    return (
      <div
        className="rounded-lg bg-slate-50 border border-slate-200/80 p-5 text-center text-slate-500"
        data-testid="complaint-no-location"
      >
        <div className="w-10 h-10 rounded-full bg-slate-100 border border-slate-200 flex items-center justify-center mx-auto mb-2 text-slate-400">
          <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
          </svg>
        </div>
        <p className="text-sm font-medium text-slate-700">No location was provided with this complaint.</p>
        {locationDescription ? (
          <p className="text-xs text-slate-600 mt-2 bg-white px-3 py-2 rounded border border-slate-200/80 inline-block max-w-full text-left font-normal">
            <span className="font-semibold text-slate-700">Description: </span>
            {locationDescription}
          </p>
        ) : (
          <p className="text-xs text-slate-400 mt-1">Coordinates and location notes were omitted by the citizen.</p>
        )}
      </div>
    );
  }

  if (!isValidCoord) {
    return (
      <div
        className="h-64 w-full rounded-lg bg-slate-50 border border-slate-200 flex flex-col items-center justify-center text-slate-500 p-4"
        data-testid="complaint-invalid-location"
        role="alert"
      >
        <p className="text-sm font-semibold text-slate-700">Invalid Location Coordinates</p>
        <p className="text-xs text-slate-500 mt-1">
          Geographic coordinates are outside valid bounds ({latitude}, {longitude}).
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-2">
      <div
        className="relative rounded-lg overflow-hidden border border-slate-200/80 shadow-2xs z-0"
        data-testid="complaint-map-container"
        role="region"
        aria-label="Complaint reported location map"
      >
        <MapContainer
          center={[latitude!, longitude!]}
          zoom={15}
          scrollWheelZoom={false}
          className="h-72 sm:h-80 w-full"
          attributionControl={true}
        >
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer">OpenStreetMap</a> contributors'
            url="https://tile.openstreetmap.org/{z}/{x}/{y}.png"
            maxZoom={19}
          />
          <Marker position={[latitude!, longitude!]} icon={customMarkerIcon}>
            <Popup>
              <div className="text-xs max-w-[220px]">
                <p className="font-semibold text-slate-900">{subject || 'Reported Issue Location'}</p>
                {locationDescription && (
                  <p className="text-slate-600 mt-1 line-clamp-2">{locationDescription}</p>
                )}
                <p className="font-mono text-[11px] text-emerald-700 mt-1 font-medium">
                  {latitude!.toFixed(5)}, {longitude!.toFixed(5)}
                </p>
              </div>
            </Popup>
          </Marker>
        </MapContainer>
      </div>

      {locationDescription && (
        <div className="text-xs text-slate-600 bg-slate-50 px-3 py-2 rounded-lg border border-slate-200/80 flex items-start gap-2">
          <svg className="w-4 h-4 text-emerald-600 shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
          </svg>
          <div>
            <span className="font-medium text-slate-700">Location Note: </span>
            {locationDescription}
          </div>
        </div>
      )}
    </div>
  );
};
