using Microsoft.EntityFrameworkCore;
using RukuServiceApi.Context;
using RukuServiceApi.Models;

namespace RukuServiceApi.Services;

public interface IDatabaseSeeder
{
    Task SeedAsync();
    Task SeedAdminAsync();
}

public class DatabaseSeeder : IDatabaseSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DatabaseSeeder> _logger;
    private readonly IConfiguration _configuration;

    public DatabaseSeeder(
        ApplicationDbContext context,
        ILogger<DatabaseSeeder> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task SeedAsync()
    {
        try
        {
            await SeedDevUsersAsync();
            await _context.SaveChangesAsync();
            _logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during database seeding");
            throw;
        }
    }

    public async Task SeedAdminAsync()
    {
        try
        {
            var email = _configuration["ADMIN_EMAIL"];
            var uid = _configuration["ADMIN_UID"];
            var displayName = _configuration["ADMIN_DISPLAY_NAME"] ?? "Administrator";

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(uid))
            {
                _logger.LogWarning(
                    "ADMIN_EMAIL or ADMIN_UID not configured, skipping admin seeding");
                return;
            }

            var normalizedEmail = email.ToLower();
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail || u.Uid == uid);

            if (existingUser != null)
            {
                _logger.LogInformation("Admin user already exists, skipping admin seeding");
                return;
            }

            var adminUser = new User
            {
                Email = email,
                Uid = uid,
                DisplayName = displayName,
                EmailVerified = true,
                Role = UserRole.Admin,
                Provider = ProviderList.Google,
            };

            _context.Users.Add(adminUser);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Seeded admin user: {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during admin seeding");
            throw;
        }
    }

    private async Task SeedDevUsersAsync()
    {
        if (await _context.Users.AnyAsync())
        {
            _logger.LogInformation("Users already exist, skipping dev user seeding");
            return;
        }

        var adminUser = new User
        {
            Email = "admin@rukuit.com",
            Uid = "admin-uid-12345",
            DisplayName = "System Administrator",
            EmailVerified = true,
            Role = UserRole.Admin,
            Provider = ProviderList.Google,
        };

        var ownerUser = new User
        {
            Email = "owner@rukuit.com",
            Uid = "owner-uid-67890",
            DisplayName = "Business Owner",
            EmailVerified = true,
            Role = UserRole.Owner,
            Provider = ProviderList.Google,
        };

        _context.Users.AddRange(adminUser, ownerUser);
        _logger.LogInformation("Seeded dev admin and owner users");
    }
}
