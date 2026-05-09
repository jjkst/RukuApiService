using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RukuServiceApi.Models;

namespace RukuServiceApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)] // Only admins can access monitoring endpoints
public class MonitoringController : ControllerBase
{
    private readonly ILogger<MonitoringController> _logger;

    public MonitoringController(ILogger<MonitoringController> logger)
    {
        _logger = logger;
    }

    [HttpGet("system-info")]
    public IActionResult GetSystemInfo()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;

            var systemInfo = new
            {
                application = new
                {
                    name = assembly.GetName().Name,
                    version = version?.ToString(),
                    startTime = process.StartTime,
                    uptime = DateTime.UtcNow - process.StartTime,
                },
                system = new
                {
                    machineName = Environment.MachineName,
                    osVersion = Environment.OSVersion.ToString(),
                    processorCount = Environment.ProcessorCount,
                    workingSet = process.WorkingSet64,
                    privateMemorySize = process.PrivateMemorySize64,
                    virtualMemorySize = process.VirtualMemorySize64,
                },
                environment = new
                {
                    environmentName = Environment.GetEnvironmentVariable(
                        "ASPNETCORE_ENVIRONMENT"
                    ) ?? "Unknown",
                    frameworkVersion = Environment.Version.ToString(),
                    is64BitProcess = Environment.Is64BitProcess,
                    is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
                },
                timestamp = DateTime.UtcNow,
            };

            return Ok(systemInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system information");
            return StatusCode(500, new { message = "Error retrieving system information" });
        }
    }

    [HttpGet("performance")]
    public IActionResult GetPerformanceMetrics()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var gc = GC.GetTotalMemory(false);

            var performanceMetrics = new
            {
                memory = new
                {
                    workingSetMB = process.WorkingSet64 / (1024 * 1024),
                    privateMemoryMB = process.PrivateMemorySize64 / (1024 * 1024),
                    virtualMemoryMB = process.VirtualMemorySize64 / (1024 * 1024),
                    gcMemoryMB = gc / (1024 * 1024),
                    gen0Collections = GC.CollectionCount(0),
                    gen1Collections = GC.CollectionCount(1),
                    gen2Collections = GC.CollectionCount(2),
                },
                cpu = new
                {
                    totalProcessorTime = process.TotalProcessorTime.TotalMilliseconds,
                    userProcessorTime = process.UserProcessorTime.TotalMilliseconds,
                    privilegedProcessorTime = process.PrivilegedProcessorTime.TotalMilliseconds,
                },
                threads = new
                {
                    threadCount = process.Threads.Count,
                    handleCount = process.HandleCount,
                },
                timestamp = DateTime.UtcNow,
            };

            return Ok(performanceMetrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance metrics");
            return StatusCode(500, new { message = "Error retrieving performance metrics" });
        }
    }

    [HttpGet("logs")]
    public async Task<IActionResult> GetRecentLogs([FromQuery] int count = 50)
    {
        try
        {
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            var logFiles = Directory
                .GetFiles(logPath, "*.txt")
                .OrderByDescending(f => System.IO.File.GetLastWriteTime(f))
                .Take(1);

            if (!logFiles.Any())
            {
                return NotFound(new { message = "No log files found" });
            }

            var logFile = logFiles.First();
            var lines = await System.IO.File.ReadAllLinesAsync(logFile);
            var recentLines = lines.TakeLast(count).ToArray();

            return Ok(
                new
                {
                    logFile = Path.GetFileName(logFile),
                    lineCount = recentLines.Length,
                    logs = recentLines,
                    timestamp = DateTime.UtcNow,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logs");
            return StatusCode(500, new { message = "Error retrieving logs" });
        }
    }

    [HttpPost("gc")]
    public IActionResult ForceGarbageCollection()
    {
        try
        {
            var beforeMemory = GC.GetTotalMemory(false);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var afterMemory = GC.GetTotalMemory(false);

            var result = new
            {
                beforeMemoryMB = beforeMemory / (1024 * 1024),
                afterMemoryMB = afterMemory / (1024 * 1024),
                freedMemoryMB = (beforeMemory - afterMemory) / (1024 * 1024),
                timestamp = DateTime.UtcNow,
            };

            _logger.LogInformation(
                "Forced garbage collection completed. Freed {FreedMemoryMB}MB",
                result.freedMemoryMB
            );
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during garbage collection");
            return StatusCode(500, new { message = "Error during garbage collection" });
        }
    }
}
