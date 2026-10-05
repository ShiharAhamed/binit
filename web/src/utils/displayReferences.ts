const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export const reportReference = (id: string, authoritative?: string | null): string => {
  if (authoritative?.trim()) return authoritative.trim();
  return uuidPattern.test(id) ? id.slice(0, 8).toUpperCase() : id;
};

export const reportLabel = (id: string, authoritative?: string | null): string =>
  `Report ${reportReference(id, authoritative)}`;

export const needLabel = (
  targetType: 'Report' | 'Bin',
  id: string,
  binCodes?: Record<string, string>,
): string => {
  if (targetType === 'Report') return reportLabel(id);
  const code = binCodes?.[id]?.trim();
  return code || (uuidPattern.test(id) ? `Bin ${id.slice(0, 8).toUpperCase()}` : 'Waste bin');
};

/** Presentation-only fallback for prose persisted before display metadata existed. */
export const humanizeSourceMentions = (text: string, binCodes?: Record<string, string>): string =>
  text.replace(/\b(Report|Bin)\s+([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\b/gi,
    (_match, type: string, id: string) => type.toLowerCase() === 'report'
      ? reportLabel(id)
      : needLabel('Bin', id, binCodes));
