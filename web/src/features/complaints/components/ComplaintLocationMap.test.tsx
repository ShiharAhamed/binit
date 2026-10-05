import React from 'react';
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ComplaintLocationMap } from './ComplaintLocationMap';

vi.mock('leaflet', () => ({
  default: {
    divIcon: vi.fn((options) => ({ options })),
  },
}));

vi.mock('react-leaflet', () => ({
  MapContainer: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="leaflet-map-container">{children}</div>
  ),
  TileLayer: () => <div data-testid="tile-layer" />,
  Marker: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="map-marker">{children}</div>
  ),
  Popup: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="map-popup">{children}</div>
  ),
}));

describe('ComplaintLocationMap', () => {
  it('renders read-only map container when valid latitude and longitude are provided', () => {
    render(
      <ComplaintLocationMap
        latitude={6.9271}
        longitude={79.8612}
        subject="Missed Organic Bin"
        locationDescription="Near main junction"
      />
    );

    expect(screen.getByTestId('complaint-map-container')).toBeInTheDocument();
    expect(screen.getByTestId('leaflet-map-container')).toBeInTheDocument();
    expect(screen.getByTestId('map-marker')).toBeInTheDocument();
    expect(screen.getByText(/Location Note:/i)).toBeInTheDocument();
    expect(screen.getAllByText('Near main junction').length).toBeGreaterThanOrEqual(1);
  });

  it('renders no-location placeholder when coordinates are null', () => {
    render(
      <ComplaintLocationMap
        latitude={null}
        longitude={null}
        locationDescription="Near market gate"
      />
    );

    expect(screen.queryByTestId('complaint-map-container')).not.toBeInTheDocument();
    expect(screen.getByTestId('complaint-no-location')).toBeInTheDocument();
    expect(
      screen.getByText('No location was provided with this complaint.')
    ).toBeInTheDocument();
    expect(screen.getByText(/Near market gate/i)).toBeInTheDocument();
  });

  it('renders no-location placeholder when coordinates are undefined', () => {
    render(<ComplaintLocationMap />);

    expect(screen.queryByTestId('complaint-map-container')).not.toBeInTheDocument();
    const noLoc = screen.getByTestId('complaint-no-location');
    expect(noLoc).toBeInTheDocument();
    expect(
      screen.getByText('Coordinates and location notes were omitted by the citizen.')
    ).toBeInTheDocument();
  });

  it('renders invalid coordinates error when latitude or longitude is out of bounds', () => {
    render(<ComplaintLocationMap latitude={120} longitude={79.8} />);

    expect(screen.getByTestId('complaint-invalid-location')).toBeInTheDocument();
    expect(screen.getByText('Invalid Location Coordinates')).toBeInTheDocument();
  });
});
