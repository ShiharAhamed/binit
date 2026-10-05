using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SmartWaste.Infrastructure.Workflow.Services;

/// <summary>Polls PostgreSQL for initial and post-verification report work; every iteration owns a fresh scope.</summary>
public sealed class ReportTriggeredWorkflowWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReportTriggeredWorkflowWorkerOptions _options;
    private readonly ILogger<ReportTriggeredWorkflowWorker> _logger;

    public ReportTriggeredWorkflowWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ReportTriggeredWorkflowWorkerOptions> options,
        ILogger<ReportTriggeredWorkflowWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>()
                    .ProcessOneAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning("Report workflow worker iteration failed ({FailureKind}).", exception.GetType().Name);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
