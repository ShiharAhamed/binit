using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SmartWaste.Api.Authentication.InternalService;
using SmartWaste.Api.Middleware;
using SmartWaste.Application.Validators;
using SmartWaste.Infrastructure;
using SmartWaste.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ─── Controllers & JSON Options ─────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
    });

// ─── FluentValidation ───────────────────────────────────────────────────────
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

// ─── Swagger / OpenAPI with JWT Support ──────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SmartWaste API",
        Version = "v1",
        Description = "Smart Waste Management System API - Foundation & Auth"
    });

    // Configure JWT Bearer authorization in Swagger UI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT Bearer token only (Swagger UI automatically prepends 'Bearer '). Example: eyJhbGciOiJIUzI1NiIsInR5cCI..."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ─── Infrastructure (EF Core + PostgreSQL + Identity + AuthService) ─────────
builder.Services.AddInfrastructure(builder.Configuration);

// ─── JWT & Internal Service Authentication ──────────────────────────────────
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer()
.AddScheme<InternalServiceAuthOptions, InternalServiceAuthHandler>(
    InternalServiceDefaults.AuthenticationScheme,
    options =>
    {
        var internalSection = builder.Configuration.GetSection("InternalService");
        options.ServiceKey = internalSection["ApiKey"];
    });

// Configure JwtBearerOptions via DI configuration to ensure dynamic configuration binding
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((options, configuration) =>
    {
        var jwtSection = configuration.GetSection("Jwt");
        var keyString = jwtSection["Key"];
        if (string.IsNullOrWhiteSpace(keyString))
        {
            keyString = "SmartWasteSecureSecretKeyForDevelopmentAndTesting123456!";
        }

        var key = Encoding.UTF8.GetBytes(keyString);

        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"] ?? "SmartWaste.Api",
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"] ?? "SmartWaste.Clients",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(InternalServiceDefaults.PolicyName, policy =>
    {
        policy.AddAuthenticationSchemes(InternalServiceDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireRole(InternalServiceDefaults.Role);
    });
});

// ─── CORS – Development Placeholder ─────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",   // React web app (dev)
                "http://localhost:5173",   // Vite dev server
                "http://localhost:8080"    // Flutter web client
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// ─── Middleware Pipeline ────────────────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartWaste API v1");
    });
}

app.UseHttpsRedirection();
app.UseCors("DevCors");

app.UseAuthentication();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseAuthorization();

app.MapControllers();

// ─── Database Seeding (Idempotent) ──────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        await DatabaseSeeder.SeedDatabaseAsync(services, app.Configuration, app.Environment.IsDevelopment());
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Note: Database seeding skipped or failed on startup (e.g. before initial migration).");
    }
}

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
