using CompCube_Models.Models.Map;
using CompCube_Server.Config;
using CompCube_Server.Data;
using CompCube_Server.Discord;
using Microsoft.AspNetCore.Mvc;

namespace CompCube_Server.Api.Controllers.Pooling;

[ApiController]
public class MapQueueApiController(ConfigHelper helper, BeatmapPoolData queue) : ControllerBase
{
    [HttpGet("/api/queue")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<VotingMap[]> GetQueue(string secret)
    {
        if (secret != helper.Secret)
            return Forbid();

        return queue.GetMaps().ToArray();
    }

    [HttpPut("/api/queue/create-batch")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult CreateBatch(string secret, int count, int batch)
    {
        if (secret != helper.Secret)
            return Forbid();

        var maps = queue.GetMaps().Take(count);
        
        foreach (var map in maps)
            queue.AssignBatch(map.Guid, batch);

        return Ok();
    }
    
}