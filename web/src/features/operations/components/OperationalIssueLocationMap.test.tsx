import React from 'react';
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { OperationalIssueLocationMap } from './OperationalIssueLocationMap';

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

describe('OperationalIssueLocationMap', () => {
  it('renders MapContainer when coordinates are valid', () => {
    render(
      <OperationalIssueLocationMap
        latitude={6.9271}
        longitude={79.8612}
        title="Hydraulic leak"
        locationDescription="Near Depot 3 entrance"
      />
    );

    expect(screen.getByTestId('operational-issue-map-container')).toBeInTheDocument();
    expect(screen.getByTestId('leaflet-map-container')).toBeInTheDocument();
    expect(screen.getByText('Location Note:')).toBeInTheDocument();
    expect(screen.getAllByText('Near Depot 3 entrance').length).toBeGreaterThanOrEqual(1);
  });

  it('renders no-location placeholder when coordinates are null', () => {
    render(<OperationalIssueLocationMap latitude={null} longitude={null} />);

    expect(screen.queryByTestId('operational-issue-map-container')).not.toBeInTheDocument();
    expect(screen.getByTestId('operational-issue-no-location')).toBeInTheDocument();
    expect(
      screen.getByText('No location was provided for this issue.')
    ).toBeInTheDocument();
  });

  it('renders locationDescription even when coordinates are absent', () => {
    render(
      <OperationalIssueLocationMap
        latitude={null}
        longitude={null}
        locationDescription="Behind Central Market garage"
      />
    );

    expect(screen.getByTestId('operational-issue-no-location')).toBeInTheDocument();
    expect(screen.getByText('Behind Central Market garage')).toBeInTheDocument();
  });

  it('renders error placeholder when coordinates are out of bounds', () => {
    render(<OperationalIssueLocationMap latitude={999} longitude={79.8612} />);

    expect(screen.getByTestId('operational-issue-invalid-location')).toBeInTheDocument();
    expect(screen.getByText('Invalid Location Coordinates')).toBeInTheDocument();
  });
});
