import { beforeEach, describe, expect, it, vi } from 'vitest';
import { axiosClient } from '../../../api/axiosClient';
import { agentWorkflowApi } from './agentWorkflowApi';

vi.mock('../../../api/axiosClient', () => ({
  axiosClient: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

describe('agentWorkflowApi', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the shared authenticated client for list, detail, and history routes', async () => {
    (axiosClient.get as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({ data: { items: [], page: 2, pageSize: 10, totalCount: 0, totalPages: 0 } })
      .mockResolvedValueOnce({ data: { id: 'workflow-1' } })
      .mockResolvedValueOnce({ data: [] });

    await agentWorkflowApi.listWorkflows({ page: 2, pageSize: 10, status: 'AwaitingDispatchApproval' });
    await agentWorkflowApi.getWorkflow('workflow-1');
    await agentWorkflowApi.getWorkflowHistory('workflow-1');

    expect(axiosClient.get).toHaveBeenNthCalledWith(1, '/agent-workflows', {
      params: { page: 2, pageSize: 10, status: 'AwaitingDispatchApproval' },
    });
    expect(axiosClient.get).toHaveBeenNthCalledWith(2, '/agent-workflows/workflow-1');
    expect(axiosClient.get).toHaveBeenNthCalledWith(3, '/agent-workflows/workflow-1/history');
  });

  it('keeps workflow creation and start as separate ASP.NET actions', async () => {
    (axiosClient.post as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({ data: { id: 'workflow-1' } })
      .mockResolvedValueOnce({ data: { id: 'workflow-1' } });

    await agentWorkflowApi.createWorkflow({ objective: 'Plan the next municipal collection cycle.' });
    await agentWorkflowApi.startWorkflow('workflow-1');

    expect(axiosClient.post).toHaveBeenNthCalledWith(1, '/agent-workflows', {
      objective: 'Plan the next municipal collection cycle.',
    });
    expect(axiosClient.post).toHaveBeenNthCalledWith(2, '/agent-workflows/workflow-1/start');
  });

  it('sends exact collection decision and execution payloads without auto-chaining', async () => {
    (axiosClient.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { id: 'workflow-1' } });
    await agentWorkflowApi.approveCollectionPlan('workflow-1', { expectedVersion: 3, reason: 'Ready to schedule.' });
    await agentWorkflowApi.requestCollectionRevision('workflow-1', { expectedVersion: 4, reason: 'Resequence the urgent bin.' });
    await agentWorkflowApi.rejectCollectionPlan('workflow-1', { expectedVersion: 5, reason: 'Insufficient coverage.' });
    await agentWorkflowApi.executeCollectionPlan('workflow-1', { expectedVersion: 6 });

    expect(axiosClient.post).toHaveBeenNthCalledWith(1, '/agent-workflows/workflow-1/collection-approval/approve', { expectedVersion: 3, reason: 'Ready to schedule.' });
    expect(axiosClient.post).toHaveBeenNthCalledWith(2, '/agent-workflows/workflow-1/collection-approval/request-revision', { expectedVersion: 4, reason: 'Resequence the urgent bin.' });
    expect(axiosClient.post).toHaveBeenNthCalledWith(3, '/agent-workflows/workflow-1/collection-approval/reject', { expectedVersion: 5, reason: 'Insufficient coverage.' });
    expect(axiosClient.post).toHaveBeenNthCalledWith(4, '/agent-workflows/workflow-1/execute-collection-plan', { expectedVersion: 6 });
  });

  it('sends exact dispatch decision and execution payloads through ASP.NET only', async () => {
    (axiosClient.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { id: 'workflow-1' } });
    await agentWorkflowApi.approveDispatchPlan('workflow-1', { expectedVersion: 7, reason: 'Approved after review.', acknowledgeWarnings: true });
    await agentWorkflowApi.requestDispatchRevision('workflow-1', { expectedVersion: 8, reason: 'Use a compatible vehicle.' });
    await agentWorkflowApi.rejectDispatchPlan('workflow-1', { expectedVersion: 9, reason: 'Capacity risk remains.' });
    await agentWorkflowApi.executeDispatchPlan('workflow-1', { expectedVersion: 10 });

    expect(axiosClient.post).toHaveBeenNthCalledWith(1, '/agent-workflows/workflow-1/dispatch-approval/approve', { expectedVersion: 7, reason: 'Approved after review.', acknowledgeWarnings: true });
    expect(axiosClient.post).toHaveBeenNthCalledWith(2, '/agent-workflows/workflow-1/dispatch-approval/request-revision', { expectedVersion: 8, reason: 'Use a compatible vehicle.' });
    expect(axiosClient.post).toHaveBeenNthCalledWith(3, '/agent-workflows/workflow-1/dispatch-approval/reject', { expectedVersion: 9, reason: 'Capacity risk remains.' });
    expect(axiosClient.post).toHaveBeenNthCalledWith(4, '/agent-workflows/workflow-1/execute-dispatch-plan', { expectedVersion: 10 });
  });
});
