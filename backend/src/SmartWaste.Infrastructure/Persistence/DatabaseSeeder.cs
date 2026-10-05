using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Entities;
using SmartWaste.Infrastructure.Identity.Services;

namespace SmartWaste.Infrastructure.Persistence;

/// <summary>
/// Idempotent database seeder for initial Identity roles and development test accounts.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedDatabaseAsync(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        bool isDevelopment)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = serviceProvider.GetRequiredService<AppDbContext>();
        var logger = serviceProvider.GetRequiredService<ILogger<AppDbContext>>();

        // 1. Seed Roles idempotently
        foreach (var roleName in AppRoles.All)
        {
            try
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                    if (roleResult.Succeeded)
                    {
                        logger.LogInformation("Seeded Identity role: {Role}", roleName);
                    }
                    else
                    {
                        logger.LogWarning("Failed to seed role {Role}: {Errors}",
                            roleName,
                            string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Exception while seeding role {Role}", roleName);
            }
        }

        // 2. Seed development accounts only in Development environment
        if (isDevelopment)
        {
            await SeedDevelopmentUsersAsync(userManager, db, configuration, logger);
        }
    }

    private static async Task SeedDevelopmentUsersAsync(
        UserManager<AppUser> userManager,
        AppDbContext db,
        IConfiguration configuration,
        ILogger logger)
    {
        // Password for development accounts - default for local university testing only
        var devPassword = configuration["Seed:DevUserPassword"] ?? "DevPassword123!";

        var devUsers = new List<(string Email, string FullName, string Phone, string Role)>
        {
            ("manager@smartwaste.local", "Municipal Manager", "+94770000001", AppRoles.MunicipalManager),
            ("officer@smartwaste.local", "Waste Officer", "+94770000002", AppRoles.WasteOfficer),
            ("driver@smartwaste.local", "Waste Driver", "+94770000003", AppRoles.Driver),
            ("citizen@smartwaste.local", "Test Citizen", "+94770000004", AppRoles.Citizen)
        };

        foreach (var devUser in devUsers)
        {
            try
            {
                var existingUser = await userManager.FindByEmailAsync(devUser.Email);
                if (existingUser == null)
                {
                    var user = new AppUser
                    {
                        Id = Guid.NewGuid(),
                        UserName = devUser.Email,
                        Email = devUser.Email,
                        FullName = devUser.FullName,
                        PhoneNumber = devUser.Phone,
                        IsActive = true,
                        EmailConfirmed = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    var createResult = await userManager.CreateAsync(user, devPassword);
                    if (createResult.Succeeded)
                    {
                        var roleResult = await userManager.AddToRoleAsync(user, devUser.Role);
                        if (roleResult.Succeeded && devUser.Role == AppRoles.Driver)
                        {
                            await DriverProfileProvisioner.EnsureAsync(db, user.Id);
                        }
                        if (roleResult.Succeeded)
                        {
                            logger.LogInformation("Seeded development account: {Email} ({Role})", devUser.Email, devUser.Role);
                        }
                        else
                        {
                            logger.LogWarning("Failed to assign seeded role {Role} to {Email}: {Errors}", devUser.Role, devUser.Email, string.Join(", ", roleResult.Errors.Select(error => error.Description)));
                        }
                    }
                    else
                    {
                        logger.LogWarning("Failed to create development user {Email}: {Errors}",
                            devUser.Email,
                            string.Join(", ", createResult.Errors.Select(e => e.Description)));
                    }
                }
                else
                {
                    if (!await userManager.IsInRoleAsync(existingUser, devUser.Role))
                    {
                        await userManager.AddToRoleAsync(existingUser, devUser.Role);
                        logger.LogInformation("Assigned missing role {Role} to existing development account: {Email}", devUser.Role, devUser.Email);
                    }

                    if (devUser.Role == AppRoles.Driver && await userManager.IsInRoleAsync(existingUser, AppRoles.Driver))
                    {
                        await DriverProfileProvisioner.EnsureAsync(db, existingUser.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Exception while seeding development user {Email}", devUser.Email);
            }
        }
    }
}
