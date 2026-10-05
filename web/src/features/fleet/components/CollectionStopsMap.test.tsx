import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { CollectionMapStop } from '../types/assignments';
import { CollectionStopsMap } from './CollectionStopsMap';

const fitBounds = vi.fn();
const setView = vi.fn();

vi.mock('leaflet', () => ({
  default: {
    divIcon: vi.fn((options) => ({ options })),
    latLngBounds: vi.fn((points) => points),
  },
}));

vi.mock('react-leaflet', () => ({
  MapContainer: ({ children }: { children: React.ReactNode }) => <div data-testid="map-container">{children}</div>,
  TileLayer: () => <div data-testid="tile-layer" />,
  Popup: ({ children }: { children: React.ReactNode }) => <div data-testid="stop-popup">{children}</div>,
  Marker: ({ alt, children, eventHandlers, icon }: { alt?: string; children: React.ReactNode; eventHandlers?: { click?: () => void }; icon: { options: { html: string } } }) => {
    const sequence = icon.options.html.match(/data-sequence="(.*?)"/)?.[1];
    return <button type="button" aria-label={alt} data-testid="collection-stop-marker" data-sequence={sequence} data-selected={icon.options.html.includes('bg-emerald-800')} onClick={eventHandlers?.click}>{children}</button>;
  },
  Polyline: ({ positions }: { positions: Array<[number, number]> }) => <div data-testid="sequence-line" data-positions={JSON.stringify(positions)} />,
  useMap: () => ({ fitBounds, setView }),
}));

const stops: CollectionMapStop[] = [
  { id: 'report-stop', sequence: 2, label: 'Report SW-002', latitude: 6.9271, longitude: 79.8612, targetType: 'Report', addressText: '2 Green Street', status: 'Pending' },
  { id: 'bin-stop', sequence: 1, label: 'Bin BIN-001', latitude: 6.928, longitude: 79.862, targetType: 'Bin', addressText: '1 Green Street', status: 'Completed' },
];

describe('CollectionStopsMap', () => {
  beforeEach(() => {
    fitBounds.mockClear();
    setView.mockClear();
  });

  it('renders numbered markers and popup details in the supplied sequence order', () => {
    render(<CollectionStopsMap stops={stops} />);

    expect(screen.getAllByTestId('collection-stop-marker').map((marker) => marker.getAttribute('data-sequence'))).toEqual(['1', '2']);
    expect(screen.getByText('Stop 1: Bin BIN-001')).toBeInTheDocument();
    expect(screen.getByText('1 Green Street')).toBeInTheDocument();
    expect(screen.getByText('Status: Completed')).toBeInTheDocument();
  });

  it('updates numbered markers and the sequence line when stop sequences change', () => {
    const { rerender } = render(<CollectionStopsMap stops={stops} />);
    rerender(<CollectionStopsMap stops={[{ ...stops[0], sequence: 1 }, { ...stops[1], sequence: 2 }]} />);

    expect(screen.getAllByTestId('collection-stop-marker').map((marker) => marker.getAttribute('data-sequence'))).toEqual(['1', '2']);
    expect(screen.getAllByTestId('collection-stop-marker')[0]).toHaveAccessibleName('Stop 1: Report SW-002');
    expect(screen.getByTestId('sequence-line')).toHaveAttribute('data-positions', JSON.stringify([[6.9271, 79.8612], [6.928, 79.862]]));
    expect(screen.getByText('Stop sequence — not driving directions')).toBeInTheDocument();
  });

  it('keeps valid markers when another stop has invalid coordinates', () => {
    render(<CollectionStopsMap stops={[...stops, { id: 'invalid', sequence: 3, label: 'Unavailable', latitude: null, longitude: 79.86 }]} />);

    expect(screen.getAllByTestId('collection-stop-marker')).toHaveLength(2);
    expect(screen.queryByLabelText('Stop 3: Unavailable')).not.toBeInTheDocument();
  });

  it('shows an empty state when no valid coordinates are supplied', () => {
    render(<CollectionStopsMap stops={[{ id: 'invalid', sequence: 1, label: 'Unavailable', latitude: null, longitude: undefined }]} />);

    expect(screen.getByTestId('collection-stops-map-empty')).toBeInTheDocument();
    expect(screen.queryByTestId('map-container')).not.toBeInTheDocument();
  });

  it('supports controlled marker selection', () => {
    const onStopSelect = vi.fn();
    render(<CollectionStopsMap stops={stops} selectedStopId="bin-stop" onStopSelect={onStopSelect} />);

    expect(screen.getByLabelText('Stop 1: Bin BIN-001')).toHaveAttribute('data-selected', 'true');
    fireEvent.click(screen.getByLabelText('Stop 2: Report SW-002'));
    expect(onStopSelect).toHaveBeenCalledWith('report-stop');
  });

  it('keeps all stops sharing a coordinate and identifies them accessibly', () => {
    render(<CollectionStopsMap stops={[
      { ...stops[0], sequence: 1, latitude: 6.9271, longitude: 79.8612 },
      { ...stops[1], sequence: 2, latitude: 6.9271, longitude: 79.8612 },
    ]} />);

    expect(screen.getAllByTestId('collection-stop-marker')).toHaveLength(2);
    expect(screen.getByTestId('shared-location-notice')).toHaveTextContent('#1 Report SW-002, #2 Bin BIN-001');
  });
});
