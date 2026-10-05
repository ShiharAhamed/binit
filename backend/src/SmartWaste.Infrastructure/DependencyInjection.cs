using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartWaste.Application.Collection.Interfaces;
using SmartWaste.Application.Complaints.Interfaces;
using SmartWaste.Application.Dashboard.Interfaces;
using SmartWaste.Application.Fleet.Interfaces;
using SmartWaste.Application.Common.Options;
using SmartWaste.Application.Interfaces;
using SmartWaste.Application.Reporting.Interfaces;
using SmartWaste.Application.Operations.Interfaces;
using SmartWaste.Application.Workflow.Interfaces;
using SmartWaste.Application.Workflow.Services;
using SmartWaste.Infrastructure.Workflow.Services;
using SmartWaste.Domain.Entities;
using SmartWaste.Infrastructure.Collection.Services;
using SmartWaste.Infrastructure.Complaints.Services;
using SmartWaste.Infrastructure.Dashboard.Services;
using SmartWaste.Infrastructure.Fleet.Services;
using SmartWaste.Infrastructure.Operations.Services;
using SmartWaste.Infrastructure.Persistence;
using SmartWaste.Infrastructure.Reporting.Services;
using SmartWaste.Infrastructure.Services;

namespace SmartWaste.Infrastructure;

/// <summary>
/// Extension methods for registering Infrastructure layer services with the DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers all Infrastructure services including the database context, Identity, and AuthService.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database Context with PostgreSQL
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions => npgsqlOptions.MigrationsAssembly(
                    typeof(AppDbContext).Assembly.FullName)
            )
        );

        // ASP.NET Core Identity configuration
        services.AddIdentity<AppUser, IdentityRole<Guid>>(options =>
        {
            // Password requirements for university project
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = false;

            // User & Email requirements
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedAccount = false;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        // Register application services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        // Component 1 — Waste Reporting & Citizen Management
        services.AddScoped<IWasteReportService, WasteReportService>();
        services.AddScoped<IWasteReportAttachmentService, WasteReportAttachmentService>();

        // Component 2 — Waste Collection & Bin Management
        services.AddScoped<IWasteBinService, WasteBinService>();
        services.AddScoped<IBinObservationService, BinObservationService>();
        services.AddScoped<ICollectionNeedService, CollectionNeedService>();
        services.AddScoped<ICollectionTaskService, CollectionTaskService>();

        // Component 3 — Driver and vehicle application services
        services.AddScoped<IDriverProfileService, DriverProfileService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IAssignmentReadService, AssignmentReadService>();
        services.AddScoped<ICollectionAssignmentService, CollectionAssignmentService>();
        services.AddScoped<IFleetPlanningAiService, FleetPlanningAiService>();

        // Component 4 — Citizen Complaints & Driver Operations Management
        services.AddScoped<IComplaintService, ComplaintService>();
        services.AddScoped<IOperationalIssueService, OperationalIssueService>();
        services.AddScoped<IWasteOfficerDashboardService, WasteOfficerDashboardService>();
        services.AddScoped<IMunicipalManagerDashboardService, MunicipalManagerDashboardService>();

        // Agentic AI Workflow Services
        services.AddSingleton<IAgentWorkflowStateMachine, AgentWorkflowStateMachine>();
        services.AddOptions<CollectionScheduleOptions>()
            .Bind(configuration.GetSection("Municipality"))
            .Validate(options => !string.IsNullOrWhiteSpace(configuration["Municipality:TimeZoneId"]) &&
                CollectionSchedulePolicy.IsValidConfiguration(options),
                "Municipality timezone and collection window must be valid.")
            .ValidateOnStart();
        services.AddScoped<IAgentWorkflowService, AgentWorkflowService>();
        var workerTimeoutSeconds = configuration.GetValue<int?>("AiService:TimeoutSeconds") ?? 300;
        services.AddOptions<ReportTriggeredWorkflowWorkerOptions>()
            .Bind(configuration.GetSection(ReportTriggeredWorkflowWorkerOptions.SectionName))
            .Validate(options => options.PollIntervalSeconds >= 1 && options.RetryDelaySeconds >= 20 &&
                options.MaxAttempts >= 1 && options.LeaseSeconds > workerTimeoutSeconds + 30,
                "The report workflow lease must exceed the AI HTTP timeout with grace; retry delay must respect Gemini spacing.")
            .ValidateOnStart();
        services.AddScoped<ReportTriggeredWorkflowProcessor>();
        services.AddHostedService<ReportTriggeredWorkflowWorker>();

        // Cloud Storage — Supabase Storage
        var storageSection = configuration.GetSection(SmartWaste.Infrastructure.Reporting.Storage.SupabaseStorageOptions.SectionName);
        services.Configure<SmartWaste.Infrastructure.Reporting.Storage.SupabaseStorageOptions>(storageSection);

        services.AddHttpClient<IFileStorageService, SmartWaste.Infrastructure.Reporting.Storage.SupabaseFileStorageService>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SmartWaste.Infrastructure.Reporting.Storage.SupabaseStorageOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl) && Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
            {
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            }
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Internal FastAPI AI Service typed client registration
        var aiSection = configuration.GetSection(AiServiceOptions.SectionName);
        services.Configure<AiServiceOptions>(aiSection);

        var aiBaseUrl = aiSection["BaseUrl"];
        if (string.IsNullOrWhiteSpace(aiBaseUrl))
        {
            aiBaseUrl = "http://127.0.0.1:8000";
        }

        var timeoutSeconds = 300;
        if (int.TryParse(aiSection["TimeoutSeconds"], out var parsedTimeout) && parsedTimeout > 0)
        {
            timeoutSeconds = parsedTimeout;
        }

        services.AddHttpClient<IAiServiceClient, AiServiceClient>(client =>
        {
            client.BaseAddress = new Uri(aiBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });
        services.AddHttpClient<IPythonOrchestrationClient, PythonOrchestrationClient>(client =>
        {
            client.BaseAddress = new Uri(aiBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        return services;
    }
}
