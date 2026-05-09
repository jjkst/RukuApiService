using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RukuServiceApi.Context;
using RukuServiceApi.Models;

namespace RukuServiceApi.Controllers;

public class TimeslotRequest
{
    public DateTime Date { get; set; }
    public List<string> Services { get; set; } = new();
}

public class AvailabilitiesController(
    ApplicationDbContext context,
    ILogger<AvailabilitiesController> logger
) : BaseController<Availability, ApplicationDbContext>(context, logger)
{
    protected override object GetEntityId(Availability entity) => entity.Id;

    [HttpGet("dates")]
    public async Task<ActionResult<IEnumerable<DateTime>>> GetAvailableDates()
    {
        try
        {
            var today = DateTime.Today;
            var availabilities = await _context
                .Availabilities.Where(a => a.EndDate.Date >= today)
                .ToListAsync();

            var allDates = availabilities
                .SelectMany(a =>
                    Enumerable
                        .Range(0, (a.EndDate - a.StartDate).Days + 1)
                        .Select(offset => a.StartDate.AddDays(offset).Date)
                )
                .Where(d => d >= today)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            _logger.LogInformation(
                "Fetched {Count} available dates from the database.",
                allDates.Count
            );
            return Ok(allDates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while fetching available dates.");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("services")]
    public async Task<ActionResult<IEnumerable<Service>>> GetAvailableServices(
        [FromQuery] DateTime date
    )
    {
        try
        {
            // Find all availabilities that include the given date
            var availabilities = await _context
                .Availabilities.Where(a =>
                    date.Date >= a.StartDate.Date && date.Date <= a.EndDate.Date
                )
                .ToListAsync();

            var services = availabilities.SelectMany(a => a.Services).Distinct().ToList();

            _logger.LogInformation(
                "Fetched {Count} services available on {Date}.",
                services.Count,
                date
            );
            return Ok(services);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while fetching available services.");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("timeslots")]
    public async Task<ActionResult<IEnumerable<string>>> GetTimeSlots(
        [FromBody] TimeslotRequest request
    )
    {
        try
        {
            var availabilitiesByDate = await _context
                .Availabilities.Where(a =>
                    request.Date.Date >= a.StartDate.Date && request.Date.Date <= a.EndDate.Date
                )
                .ToListAsync();

            if (!availabilitiesByDate.Any())
            {
                _logger.LogInformation("No availabilities for the provided date");
                return NotFound(
                    new
                    {
                        message = "No availabilities for the provided date",
                        date = request.Date.ToString("yyyy-MM-dd"),
                    }
                );
            }

            var filteredAvailabilitiesByDateAndService = availabilitiesByDate
                .Where(a => a.Services.Any(s => request.Services.Contains(s)))
                .ToList();

            if (!filteredAvailabilitiesByDateAndService.Any())
            {
                _logger.LogInformation("No availabilities found for the provided services");
                return NotFound(
                    new
                    {
                        message = "No availabilities found for the provided services",
                        date = request.Date.ToString("yyyy-MM-dd"),
                        services = request.Services,
                    }
                );
            }

            var timeslots = filteredAvailabilitiesByDateAndService
                .SelectMany(a => a.Timeslots ?? Enumerable.Empty<string>())
                .Distinct()
                .ToList();

            _logger.LogInformation(
                "Fetched {Count} timeslots for date {Date} and services {Services}.",
                timeslots.Count,
                request.Date.ToString("yyyy-MM-dd"),
                string.Join(",", request.Services)
            );

            return Ok(timeslots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while fetching timeslots.");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public override async Task<ActionResult<Availability>> Create(
        [FromBody] Availability entity
    )
    {
        var validationResult = await ValidateAvailabilityAsync(entity);
        if (validationResult != null)
            return validationResult;
        return await base.Create(entity);
    }

    [HttpPut("{id}")]
    public override async Task<ActionResult<Availability>> Update(
        int id,
        [FromBody] Availability entity
    )
    {
        var validationResult = await ValidateAvailabilityAsync(entity, id);
        if (validationResult != null)
            return validationResult;
        return await base.Update(id, entity);
    }

    private async Task<ActionResult<Availability>?> ValidateAvailabilityAsync(
        Availability entity,
        int? excludeId = null
    )
    {
        if (entity.StartDate <= DateTime.Today)
            return BadRequest(new { message = "StartDate cannot be today or in the past." });

        if (entity.EndDate <= entity.StartDate)
            return BadRequest(new { message = "EndDate must be after StartDate." });

        bool duplicate = await _context.Availabilities.AnyAsync(ps =>
            (excludeId == null || ps.Id != excludeId)
            && ps.StartDate < entity.EndDate
            && ps.EndDate > entity.StartDate
        );
        if (duplicate)
            return Conflict(
                new
                {
                    message = "Availability dates are colliding with an existing availability.",
                    code = "DUPLICATE_AVAILABILITY",
                }
            );

        return null;
    }
}
