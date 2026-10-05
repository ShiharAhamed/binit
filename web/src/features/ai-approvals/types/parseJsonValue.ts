import type { JsonValue } from './agentWorkflow';

/**
 * Parses a persisted workflow JSON value without allowing malformed legacy data
 * to break a future workflow screen. The public API currently returns JSON
 * objects, while the string branch supports older persisted representations.
 */
export const parseJsonValue = <T>(value: JsonValue | undefined): T | null => {
  if (value === undefined || value === null) {
    return null;
  }

  if (typeof value === 'string') {
    try {
      return JSON.parse(value) as T;
    } catch {
      return null;
    }
  }

  if (typeof value === 'object') {
    return value as T;
  }

  return null;
};
