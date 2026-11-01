using CompCube_Models.Models.ClientData;
using CompCube_Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CompCube_Server.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public class UserFlairController(UserFlairData flairData) : ControllerBase
{
    [HttpGet("/api/flairs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<Flair[]> GetFlairs() => flairData.GetAllFlairs();

    [HttpPost("/api/flair/create")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult AddFlair(string name, string color, string permissions)
    {
        var flair = flairData.CreateFlair(name, color, permissions.Split(','));
        
        return Ok(flair);
    }

    [HttpPatch("/api/flair/update")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult UpdateFlair(string guid, string? name = null, string? color = null, string? permissions = null)
    {
        if (flairData.UpdateFlair(guid, name, color, permissions?.Split(',')))
            return Ok();
        
        return NotFound();
    }
    

    [HttpDelete("/api/flair/delete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult DeleteFlair(string guid)
    {
        if (flairData.DeleteFlair(guid))
            return Ok();

        return NotFound();
    }
}