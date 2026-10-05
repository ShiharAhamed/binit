namespace SmartWaste.Infrastructure.Workflow.Services;

/// <summary>Bounded polling and durable lease settings for citizen-report AI phases.</summary>
public sealed class ReportTriggeredWorkflowWorkerOptions
{
    public const string SectionName = "ReportTriggeredWorkflowWorker";

    public bool Enabled { get; set; }
    public int PollIntervalSeconds { get; set; } = 15;
    public int LeaseSeconds { get; set; } = 420;
    public int RetryDelaySeconds { get; set; } = 60;
    public int MaxAttempts { get; set; } = 3;
}
