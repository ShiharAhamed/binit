import { axiosClient } from '../../../api/axiosClient';
import type {
  AgentWorkflowDetail,
  AgentWorkflowListParams,
  AgentWorkflowSummary,
  AgentWorkflowTransition,
  ApproveCollectionPlanningRequest,
  ApproveDispatchPlanRequest,
  CreateAgentWorkflowRequest,
  ExecuteCollectionPlanRequest,
  ExecuteDispatchPlanRequest,
  PagedAgentWorkflows,
  RejectCollectionPlanningRequest,
  RejectDispatchPlanRequest,
  RequestCollectionRevisionRequest,
  RequestDispatchRevisionRequest,
} from '../types/agentWorkflow';

const workflowsPath = '/agent-workflows';

export const agentWorkflowApi = {
  createWorkflow: async (request: CreateAgentWorkflowRequest): Promise<AgentWorkflowSummary> => {
    const response = await axiosClient.post<AgentWorkflowSummary>(workflowsPath, request);
    return response.data;
  },

  listWorkflows: async (params?: AgentWorkflowListParams): Promise<PagedAgentWorkflows> => {
    const cleanParams: Record<string, string | number> = {};
    if (params?.page !== undefined && params.page > 0) cleanParams.page = params.page;
    if (params?.pageSize !== undefined && params.pageSize > 0) cleanParams.pageSize = params.pageSize;
    if (params?.status) cleanParams.status = params.status;

    const response = await axiosClient.get<PagedAgentWorkflows>(workflowsPath, { params: cleanParams });
    return response.data;
  },

  getWorkflow: async (id: string): Promise<AgentWorkflowDetail> => {
    const response = await axiosClient.get<AgentWorkflowDetail>(`${workflowsPath}/${id}`);
    return response.data;
  },

  getWorkflowHistory: async (id: string): Promise<AgentWorkflowTransition[]> => {
    const response = await axiosClient.get<AgentWorkflowTransition[]>(`${workflowsPath}/${id}/history`);
    return response.data;
  },

  startWorkflow: async (id: string): Promise<AgentWorkflowSummary> => {
    const response = await axiosClient.post<AgentWorkflowSummary>(`${workflowsPath}/${id}/start`);
    return response.data;
  },

  approveCollectionPlan: async (id: string, request: ApproveCollectionPlanningRequest): Promise<AgentWorkflowDetail> => {
    const response = await axiosClient.post<AgentWorkflowDetail>(`${workflowsPath}/${id}/collection-approval/approve`, request);
    return response.data;
  },

  requestCollectionRevision: async (id: string, request: RequestCollectionRevisionRequest): Promise<AgentWorkflowDetail> => {
    const response = await axiosClient.post<AgentWorkflowDetail>(`${workflowsPath}/${id}/collection-approval/request-revision`, request);
    return response.data;
  },

  rejectCollectionPlan: async (id: string, request: RejectCollectionPlanningRequest): Promise<AgentWorkflowDetail> => {
    const response = await axiosClient.post<AgentWorkflowDetail>(`${workflowsPath}/${id}/collection-approval/reject`, request);
    return response.data;
  },

  executeCollectionPlan: async (id: string, request: ExecuteCollectionPlanRequest): Promise<AgentWorkflowDetail> => {
    const response = await axiosClient.post<AgentWorkflowDetail>(`${workflowsPath}/${id}/execute-collection-plan`, request);
    return response.data;
  },

  approveDispatchPlan: async (id: string, request: ApproveDispatchPlanRequest): Promise<AgentWorkflowDetail> => {
    const response = await axiosClient.post<AgentWorkflowDetail>(`${workflowsPath}/${id}/dispatch-approval/approve`, request);
    return response.data;
  },

  requestDispatchRevision: async (id: string, request: RequestDispatchRevisionRequest): Promise<AgentWorkflowDetail> => {
    const response = await axiosClient.post<AgentWorkflowDetail>(`${workflowsPath}/${id}/dispatch-approval/request-revision`, request);
    return response.data;
  },

  rejectDispatchPlan: async (id: string, request: RejectDispatchPlanRequest): Promise<AgentWorkflowDetail> => {
    const response = await axiosClient.post<AgentWorkflowDetail>(`${workflowsPath}/${id}/dispatch-approval/reject`, request);
    return response.data;
  },

  executeDispatchPlan: async (id: string, request: ExecuteDispatchPlanRequest): Promise<AgentWorkflowDetail> => {
    const response = await axiosClient.post<AgentWorkflowDetail>(`${workflowsPath}/${id}/execute-dispatch-plan`, request);
    return response.data;
  },
};
