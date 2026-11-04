using CompCube_Models.Models.Map;
using CompCube_Server.Data;
using CompCube_Server.Discord;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CompCube_Server.Api.Controllers.Pooling;

[ApiController]
public class MapQueueApiController(BeatmapPoolData beatmapPoolData) : ControllerBase
{
    [HttpGet("/api/queue/qualified")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Authorize(Policy = "RequirePooler")]
    public ActionResult<VotingMap[]> GetQualifiedMaps()
    {
        return beatmapPoolData.GetQualifiedMaps();
    }

    [HttpPost("/api/maps/assignPool")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Authorize(Policy = "RequirePooler")]
    public IActionResult AssignPool(string guid, int poolNum)
    {
        var map = beatmapPoolData.GetBeatmapByGuid(guid);
        
        if (map is null)
            return NotFound();
        
        beatmapPoolData.AssignPoolToMap(guid, poolNum);

        return Ok();
    }
}