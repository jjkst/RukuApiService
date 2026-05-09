using RukuServiceApi.Context;
using RukuServiceApi.Models;

namespace RukuServiceApi.Controllers;

public class SchedulesController(
    ApplicationDbContext context,
    ILogger<SchedulesController> logger
) : BaseController<Schedule, ApplicationDbContext>(context, logger)
{
    protected override object GetEntityId(Schedule entity) => entity.Id;
}
