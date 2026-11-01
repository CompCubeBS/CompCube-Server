using CompCube_Models.Models.ClientData;
using CompCube_Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CompCube_Server.Api.Controllers;

[ApiController]
public class UserApiController(UserData userData) : ControllerBase
{
    [HttpGet("/api/user/id/{id}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<UserStatistics> GetUserById(string id)
    {
        var user = userData.GetUserStatisticsByPlatformId(id);
        
        if (user == null) 
            return NotFound();

        return user;
    }

    [HttpPatch("/api/user/id/{id}/ban")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Authorize(Policy = "RequireModerator")]
    public IActionResult BanUser(string id, bool banned = true)
    {
        if (userData.SetBannedStatus(id, banned))
            return Ok();

        return NotFound();
    }

    [HttpPatch("/api/user/id/{id}/flairs")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Authorize(Policy = "RequireAdmin")]
    public IActionResult SetFlairs(string id, string flairGuids)
    {
        var flairs = flairGuids.Split(',');
        
        if (userData.SetFlairs(id, flairs))
            return Ok();
        
        return NotFound();
    }
}