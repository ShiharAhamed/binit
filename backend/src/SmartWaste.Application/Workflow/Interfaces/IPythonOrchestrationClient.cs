using SmartWaste.Application.Workflow.DTOs.Transport;

namespace SmartWaste.Application.Workflow.Interfaces;

public interface IPythonOrchestrationClient
{
    Task<PythonOrchestrationEnvelope> StartAsync(PythonWorkflowStartRequest request, CancellationToken cancellationToken = default);
    Task<PythonOrchestrationEnvelope> ResumeAfterCollectionApprovalAsync(PythonWorkflowResumeRequest request, CancellationToken cancellationToken = default);
    Task<PythonOrchestrationEnvelope> ResumeAfterReportVerificationAsync(PythonWorkflowResumeRequest request, CancellationToken cancellationToken = default);
}
