using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace RukuServiceApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseController<TEntity, TContext>(TContext context, ILogger logger)
    : ControllerBase
    where TEntity : class
    where TContext : DbContext
{
    protected readonly TContext _context = context;
    protected readonly ILogger _logger = logger;

    [HttpGet]
    public virtual async Task<ActionResult<IEnumerable<TEntity>>> GetAll()
    {
        try
        {
            var entities = await _context.Set<TEntity>().ToListAsync();
            _logger.LogInformation(
                "Fetched {Count} entities from the database.",
                entities.Count
            );
            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while fetching entities.");
            throw;
        }
    }

    [HttpGet("{id:int}")]
    public virtual async Task<ActionResult<TEntity>> GetById(int id)
    {
        try
        {
            var entity = await _context.Set<TEntity>().FindAsync(id);

            if (entity == null)
            {
                _logger.LogWarning("Entity with ID {Id} not found.", id);
                return NotFound($"Entity with ID {id} not found.");
            }

            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "An error occurred while fetching the entity with ID {Id}.",
                id
            );
            throw; // Let the global exception middleware handle it
        }
    }

    [HttpPost]
    public virtual async Task<ActionResult<TEntity>> Create([FromBody] TEntity entity)
    {
        try
        {
            if (entity == null)
            {
                _logger.LogWarning("Received a null entity object in the request.");
                return BadRequest("Entity object is null.");
            }

            _context.Set<TEntity>().Add(entity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created a new entity.");
            return CreatedAtAction(nameof(GetById), new { id = GetEntityId(entity) }, entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while creating a new entity.");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id:int}")]
    public virtual async Task<ActionResult<TEntity>> Update(
        int id,
        [FromBody] TEntity updatedEntity
    )
    {
        try
        {
            var entity = await _context.Set<TEntity>().FindAsync(id);

            if (entity == null)
            {
                _logger.LogWarning("Entity with ID {Id} not found for update.", id);
                return NotFound($"Entity with ID {id} not found.");
            }

            _context.Entry(entity).CurrentValues.SetValues(updatedEntity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated entity with ID {Id}.", id);
            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "An error occurred while updating the entity with ID {Id}.",
                id
            );
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id:int}")]
    public virtual async Task<IActionResult> Delete(int id)
    {
        try
        {
            var entity = await _context.Set<TEntity>().FindAsync(id);

            if (entity == null)
            {
                _logger.LogWarning("Entity with ID {Id} not found for deletion.", id);
                return NotFound($"Entity with ID {id} not found.");
            }

            _context.Set<TEntity>().Remove(entity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted entity with ID {Id}.", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "An error occurred while deleting the entity with ID {Id}.",
                id
            );
            return StatusCode(500, "Internal server error");
        }
    }

    protected abstract object GetEntityId(TEntity entity);
}
