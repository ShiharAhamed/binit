import { useEffect, useMemo, useState } from 'react';
import { MapContainer, Marker, Polyline, Popup, TileLayer, useMap } from 'react-leaflet';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import type { CollectionMapStop } from '../types/assignments';

export interface CollectionStopsMapProps {
  stops: CollectionMapStop[];
  selectedStopId?: string | null;
  onStopSelect?: (stopId: string) => void;
  showSequenceLine?: boolean;
}

interface ValidCollectionMapStop extends CollectionMapStop {
  latitude: number;
  longitude: number;
}

export const isValidCollectionStopLocation = (latitude: unknown, longitude: unknown): latitude is number =>
  typeof latitude === 'number' && Number.isFinite(latitude) && latitude >= -90 && latitude <= 90
  && typeof longitude === 'number' && Number.isFinite(longitude) && longitude >= -180 && longitude <= 180;

const stopStatusLabel = (status: string) => status.replace(/([a-z])([A-Z])/g, '$1 $2');

const stopMarkerIcon = (sequence: number, selected: boolean) => L.divIcon({
  className: 'smartwaste-collection-stop-marker',
  html: `<span data-sequence="${sequence}" class="flex h-9 w-9 items-center justify-center rounded-full border-2 border-white text-sm font-bold text-white shadow-md ${selected ? 'bg-emerald-800 ring-4 ring-emerald-200' : 'bg-emerald-600'}">${sequence}</span>`,
  iconSize: [36, 36],
  iconAnchor: [18, 18],
  popupAnchor: [0, -18],
});

const MapBounds: React.FC<{ positions: [number, number][]; boundsKey: string }> = ({ positions, boundsKey }) => {
  const map = useMap();

  useEffect(() => {
    if (positions.length === 1) {
      map.setView(positions[0], 15);
      return;
    }

    map.fitBounds(L.latLngBounds(positions), {
      padding: [32, 32],
      maxZoom: 15,
    });
  }, [boundsKey, map, positions]);

  return null;
};

const SharedLocationNotice: React.FC<{ stops: ValidCollectionMapStop[] }> = ({ stops }) => {
  const groups = useMemo(() => {
    const groupedStops = new Map<string, ValidCollectionMapStop[]>();
    stops.forEach((stop) => {
      const key = `${stop.latitude},${stop.longitude}`;
      groupedStops.set(key, [...(groupedStops.get(key) ?? []), stop]);
    });
    return [...groupedStops.values()].filter((group) => group.length > 1);
  }, [stops]);

  if (groups.length === 0) return null;

  return <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900" data-testid="shared-location-notice">
    <p className="font-semibold">Multiple stops share a location</p>
    <ul className="mt-1 list-inside list-disc space-y-0.5">
      {groups.map((group) => <li key={group.map((stop) => stop.id).join('-')}>
        {group.map((stop) => `#${stop.sequence} ${stop.label}`).join(', ')}
      </li>)}
    </ul>
  </div>;
};

export const CollectionStopsMap: React.FC<CollectionStopsMapProps> = ({
  stops,
  selectedStopId = null,
  onStopSelect,
  showSequenceLine = true,
}) => {
  const [tilesLoaded, setTilesLoaded] = useState(false);
  const validStops = useMemo(() => stops
    .filter((stop): stop is ValidCollectionMapStop => isValidCollectionStopLocation(stop.latitude, stop.longitude))
    .sort((left, right) => left.sequence - right.sequence), [stops]);
  const boundsKey = useMemo(() => validStops
    .map((stop) => `${stop.id}:${stop.latitude}:${stop.longitude}`)
    .sort()
    .join('|'), [validStops]);
  const boundsPositions = useMemo(() => validStops.map((stop) => [stop.latitude, stop.longitude] as [number, number]), [boundsKey]);

  if (validStops.length === 0) return <div className="flex h-72 w-full flex-col items-center justify-center rounded-xl border border-slate-200 bg-slate-50 p-6 text-center" data-testid="collection-stops-map-empty" role="status">
    <p className="text-sm font-semibold text-slate-700">Collection locations unavailable</p>
    <p className="mt-1 max-w-sm text-xs leading-5 text-slate-500">No valid stop coordinates are available to display on the map.</p>
  </div>;

  const center: [number, number] = [validStops[0].latitude, validStops[0].longitude];
  const sequencePositions = validStops.map((stop) => [stop.latitude, stop.longitude] as [number, number]);

  return <div className="space-y-2" data-testid="collection-stops-map">
    <div className="relative z-0 h-80 w-full overflow-hidden rounded-xl border border-slate-200/80 shadow-2xs sm:h-96" role="region" aria-label="Collection stops map">
      <MapContainer center={center} zoom={15} scrollWheelZoom={false} className="h-full w-full" attributionControl>
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer">OpenStreetMap</a> contributors'
          url="https://tile.openstreetmap.org/{z}/{x}/{y}.png"
          maxZoom={19}
          eventHandlers={{ load: () => setTilesLoaded(true), tileerror: () => setTilesLoaded(true) }}
        />
        <MapBounds positions={boundsPositions} boundsKey={boundsKey} />
        {showSequenceLine && validStops.length > 1 && <Polyline positions={sequencePositions} pathOptions={{ color: '#047857', dashArray: '6 8', weight: 3, opacity: 0.75 }} />}
        {validStops.map((stop) => <Marker
          key={stop.id}
          position={[stop.latitude, stop.longitude]}
          icon={stopMarkerIcon(stop.sequence, stop.id === selectedStopId)}
          alt={`Stop ${stop.sequence}: ${stop.label}`}
          eventHandlers={onStopSelect ? { click: () => onStopSelect(stop.id) } : undefined}
        >
          <Popup>
            <div className="min-w-44 text-xs">
              <p className="font-semibold text-slate-900">Stop {stop.sequence}: {stop.label}</p>
              {stop.targetType && <p className="mt-1 text-slate-600">{stop.targetType} collection</p>}
              {stop.addressText?.trim() && <p className="mt-1 text-slate-600">{stop.addressText}</p>}
              {stop.status && <p className="mt-1 font-medium text-emerald-800">Status: {stopStatusLabel(stop.status)}</p>}
            </div>
          </Popup>
        </Marker>)}
      </MapContainer>
      {!tilesLoaded && <span className="sr-only" aria-live="polite">Loading map tiles</span>}
    </div>
    {showSequenceLine && validStops.length > 1 && <p className="text-xs text-slate-500">Stop sequence — not driving directions</p>}
    <SharedLocationNotice stops={validStops} />
  </div>;
};
