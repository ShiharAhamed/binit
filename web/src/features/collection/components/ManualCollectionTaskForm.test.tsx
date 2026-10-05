import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ManualCollectionTaskForm, colomboLocalToUtcIso } from './ManualCollectionTaskForm';

const binNeed = { id: 'bin-1', targetType: 'Bin' as const, collectionReason: 'FullOrBlockedBin' as const, title: 'BIN-1', latitude: 6.93, longitude: 79.85, addressText: 'Main Street', wasteTypes: ['General'], urgency: 'High', triggerDate: '2026-09-20T10:00:00Z', attachmentCount: 0, binDetails: { binCode: 'BIN-1', capacityLiters: 660, latestFillLevelPercent: 100, latestCondition: 'Good' as const, observationAgeHours: 1 } };

describe('ManualCollectionTaskForm', () => {
  it('converts Asia/Colombo wall-clock time to UTC without a local-browser offset', () => {
    expect(colomboLocalToUtcIso('2099-01-01T10:00')).toBe('2099-01-01T04:30:00.000Z');
  });
  it('requires an OfficerDiscretion justification and preserves values when invalid', async () => {
    const user = userEvent.setup(); const submit = vi.fn();
    render(<ManualCollectionTaskForm need={binNeed} isSubmitting={false} onSubmit={submit} onCancel={vi.fn()} />);
    await user.selectOptions(screen.getByLabelText(/Collection reason/), 'OfficerDiscretion');
    await user.type(screen.getByLabelText(/Scheduled collection time/), '2099-01-01T10:00');
    await user.click(screen.getByRole('button', { name: 'Schedule Task' }));
    expect(screen.getByText(/at least 5 characters/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Scheduled collection time/)).toHaveValue('2099-01-01T10:00');
    expect(submit).not.toHaveBeenCalled();
  });
  it('sends the exact bin request shape and cancel does not submit', async () => {
    const user = userEvent.setup(); const submit = vi.fn(); const cancel = vi.fn();
    render(<ManualCollectionTaskForm need={binNeed} isSubmitting={false} onSubmit={submit} onCancel={cancel} />);
    await user.type(screen.getByLabelText(/Scheduled collection time/), '2099-01-01T10:00');
    await user.type(screen.getByLabelText(/Handling notes/), 'Use rear access');
    await user.click(screen.getByRole('button', { name: 'Schedule Task' }));
    expect(submit).toHaveBeenCalledWith({ wasteReportId: null, wasteBinId: 'bin-1', collectionReason: 'FullOrBlockedBin', scheduledAt: '2099-01-01T04:30:00.000Z', handlingNotes: 'Use rear access', schedulingReason: null });
    await user.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(cancel).toHaveBeenCalledOnce();
  });
  it('disables both actions while a scheduling request is pending', () => {
    render(<ManualCollectionTaskForm need={binNeed} isSubmitting={true} onSubmit={vi.fn()} onCancel={vi.fn()} />);
    expect(screen.getByRole('button', { name: /Schedule Task/ })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled();
  });
});
