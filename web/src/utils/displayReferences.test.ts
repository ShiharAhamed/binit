import { describe, expect, it } from 'vitest';
import { humanizeSourceMentions, needLabel, reportLabel, reportReference } from './displayReferences';

describe('display-only report and bin references', () => {
  const reportId = 'c17add4f-79f3-4a0a-a9e0-150fde7d7827';
  const binId = 'a3711544-f9a1-4a59-b89e-df4fa9407f03';

  it('prefers an authoritative report reference and derives the same historical fallback', () => {
    expect(reportReference(reportId, 'C17ADD4F')).toBe('C17ADD4F');
    expect(reportLabel(reportId, 'C17ADD4F')).toBe('Report C17ADD4F');
    expect(reportLabel(reportId)).toBe('Report C17ADD4F');
  });

  it('uses the actual stored bin code and a safe fallback when absent', () => {
    expect(needLabel('Bin', binId, { [binId]: 'BIN-COL-0042' })).toBe('BIN-COL-0042');
    expect(needLabel('Bin', binId)).toBe('Bin A3711544');
    expect(needLabel('Bin', 'legacy-bin')).toBe('Waste bin');
    expect(needLabel('Report', reportId)).toBe('Report C17ADD4F');
  });

  it('humanizes historical source mentions without changing unrelated text', () => {
    expect(humanizeSourceMentions(`Review Report ${reportId} and Bin ${binId}.`, { [binId]: 'BIN-COL-0042' }))
      .toBe('Review Report C17ADD4F and BIN-COL-0042.');
    expect(humanizeSourceMentions(`Bin ${binId}`)).toBe('Bin A3711544');
  });
});
