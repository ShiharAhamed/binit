using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SmartWaste.Application.DTOs.Users;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Entities;
using SmartWaste.Infrastructure.Persistence;
using SmartWaste.Infrastructure.Services;

namespace SmartWaste.Tests.Fleet.Services;

public class UserServiceDriverProvisioningTests
{
    [Fact]
    public async Task CreateUserAsync_ProvisionsAnInternalAvailableProfileForADriverRole()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"SmartWaste_UserDriverProvisioning_{Guid.NewGuid():N}")
            .Options);
        var userStore = new UserStore<AppUser, IdentityRole<Guid>, AppDbContext, Guid>(db);
        var roleStore = new RoleStore<IdentityRole<Guid>, AppDbContext, Guid>(db);
        var options = Options.Create(new IdentityOptions());
        var userManager = new UserManager<AppUser>(userStore, options, new PasswordHasher<AppUser>(), new[] { new UserValidator<AppUser>() }, new[] { new PasswordValidator<AppUser>() }, new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), new Mock<IServiceProvider>().Object, new Mock<ILogger<UserManager<AppUser>>>().Object);
        var roleManager = new RoleManager<IdentityRole<Guid>>(roleStore, Array.Empty<IRoleValidator<IdentityRole<Guid>>>(), new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), new Mock<ILogger<RoleManager<IdentityRole<Guid>>>>().Object);
        var service = new UserService(userManager, roleManager, db);

        var response = await service.CreateUserAsync(new CreateUserRequest
        {
            FullName = "Automatic Driver",
            Email = "automatic.driver@example.test",
            Username = "automatic.driver",
            Role = AppRoles.Driver
        });

        var profile = await db.DriverProfiles.SingleAsync();
        Assert.Equal(response.User.Id, profile.UserId);
        Assert.Null(profile.LicenseNumber);
        Assert.Equal(DriverAvailabilityStatus.Available, profile.AvailabilityStatus);
        var user = await userManager.FindByIdAsync(response.User.Id.ToString());
        Assert.NotNull(user);
        Assert.True(await userManager.IsInRoleAsync(user!, AppRoles.Driver));
    }
}
