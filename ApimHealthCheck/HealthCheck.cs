using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ApimHealthCheck;

public class HealthCheck
{
    private readonly ILogger<HealthCheck> _logger;

    public HealthCheck(ILogger<HealthCheck> logger)
    {
        _logger = logger;
    }

    [Function("Alive")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get","post")] HttpRequest req)
    {
        _logger.LogInformation("Health check triggered");
        
        // Read optional query parameter
        string? echoValue = req.Query["echo"];
        
        var response = new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "ApimHealthCheck",
            version = "1.0.0",
            region = Environment.GetEnvironmentVariable("REGION_NAME") ?? "unknown",
            echo = echoValue,
            system = new
            {
                osVersion = RuntimeInformation.OSDescription,
                platform = RuntimeInformation.OSArchitecture.ToString(),
                freeMemoryMB = GetAvailableMemoryMB(),
                freeDiskSpaceGB = GetFreeDiskSpaceGB()
            }
        };
        
        return new OkObjectResult(response);
    }

    private long GetAvailableMemoryMB()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Read /proc/meminfo for available memory on Linux
                var memInfo = File.ReadAllLines("/proc/meminfo");
                foreach (var line in memInfo)
                {
                    if (line.StartsWith("MemAvailable:"))
                    {
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && long.TryParse(parts[1], out long memKB))
                        {
                            return memKB / 1024; // Convert KB to MB
                        }
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Use Performance Counter for Windows
                using var pc = new PerformanceCounter("Memory", "Available MBytes");
                return (long)pc.NextValue();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read available memory");
        }
        
        return -1; // Indicates unavailable
    }

    private long GetFreeDiskSpaceGB()
    {
        try
        {
            // DriveInfo works on both Linux and Windows
            var driveInfo = new DriveInfo(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "C:\\" : "/");
            return driveInfo.AvailableFreeSpace / (1024 * 1024 * 1024); // Convert bytes to GB
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read free disk space");
        }
        
        return -1; // Indicates unavailable
    }
}