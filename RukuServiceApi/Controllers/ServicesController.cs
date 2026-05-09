using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RukuServiceApi.Context;
using RukuServiceApi.Models;

namespace RukuServiceApi.Controllers;

[Authorize(Policy = AuthorizationPolicies.AdminOrOwner)] // Only admins and owners can manage services
public class ServicesController(
    ApplicationDbContext context,
    ILogger<ServicesController> logger
) : BaseController<Service, ApplicationDbContext>(context, logger)
{
    protected override object GetEntityId(Service entity) => entity.Id;

    [HttpPost("create")]
    public async Task<ActionResult<Service>> CreateService(
        [FromBody] CreateServiceRequest request
    )
    {
        try
        {
            // Check for duplicates
            bool duplicateService = await _context.Services.AnyAsync(ps =>
                ps.Title == request.Title || ps.Description == request.Description
            );

            if (duplicateService)
            {
                return Conflict(
                    new
                    {
                        message = $"Service with title '{request.Title}' or description '{request.Description}' already exists.",
                        code = "DUPLICATE_SERVICE",
                    }
                );
            }

            // Create service from request
            var service = new Service
            {
                Title = request.Title,
                FileName = request.FileName,
                Description = request.Description,
                Features = request.Features ?? new List<string>(),
                PricingPlans = request.PricingPlans ?? new List<PricingPlan>(),
            };

            _context.Services.Add(service);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created a new service: {Title}", service.Title);
            return CreatedAtAction(nameof(GetById), new { id = service.Id }, service);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while creating a new service.");
            throw; // Let the global exception middleware handle it
        }
    }

    [HttpPut("update/{id:int}")]
    public async Task<ActionResult<Service>> UpdateService(
        int id,
        [FromBody] UpdateServiceRequest request
    )
    {
        try
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null)
            {
                _logger.LogWarning("Service with ID {Id} not found for update.", id);
                return NotFound($"Service with ID {id} not found.");
            }

            // Check for duplicates (excluding current service)
            bool duplicateService = await _context.Services.AnyAsync(ps =>
                ps.Id != id
                && (ps.Title == request.Title || ps.Description == request.Description)
            );

            if (duplicateService)
            {
                return Conflict(
                    new
                    {
                        message = $"Service with title '{request.Title}' or description '{request.Description}' already exists.",
                        code = "DUPLICATE_SERVICE",
                    }
                );
            }

            // Update service properties
            service.Title = request.Title;
            service.FileName = request.FileName;
            service.Description = request.Description;
            service.Features = request.Features ?? new List<string>();
            service.PricingPlans = request.PricingPlans ?? new List<PricingPlan>();

            _context.Services.Update(service);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated service with ID {Id}.", id);
            return Ok(service);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while updating service with ID {Id}.", id);
            throw; // Let the global exception middleware handle it
        }
    }
}
