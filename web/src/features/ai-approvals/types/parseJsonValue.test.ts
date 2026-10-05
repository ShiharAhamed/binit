import { describe, expect, it } from 'vitest';
import { parseJsonValue } from './parseJsonValue';

describe('parseJsonValue', () => {
  it('returns public API JSON objects without reparsing them', () => {
    expect(parseJsonValue<{ status: string }>({ status: 'completed' })).toEqual({ status: 'completed' });
  });

  it('parses legacy JSON strings and safely rejects malformed values', () => {
    expect(parseJsonValue<{ status: string }>('{"status":"completed"}')).toEqual({ status: 'completed' });
    expect(parseJsonValue('{not-json')).toBeNull();
    expect(parseJsonValue(null)).toBeNull();
  });
});
