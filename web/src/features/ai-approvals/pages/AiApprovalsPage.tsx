import React, { useMemo, useState } from 'react';
import axios from 'axios';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { Button } from '../../../components/ui/Button';
import { Card, CardDescription, CardHeader, CardTitle } from '../../../components/ui/Card';
import { AiWorkflowIcon, LeafIcon } from '../../../components/ui/Icons';
import { LoadingSpinner } from '../../../components/ui/LoadingSpinner';
import { useAuthStore } from '../../../store/authStore';
import { agentWorkflowApi } from '../api/agentWorkflowApi';
import { NewOperationDialog } from '../components/NewOperationDialog';
import { WorkflowTable } from '../components/WorkflowTable';
import type { AgentWorkflowSummary } from '../types/agentWorkflow';
import { getAiApprovalsBasePath, getTriggerReportLabel, isWorkflowInProgress, matchesWorkflowFilter, needsHumanAttention, type WorkflowDisplayFilter } from '../utils/workflowPresentation';

const pageSize = 20;
const emptyWorkflows: AgentWorkflowSummary[] = [];

class StartWorkflowError extends Error {
  readonly workflow: AgentWorkflowSummary;
  readonly originalError: unknown;

  constructor(workflow: AgentWorkflowSummary, originalError: unknown) {
    super('The workflow was created but could not be started.');
    this.workflow = workflow;
    this.originalError = originalError;
  }
}

const getErrorMessage = (error: unknown): string => {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data;
    if (data && typeof data === 'object') {
      const detail = (data as Record<string, unknown>).detail;
      const title = (data as Record<string, unknown>).title;
      if (typeof detail === 'string') return detail;
      if (typeof title === 'string') return title;
    }
    if (error.response?.status === 409) return 'The workflow changed before it could be started. Refresh the list and try again.';
  }
  return 'Unable to start the collection operation. Please try again.';
};

/** Shared dashboard for equal-authority MunicipalManager and WasteOfficer users. */
export const AiApprovalsPage: React.FC = () => {
  const { user } = useAuthStore();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const workflowBasePath = getAiApprovalsBasePath(user?.role);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState<WorkflowDisplayFilter>('all');
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [startFailure, setStartFailure] = useState<StartWorkflowError | null>(null);
  const workflowsQuery = useQuery({ queryKey: ['agent-workflows', page], queryFn: () => agentWorkflowApi.listWorkflows({ page, pageSize }) });

  const startOperation = useMutation({
    mutationFn: async (objective: string) => {
      const created = await agentWorkflowApi.createWorkflow({ objective });
      try { return await agentWorkflowApi.startWorkflow(created.id); } catch (error) { throw new StartWorkflowError(created, error); }
    },
    onSuccess: async (workflow) => {
      setStartFailure(null);
      await queryClient.invalidateQueries({ queryKey: ['agent-workflows'] });
      navigate(`${workflowBasePath}/${workflow.id}`);
    },
    onError: async (error) => {
      setStartFailure(error instanceof StartWorkflowError ? error : null);
      await queryClient.invalidateQueries({ queryKey: ['agent-workflows'] });
    },
  });

  const pageWorkflows = workflowsQuery.data?.items ?? emptyWorkflows;
  const visibleWorkflows = useMemo(() => {
    const normalizedSearch = search.trim().toLocaleLowerCase();
    return pageWorkflows.filter((workflow) => matchesWorkflowFilter(workflow.status, filter) && (!normalizedSearch || workflow.objective.toLocaleLowerCase().includes(normalizedSearch) || getTriggerReportLabel(workflow)?.toLocaleLowerCase().includes(normalizedSearch)));
  }, [filter, pageWorkflows, search]);
  const pageSummary = useMemo(() => ({
    attention: pageWorkflows.filter((workflow) => needsHumanAttention(workflow.status)).length,
    inProgress: pageWorkflows.filter((workflow) => isWorkflowInProgress(workflow.status)).length,
    completed: pageWorkflows.filter((workflow) => workflow.status === 'Completed').length,
  }), [pageWorkflows]);

  return (
    <div className="space-y-6">
      <div className="border-b border-slate-200/80 pb-5">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <div className="mb-1 flex items-center gap-1.5 text-xs font-medium text-slate-400"><span>Operations</span><span>/</span><span className="font-semibold text-slate-600">AI Approvals</span></div>
            <h1 className="flex items-center gap-2 text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl"><span>AI Approvals</span><AiWorkflowIcon className="h-6 w-6 shrink-0 text-emerald-600" /></h1>
            <p className="mt-1 text-xs leading-relaxed text-slate-500 sm:text-sm">Monitor Agentic AI workflows and manage collection operations.</p>
          </div>
          <Button type="button" onClick={() => setIsDialogOpen(true)} className="shrink-0">New End-to-End Collection Operation</Button>
        </div>
      </div>

      <section aria-label="Workflow summary" className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Card className="p-4"><CardDescription>Total workflows</CardDescription><p className="mt-1 text-2xl font-bold text-slate-900">{workflowsQuery.data?.totalCount ?? '—'}</p></Card>
        <Card className="p-4"><CardDescription>Needs attention · this page</CardDescription><p className="mt-1 text-2xl font-bold text-amber-700">{workflowsQuery.isLoading ? '—' : pageSummary.attention}</p></Card>
        <Card className="p-4"><CardDescription>In progress · this page</CardDescription><p className="mt-1 text-2xl font-bold text-slate-900">{workflowsQuery.isLoading ? '—' : pageSummary.inProgress}</p></Card>
        <Card className="p-4"><CardDescription>Workflow Completed · this page</CardDescription><p className="mt-1 text-2xl font-bold text-emerald-700">{workflowsQuery.isLoading ? '—' : pageSummary.completed}</p></Card>
      </section>

      <Card className="overflow-hidden p-0">
        <CardHeader className="mb-0 border-b border-slate-100 p-5 sm:p-6">
          <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between"><div><CardTitle>Agentic workflows</CardTitle><CardDescription>Newest workflows first. Search and category filters apply to the current server page.</CardDescription></div><div className="flex flex-col gap-2 sm:flex-row"><input aria-label="Search workflows" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search objectives" className="rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-emerald-500 focus:outline-none focus:ring-2 focus:ring-emerald-500" /><select aria-label="Workflow status filter" value={filter} onChange={(event) => setFilter(event.target.value as WorkflowDisplayFilter)} className="rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-emerald-500 focus:outline-none focus:ring-2 focus:ring-emerald-500"><option value="all">All workflows</option><option value="attention">Needs attention</option><option value="inProgress">In progress</option><option value="completed">Workflow Completed</option><option value="terminal">Revision, failed, or rejected</option></select></div></div>
        </CardHeader>
        {workflowsQuery.isLoading ? <div role="status" className="flex min-h-56 items-center justify-center gap-3 text-sm text-slate-500"><LoadingSpinner size="md" label="Loading workflows" />Loading workflows…</div>
          : workflowsQuery.isError ? <div className="m-5 rounded-lg border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800"><p>Unable to load AI workflows.</p><Button type="button" variant="secondary" size="sm" onClick={() => workflowsQuery.refetch()} className="mt-3">Try again</Button></div>
          : pageWorkflows.length === 0 ? <div className="flex min-h-56 flex-col items-center justify-center px-6 text-center"><LeafIcon className="h-8 w-8 text-emerald-600" /><p className="mt-3 font-semibold text-slate-900">No AI workflows yet.</p><p className="mt-1 text-sm text-slate-500">Start an end-to-end collection operation to create the first workflow.</p></div>
          : visibleWorkflows.length === 0 ? <div className="min-h-40 px-6 py-12 text-center"><p className="font-semibold text-slate-900">No workflows match the current filters.</p><Button type="button" variant="secondary" size="sm" className="mt-3" onClick={() => { setSearch(''); setFilter('all'); }}>Clear filters</Button></div>
          : <WorkflowTable workflows={visibleWorkflows} workflowBasePath={workflowBasePath} role={user?.role} />}
        {!workflowsQuery.isLoading && !workflowsQuery.isError && (workflowsQuery.data?.totalPages ?? 0) > 1 && <div className="flex items-center justify-between border-t border-slate-100 px-5 py-3 text-sm text-slate-600"><span>Page {workflowsQuery.data?.page} of {workflowsQuery.data?.totalPages}</span><div className="flex gap-2"><Button type="button" variant="secondary" size="sm" disabled={page <= 1} onClick={() => setPage((current) => current - 1)}>Previous</Button><Button type="button" variant="secondary" size="sm" disabled={page >= (workflowsQuery.data?.totalPages ?? 1)} onClick={() => setPage((current) => current + 1)}>Next</Button></div></div>}
      </Card>

      <NewOperationDialog
        isOpen={isDialogOpen}
        isSubmitting={startOperation.isPending}
        errorMessage={startOperation.isError ? (startFailure ? `${getErrorMessage(startFailure.originalError)} The workflow was created and remains available.` : getErrorMessage(startOperation.error)) : null}
        createdWorkflowId={startFailure?.workflow.id ?? null}
        workflowBasePath={workflowBasePath}
        onClose={() => { if (!startOperation.isPending) { setIsDialogOpen(false); setStartFailure(null); startOperation.reset(); } }}
        onSubmit={(objective) => { setStartFailure(null); startOperation.mutate(objective); }}
      />
    </div>
  );
};

export default AiApprovalsPage;
