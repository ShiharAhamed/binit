export const canReviewWasteReports = (role?: string): boolean =>
  role === 'MunicipalManager' || role === 'WasteOfficer';

export const getWasteReportDetailPath = (role: string | undefined, reportId: string): string =>
  `${role === 'MunicipalManager' ? '/manager/reports' : '/officer/waste-reports'}/${reportId}`;
