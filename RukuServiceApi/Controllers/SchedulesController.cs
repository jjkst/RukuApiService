using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RukuServiceApi.Context;
using RukuServiceApi.Models;

namespace RukuServiceApi.Controllers;

[Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
public class SchedulesController(
    ApplicationDbContext context,
    ILogger<SchedulesController> logger
) : BaseController<Schedule, ApplicationDbContext>(context, logger)
{
    protected override object GetEntityId(Schedule entity) => entity.Id;

    private string? CurrentUid => User.FindFirst("uid")?.Value;

    private bool IsPrivileged =>
        User.IsInRole(nameof(UserRole.Admin)) || User.IsInRole(nameof(UserRole.Owner));

    [HttpGet]
    public override async Task<ActionResult<IEnumerable<Schedule>>> GetAll()
    {
        if (IsPrivileged)
            return await base.GetAll();

        if (string.IsNullOrEmpty(CurrentUid))
            return Ok(Array.Empty<Schedule>());

        var schedules = await _context.Schedules
            .Where(s => s.Uid == CurrentUid)
            .ToListAsync();
        return Ok(schedules);
    }

    [HttpGet("{id:int}")]
    public override async Task<ActionResult<Schedule>> GetById(int id)
    {
        var schedule = await _context.Schedules.FindAsync(id);
        if (schedule == null) return NotFound($"Schedule with ID {id} not found.");

        if (!IsPrivileged && schedule.Uid != CurrentUid)
            return NotFound($"Schedule with ID {id} not found.");

        return Ok(schedule);
    }

    [HttpPost]
    public override async Task<ActionResult<Schedule>> Create([FromBody] Schedule entity)
    {
        if (entity == null)
            return BadRequest("Entity object is null.");

        entity.Uid = CurrentUid;
        return await base.Create(entity);
    }

    [HttpPut("{id:int}")]
    public override async Task<ActionResult<Schedule>> Update(int id, [FromBody] Schedule entity)
    {
        var existing = await _context.Schedules.FindAsync(id);
        if (existing == null) return NotFound($"Schedule with ID {id} not found.");

        if (!IsPrivileged && existing.Uid != CurrentUid)
            return NotFound($"Schedule with ID {id} not found.");

        entity.Uid = existing.Uid;
        _context.Entry(existing).State = EntityState.Detached;
        return await base.Update(id, entity);
    }

    [HttpDelete("{id:int}")]
    public override async Task<IActionResult> Delete(int id)
    {
        var existing = await _context.Schedules.FindAsync(id);
        if (existing == null) return NotFound($"Schedule with ID {id} not found.");

        if (!IsPrivileged && existing.Uid != CurrentUid)
            return NotFound($"Schedule with ID {id} not found.");

        return await base.Delete(id);
    }
}
