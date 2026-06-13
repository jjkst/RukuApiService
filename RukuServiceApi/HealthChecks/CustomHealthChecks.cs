using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RukuServiceApi.Context;
using RukuServiceApi.Models;
using RukuServiceApi.Services;

namespace RukuServiceApi.HealthChecks;

public class DatabaseHealthCheck(
    ApplicationDbContext context,
    ILogger<DatabaseHealthCheck> logger
    ) : IHealthCheck
{
    private readonly ApplicationDbContext _context = context;
    private readonly ILogger<DatabaseHealthCheck> _logger = logger;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Test database connectivity
            await _context.Database.CanConnectAsync(cancellationToken);

            // Test a simple query
            var userCount = await _context.Users.CountAsync(cancellationToken);

            var data = new Dictionary<string, object>
            {
                { "userCount", userCount },
                { "timestamp", DateTime.UtcNow },
            };

            _logger.LogDebug("Database health check passed");
            return HealthCheckResult.Healthy("Database is accessible", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database is not accessible", ex);
        }
    }
}

public class EmailServiceHealthCheck(
    IOptions<EmailSettings> emailSettings,
    ILogger<EmailServiceHealthCheck> logger
    ) : IHealthCheck
{
    private readonly IOptions<EmailSettings> _emailSettings = emailSettings;
    private readonly ILogger<EmailServiceHealthCheck> _logger = logger;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var settings = _emailSettings.Value;

            // Basic validation of email settings
            if (
                string.IsNullOrEmpty(settings.RecipientEmail)
                || string.IsNullOrEmpty(settings.ResendApiKey)
            )
            {
                return Task.FromResult(
                    HealthCheckResult.Degraded("Email settings are incomplete")
                );
            }

            var data = new Dictionary<string, object>
            {
                { "recipientEmail", settings.RecipientEmail },
                { "resendApiKey", settings.ResendApiKey },
                { "timestamp", DateTime.UtcNow },
            };

            _logger.LogDebug("Email service health check passed");
            return Task.FromResult(
                HealthCheckResult.Healthy("Email service is configured", data)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email service health check failed");
            return Task.FromResult(
                HealthCheckResult.Unhealthy("Email service is not accessible", ex)
            );
        }
    }
}

public class FileSystemHealthCheck : IHealthCheck
{
    private readonly IOptions<FileUploadSettings> _fileSettings;
    private readonly ILogger<FileSystemHealthCheck> _logger;

    public FileSystemHealthCheck(
        IOptions<FileUploadSettings> fileSettings,
        ILogger<FileSystemHealthCheck> logger
    )
    {
        _fileSettings = fileSettings;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var settings = _fileSettings.Value;
            var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), settings.UploadPath);

            // Check if upload directory exists and is writable
            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            // Test write access
            var testFile = Path.Combine(uploadPath, $"health_check_{Guid.NewGuid()}.tmp");
            await File.WriteAllTextAsync(testFile, "health check", cancellationToken);
            File.Delete(testFile);

            var data = new Dictionary<string, object>
            {
                { "uploadPath", uploadPath },
                { "maxFileSizeBytes", settings.MaxFileSizeBytes },
                { "allowedExtensions", settings.AllowedExtensions },
                { "timestamp", DateTime.UtcNow },
            };

            _logger.LogDebug("File system health check passed");
            return HealthCheckResult.Healthy("File system is accessible", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File system health check failed");
            return HealthCheckResult.Unhealthy("File system is not accessible", ex);
        }
    }
}

public class MemoryHealthCheck : IHealthCheck
{
    private readonly ILogger<MemoryHealthCheck> _logger;

    public MemoryHealthCheck(ILogger<MemoryHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var memoryUsage = process.WorkingSet64;
            var memoryUsageMB = memoryUsage / (1024 * 1024);

            var data = new Dictionary<string, object>
            {
                { "memoryUsageMB", memoryUsageMB },
                { "processId", process.Id },
                { "timestamp", DateTime.UtcNow },
            };

            // Consider unhealthy if memory usage exceeds 1GB
            if (memoryUsageMB > 1024)
            {
                _logger.LogWarning(
                    "High memory usage detected: {MemoryUsageMB}MB",
                    memoryUsageMB
                );
                return Task.FromResult(
                    HealthCheckResult.Degraded(
                        $"High memory usage: {memoryUsageMB}MB",
                        data: data
                    )
                );
            }

            _logger.LogDebug("Memory health check passed: {MemoryUsageMB}MB", memoryUsageMB);
            return Task.FromResult(
                HealthCheckResult.Healthy($"Memory usage is normal: {memoryUsageMB}MB", data)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Memory health check failed");
            return Task.FromResult(
                HealthCheckResult.Unhealthy("Memory health check failed", ex)
            );
        }
    }
}
