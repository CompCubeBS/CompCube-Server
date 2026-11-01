using CompCube_Models.Models.ClientData;
using CompCube_Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CompCube_Server.Api.Controllers;

[ApiController]
public class UserApiController(UserData userData) : ControllerBase
{
    [HttpGet("api/user/@me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<UserStatistics> GetSelf()
    {
        var self = User.Claims.FirstOrDefault(c => c.Type == "platformId")?.Value;

        if (self == null)
            return Unauthorized();

        return GetUserByPlatformId(self);
    }
    
    [HttpGet("/api/user/id/{platformId}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<UserStatistics> GetUserByPlatformId(string id)
    {
        var user = userData.GetUserStatisticsByPlatformId(id);
        
        if (user == null) 
            return NotFound();

        return user;
    }

    [HttpPatch("/api/user/id/{platformId}/ban")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Authorize(Policy = "RequireModerator")]
    public IActionResult BanUser(string platformId, bool banned = true)
    {
        if (userData.SetBannedStatus(platformId, banned))
            return Ok();

        return NotFound();
    }

    [HttpPatch("/api/user/id/{platformId}/flairs")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Authorize(Policy = "RequireAdmin")]
    public IActionResult SetFlairs(string platformId, string flairGuids)
    {
        var flairs = flairGuids.Split(',');
        
        if (userData.SetFlairs(platformId, flairs))
            return Ok();
        
        return NotFound();
    }
}