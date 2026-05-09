using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RukuServiceApi.Context;
using RukuServiceApi.Models;

namespace RukuServiceApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PublicServicesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PublicServicesController> _logger;

    public PublicServicesController(
        ApplicationDbContext context,
        ILogger<PublicServicesController> logger
    )
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Service>>> GetServices()
    {
        try
        {
            var services = await _context.Services.ToListAsync();
            _logger.LogInformation(
                "Fetched {Count} services for public viewing.",
                services.Count
            );
            return Ok(services);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while fetching public services.");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Service>> GetService(int id)
    {
        try
        {
            var service = await _context.Services.FindAsync(id);

            if (service == null)
            {
                _logger.LogWarning("Service with ID {Id} not found.", id);
                return NotFound($"Service with ID {id} not found.");
            }

            return Ok(service);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while fetching service with ID {Id}.", id);
            return StatusCode(500, "Internal server error");
        }
    }
}
