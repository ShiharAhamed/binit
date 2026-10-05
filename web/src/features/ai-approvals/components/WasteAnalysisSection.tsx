import React from 'react';
import { humanizeSourceMentions, reportLabel } from '../../../utils/displayReferences';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../../components/ui/Card';
import type { AgentWorkflowStep } from '../types/agentWorkflow';
import { getWasteAnalysisOutput } from '../utils/workflowDetailPresentation';

const reportReference = (reportId: string): string => reportLabel(reportId);

export const WasteAnalysisSection: React.FC<{ step: AgentWorkflowStep | null; reportId?: string | null }> = ({ step, reportId }) => {
  const output = getWasteAnalysisOutput(step);
  const matchesTrigger = !reportId || (output.kind === 'value' && output.value.analyses.length === 1 && output.value.analyses[0].reportId.toLowerCase() === reportId.toLowerCase());
  return <Card><CardHeader><CardTitle>{reportId ? 'AI Advisory Analysis' : 'Waste Analysis'}</CardTitle><CardDescription>{reportId ? 'Analysis of the triggering report. Recommendations do not verify the report or set its final priority.' : 'AI analysis of verified waste reports. Recommendations are advisory.'}</CardDescription></CardHeader><CardContent>
    {!matchesTrigger && <p role="alert" className="text-sm text-rose-700">The saved analysis does not match the triggering report. Do not use it for verification guidance.</p>}
    {output.kind === 'missing' && <p className="text-sm text-slate-500">Waste Analysis result unavailable.</p>}
    {output.kind === 'invalid' && <p className="text-sm text-slate-500">Result unavailable or incompatible with the current format.</p>}
    {output.kind === 'value' && matchesTrigger && <div className="space-y-5">
      <div className="grid grid-cols-1 gap-3 text-sm sm:grid-cols-3"><div><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Reports analysed</p><p className="mt-1 font-semibold text-slate-900">{output.value.sourceTotalCount}</p></div><div><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Source page</p><p className="mt-1 font-semibold text-slate-900">{output.value.sourcePage}</p></div><div><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Page size</p><p className="mt-1 font-semibold text-slate-900">{output.value.sourcePageSize}</p></div></div>
      {output.value.status === 'empty' ? <p className="rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">No verified waste reports were available for analysis.</p> : <ul className="space-y-4">{output.value.analyses.map((analysis) => <li key={analysis.reportId} className="rounded-lg border border-slate-200 p-4"><div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between"><div><h3 className="font-semibold text-slate-900">{reportReference(analysis.reportId)}</h3><p className="mt-1 text-sm text-slate-600">{humanizeSourceMentions(analysis.categoryAssessment)}</p></div><div className="flex gap-2 text-xs"><span className="rounded-full bg-slate-100 px-2.5 py-1 font-semibold text-slate-700">Advisory Priority: {analysis.recommendedPriority}</span><span className="rounded-full bg-slate-100 px-2.5 py-1 font-semibold text-slate-700">Confidence: {analysis.confidence}</span></div></div><div className="mt-4 grid gap-4 text-sm sm:grid-cols-2"><div><p className="font-semibold text-slate-700">Suggested handling</p><p className="mt-1 text-slate-600">{humanizeSourceMentions(analysis.recommendedHandling)}</p></div><div><p className="font-semibold text-slate-700">Operational concerns</p>{analysis.operationalConcerns.length ? <ul className="mt-1 list-disc space-y-1 pl-5 text-slate-600">{analysis.operationalConcerns.map((concern) => <li key={concern}>{humanizeSourceMentions(concern)}</li>)}</ul> : <p className="mt-1 text-slate-500">No recorded concerns.</p>}</div></div><p className="mt-4 border-t border-slate-100 pt-3 text-sm text-slate-600"><span className="font-semibold text-slate-700">Assessment rationale: </span>{humanizeSourceMentions(analysis.rationale)}</p></li>)}</ul>}
    </div>}
  </CardContent></Card>;
};
